using System;
using UnityEngine;
using PerspectivePuzzle.Domain;

namespace PerspectivePuzzle.Presentation
{
    /// <summary>Visual-score-driven two-step onboarding. It never compares authored transforms.</summary>
    public sealed class PaintingTutorialSequence : MonoBehaviour
    {
        public enum AssistanceLevel { Normal, Warm, Rescue }
        [SerializeField] private PaintingCompositionEvaluator _evaluator;
        [SerializeField] private PaintingManipulationController _manipulation;
        [SerializeField] private PaintingManipulablePiece _bridge;
        [SerializeField] private PaintingManipulablePiece _pavilion;
        [SerializeField] private int _bridgeEvaluatorIndex = 6;
        [SerializeField] private int _pavilionEvaluatorIndex = 5;
        [SerializeField, Min(1f)] private float _warmAfterSeconds = 20f;
        [SerializeField, Min(1f)] private float _rescueAfterSeconds = 45f;
        [SerializeField, Min(0.1f)] private float _warmMagnetRadius = 1.15f;

        private bool _stepAttempted;
        private float _stepAttemptStartedAt;
        private AssistanceLevel _assistance;

        public bool IsConfigured { get; private set; }
        public bool PavilionUnlocked { get; private set; }
        public int CurrentStep => PavilionUnlocked ? 2 : 1;
        public AssistanceLevel Assistance => _assistance;
        public PaintingManipulablePiece ActivePiece => PavilionUnlocked ? _pavilion : _bridge;
        public bool CompletionReady { get; private set; }
        public float ActiveStepElapsed => _stepAttempted ? Mathf.Max(0f, Time.unscaledTime - _stepAttemptStartedAt) : 0f;
        public event Action PavilionWasUnlocked;

        private void Awake()
        {
            if (_manipulation == null) _manipulation = GetComponent<PaintingManipulationController>();
            if (_evaluator != null && _manipulation != null && _bridge != null && _pavilion != null)
                Configure(_evaluator, _manipulation, _bridge, _pavilion, _bridgeEvaluatorIndex);
        }

        private void OnEnable()
        {
            if (IsConfigured) Subscribe();
        }

        private void OnDisable()
        {
            if (IsConfigured && _evaluator != null) Unsubscribe();
        }

        private void Update()
        {
            if (!IsConfigured || !_stepAttempted) return;
            AssistanceLevel next = AssistanceForElapsed(ActiveStepElapsed);
            if (next == _assistance) return;
            _assistance = next;
            ApplyMagnet();
        }

        public AssistanceLevel AssistanceForElapsed(float elapsedSeconds)
        {
            if (elapsedSeconds < 0f) throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            return elapsedSeconds >= _rescueAfterSeconds ? AssistanceLevel.Rescue
                : elapsedSeconds >= _warmAfterSeconds ? AssistanceLevel.Warm : AssistanceLevel.Normal;
        }

        public void ConfigureAssistance(float warmAfterSeconds, float rescueAfterSeconds, float magnetRadius)
        {
            if (warmAfterSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(warmAfterSeconds));
            if (rescueAfterSeconds <= warmAfterSeconds) throw new ArgumentOutOfRangeException(nameof(rescueAfterSeconds));
            if (magnetRadius <= 0f) throw new ArgumentOutOfRangeException(nameof(magnetRadius));
            _warmAfterSeconds = warmAfterSeconds;
            _rescueAfterSeconds = rescueAfterSeconds;
            _warmMagnetRadius = magnetRadius;
        }

        public void Configure(PaintingCompositionEvaluator evaluator, PaintingManipulationController manipulation,
            PaintingManipulablePiece bridge,
            PaintingManipulablePiece pavilion, int bridgeEvaluatorIndex)
        {
            _evaluator = evaluator != null ? evaluator : throw new ArgumentNullException(nameof(evaluator));
            _manipulation = manipulation != null ? manipulation : throw new ArgumentNullException(nameof(manipulation));
            _bridge = bridge != null ? bridge : throw new ArgumentNullException(nameof(bridge));
            _pavilion = pavilion != null ? pavilion : throw new ArgumentNullException(nameof(pavilion));
            if (bridgeEvaluatorIndex < 0) throw new ArgumentOutOfRangeException(nameof(bridgeEvaluatorIndex));
            _bridgeEvaluatorIndex = bridgeEvaluatorIndex;
            // Mist Valley now teaches free composition with both hero pieces.
            // Keeping the pavilion behind a hidden bridge-scoring gate made the
            // authored solution impossible to discover through normal play.
            PavilionUnlocked = true;
            CompletionReady = false;
            _assistance = AssistanceLevel.Normal;
            _stepAttempted = false;
            SetPavilionAvailable(true);
            IsConfigured = true;
        }

        private void OnDiagnosed(System.Collections.Generic.IReadOnlyList<PieceVisualDiagnostic> diagnostics)
        {
            if (diagnostics == null) return;
            if (!PavilionUnlocked)
            {
                if (_bridgeEvaluatorIndex >= diagnostics.Count
                    || diagnostics[_bridgeEvaluatorIndex].Guidance != VisualGuidanceKind.NearlyAligned) return;
                PavilionUnlocked = true;
                SetPavilionAvailable(true);
                BeginNewStep();
                PavilionWasUnlocked?.Invoke();
            }
            CompletionReady = _pavilionEvaluatorIndex >= 0 && _pavilionEvaluatorIndex < diagnostics.Count
                && diagnostics[_pavilionEvaluatorIndex].Guidance == VisualGuidanceKind.NearlyAligned;
        }

        private void SetPavilionAvailable(bool available)
        {
            _manipulation.SetPieceAvailable(_pavilion, available);
        }

        private void Subscribe()
        {
            _evaluator.Diagnosed += OnDiagnosed;
            _manipulation.PlacementStarted += OnPlacementStarted;
        }

        private void Unsubscribe()
        {
            _evaluator.Diagnosed -= OnDiagnosed;
            _manipulation.PlacementStarted -= OnPlacementStarted;
        }

        private void OnPlacementStarted(PaintingManipulablePiece piece)
        {
            if (piece != ActivePiece || _stepAttempted) return;
            _stepAttempted = true;
            _stepAttemptStartedAt = Time.unscaledTime;
        }

        private void BeginNewStep()
        {
            _stepAttempted = false;
            _stepAttemptStartedAt = 0f;
            _assistance = AssistanceLevel.Normal;
            ApplyMagnet();
        }

        private void ApplyMagnet()
        {
            bool widened = _assistance != AssistanceLevel.Normal;
            _manipulation.ConfigureSolutionMagnet(widened ? ActivePiece : null,
                widened ? _warmMagnetRadius : 0f);
        }
    }
}
