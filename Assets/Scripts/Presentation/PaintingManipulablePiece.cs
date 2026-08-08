using System;
using System.Collections.Generic;
using UnityEngine;

namespace PerspectivePuzzle.Presentation
{
    /// <summary>
    /// T-010B: marks one manipulable scenery piece — any direct scenery root
    /// wired by the deterministic scene builder — and owns everything that
    /// piece needs for manipulation: the authored world
    /// position/rotation/local scale captured exactly once during the
    /// deterministic scene build (and serialized into the scene so runtime
    /// reset never re-captures a manipulated pose), the signed-yaw offset
    /// relative to the authored rotation, the cached renderers below the
    /// root and the single selection collider on it, and the renderer-based
    /// selected/unselected visual feedback. Feedback is
    /// applied through per-renderer <see cref="MaterialPropertyBlock"/>s, so
    /// no shared material is ever instantiated or mutated and the evaluator's
    /// separate per-ID materials are never affected; the highlight is a
    /// restrained warm color accent, and prior property blocks are restored on
    /// deselect or disable.
    /// </summary>
    public sealed class PaintingManipulablePiece : MonoBehaviour
    {
        private const string BaseColorProperty = "_BaseColor";
        private static readonly Color HighlightTint = new Color(1f, 0.82f, 0.62f);
        private const float HighlightAmount = 0.22f;

        [SerializeField] private bool _configured;
        [SerializeField] private Vector3 _authoredPosition;
        [SerializeField] private Quaternion _authoredRotation;
        [SerializeField] private Vector3 _authoredLocalScale;

        private Renderer[] _renderers = Array.Empty<Renderer>();
        private Collider _collider;
        private bool _selected;
        private bool _highlightApplied;
        private readonly List<RendererBlockState> _blockStates = new List<RendererBlockState>();

        /// <summary>True once <see cref="Configure"/> captured the authored state; serialized with the scene.</summary>
        public bool IsConfigured => _configured;

        /// <summary>The piece root this component manipulates.</summary>
        public Transform Root => transform;

        /// <summary>World position at deterministic configuration time.</summary>
        public Vector3 AuthoredPosition => _authoredPosition;

        /// <summary>World rotation at deterministic configuration time.</summary>
        public Quaternion AuthoredRotation => _authoredRotation;

        /// <summary>Local scale at deterministic configuration time.</summary>
        public Vector3 AuthoredLocalScale => _authoredLocalScale;

        /// <summary>Renderers below the root, including inactive ones; empty until configured.</summary>
        public IReadOnlyList<Renderer> Renderers => _renderers;

        /// <summary>
        /// The single selection collider on the root. Re-caches lazily so the
        /// manipulation controller works regardless of Awake order; throws
        /// when the configured piece has no collider, so a miswired piece
        /// fails loudly.
        /// </summary>
        public Collider SelectionCollider
        {
            get
            {
                EnsureCached();
                if (_collider == null)
                    throw new InvalidOperationException($"PaintingManipulablePiece on '{name}' has no collider on its root.");
                return _collider;
            }
        }

        private void Awake()
        {
            // The renderer/collider caches are scene references, not
            // serialized state; re-cache after every scene load. The authored
            // transform is serialized and is never re-captured at runtime.
            EnsureCached();
        }

        private void OnEnable()
        {
            if (_selected)
                ApplyHighlight();
        }

        private void OnDisable()
        {
            // Restore prior property blocks even when the piece is disabled
            // mid-selection, so no highlight leaks into the scene.
            RestorePropertyBlocks();
        }

        private void OnDestroy()
        {
            RestorePropertyBlocks();
        }

        /// <summary>
        /// Captures the authored world position/rotation/local scale exactly
        /// once, caches every renderer below the root and validates the single
        /// selection collider on it. Called by the deterministic editor scene
        /// builder after the piece root, its fitted collider and all child
        /// renderers are final, so the captured values are the true authored
        /// pose. Throws on a second call, on a missing renderer, or when the
        /// root does not carry exactly one collider, so a miswired piece
        /// fails the build loudly.
        /// </summary>
        /// <exception cref="InvalidOperationException">Already configured, no renderer below the root, or not exactly one collider on the root.</exception>
        public void Configure()
        {
            if (_configured)
                throw new InvalidOperationException(
                    $"PaintingManipulablePiece on '{name}' is already configured; the authored transform is captured exactly once.");

            Collider[] colliders = GetComponents<Collider>();
            if (colliders.Length != 1)
                throw new InvalidOperationException(
                    $"PaintingManipulablePiece on '{name}' must have exactly one selection collider on its root, found {colliders.Length}.");

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                throw new InvalidOperationException($"PaintingManipulablePiece on '{name}' has no Renderer below its root.");

            _authoredPosition = transform.position;
            _authoredRotation = transform.rotation;
            _authoredLocalScale = transform.localScale;
            _renderers = renderers;
            _collider = colliders[0];
            _configured = true;
        }

