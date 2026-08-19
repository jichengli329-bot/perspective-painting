using System;
using UnityEngine;
using UnityEngine.UI;
using PerspectivePuzzle.Domain;

namespace PerspectivePuzzle.Presentation
{
    /// <summary>
    /// T-012 signature completion: after real player interaction and a stable
    /// passing composition, locks manipulation, fades the guidance rail,
    /// carries the Build Camera into the exact Composition Camera view, holds
    /// the completed painting, then moves to a side reveal showing the actual
    /// three-dimensional arrangement. Uses deterministic smoothstep timing;
    /// no tween package or physics state.
    /// </summary>
    public sealed class PaintingCompletionReveal : MonoBehaviour
    {
        public enum RevealPhase { Idle, ToPainting, HoldPainting, ToSecondary, HoldSecondary, ToPerspective, Complete }

        [SerializeField] private PaintingCompositionEvaluator _evaluator;
        [SerializeField] private PaintingGoalGate _goalGate;
        [SerializeField] private PaintingTutorialSequence _tutorialSequence;
        [SerializeField] private PaintingDepthTutorialSequence _depthTutorialSequence;
        [SerializeField] private PaintingManipulationController _manipulation;
        [SerializeField] private Camera _buildCamera;
        [SerializeField] private Camera _compositionCamera;
        [SerializeField] private Camera _secondaryRevealCamera;
        [SerializeField] private CanvasGroup _guidanceGroup;
        [SerializeField] private CanvasGroup _revealGroup;
        [SerializeField] private Text _revealText;
        [SerializeField] private Vector3 _perspectivePosition = new Vector3(-6.4f, 4.8f, 6.2f);
        [SerializeField] private Vector3 _perspectiveTarget = new Vector3(0f, 1.1f, 0f);
        [SerializeField, Min(0.1f)] private float _stablePassSeconds = 0.75f;
        [SerializeField, Min(0.1f)] private float _toPaintingSeconds = 1.45f;
        [SerializeField, Min(0f)] private float _holdPaintingSeconds = 1.05f;
        [SerializeField, Min(0f)] private float _holdSecondarySeconds = 1.0f;
        [SerializeField, Min(0.1f)] private float _toPerspectiveSeconds = 1.8f;

        private bool _configured;
        private bool _playerInteracted;
        private float _stableTimer;
        private float _phaseTimer;
        private Vector3 _phaseStartPosition;
        private Quaternion _phaseStartRotation;
        private float _phaseStartFov;

        public bool IsConfigured => _configured;
        public RevealPhase Phase { get; private set; }
        public bool IsRevealing => Phase != RevealPhase.Idle && Phase != RevealPhase.Complete;
        public bool HasCompleted => Phase == RevealPhase.Complete;
        public event Action RevealCompleted;

        private void Awake()
        {
            if (_evaluator != null && _manipulation != null && _buildCamera != null
                && _compositionCamera != null && _guidanceGroup != null
                && _revealGroup != null && _revealText != null)
                Configure();
        }

        private void OnEnable()
        {
            if (_configured)
                _manipulation.PlayerInteracted += OnPlayerInteracted;
        }

        private void OnDisable()
        {
            if (_configured && _manipulation != null)
                _manipulation.PlayerInteracted -= OnPlayerInteracted;
        }

        private void Update()
        {
            if (!_configured)
                return;
            if (Phase == RevealPhase.Idle)
            {
                CompositionScoreResult result = _evaluator.LatestResult;
                bool satisfied = _goalGate != null ? _goalGate.IsSatisfied : result != null && result.PassesPolicy;
                if (_tutorialSequence != null) satisfied &= _tutorialSequence.CompletionReady;
                if (_depthTutorialSequence != null) satisfied &= _depthTutorialSequence.CompletionReady;
                if (_playerInteracted && satisfied)
                {
                    _stableTimer += Time.unscaledDeltaTime;
                    if (_stableTimer >= _stablePassSeconds)
                        BeginReveal();
                }
                else
                {
                    _stableTimer = 0f;
                }
                return;
            }

            _phaseTimer += Time.unscaledDeltaTime;
            switch (Phase)
            {
                case RevealPhase.ToPainting:
                    UpdateToPainting();
                    break;
                case RevealPhase.HoldPainting:
                    if (_phaseTimer >= _holdPaintingSeconds)
                    {
                        if (_secondaryRevealCamera != null) BeginSecondaryMove();
                        else BeginPerspectiveMove();
                    }
                    break;
                case RevealPhase.ToSecondary:
                    UpdateToSecondary();
                    break;
                case RevealPhase.HoldSecondary:
                    if (_phaseTimer >= _holdSecondarySeconds) BeginPerspectiveMove();
                    break;
                case RevealPhase.ToPerspective:
                    UpdateToPerspective();
                    break;
            }
        }

