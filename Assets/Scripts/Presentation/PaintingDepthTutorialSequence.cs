using System;
using System.Collections.Generic;
using UnityEngine;
using PerspectivePuzzle.Domain;

namespace PerspectivePuzzle.Presentation
{
    /// <summary>Moon Garden guidance without hidden ability gates.</summary>
    public sealed class PaintingDepthTutorialSequence : MonoBehaviour
    {
        [SerializeField] private PaintingCompositionEvaluator _evaluator;
        [SerializeField] private PaintingManipulationController _manipulation;
        [SerializeField] private PaintingManipulablePiece _bridge;
        [SerializeField] private PaintingManipulablePiece _farMountain;
        [SerializeField] private PaintingManipulablePiece _middleMountain;
        [SerializeField] private PaintingManipulablePiece _pavilion;
        [SerializeField] private int _bridgeIndex = 6;
        [SerializeField] private int _farMountainIndex = 1;
        [SerializeField] private int _middleMountainIndex = 2;
        [SerializeField] private int _pavilionIndex = 5;

        public bool IsConfigured { get; private set; }
        public int CurrentStep { get; private set; } = 1;
        public bool CompletionReady { get; private set; }
        public event Action StepChanged;
        private PieceStart _farMountainStart;
        private PieceStart _middleMountainStart;
        private PieceStart _pavilionStart;

        private void Awake()
        {
            if (_evaluator != null && _manipulation != null && _bridge != null && _farMountain != null
                && _middleMountain != null && _pavilion != null)
                Configure();
        }

        private void OnEnable() { if (IsConfigured) _evaluator.Diagnosed += OnDiagnosed; }
        private void OnDisable() { if (IsConfigured && _evaluator != null) _evaluator.Diagnosed -= OnDiagnosed; }

        public void Configure()
        {
            _farMountainStart = new PieceStart(_farMountain);
            _middleMountainStart = new PieceStart(_middleMountain);
            _pavilionStart = new PieceStart(_pavilion);
            // All four pieces are visible in their honest unsolved poses and
            // operable from the start. The old staged gate restored locked
            // pieces to the answer, then made them jump when unlocked.
            SetAvailability(bridge: true, mountains: true, pavilion: true);
            CurrentStep = 1;
            CompletionReady = false;
            IsConfigured = true;
        }

        public bool IsStagePiece(PaintingManipulablePiece piece)
        {
            return piece == _bridge || piece == _farMountain
                || piece == _middleMountain || piece == _pavilion;
        }

        public int PreferredEvaluatorIndex(IReadOnlyList<PieceVisualDiagnostic> diagnostics)
        {
            if (diagnostics == null) return _bridgeIndex;
            if (!IsAligned(diagnostics, _bridgeIndex)) return _bridgeIndex;
            if (!IsAligned(diagnostics, _farMountainIndex)) return _farMountainIndex;
            if (!IsAligned(diagnostics, _middleMountainIndex)) return _middleMountainIndex;
            return _pavilionIndex;
        }

        private void OnDiagnosed(IReadOnlyList<PieceVisualDiagnostic> diagnostics)
        {
            if (diagnostics == null) return;
            int previousStep = CurrentStep;
            CurrentStep = !IsAligned(diagnostics, _bridgeIndex) ? 1
                : (!IsAligned(diagnostics, _farMountainIndex) || !IsAligned(diagnostics, _middleMountainIndex)) ? 2
                : 3;
            CompletionReady = IsAligned(diagnostics, _bridgeIndex)
                && IsAligned(diagnostics, _farMountainIndex)
                && IsAligned(diagnostics, _middleMountainIndex)
                && IsAligned(diagnostics, _pavilionIndex);
            if (previousStep != CurrentStep) StepChanged?.Invoke();
        }

        private static bool IsAligned(IReadOnlyList<PieceVisualDiagnostic> diagnostics, int index)
            => index >= 0 && index < diagnostics.Count
                && diagnostics[index].Guidance == VisualGuidanceKind.NearlyAligned;

        private void SetAvailability(bool bridge, bool mountains, bool pavilion)
        {
            _manipulation.SetPieceAvailable(_bridge, bridge);
            _manipulation.SetPieceAvailable(_farMountain, mountains);
            _manipulation.SetPieceAvailable(_middleMountain, mountains);
            _manipulation.SetPieceAvailable(_pavilion, pavilion);
        }

        private readonly struct PieceStart
        {
            private readonly Vector3 _position;
            private readonly Quaternion _rotation;
            private readonly Vector3 _scale;
            public PieceStart(PaintingManipulablePiece piece)
            {
                _position = piece.Root.position;
                _rotation = piece.Root.rotation;
                _scale = piece.Root.localScale;
            }
            public void Apply(PaintingManipulablePiece piece)
            {
                piece.Root.position = _position;
                piece.Root.rotation = _rotation;
                piece.Root.localScale = _scale;
            }
        }
    }
}
