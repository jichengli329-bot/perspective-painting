using System;
using System.Collections.Generic;

namespace PerspectivePuzzle.Domain
{
    /// <summary>
    /// Scoring weights and pass thresholds for
    /// <see cref="CompositionScorer.Compare"/>. Weights are normalized by
    /// their sum at construction; thresholds live in [0, 1].
    /// </summary>
    public readonly struct CompositionPolicy
    {
        /// <summary>Normalized weight of the silhouette IoU term.</summary>
        public float SilhouetteWeight { get; }

        /// <summary>Normalized weight of the mean per-piece IoU term.</summary>
        public float PieceWeight { get; }

        /// <summary>Normalized weight of the identity accuracy term.</summary>
        public float IdentityWeight { get; }

        /// <summary>Minimum weighted score required to pass, in [0, 1].</summary>
        public float PassThreshold { get; }

        /// <summary>Minimum per-piece target coverage required to pass, in [0, 1].</summary>
        public float MinimumCoverageThreshold { get; }

        /// <summary>
        /// Creates a policy from raw nonnegative finite weights and pass
        /// thresholds. Weights are normalized by their sum; a zero sum and
        /// NaN/infinity weights or thresholds outside [0, 1] are rejected.
        /// </summary>
        public CompositionPolicy(float silhouetteWeight, float pieceWeight, float identityWeight, float passThreshold, float minimumCoverageThreshold)
        {
            if (float.IsNaN(silhouetteWeight) || float.IsNaN(pieceWeight) || float.IsNaN(identityWeight))
                throw new ArgumentException("Weights must not be NaN.", nameof(silhouetteWeight));
            if (float.IsInfinity(silhouetteWeight) || float.IsInfinity(pieceWeight) || float.IsInfinity(identityWeight))
                throw new ArgumentException("Weights must be finite.", nameof(silhouetteWeight));
            if (silhouetteWeight < 0 || pieceWeight < 0 || identityWeight < 0)
                throw new ArgumentOutOfRangeException(nameof(silhouetteWeight), silhouetteWeight, "Weights must be nonnegative.");

            float sum = silhouetteWeight + pieceWeight + identityWeight;
            if (sum <= 0)
                throw new ArgumentException("Weight sum must be positive.", nameof(silhouetteWeight));

            if (float.IsNaN(passThreshold) || float.IsNaN(minimumCoverageThreshold))
                throw new ArgumentException("Thresholds must not be NaN.", nameof(passThreshold));
            if (passThreshold < 0 || passThreshold > 1)
                throw new ArgumentOutOfRangeException(nameof(passThreshold), passThreshold, "Pass threshold must be within [0, 1].");
            if (minimumCoverageThreshold < 0 || minimumCoverageThreshold > 1)
                throw new ArgumentOutOfRangeException(nameof(minimumCoverageThreshold), minimumCoverageThreshold, "Minimum coverage threshold must be within [0, 1].");

            SilhouetteWeight = silhouetteWeight / sum;
            PieceWeight = pieceWeight / sum;
            IdentityWeight = identityWeight / sum;
            PassThreshold = passThreshold;
            MinimumCoverageThreshold = minimumCoverageThreshold;
        }

        /// <summary>
        /// 0.40 * silhouette + 0.45 * mean piece IoU + 0.15 * identity
        /// accuracy; passes at 0.93 weighted score and 0.80 minimum coverage.
        /// </summary>
        public static CompositionPolicy Default => new CompositionPolicy(0.40f, 0.45f, 0.15f, 0.93f, 0.80f);
    }

    /// <summary>
    /// Scores a current composition buffer against a target by silhouettes,
    /// per-piece IoU, and exact piece identity. Pure, deterministic, and free
    /// of Unity dependencies; intended for repeated 256x144 comparisons.
    /// </summary>
    public static class CompositionScorer
    {
        private const uint MaxRgbValue = 0x00FFFFFF;

        /// <summary>
        /// Compares <paramref name="current"/> against <paramref name="target"/>
        /// using the default policy.
        /// </summary>
        public static CompositionScoreResult Compare(CompositionIdBuffer target, CompositionIdBuffer current, IReadOnlyList<uint> requiredPieceIds)
        {
            return Compare(target, current, requiredPieceIds, CompositionPolicy.Default);
        }

