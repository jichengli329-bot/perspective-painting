using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using PerspectivePuzzle.Domain;

namespace PerspectivePuzzle.Presentation
{
    /// <summary>
    /// Samples the current machine-readable piece-ID view of a composition at
    /// a fixed frequency and scores it against a readable target Object-ID
    /// texture with <see cref="CompositionScorer"/>. The ID view is rendered
    /// into one persistent linear ARGB32 render texture through a
    /// <see cref="CommandBuffer"/> that reuses the composition camera's
    /// view/projection matrices and one runtime unlit material per piece
    /// ID, so live renderer materials, renderer enabled states, the camera's
    /// target texture, and the beauty presentation are never touched. Pixels
    /// are read back only via <see cref="AsyncGPUReadback"/>, with at most
    /// one request in flight; each completed sample produces an immutable
    /// <see cref="CompositionScoreResult"/> stored in <see cref="LatestResult"/>
    /// and raised through <see cref="Evaluated"/>.
    ///
    /// Wiring and trigger APIs: <see cref="Configure"/> (used by the
    /// deterministic editor scene builder and PlayMode tests),
    /// <see cref="ValidateConfiguration"/>, and <see cref="RequestEvaluationNow"/>.
    /// Automatic sampling runs only while the component is active, enabled,
    /// and configured. All GPU resources are created once and released on
    /// disable/destroy.
    /// </summary>
    public sealed class PaintingCompositionEvaluator : MonoBehaviour
    {
        private const string IdShaderName = "PerspectivePuzzle/PaintingObjectId";
        private const string BaseColorProperty = "_ObjectIdColor";
        private const string ZTestProperty = "_ZTest";

        /// <summary>Raised on the main thread after each successful sample, carrying the immutable result.</summary>
        public event Action<CompositionScoreResult> Evaluated;

        [SerializeField] private Camera _compositionCamera;
        [SerializeField] private Texture2D _targetTexture;
        [SerializeField] private PaintingPieceId[] _pieces = Array.Empty<PaintingPieceId>();
        [SerializeField, Min(1)] private int _width = 256;
        [SerializeField, Min(1)] private int _height = 144;
        [SerializeField, Range(1f, 10f)] private float _frequencyHz = 6f;
        [SerializeField] private bool _autoSample = true;

        // Score policy values matching CompositionPolicy.Default (T-009A).
        [SerializeField, Range(0f, 1f)] private float _silhouetteWeight = 0.40f;
        [SerializeField, Range(0f, 1f)] private float _pieceWeight = 0.45f;
        [SerializeField, Range(0f, 1f)] private float _identityWeight = 0.15f;
        [SerializeField, Range(0f, 1f)] private float _passThreshold = 0.93f;
        [SerializeField, Range(0f, 1f)] private float _minimumCoverageThreshold = 0.80f;

        private RenderTexture _targetRt;
        private CommandBuffer _commandBuffer;
        private Dictionary<uint, Material> _idMaterials;
        private readonly List<RendererDraw> _draws = new List<RendererDraw>();
        private CompositionIdBuffer _targetBuffer;
        private uint[] _requiredIds = Array.Empty<uint>();
        private bool _configured;
        private bool _resourcesBuilt;
        private bool _requestInFlight;
        private float _sampleAccumulator;

        /// <summary>True once <see cref="Configure"/> succeeded; automatic sampling is gated on this.</summary>
        public bool IsConfigured => _configured;

        /// <summary>Immutable packed-ID buffer built once from the target texture; null until configured.</summary>
        public CompositionIdBuffer Target => _targetBuffer;

        /// <summary>Score policy reconstructed from the serialized values (T-009A defaults).</summary>
        public CompositionPolicy Policy => new CompositionPolicy(_silhouetteWeight, _pieceWeight, _identityWeight, _passThreshold, _minimumCoverageThreshold);

        /// <summary>Most recent comparison result; null until the first successful sample.</summary>
        public CompositionScoreResult LatestResult { get; private set; }

        /// <summary>One cached renderer/submesh with the material to draw it with.</summary>
        private readonly struct RendererDraw
        {
            public readonly Renderer Renderer;
            public readonly Material Material;
            public readonly int SubMeshCount;

            public RendererDraw(Renderer renderer, Material material, int subMeshCount)
            {
                Renderer = renderer;
                Material = material;
                SubMeshCount = subMeshCount;
            }
        }

