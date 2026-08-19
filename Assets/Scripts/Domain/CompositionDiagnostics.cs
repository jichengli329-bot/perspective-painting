using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PerspectivePuzzle.Domain
{
    /// <summary>
    /// Visual hint about how a piece should be adjusted to approach its
    /// target pose. Produced by <see cref="CompositionDiagnostics.Analyze"/>.
    /// </summary>
    public enum VisualGuidanceKind
    {
        None,
        MoveLeft,
        MoveRight,
        MoveUp,
        MoveDown,
        BringForward,
        SendBackward,
        Rotate,
        ReconsiderOcclusion,
        NearlyAligned,
    }

    /// <summary>
    /// Immutable per-piece visual diagnostic. Instances are produced by
    /// <see cref="CompositionDiagnostics.Analyze"/> and never mutate after
    /// construction. Centroids and the centroid delta are in normalized
    /// 0..1 coordinates; principal-axis angles are in degrees and
    /// anisotropies are in [0, 1].
    /// </summary>
    public readonly struct PieceVisualDiagnostic
    {
        /// <summary>Packed 24-bit RGB ID of this piece. Never black.</summary>
        public uint Id { get; }

        /// <summary>Normalized centroid X of the piece in the target buffer.</summary>
        public float TargetCentroidX { get; }

        /// <summary>Normalized centroid Y of the piece in the target buffer.</summary>
        public float TargetCentroidY { get; }

        /// <summary>
        /// Normalized centroid X of the piece in the current buffer; equals
        /// the target centroid when the piece is missing from current.
        /// </summary>
        public float CurrentCentroidX { get; }

        /// <summary>
        /// Normalized centroid Y of the piece in the current buffer; equals
        /// the target centroid when the piece is missing from current.
        /// </summary>
        public float CurrentCentroidY { get; }

        /// <summary>Normalized centroid delta X (target minus current).</summary>
        public float CentroidDeltaX { get; }

        /// <summary>Normalized centroid delta Y (target minus current).</summary>
        public float CentroidDeltaY { get; }

        /// <summary>Foreground pixels of this ID in the target buffer.</summary>
        public int TargetPixelArea { get; }

        /// <summary>Foreground pixels of this ID in the current buffer.</summary>
        public int CurrentPixelArea { get; }

        /// <summary>Current pixel area divided by target pixel area.</summary>
        public float AreaRatio { get; }

        /// <summary>Intersection divided by union of the two regions, in [0, 1].</summary>
        public float IoU { get; }

        /// <summary>Intersection divided by target pixels, in [0, 1].</summary>
        public float TargetCoverage { get; }

        /// <summary>Target principal-axis angle in degrees, in (-90, 90].</summary>
        public float TargetPrincipalAxisAngle { get; }

        /// <summary>Current principal-axis angle in degrees, in (-90, 90].</summary>
        public float CurrentPrincipalAxisAngle { get; }

        /// <summary>Target elongation, (lambdaMax-lambdaMin)/(lambdaMax+lambdaMin), in [0, 1].</summary>
        public float TargetAnisotropy { get; }

        /// <summary>Current elongation, (lambdaMax-lambdaMin)/(lambdaMax+lambdaMin), in [0, 1].</summary>
        public float CurrentAnisotropy { get; }

        /// <summary>Selected guidance kind under the precedence rules.</summary>
        public VisualGuidanceKind Guidance { get; }

        internal PieceVisualDiagnostic(
            uint id,
            float targetCentroidX,
            float targetCentroidY,
            float currentCentroidX,
            float currentCentroidY,
            float centroidDeltaX,
            float centroidDeltaY,
            int targetPixelArea,
            int currentPixelArea,
            float areaRatio,
            float iou,
            float targetCoverage,
            float targetPrincipalAxisAngle,
            float currentPrincipalAxisAngle,
            float targetAnisotropy,
            float currentAnisotropy,
            VisualGuidanceKind guidance)
        {
            if (id == 0)
                throw new ArgumentException("Piece ID must not be black.", nameof(id));
            if (id > MaxRgbValue)
                throw new ArgumentOutOfRangeException(nameof(id), id, "Piece ID must fit in 24-bit RGB (0x000000..0xFFFFFF).");
            if (targetPixelArea < 0 || currentPixelArea < 0)
                throw new ArgumentOutOfRangeException(nameof(targetPixelArea), targetPixelArea, "Pixel areas must be nonnegative.");

            Id = id;
            TargetCentroidX = targetCentroidX;
            TargetCentroidY = targetCentroidY;
            CurrentCentroidX = currentCentroidX;
            CurrentCentroidY = currentCentroidY;
            CentroidDeltaX = centroidDeltaX;
            CentroidDeltaY = centroidDeltaY;
            TargetPixelArea = targetPixelArea;
            CurrentPixelArea = currentPixelArea;
            AreaRatio = areaRatio;
            IoU = iou;
            TargetCoverage = targetCoverage;
            TargetPrincipalAxisAngle = targetPrincipalAxisAngle;
            CurrentPrincipalAxisAngle = currentPrincipalAxisAngle;
            TargetAnisotropy = targetAnisotropy;
            CurrentAnisotropy = currentAnisotropy;
            Guidance = guidance;
        }

        private const uint MaxRgbValue = 0x00FFFFFF;
    }

    /// <summary>
    /// Produces per-piece visual diagnostics for a current composition
    /// against a target, independent of Unity. Pure, deterministic, and
    /// intended for repeated 256x144 comparisons.
    /// </summary>
    public static class CompositionDiagnostics
    {
        private const float MoveThreshold = 0.055f;
        private const float NearlyAlignedMinimumIoU = 0.82f;
        private const float NearlyAlignedMinimumCoverage = 0.88f;
        private const float BringForwardMaximumRatio = 0.72f;
        private const float SendBackwardMinimumRatio = 1.38f;
        private const float RotateMinimumAnisotropy = 0.22f;
        private const float RotateMinimumAngleDifference = 12f;
        private const float ReconsiderOcclusionMaximumIoU = 0.70f;
        private const double TraceEpsilon = 1e-12;

        /// <summary>
        /// Analyzes <paramref name="current"/> against <paramref name="target"/>
        /// and returns one <see cref="PieceVisualDiagnostic"/> per required
        /// piece, in required-piece order. Validation matches
        /// <see cref="CompositionScorer.Compare"/>.
        /// </summary>
        public static IReadOnlyList<PieceVisualDiagnostic> Analyze(
            CompositionIdBuffer target,
            CompositionIdBuffer current,
            IReadOnlyList<uint> requiredPieceIds)
        {
            CompositionScoreResult scored = CompositionScorer.Compare(target, current, requiredPieceIds);

            var diagnostics = new PieceVisualDiagnostic[requiredPieceIds.Count];
            for (int j = 0; j < requiredPieceIds.Count; j++)
            {
                uint id = requiredPieceIds[j];
                CompositionPieceResult piece = scored.Pieces[j];

                RegionMoments targetMoments = ComputeMoments(target, id);
                RegionMoments currentMoments = ComputeMoments(current, id);

                float targetCx = (float)(targetMoments.MeanX / target.Width);
                float targetCy = (float)(targetMoments.MeanY / target.Height);
                float currentCx = currentMoments.Count > 0 ? (float)(currentMoments.MeanX / current.Width) : targetCx;
                float currentCy = currentMoments.Count > 0 ? (float)(currentMoments.MeanY / current.Height) : targetCy;

                float targetAngle = (float)targetMoments.AngleDegrees;
                float currentAngle = currentMoments.Count > 0 ? (float)currentMoments.AngleDegrees : 0f;
                float targetAnisotropy = (float)targetMoments.Anisotropy;
                float currentAnisotropy = currentMoments.Count > 0 ? (float)currentMoments.Anisotropy : 0f;

                float centroidDeltaX = targetCx - currentCx;
                float centroidDeltaY = targetCy - currentCy;

                float areaRatio = piece.TargetPixels > 0 ? (float)piece.CurrentPixels / piece.TargetPixels : 0f;

                VisualGuidanceKind guidance = Classify(
                    piece.IoU,
                    piece.TargetCoverage,
                    centroidDeltaX,
                    centroidDeltaY,
                    areaRatio,
                    targetAnisotropy,
                    currentAnisotropy,
                    targetAngle,
                    currentAngle);

                diagnostics[j] = new PieceVisualDiagnostic(
                    id,
                    targetCx,
                    targetCy,
                    currentCx,
                    currentCy,
                    centroidDeltaX,
                    centroidDeltaY,
                    piece.TargetPixels,
                    piece.CurrentPixels,
                    areaRatio,
                    piece.IoU,
                    piece.TargetCoverage,
                    targetAngle,
                    currentAngle,
                    targetAnisotropy,
                    currentAnisotropy,
                    guidance);
            }

            return new ReadOnlyCollection<PieceVisualDiagnostic>(diagnostics);
        }

        /// <summary>
        /// Applies the documented classification precedence. A missing piece
        /// (zero current area) is reported as BringForward rather than a
        /// fabricated movement direction.
        /// </summary>
        private static VisualGuidanceKind Classify(
            float iou,
            float coverage,
            float deltaX,
            float deltaY,
            float areaRatio,
            float targetAnisotropy,
            float currentAnisotropy,
            float targetAngle,
            float currentAngle)
        {
            if (iou >= NearlyAlignedMinimumIoU && coverage >= NearlyAlignedMinimumCoverage)
                return VisualGuidanceKind.NearlyAligned;

            float absDeltaX = Math.Abs(deltaX);
            float absDeltaY = Math.Abs(deltaY);
            if (absDeltaX >= MoveThreshold || absDeltaY >= MoveThreshold)
            {
                if (absDeltaX >= absDeltaY)
                    return deltaX > 0 ? VisualGuidanceKind.MoveRight : VisualGuidanceKind.MoveLeft;
                return deltaY > 0 ? VisualGuidanceKind.MoveUp : VisualGuidanceKind.MoveDown;
            }

            if (areaRatio <= BringForwardMaximumRatio)
                return VisualGuidanceKind.BringForward;
            if (areaRatio >= SendBackwardMinimumRatio)
                return VisualGuidanceKind.SendBackward;

            if (targetAnisotropy >= RotateMinimumAnisotropy
                && currentAnisotropy >= RotateMinimumAnisotropy
                && UndirectedAxisDifference(targetAngle, currentAngle) >= RotateMinimumAngleDifference)
                return VisualGuidanceKind.Rotate;

            if (iou < ReconsiderOcclusionMaximumIoU)
                return VisualGuidanceKind.ReconsiderOcclusion;

            return VisualGuidanceKind.None;
        }

        /// <summary>
        /// Difference between two principal-axis angles interpreted as an
        /// undirected 180-degree axis, mapped into [0, 90].
        /// </summary>
        private static float UndirectedAxisDifference(float a, float b)
        {
            float raw = Math.Abs(a - b);
            return raw > 90f ? 180f - raw : raw;
        }

        /// <summary>
        /// Accumulates pixel-center statistics for one exact ID. Centroid
        /// values are normalized by <see cref="CompositionIdBuffer.Width"/> /
        /// <see cref="CompositionIdBuffer.Height"/> by the caller; the second
        /// central moments are kept in raw pixel coordinates so that
        /// anisotropy and the principal axis are geometrically faithful even
        /// for non-square buffers.
        /// </summary>
        private static RegionMoments ComputeMoments(CompositionIdBuffer buffer, uint id)
        {
            var moments = new RegionMoments();
            int width = buffer.Width;
            for (int i = 0; i < buffer.PixelCount; i++)
            {
                if (buffer.GetPixelAt(i) != id)
                    continue;

                double x = (i % width) + 0.5;
                double y = (i / width) + 0.5;

                moments.Count++;
                moments.SumX += x;
                moments.SumY += y;
                moments.SumXX += x * x;
                moments.SumYY += y * y;
                moments.SumXY += x * y;
            }

            return moments;
        }

        private struct RegionMoments
        {
            public int Count;
            public double SumX;
            public double SumY;
            public double SumXX;
            public double SumYY;
            public double SumXY;

            /// <summary>Raw pixel-center X centroid (mean of x + 0.5).</summary>
            public double MeanX => Count > 0 ? SumX / Count : 0d;

            /// <summary>Raw pixel-center Y centroid (mean of y + 0.5).</summary>
            public double MeanY => Count > 0 ? SumY / Count : 0d;

            public double Mxx => Count > 0 ? SumXX / Count - MeanX * MeanX : 0d;

            public double Myy => Count > 0 ? SumYY / Count - MeanY * MeanY : 0d;

            public double Mxy => Count > 0 ? SumXY / Count - MeanX * MeanY : 0d;

            /// <summary>Principal axis as 0.5 * atan2(2*mxy, mxx-myy), in degrees.</summary>
            public double AngleDegrees
            {
                get
                {
                    double theta = 0.5d * Math.Atan2(2d * Mxy, Mxx - Myy);
                    return theta * (180d / Math.PI);
                }
            }

            /// <summary>Elongation (lambdaMax-lambdaMin)/(lambdaMax+lambdaMin), zero for degenerate regions.</summary>
            public double Anisotropy
            {
                get
                {
                    double trace = Mxx + Myy;
                    if (trace <= TraceEpsilon)
                        return 0d;
                    double difference = Math.Sqrt((Mxx - Myy) * (Mxx - Myy) + 4d * Mxy * Mxy);
                    return difference / trace;
                }
            }
        }
    }
}
