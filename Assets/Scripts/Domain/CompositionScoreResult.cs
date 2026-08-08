using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PerspectivePuzzle.Domain
{
    /// <summary>
    /// Immutable per-piece comparison outcome for one required piece ID.
    /// Instances are produced by <see cref="CompositionScorer.Compare"/>.
    /// </summary>
    public readonly struct CompositionPieceResult
    {
        /// <summary>Packed 24-bit RGB ID of this piece. Never black.</summary>
        public uint Id { get; }

        /// <summary>Foreground pixels of this ID in the target buffer.</summary>
        public int TargetPixels { get; }

        /// <summary>Foreground pixels of this ID in the current buffer.</summary>
        public int CurrentPixels { get; }

        /// <summary>Pixels where both buffers carry this exact ID.</summary>
        public int Intersection { get; }

        /// <summary>Target plus current pixels minus the intersection.</summary>
        public int Union { get; }

        /// <summary>
        /// Intersection divided by union, in [0, 1]; 1 only where both sides
        /// are empty.
        /// </summary>
        public float IoU => Union > 0 ? (float)Intersection / Union : 1f;

        /// <summary>
        /// Intersection divided by target pixels, in [0, 1]; 1 only where
        /// both sides are empty.
        /// </summary>
        public float TargetCoverage => TargetPixels > 0 ? (float)Intersection / TargetPixels : 1f;

        public CompositionPieceResult(uint id, int targetPixels, int currentPixels, int intersection)
        {
            if (id == 0)
                throw new ArgumentException("Piece ID must not be black.", nameof(id));
            if (id > 0x00FFFFFF)
                throw new ArgumentOutOfRangeException(nameof(id), id, "Piece ID must fit in 24-bit RGB (0x000000..0xFFFFFF).");
            if (targetPixels < 0)
                throw new ArgumentOutOfRangeException(nameof(targetPixels), targetPixels, "Target pixel count must be nonnegative.");
            if (currentPixels < 0)
                throw new ArgumentOutOfRangeException(nameof(currentPixels), currentPixels, "Current pixel count must be nonnegative.");
            if (intersection < 0 || intersection > targetPixels || intersection > currentPixels)
                throw new ArgumentOutOfRangeException(nameof(intersection), intersection, "Intersection must be within both pixel counts.");

            Id = id;
            TargetPixels = targetPixels;
            CurrentPixels = currentPixels;
            Intersection = intersection;
            Union = targetPixels + currentPixels - intersection;
        }
    }

    /// <summary>
    /// Immutable result of scoring a current composition buffer against a
    /// target. Instances are produced by <see cref="CompositionScorer.Compare"/>
    /// and never mutate after construction.
    /// </summary>
    public sealed class CompositionScoreResult
    {
        private readonly IReadOnlyList<CompositionPieceResult> _pieces;

        /// <summary>Extent along the X axis of both compared buffers.</summary>
        public int Width { get; }

        /// <summary>Extent along the Y axis of both compared buffers.</summary>
        public int Height { get; }

        /// <summary>Total number of compared pixels (width x height).</summary>
        public int TotalPixels => Width * Height;

        /// <summary>Nonblack pixels in the target buffer.</summary>
        public int TargetForegroundPixels { get; }

        /// <summary>Nonblack pixels in the current buffer.</summary>
        public int CurrentForegroundPixels { get; }

        /// <summary>Pixels where both buffers are nonblack.</summary>
        public int SilhouetteIntersection { get; }

        /// <summary>Pixels where at least one buffer is nonblack.</summary>
        public int SilhouetteUnion { get; }

        /// <summary>
        /// Silhouette intersection divided by union, in [0, 1]; 1 only where
        /// both sides are empty.
        /// </summary>
        public float SilhouetteIoU => SilhouetteUnion > 0 ? (float)SilhouetteIntersection / SilhouetteUnion : 1f;

        /// <summary>Pixels where both buffers carry the same nonblack ID.</summary>
        public int ExactSameIdPixels { get; }

        /// <summary>
        /// Exact same-ID pixels divided by the silhouette union, in [0, 1]; 1
        /// only where both sides are empty.
        /// </summary>
        public float IdentityAccuracy => SilhouetteUnion > 0 ? (float)ExactSameIdPixels / SilhouetteUnion : 1f;

        /// <summary>Per-piece results in required-piece order.</summary>
        public IReadOnlyList<CompositionPieceResult> Pieces => _pieces;

        /// <summary>Arithmetic mean of the per-piece IoU over required pieces.</summary>
        public float MeanPieceIoU { get; }

        /// <summary>Smallest per-piece target coverage over required pieces.</summary>
        public float MinimumPieceCoverage { get; }

        /// <summary>Weighted sum of silhouette, mean piece IoU, and identity accuracy.</summary>
        public float WeightedScore { get; }

        /// <summary>True when every packed pixel of both buffers matches exactly.</summary>
        public bool IsExactMatch => ExactSameIdPixels == SilhouetteUnion;

        /// <summary>
        /// True when the weighted score and the minimum piece coverage both
        /// meet the policy thresholds. Not the same as <see cref="IsExactMatch"/>.
        /// </summary>
        public bool PassesPolicy { get; }

        internal CompositionScoreResult(
            int width,
            int height,
            int targetForegroundPixels,
            int currentForegroundPixels,
            int silhouetteIntersection,
            int silhouetteUnion,
            int exactSameIdPixels,
            CompositionPieceResult[] pieces,
            CompositionPolicy policy)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be positive.");
            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be positive.");
            if (targetForegroundPixels < 0 || currentForegroundPixels < 0)
                throw new ArgumentOutOfRangeException(nameof(targetForegroundPixels), targetForegroundPixels, "Foreground pixel counts must be nonnegative.");
            if (silhouetteUnion < 0 || silhouetteIntersection < 0 || silhouetteIntersection > silhouetteUnion)
                throw new ArgumentOutOfRangeException(nameof(silhouetteIntersection), silhouetteIntersection, "Silhouette intersection must be within [0, union].");
            if (exactSameIdPixels < 0 || exactSameIdPixels > silhouetteUnion)
                throw new ArgumentOutOfRangeException(nameof(exactSameIdPixels), exactSameIdPixels, "Exact same-ID pixel count must be within [0, union].");
            if (pieces == null)
                throw new ArgumentNullException(nameof(pieces));
            if (pieces.Length == 0)
                throw new ArgumentException("At least one per-piece result is required.", nameof(pieces));

            Width = width;
            Height = height;
            TargetForegroundPixels = targetForegroundPixels;
            CurrentForegroundPixels = currentForegroundPixels;
            SilhouetteIntersection = silhouetteIntersection;
            SilhouetteUnion = silhouetteUnion;
            ExactSameIdPixels = exactSameIdPixels;
            _pieces = new ReadOnlyCollection<CompositionPieceResult>(pieces);

            float mean = 0f;
            float minimumCoverage = float.MaxValue;
            for (int i = 0; i < pieces.Length; i++)
            {
                mean += pieces[i].IoU;
                if (pieces[i].TargetCoverage < minimumCoverage)
                    minimumCoverage = pieces[i].TargetCoverage;
            }

            MeanPieceIoU = mean / pieces.Length;
            MinimumPieceCoverage = minimumCoverage;

            WeightedScore = policy.SilhouetteWeight * SilhouetteIoU
                + policy.PieceWeight * MeanPieceIoU
                + policy.IdentityWeight * IdentityAccuracy;

            PassesPolicy = WeightedScore >= policy.PassThreshold
                && MinimumPieceCoverage >= policy.MinimumCoverageThreshold;
        }
    }
}