        private void Awake()
        {
            // Convenience path for scenes wired by the deterministic builder:
            // fully serialized references configure at startup; tests and the
            // builder can also call Configure explicitly with any values.
            if (_compositionCamera != null && _targetTexture != null && _pieces != null && _pieces.Length > 0)
                Configure(_compositionCamera, _targetTexture, _pieces);
        }

        private void OnEnable()
        {
            if (_configured && !_resourcesBuilt)
                RebuildResources();
        }

        private void OnDisable()
        {
            ReleaseResources();
        }

        private void OnDestroy()
        {
            ReleaseResources();
        }

        private void LateUpdate()
        {
            if (!_autoSample || !_configured || !isActiveAndEnabled)
                return;

            _sampleAccumulator += Time.deltaTime;
            if (_sampleAccumulator >= 1f / _frequencyHz)
            {
                _sampleAccumulator = 0f;
                RenderAndRequest(false);
            }
        }

        /// <summary>
        /// Wires the evaluator to a composition camera, a readable target
        /// Object-ID texture, and the ordered pieces, then validates the
        /// configuration and builds all GPU resources. Reconfiguring later
        /// releases the previous resources first. Throws with a clear message
        /// on any invalid input. The required piece-ID order is the order of
        /// <paramref name="pieces"/>.
        /// </summary>
        public void Configure(Camera compositionCamera, Texture2D targetTexture, IReadOnlyList<PaintingPieceId> pieces, int width = 256, int height = 144, float frequencyHz = 6f, CompositionPolicy? policy = null)
        {
            if (compositionCamera == null)
                throw new ArgumentNullException(nameof(compositionCamera));
            if (targetTexture == null)
                throw new ArgumentNullException(nameof(targetTexture));
            if (pieces == null)
                throw new ArgumentNullException(nameof(pieces));

            _compositionCamera = compositionCamera;
            _targetTexture = targetTexture;
            _pieces = new PaintingPieceId[pieces.Count];
            for (int i = 0; i < pieces.Count; i++)
                _pieces[i] = pieces[i];
            _width = width;
            _height = height;
            _frequencyHz = frequencyHz;
            if (policy.HasValue)
            {
                _silhouetteWeight = policy.Value.SilhouetteWeight;
                _pieceWeight = policy.Value.PieceWeight;
                _identityWeight = policy.Value.IdentityWeight;
                _passThreshold = policy.Value.PassThreshold;
                _minimumCoverageThreshold = policy.Value.MinimumCoverageThreshold;
            }

            ValidateConfiguration();
            BuildTarget();
            RebuildResources();
            _sampleAccumulator = 0f;
            _configured = true;
        }

        /// <summary>
        /// Validates the current serialized/configured state without
        /// allocating GPU resources: camera, target texture readability and
        /// dimensions, nonempty ordered pieces with unique in-range IDs and
        /// cached renderers, render dimensions, sampling frequency, policy
        /// weights, and the presence of the project PaintingObjectId shader.
        /// Throws with a clear message on the first problem found.
        /// </summary>
        public void ValidateConfiguration()
        {
            if (_compositionCamera == null)
                throw new ArgumentException("Composition camera is not assigned.", nameof(_compositionCamera));
            if (_targetTexture == null)
                throw new ArgumentException("Target Object-ID texture is not assigned.", nameof(_targetTexture));
            if (!_targetTexture.isReadable)
                throw new ArgumentException($"Target texture '{_targetTexture.name}' must be readable (Read/Write enabled in its import settings).", nameof(_targetTexture));
            if (_targetTexture.width != _width || _targetTexture.height != _height)
                throw new ArgumentException($"Target texture is {_targetTexture.width}x{_targetTexture.height} but the evaluator renders {_width}x{_height}; dimensions must match.", nameof(_targetTexture));
            if (_pieces == null || _pieces.Length == 0)
                throw new ArgumentException("At least one ordered piece is required.", nameof(_pieces));
            if (_width <= 0)
                throw new ArgumentOutOfRangeException(nameof(_width), _width, "Render width must be positive.");
            if (_height <= 0)
                throw new ArgumentOutOfRangeException(nameof(_height), _height, "Render height must be positive.");
            if (_frequencyHz < 1f || _frequencyHz > 10f)
                throw new ArgumentOutOfRangeException(nameof(_frequencyHz), _frequencyHz, "Sampling frequency must be within 1..10 Hz.");

            var seenIds = new HashSet<uint>();
            for (int i = 0; i < _pieces.Length; i++)
            {
                PaintingPieceId piece = _pieces[i];
                if (piece == null)
                    throw new ArgumentException($"Piece {i} is null.", nameof(_pieces));
                uint id = piece.Id;
                if (id < 1 || id > 0x00FFFFFF)
                    throw new ArgumentOutOfRangeException(nameof(_pieces), id, $"Piece {i} ('{piece.name}') has invalid ID 0x{id:X8}; IDs must be within 1..0xFFFFFF.");
                if (!seenIds.Add(id))
                    throw new ArgumentException($"Piece ID 0x{id:X6} is duplicated; piece IDs must be unique.", nameof(_pieces));
                piece.Configure((int)id);
                if (piece.Renderers.Count == 0)
                    throw new InvalidOperationException($"Piece '{piece.name}' (ID 0x{id:X6}) has no renderer below its root.");
            }

            // Reconstructing the policy runs its constructor validation over
            // the serialized weights and thresholds.
            _ = Policy;
            if (Shader.Find(IdShaderName) == null)
                throw new InvalidOperationException($"Shader '{IdShaderName}' was not found; is Assets/Shaders/PaintingObjectId.shader present?");
        }

