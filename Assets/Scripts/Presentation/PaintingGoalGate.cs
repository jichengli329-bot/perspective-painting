using System;
using System.Collections.Generic;
using UnityEngine;

namespace PerspectivePuzzle.Presentation
{
    /// <summary>Combines one primary painting goal with optional silhouette goals.</summary>
    public sealed class PaintingGoalGate : MonoBehaviour
    {
        [SerializeField] private PaintingCompositionEvaluator _primary;
        [SerializeField] private PaintingCompositionEvaluator[] _secondary = Array.Empty<PaintingCompositionEvaluator>();
        [SerializeField, Range(0f, 1f)] private float _secondarySilhouetteThreshold = 0.82f;

        public PaintingCompositionEvaluator Primary => _primary;
        public bool IsConfigured => _primary != null && _secondary != null;
        public bool IsSatisfied
        {
            get
            {
                if (!IsConfigured || _primary.LatestResult == null) return false;
                var scores = new float[_secondary.Length];
                for (int i = 0; i < _secondary.Length; i++)
                    scores[i] = _secondary[i]?.LatestResult?.SilhouetteIoU ?? -1f;
                return AreGoalsSatisfied(_primary.LatestResult.PassesPolicy, scores, _secondarySilhouetteThreshold);
            }
        }

        public int GoalCount => 1 + (_secondary?.Length ?? 0);
        public float SecondaryProgress => _secondary == null || _secondary.Length == 0
            ? 1f : (_secondary[0]?.LatestResult?.SilhouetteIoU ?? 0f);

        public void Configure(PaintingCompositionEvaluator primary,
            PaintingCompositionEvaluator[] secondary, float secondarySilhouetteThreshold = 0.82f)
        {
            _primary = primary != null ? primary : throw new ArgumentNullException(nameof(primary));
            _secondary = secondary ?? Array.Empty<PaintingCompositionEvaluator>();
            if (secondarySilhouetteThreshold < 0f || secondarySilhouetteThreshold > 1f)
                throw new ArgumentOutOfRangeException(nameof(secondarySilhouetteThreshold));
            for (int i = 0; i < _secondary.Length; i++)
                if (_secondary[i] == null) throw new ArgumentException("Secondary evaluators cannot contain null.", nameof(secondary));
            _secondarySilhouetteThreshold = secondarySilhouetteThreshold;
        }

        public static bool AreGoalsSatisfied(bool primaryPass, IReadOnlyList<float> secondaryScores, float threshold)
        {
            if (secondaryScores == null) throw new ArgumentNullException(nameof(secondaryScores));
            if (threshold < 0f || threshold > 1f) throw new ArgumentOutOfRangeException(nameof(threshold));
            if (!primaryPass) return false;
            for (int i = 0; i < secondaryScores.Count; i++)
                if (secondaryScores[i] < threshold) return false;
            return true;
        }
    }
}