        /// <summary>
        /// Re-caches the renderers and the selection collider after a scene
        /// reload, so the component works regardless of Awake order relative
        /// to the manipulation controller. No-op when not configured; throws
        /// when a configured piece is missing its renderer or collider.
        /// </summary>
        public void EnsureCached()
        {
            if (!_configured)
                return;
            if (_renderers.Length == 0)
            {
                Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0)
                    throw new InvalidOperationException($"PaintingManipulablePiece on '{name}' has no Renderer below its root.");
                _renderers = renderers;
            }
            if (_collider == null)
            {
                _collider = GetComponent<Collider>();
                if (_collider == null)
                    throw new InvalidOperationException($"PaintingManipulablePiece on '{name}' has no collider on its root.");
            }
        }

        /// <summary>Restores the exact authored world pose.</summary>
        public void RestoreAuthored()
        {
            transform.position = _authoredPosition;
            transform.rotation = _authoredRotation;
            transform.localScale = _authoredLocalScale;
        }

        /// <summary>
        /// Signed yaw offset in degrees of <paramref name="worldRotation"/>
        /// relative to this piece's authored rotation around the authored Y
        /// axis, normalized to (-180, 180]. Stable for any world rotation and
        /// never changes the authored pose; zero until configured.
        /// </summary>
        public float AuthoredSignedYawOffset(Quaternion worldRotation)
        {
            float yaw = (Quaternion.Inverse(_authoredRotation) * worldRotation).eulerAngles.y;
            return yaw > 180f ? yaw - 360f : yaw;
        }

        /// <summary>
        /// Owns the renderer-based selection feedback: while selected, every
        /// cached renderer draws with a restrained warm color accent through
        /// its own <see cref="MaterialPropertyBlock"/> (shared materials are
        /// never instantiated or mutated, and the evaluator's separate ID
        /// materials are never touched); on deselect the prior property blocks
        /// are restored.
        /// </summary>
        public void SetSelected(bool selected)
        {
            if (selected == _selected)
                return;
            _selected = selected;
            if (_selected)
                ApplyHighlight();
            else
                RestorePropertyBlocks();
        }

        private void ApplyHighlight()
        {
            if (_highlightApplied)
                return;

            _blockStates.Clear();
            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer renderer = _renderers[i];
                if (renderer == null)
                    continue;

                var prior = new MaterialPropertyBlock();
                bool hadBlock = renderer.HasPropertyBlock();
                if (hadBlock)
                    renderer.GetPropertyBlock(prior);

                Color baseColor = Color.white;
                if (renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty(BaseColorProperty))
                    baseColor = renderer.sharedMaterial.GetColor(BaseColorProperty);

                var block = new MaterialPropertyBlock();
                if (hadBlock)
                    renderer.GetPropertyBlock(block);
                block.SetColor(BaseColorProperty, Color.Lerp(baseColor, HighlightTint, HighlightAmount));
                renderer.SetPropertyBlock(block);

                _blockStates.Add(new RendererBlockState(renderer, hadBlock, prior));
            }
            _highlightApplied = true;
        }

        private void RestorePropertyBlocks()
        {
            if (!_highlightApplied)
                return;
            for (int i = 0; i < _blockStates.Count; i++)
            {
                RendererBlockState state = _blockStates[i];
                if (state.Renderer == null)
                    continue;
                if (state.HadBlock)
                    state.Renderer.SetPropertyBlock(state.PriorBlock);
                else
                    state.Renderer.SetPropertyBlock(null);
            }
            _blockStates.Clear();
            _highlightApplied = false;
        }

        /// <summary>One cached renderer plus its prior property block, so deselect restores it exactly.</summary>
        private readonly struct RendererBlockState
        {
            public readonly Renderer Renderer;
            public readonly bool HadBlock;
            public readonly MaterialPropertyBlock PriorBlock;

            public RendererBlockState(Renderer renderer, bool hadBlock, MaterialPropertyBlock priorBlock)
            {
                Renderer = renderer;
                HadBlock = hadBlock;
                PriorBlock = priorBlock;
            }
        }
    }
}