        /// <summary>
        /// Forces an immediate render and readback outside the sampling
        /// cadence, for PlayMode tests and the editor builder. Requires the
        /// component to be configured and active; throws otherwise.
        /// </summary>
        public void RequestEvaluationNow()
        {
            if (!_configured)
                throw new InvalidOperationException("PaintingCompositionEvaluator is not configured; call Configure(...) first.");
            if (!isActiveAndEnabled)
                throw new InvalidOperationException("PaintingCompositionEvaluator must be active and enabled to request an evaluation.");
            RenderAndRequest(true);
        }

        private void BuildTarget()
        {
            uint[] packed = PackColor32s(_targetTexture.GetPixels32(), _width, _height);
            _targetBuffer = CompositionIdBuffer.FromPixels(_width, _height, packed);

            _requiredIds = new uint[_pieces.Length];
            for (int i = 0; i < _pieces.Length; i++)
                _requiredIds[i] = _pieces[i].Id;

            // Mirror CompositionScorer's target constraints so a misbuilt
            // target fails here instead of at the first sample.
            var seen = new bool[_requiredIds.Length];
            for (int i = 0; i < packed.Length; i++)
            {
                uint t = packed[i];
                if (t == 0)
                    continue;
                int slot = -1;
                for (int j = 0; j < _requiredIds.Length; j++)
                {
                    if (_requiredIds[j] == t)
                    {
                        slot = j;
                        break;
                    }
                }
                if (slot < 0)
                    throw new ArgumentException($"Target texture contains ID 0x{t:X6}, which is not among the required pieces.", nameof(_targetTexture));
                seen[slot] = true;
            }
            for (int j = 0; j < seen.Length; j++)
            {
                if (!seen[j])
                    throw new ArgumentException($"Required piece ID 0x{_requiredIds[j]:X6} never appears in the target texture.", nameof(_targetTexture));
            }
        }

        private void RenderAndRequest(bool manual)
        {
            if (_requestInFlight)
            {
                if (manual)
                    Debug.LogWarning("PaintingCompositionEvaluator already has an async readback in flight; the request was skipped.");
                return;
            }

            // Rebuild the ID pass: draw every cached renderer/submesh of
            // every piece with its per-ID Unlit material, using the
            // composition camera's matrices. The projection is flipped for
            // render-into-texture (GL.GetGPUProjectionMatrix with
            // renderIntoTexture: true) so readback rows land bottom-up,
            // exactly like Texture2D.GetPixels32(); on Direct3D/Metal/Vulkan
            // the flip reflects winding, so backface culling is inverted to
            // match.
            _commandBuffer.Clear();
            _commandBuffer.SetRenderTarget(_targetRt);
            _commandBuffer.ClearRenderTarget(true, true, Color.black,
                SystemInfo.usesReversedZBuffer ? 0f : 1f);
            _commandBuffer.SetViewProjectionMatrices(
                _compositionCamera.worldToCameraMatrix,
                GL.GetGPUProjectionMatrix(BuildSamplingProjection(), true));
            if (RequiresProjectionFlip())
                _commandBuffer.SetInvertCulling(true);

            for (int d = 0; d < _draws.Count; d++)
            {
                RendererDraw draw = _draws[d];
                if (draw.Renderer == null || draw.Material == null)
                    continue;
                for (int s = 0; s < draw.SubMeshCount; s++)
                    _commandBuffer.DrawRenderer(draw.Renderer, draw.Material, s);
            }

            Graphics.ExecuteCommandBuffer(_commandBuffer);

            _requestInFlight = true;
            AsyncGPUReadback.Request(_targetRt, 0, TextureFormat.RGBA32, OnReadbackCompleted);
        }

