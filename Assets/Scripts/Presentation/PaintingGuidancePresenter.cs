using System;
using UnityEngine;
using UnityEngine.UI;
using PerspectivePuzzle.Domain;

namespace PerspectivePuzzle.Presentation
{
    /// <summary>
    /// Product-facing T-011 guidance rail: keeps a live beauty view from the
    /// Composition Camera beside the target painting, then translates the
    /// machine score into one calm progress line and one worst-piece focus
    /// hint. Object-ID colours, grids and raw percentages are never shown.
    /// </summary>
    public sealed class PaintingGuidancePresenter : MonoBehaviour
    {
        [SerializeField] private PaintingCompositionEvaluator _evaluator;
        [SerializeField] private Camera _compositionCamera;
        [SerializeField] private RawImage _targetImage;
        [SerializeField] private RawImage _liveImage;
        [SerializeField] private RectTransform _progressFill;
        [SerializeField] private Text _statusText;
        [SerializeField] private Text _focusText;
        [SerializeField] private string[] _pieceNames = Array.Empty<string>();
        [SerializeField, Min(64)] private int _liveWidth = 640;
        [SerializeField, Min(64)] private int _liveHeight = 360;
        [SerializeField, Min(0.01f)] private float _progressSmoothTime = 0.22f;

        private RenderTexture _liveTexture;
        private RenderTexture _previousCameraTarget;
        private float _targetProgress;
        private float _displayedProgress;
        private float _progressVelocity;
        private int _worstPieceIndex = -1;
        private bool _configured;

        public bool IsConfigured => _configured;
        public RenderTexture LiveTexture => _liveTexture;
        public Texture TargetTexture => _targetImage != null ? _targetImage.texture : null;
        public float DisplayedProgress => _displayedProgress;
        public int WorstPieceIndex => _worstPieceIndex;
        public string Status => _statusText != null ? _statusText.text : string.Empty;
        public string Focus => _focusText != null ? _focusText.text : string.Empty;

        private void Awake()
        {
            if (_evaluator != null && _compositionCamera != null && _targetImage != null
                && _liveImage != null && _progressFill != null && _statusText != null
                && _focusText != null && _pieceNames != null && _pieceNames.Length > 0)
            {
                Configure();
            }
        }

        private void OnEnable()
        {
            if (_configured)
                _evaluator.Evaluated += OnEvaluated;
        }

        private void OnDisable()
        {
            if (_configured && _evaluator != null)
                _evaluator.Evaluated -= OnEvaluated;
            ReleaseLiveTexture();
        }

        private void OnDestroy()
        {
            ReleaseLiveTexture();
        }

        private void Update()
        {
            if (!_configured)
                return;
            _displayedProgress = Mathf.SmoothDamp(
                _displayedProgress, _targetProgress, ref _progressVelocity, _progressSmoothTime);
            ApplyProgress(_displayedProgress);
        }

        /// <summary>Validates serialized wiring, creates the live view and initializes restrained copy.</summary>
        public void Configure()
        {
            if (_evaluator == null) throw new ArgumentNullException(nameof(_evaluator));
            if (_compositionCamera == null) throw new ArgumentNullException(nameof(_compositionCamera));
            if (_targetImage == null) throw new ArgumentNullException(nameof(_targetImage));
            if (_liveImage == null) throw new ArgumentNullException(nameof(_liveImage));
            if (_progressFill == null) throw new ArgumentNullException(nameof(_progressFill));
            if (_statusText == null) throw new ArgumentNullException(nameof(_statusText));
            if (_focusText == null) throw new ArgumentNullException(nameof(_focusText));
            if (_pieceNames == null || _pieceNames.Length == 0)
                throw new ArgumentException("At least one display piece name is required.", nameof(_pieceNames));
            if (_liveWidth < 64 || _liveHeight < 64)
                throw new ArgumentOutOfRangeException(nameof(_liveWidth));

            ReleaseLiveTexture();
            _liveTexture = new RenderTexture(_liveWidth, _liveHeight, 24, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB)
            {
                name = "Painting Live View (Runtime)",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false
            };
            _liveTexture.Create();
            _previousCameraTarget = _compositionCamera.targetTexture;
            _compositionCamera.targetTexture = _liveTexture;
            _liveImage.texture = _liveTexture;
            _liveImage.uvRect = new Rect(0f, 0f, 1f, 1f);

            _targetProgress = _displayedProgress = 0f;
            _progressVelocity = 0f;
            _worstPieceIndex = -1;
            _statusText.text = "Arrange the scene";
            _focusText.text = "Match the target painting";
            ApplyProgress(0f);
            _configured = true;
        }

        private void OnEvaluated(CompositionScoreResult result)
        {
            if (result == null)
                return;
            _targetProgress = Mathf.Clamp01(result.WeightedScore);

            int count = Mathf.Min(result.Pieces.Count, _pieceNames.Length);
            float worstCoverage = float.PositiveInfinity;
            _worstPieceIndex = -1;
            for (int i = 0; i < count; i++)
            {
                if (result.Pieces[i].TargetCoverage < worstCoverage)
                {
                    worstCoverage = result.Pieces[i].TargetCoverage;
                    _worstPieceIndex = i;
                }
            }

            if (result.PassesPolicy)
            {
                _statusText.text = "Painting aligned";
                _focusText.text = "Hold the composition";
            }
            else
            {
                _statusText.text = result.WeightedScore < 0.55f
                    ? "Arrange the scene"
                    : result.WeightedScore < 0.82f ? "Composition forming" : "Almost aligned";
                _focusText.text = _worstPieceIndex >= 0
                    ? "Focus: " + _pieceNames[_worstPieceIndex]
                    : "Match the target painting";
            }
        }

        private void ApplyProgress(float value)
        {
            Vector2 anchorMax = _progressFill.anchorMax;
            anchorMax.x = Mathf.Clamp01(value);
            _progressFill.anchorMax = anchorMax;
        }

        private void ReleaseLiveTexture()
        {
            if (_compositionCamera != null && _compositionCamera.targetTexture == _liveTexture)
                _compositionCamera.targetTexture = _previousCameraTarget;
            if (_liveImage != null && _liveImage.texture == _liveTexture)
                _liveImage.texture = null;
            if (_liveTexture != null)
            {
                _liveTexture.Release();
                Destroy(_liveTexture);
                _liveTexture = null;
            }
        }
    }
}