        public void Configure()
        {
            if (_evaluator == null) throw new ArgumentNullException(nameof(_evaluator));
            if (_manipulation == null) throw new ArgumentNullException(nameof(_manipulation));
            if (_buildCamera == null) throw new ArgumentNullException(nameof(_buildCamera));
            if (_compositionCamera == null) throw new ArgumentNullException(nameof(_compositionCamera));
            if (_guidanceGroup == null) throw new ArgumentNullException(nameof(_guidanceGroup));
            if (_revealGroup == null) throw new ArgumentNullException(nameof(_revealGroup));
            if (_revealText == null) throw new ArgumentNullException(nameof(_revealText));
            Phase = RevealPhase.Idle;
            _revealGroup.alpha = 0f;
            _revealGroup.blocksRaycasts = false;
            _configured = true;
        }

        /// <summary>Public deterministic entry used by the completion trigger and PlayMode tests.</summary>
        public void BeginReveal()
        {
            if (!_configured || Phase != RevealPhase.Idle)
                return;
            _manipulation.SetInputLocked(true);
            Phase = RevealPhase.ToPainting;
            _phaseTimer = 0f;
            CaptureCameraStart();
            _revealText.text = "画面重合";
            _revealGroup.alpha = 0f;
        }

        private void OnPlayerInteracted() => _playerInteracted = true;

        private void UpdateToPainting()
        {
            float t = Smooth01(_phaseTimer / _toPaintingSeconds);
            _buildCamera.transform.position = Vector3.LerpUnclamped(
                _phaseStartPosition, _compositionCamera.transform.position, t);
            _buildCamera.transform.rotation = Quaternion.SlerpUnclamped(
                _phaseStartRotation, _compositionCamera.transform.rotation, t);
            _buildCamera.fieldOfView = Mathf.LerpUnclamped(_phaseStartFov, _compositionCamera.fieldOfView, t);
            _guidanceGroup.alpha = 1f - t;
            _revealGroup.alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, t));
            if (_phaseTimer >= _toPaintingSeconds)
            {
                SnapToComposition();
                Phase = RevealPhase.HoldPainting;
                _phaseTimer = 0f;
            }
        }

        private void BeginPerspectiveMove()
        {
            Phase = RevealPhase.ToPerspective;
            _phaseTimer = 0f;
            CaptureCameraStart();
            _revealText.text = "透视结构揭晓";
        }

        private void BeginSecondaryMove()
        {
            Phase = RevealPhase.ToSecondary;
            _phaseTimer = 0f;
            CaptureCameraStart();
            _revealText.text = "第二视角重合";
        }

        private void UpdateToSecondary()
        {
            float t = Smooth01(_phaseTimer / _toPaintingSeconds);
            _buildCamera.transform.position = Vector3.LerpUnclamped(_phaseStartPosition, _secondaryRevealCamera.transform.position, t);
            _buildCamera.transform.rotation = Quaternion.SlerpUnclamped(_phaseStartRotation, _secondaryRevealCamera.transform.rotation, t);
            _buildCamera.fieldOfView = Mathf.LerpUnclamped(_phaseStartFov, _secondaryRevealCamera.fieldOfView, t);
            _revealGroup.alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 1f, t));
            if (_phaseTimer >= _toPaintingSeconds)
            {
                _buildCamera.transform.position = _secondaryRevealCamera.transform.position;
                _buildCamera.transform.rotation = _secondaryRevealCamera.transform.rotation;
                _buildCamera.fieldOfView = _secondaryRevealCamera.fieldOfView;
                Phase = RevealPhase.HoldSecondary;
                _phaseTimer = 0f;
            }
        }

        private void UpdateToPerspective()
        {
            float t = Smooth01(_phaseTimer / _toPerspectiveSeconds);
            Quaternion targetRotation = Quaternion.LookRotation(
                (_perspectiveTarget - _perspectivePosition).normalized, Vector3.up);
            _buildCamera.transform.position = Vector3.LerpUnclamped(_phaseStartPosition, _perspectivePosition, t);
            _buildCamera.transform.rotation = Quaternion.SlerpUnclamped(_phaseStartRotation, targetRotation, t);
            _revealGroup.alpha = 1f - Mathf.SmoothStep(0f, 0.72f, t);
            if (_phaseTimer >= _toPerspectiveSeconds)
            {
                _buildCamera.transform.position = _perspectivePosition;
                _buildCamera.transform.rotation = targetRotation;
                _revealGroup.alpha = 0f;
                Phase = RevealPhase.Complete;
                RevealCompleted?.Invoke();
            }
        }

        private void CaptureCameraStart()
        {
            _phaseStartPosition = _buildCamera.transform.position;
            _phaseStartRotation = _buildCamera.transform.rotation;
            _phaseStartFov = _buildCamera.fieldOfView;
        }

        private void SnapToComposition()
        {
            _buildCamera.transform.position = _compositionCamera.transform.position;
            _buildCamera.transform.rotation = _compositionCamera.transform.rotation;
            _buildCamera.fieldOfView = _compositionCamera.fieldOfView;
        }

        private static float Smooth01(float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * (3f - 2f * t);
        }
    }
}