        private Matrix4x4 BuildSamplingProjection()
        {
            float aspect = (float)_width / _height;
            if (_compositionCamera.orthographic)
            {
                float halfHeight = _compositionCamera.orthographicSize;
                float halfWidth = halfHeight * aspect;
                return Matrix4x4.Ortho(-halfWidth, halfWidth, -halfHeight, halfHeight,
                    _compositionCamera.nearClipPlane, _compositionCamera.farClipPlane);
            }

            return Matrix4x4.Perspective(_compositionCamera.fieldOfView, aspect,
                _compositionCamera.nearClipPlane, _compositionCamera.farClipPlane);
        }

        private void OnReadbackCompleted(AsyncGPUReadbackRequest request)
        {
            if (this == null) // destroyed while the readback was in flight
                return;
            _requestInFlight = false;

            if (request.hasError)
            {
                Debug.LogWarning("PaintingCompositionEvaluator: async readback of the ID render failed; the sample was skipped.");
                return;
            }
            if (!request.done)
                return;

            // Never evaluate after disable/destroy or a reconfiguration.
            if (!_configured || !isActiveAndEnabled)
                return;

            NativeArray<byte> data = request.GetData<byte>();
            if (!data.IsCreated || data.Length != _width * _height * 4)
            {
                Debug.LogWarning("PaintingCompositionEvaluator: unexpected readback payload size; the sample was skipped.");
                return;
            }

            var bytes = new byte[data.Length];
            data.CopyTo(bytes);
            uint[] packed = PackReadbackBytes(bytes, _width, _height);
            CompositionIdBuffer current = CompositionIdBuffer.FromPixels(_width, _height, packed);
            CompositionScoreResult result = CompositionScorer.Compare(_targetBuffer, current, _requiredIds, Policy);
            LatestResult = result;
            Evaluated?.Invoke(result);
        }