        /// <summary>
        /// Compares <paramref name="current"/> against <paramref name="target"/>.
        /// Every required piece ID must appear in the target, and every
        /// nonblack target pixel must belong to a required piece.
        /// </summary>
        public static CompositionScoreResult Compare(CompositionIdBuffer target, CompositionIdBuffer current, IReadOnlyList<uint> requiredPieceIds, CompositionPolicy policy)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (current == null)
                throw new ArgumentNullException(nameof(current));
            if (requiredPieceIds == null)
                throw new ArgumentNullException(nameof(requiredPieceIds));
            if (requiredPieceIds.Count == 0)
                throw new ArgumentException("At least one required piece must be given.", nameof(requiredPieceIds));
            if (target.Width != current.Width || target.Height != current.Height)
                throw new ArgumentException("Target and current buffers must have matching dimensions.", nameof(current));

            for (int i = 0; i < requiredPieceIds.Count; i++)
            {
                uint id = requiredPieceIds[i];
                if (id == 0)
                    throw new ArgumentException("Required piece IDs must not be black.", nameof(requiredPieceIds));
                if (id > MaxRgbValue)
                    throw new ArgumentOutOfRangeException(nameof(requiredPieceIds), id, "Required piece ID must fit in 24-bit RGB (0x000000..0xFFFFFF).");
                for (int j = 0; j < i; j++)
                {
                    if (requiredPieceIds[j] == id)
                        throw new ArgumentException("Required piece IDs must be unique.", nameof(requiredPieceIds));
                }
            }

            // Every nonblack target pixel must be a required ID, and every
            // required ID must appear in the target.
            bool[] seen = new bool[requiredPieceIds.Count];
            for (int i = 0; i < target.PixelCount; i++)
            {
                uint t = target.GetPixelAt(i);
                if (t == 0)
                    continue;

                int slot = -1;
                for (int j = 0; j < requiredPieceIds.Count; j++)
                {
                    if (requiredPieceIds[j] == t)
                    {
                        slot = j;
                        break;
                    }
                }

                if (slot < 0)
                    throw new ArgumentException("Target contains a nonblack ID that is not a required piece.", nameof(target));
                seen[slot] = true;
            }

            for (int j = 0; j < seen.Length; j++)
            {
                if (!seen[j])
                    throw new ArgumentException("Every required piece ID must appear in the target.", nameof(requiredPieceIds));
            }

            // Single pass for silhouette and identity counters.
            int targetForeground = 0;
            int currentForeground = 0;
            int silhouetteIntersection = 0;
            int silhouetteUnion = 0;
            int exactSameId = 0;
            for (int i = 0; i < target.PixelCount; i++)
            {
                uint t = target.GetPixelAt(i);
                uint c = current.GetPixelAt(i);
                if (t != 0)
                    targetForeground++;
                if (c != 0)
                    currentForeground++;
                if (t != 0 && c != 0)
                    silhouetteIntersection++;
                if (t != 0 || c != 0)
                    silhouetteUnion++;
                if (t != 0 && t == c)
                    exactSameId++;
            }

            // Per-piece counters in required order.
            var pieces = new CompositionPieceResult[requiredPieceIds.Count];
            for (int j = 0; j < requiredPieceIds.Count; j++)
            {
                uint id = requiredPieceIds[j];
                int targetCount = 0;
                int currentCount = 0;
                int intersection = 0;
                for (int i = 0; i < target.PixelCount; i++)
                {
                    uint t = target.GetPixelAt(i);
                    uint c = current.GetPixelAt(i);
                    if (t == id)
                        targetCount++;
                    if (c == id)
                        currentCount++;
                    if (t == id && c == id)
                        intersection++;
                }

                pieces[j] = new CompositionPieceResult(id, targetCount, currentCount, intersection);
            }

            return new CompositionScoreResult(
                target.Width,
                target.Height,
                targetForeground,
                currentForeground,
                silhouetteIntersection,
                silhouetteUnion,
                exactSameId,
                pieces,
                policy);
        }
    }
}