        private void RebuildResources()
        {
            ReleaseResources();

            _targetRt = new RenderTexture(_width, _height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
            {
                name = "PaintingCompositionId",
                filterMode = FilterMode.Point,
                hideFlags = HideFlags.HideAndDontSave
            };
            _targetRt.Create();

            _commandBuffer = new CommandBuffer { name = "PaintingCompositionIdPass" };

            Shader idShader = Shader.Find(IdShaderName);
            _idMaterials = new Dictionary<uint, Material>(_requiredIds.Length);
            _draws.Clear();
            for (int i = 0; i < _requiredIds.Length; i++)
            {
                uint id = _requiredIds[i];
                var material = new Material(idShader)
                {
                    name = $"PaintingId-{id:X6}",
                    hideFlags = HideFlags.HideAndDontSave
                };
                material.SetColor(BaseColorProperty, IdToColor(id));
                material.SetFloat(ZTestProperty, (float)(SystemInfo.usesReversedZBuffer
                    ? CompareFunction.GreaterEqual
                    : CompareFunction.LessEqual));
                _idMaterials.Add(id, material);

                // Cache every renderer/submesh below the piece root once so
                // the per-sample pass never discovers renderers or allocates.
                PaintingPieceId piece = _pieces[i];
                if (piece == null)
                    continue;
                IReadOnlyList<Renderer> renderers = piece.Renderers;
                for (int r = 0; r < renderers.Count; r++)
                {
                    Renderer renderer = renderers[r];
                    if (renderer == null)
                        continue;
                    _draws.Add(new RendererDraw(renderer, material, GetSubmeshCount(renderer)));
                }
            }

            _resourcesBuilt = true;
        }

        private void ReleaseResources()
        {
            _resourcesBuilt = false;
            if (_targetRt != null)
            {
                _targetRt.Release();
                Destroy(_targetRt);
                _targetRt = null;
            }
            if (_commandBuffer != null)
            {
                _commandBuffer.Dispose();
                _commandBuffer = null;
            }
            if (_idMaterials != null)
            {
                foreach (Material material in _idMaterials.Values)
                {
                    if (material != null)
                        Destroy(material);
                }
                _idMaterials.Clear();
            }
            _draws.Clear();
        }

        /// <summary>
        /// Number of submeshes a renderer draws with. Renderer exposes no
        /// submesh count directly; it is read from the mesh at rebuild time.
        /// </summary>
        private static int GetSubmeshCount(Renderer renderer)
        {
            if (renderer is MeshRenderer)
            {
                var filter = renderer.GetComponent<MeshFilter>();
                return filter != null && filter.sharedMesh != null ? filter.sharedMesh.subMeshCount : 1;
            }
            if (renderer is SkinnedMeshRenderer skinned)
                return skinned.sharedMesh != null ? skinned.sharedMesh.subMeshCount : 1;
            return 1;
        }

        /// <summary>
        /// True on graphics APIs whose NDC Y axis points down, where
        /// <see cref="GL.GetGPUProjectionMatrix"/> flips the projection for
        /// render-into-texture and the flip reflects triangle winding.
        /// </summary>
        private static bool RequiresProjectionFlip()
        {
            switch (SystemInfo.graphicsDeviceType)
            {
                case GraphicsDeviceType.Direct3D11:
                case GraphicsDeviceType.Direct3D12:
                case GraphicsDeviceType.Metal:
                case GraphicsDeviceType.Vulkan:
                    return true;
                default:
                    return false;
            }
        }

        private static Color IdToColor(uint id)
        {
            return new Color(((id >> 16) & 0xFF) / 255f, ((id >> 8) & 0xFF) / 255f, (id & 0xFF) / 255f, 1f);
        }

        /// <summary>
        /// Packs readback bytes into row-major packed 0xRRGGBB pixels. The
        /// input is the RGBA byte layout (byte 0 = red) produced by
        /// <see cref="AsyncGPUReadback.Request(RenderTexture, int, TextureFormat, Action{AsyncGPUReadbackRequest})"/>
        /// with <see cref="TextureFormat.RGBA32"/>. Rows follow Unity's
        /// <see cref="Texture2D.GetPixels32()"/> convention: index 0 is the
        /// bottom-left pixel and rows progress bottom to top. GPU readback rows
        /// arrive top to bottom on the supported render-texture path, so this
        /// method reverses row order. This method is the single place that knows
        /// about readback layout, so T-009B2 can pin the convention down with
        /// tests.
        /// </summary>
        internal static uint[] PackReadbackBytes(byte[] bytes, int width, int height)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be positive.");
            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be positive.");
            if (bytes.Length != width * height * 4)
                throw new ArgumentException($"Expected {width * height * 4} bytes for {width}x{height} RGBA data, got {bytes.Length}.", nameof(bytes));

            var packed = new uint[width * height];
            for (int sourceY = 0; sourceY < height; sourceY++)
            {
                int destinationY = height - 1 - sourceY;
                for (int x = 0; x < width; x++)
                {
                    int source = (sourceY * width + x) * 4;
                    int destination = destinationY * width + x;
                    packed[destination] = ((uint)bytes[source] << 16)
                        | ((uint)bytes[source + 1] << 8)
                        | bytes[source + 2];
                }
            }
            return packed;
        }

        /// <summary>
        /// Packs a <see cref="Texture2D.GetPixels32()"/> result into row-major
        /// packed 0xRRGGBB pixels, using the same bottom-up row convention as
        /// <see cref="PackReadbackBytes"/> so target and current buffers
        /// align pixel-for-pixel.
        /// </summary>
        internal static uint[] PackColor32s(Color32[] colors, int width, int height)
        {
            if (colors == null)
                throw new ArgumentNullException(nameof(colors));
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be positive.");
            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be positive.");
            if (colors.Length != width * height)
                throw new ArgumentException($"Expected {width * height} colors for {width}x{height} data, got {colors.Length}.", nameof(colors));

            var packed = new uint[width * height];
            for (int i = 0; i < colors.Length; i++)
                packed[i] = ((uint)colors[i].r << 16) | ((uint)colors[i].g << 8) | colors[i].b;
            return packed;
        }
    }
}
