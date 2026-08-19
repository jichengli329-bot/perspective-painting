using System;
using System.Collections.ObjectModel;
using NUnit.Framework;
using PerspectivePuzzle.Domain;

namespace PerspectivePuzzle.Domain.Tests
{
    [TestFixture]
    public class CompositionDiagnosticsTests
    {
        private const uint Red = 0xFF0000;
        private const uint Green = 0x00FF00;
        private const uint Blue = 0x0000FF;

        private static CompositionIdBuffer Buffer(int width, int height, params uint[] pixels)
        {
            return CompositionIdBuffer.FromPixels(width, height, pixels);
        }

        private static CompositionIdBuffer Buffer3x3(params uint[] pixels)
        {
            return CompositionIdBuffer.FromPixels(3, 3, pixels);
        }

        /// <summary>Row-major array with one solid rectangle of the given ID.</summary>
        private static uint[] Rectangle(int width, int height, uint id, int x0, int y0, int w, int h)
        {
            var pixels = new uint[width * height];
            for (int y = y0; y < y0 + h; y++)
            {
                for (int x = x0; x < x0 + w; x++)
                {
                    pixels[y * width + x] = id;
                }
            }
            return pixels;
        }

        [Test]
        public void AnalyzeValidatesArguments()
        {
            var buffer = Buffer3x3(Red, 0, 0, 0, 0, 0, 0, 0, 0);

            Assert.That(() => CompositionDiagnostics.Analyze(null, buffer, new[] { Red }),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => CompositionDiagnostics.Analyze(buffer, null, new[] { Red }),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => CompositionDiagnostics.Analyze(buffer, buffer, null),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => CompositionDiagnostics.Analyze(buffer, buffer, new uint[0]),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void AnalyzeRejectsDimensionMismatch()
        {
            var target = Buffer(2, 2, Red, 0, 0, 0);
            var current = Buffer(3, 2, Red, 0, 0, 0, 0, 0);

            Assert.That(() => CompositionDiagnostics.Analyze(target, current, new[] { Red }),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void AnalyzeRejectsBlackOutOfRangeAndDuplicateRequiredIds()
        {
            var buffer = Buffer3x3(Red, 0, 0, 0, 0, 0, 0, 0, 0);

            Assert.That(() => CompositionDiagnostics.Analyze(buffer, buffer, new[] { 0u, Red }),
                Throws.TypeOf<ArgumentException>());
            Assert.That(() => CompositionDiagnostics.Analyze(buffer, buffer, new[] { 0x1000000u }),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => CompositionDiagnostics.Analyze(buffer, buffer, new[] { Red, Red }),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void AnalyzeRejectsUnknownTargetIds()
        {
            var target = Buffer3x3(Red, 0xABCDEF, 0, 0, 0, 0, 0, 0, 0);
            var current = Buffer3x3(Red, 0, 0, 0, 0, 0, 0, 0, 0);

            Assert.That(() => CompositionDiagnostics.Analyze(target, current, new[] { Red }),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void AnalyzeRejectsRequiredIdAbsentFromTarget()
        {
            var target = Buffer3x3(Red, 0, 0, 0, 0, 0, 0, 0, 0);

            Assert.That(() => CompositionDiagnostics.Analyze(target, target, new[] { Red, Green }),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void NearlyAlignedTakesPrecedenceOverMovement()
        {
            // An 11x3 bar shifted by one pixel: IoU 30/36 and coverage
            // 30/33 clear the NearlyAligned thresholds even though the
            // normalized centroid delta exceeds the movement threshold.
            var target = Buffer(12, 3, Rectangle(12, 3, Red, 0, 0, 11, 3));
            var current = Buffer(12, 3, Rectangle(12, 3, Red, 1, 0, 11, 3));

            var result = CompositionDiagnostics.Analyze(target, current, new[] { Red });

            Assert.That(result.Count, Is.EqualTo(1));
            var diagnostic = result[0];
            Assert.That(diagnostic.Guidance, Is.EqualTo(VisualGuidanceKind.NearlyAligned));
            Assert.That(diagnostic.IoU, Is.EqualTo(30f / 36f).Within(1e-6f));
            Assert.That(diagnostic.TargetCoverage, Is.EqualTo(30f / 33f).Within(1e-6f));
            Assert.That(diagnostic.TargetPixelArea, Is.EqualTo(33));
            Assert.That(diagnostic.CurrentPixelArea, Is.EqualTo(33));
        }

        [Test]
        public void MoveRightChosenWhenHorizontalDeltaLarger()
        {
            // Both axes exceed the movement threshold; X has the larger
            // normalized delta and is positive, so MoveRight wins.
            var target = Buffer(8, 4, Rectangle(8, 4, Red, 5, 2, 1, 1));
            var current = Buffer(8, 4, Rectangle(8, 4, Red, 1, 1, 1, 1));

            var result = CompositionDiagnostics.Analyze(target, current, new[] { Red });

            var diagnostic = result[0];
            Assert.That(diagnostic.Guidance, Is.EqualTo(VisualGuidanceKind.MoveRight));
            Assert.That(diagnostic.CentroidDeltaX, Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(diagnostic.CentroidDeltaY, Is.EqualTo(0.25f).Within(1e-6f));
        }

        [Test]
        public void MoveLeftWhenHorizontalDeltaIsNegative()
        {
            var target = Buffer(8, 4, Rectangle(8, 4, Red, 1, 1, 1, 1));
            var current = Buffer(8, 4, Rectangle(8, 4, Red, 5, 2, 1, 1));

            var result = CompositionDiagnostics.Analyze(target, current, new[] { Red });

            Assert.That(result[0].Guidance, Is.EqualTo(VisualGuidanceKind.MoveLeft));
            Assert.That(result[0].CentroidDeltaX, Is.EqualTo(-0.5f).Within(1e-6f));
        }

        [Test]
        public void MoveUpChosenWhenVerticalDeltaLarger()
        {
            // Both axes exceed the movement threshold; Y has the larger
            // normalized delta and is positive, so MoveUp wins.
            var target = Buffer(8, 4, Rectangle(8, 4, Red, 2, 3, 1, 1));
            var current = Buffer(8, 4, Rectangle(8, 4, Red, 1, 1, 1, 1));

            var result = CompositionDiagnostics.Analyze(target, current, new[] { Red });

            var diagnostic = result[0];
            Assert.That(diagnostic.Guidance, Is.EqualTo(VisualGuidanceKind.MoveUp));
            Assert.That(diagnostic.CentroidDeltaX, Is.EqualTo(0.125f).Within(1e-6f));
            Assert.That(diagnostic.CentroidDeltaY, Is.EqualTo(0.5f).Within(1e-6f));
        }

        [Test]
        public void MoveDownWhenVerticalDeltaIsNegative()
        {
            var target = Buffer(8, 4, Rectangle(8, 4, Red, 1, 1, 1, 1));
            var current = Buffer(8, 4, Rectangle(8, 4, Red, 2, 3, 1, 1));

            var result = CompositionDiagnostics.Analyze(target, current, new[] { Red });

            Assert.That(result[0].Guidance, Is.EqualTo(VisualGuidanceKind.MoveDown));
            Assert.That(result[0].CentroidDeltaY, Is.EqualTo(-0.5f).Within(1e-6f));
        }

        [Test]
        public void BringForwardWhenCurrentPieceIsMuchSmaller()
        {
            // A 2x2 block centered inside a 3x3 block keeps the centroid
            // delta below the movement threshold while the area ratio is 4/9.
            var target = Buffer(10, 10, Rectangle(10, 10, Red, 4, 4, 3, 3));
            var current = Buffer(10, 10, Rectangle(10, 10, Red, 4, 4, 2, 2));

            var result = CompositionDiagnostics.Analyze(target, current, new[] { Red });

            var diagnostic = result[0];
            Assert.That(diagnostic.Guidance, Is.EqualTo(VisualGuidanceKind.BringForward));
            Assert.That(diagnostic.AreaRatio, Is.EqualTo(4f / 9f).Within(1e-6f));
            Assert.That(diagnostic.TargetPixelArea, Is.EqualTo(9));
            Assert.That(diagnostic.CurrentPixelArea, Is.EqualTo(4));
        }

        [Test]
        public void SendBackwardWhenCurrentPieceIsMuchLarger()
        {
            var target = Buffer(10, 10, Rectangle(10, 10, Red, 4, 4, 2, 2));
            var current = Buffer(10, 10, Rectangle(10, 10, Red, 4, 4, 3, 3));

            var result = CompositionDiagnostics.Analyze(target, current, new[] { Red });

            Assert.That(result[0].Guidance, Is.EqualTo(VisualGuidanceKind.SendBackward));
            Assert.That(result[0].AreaRatio, Is.EqualTo(9f / 4f).Within(1e-6f));
        }

        [Test]
        public void RotateWhenBothElongatedAndAxesDiffer()
        {
            // A horizontal bar vs a vertical bar share centroid and area, so
            // neither movement nor depth rules fire; both are elongated and
            // the undirected axis difference is 90 degrees.
            var target = Buffer(6, 6, Rectangle(6, 6, Red, 0, 2, 6, 2));
            var current = Buffer(6, 6, Rectangle(6, 6, Red, 2, 0, 2, 6));

            var result = CompositionDiagnostics.Analyze(target, current, new[] { Red });

            var diagnostic = result[0];
            Assert.That(diagnostic.Guidance, Is.EqualTo(VisualGuidanceKind.Rotate));
            Assert.That(diagnostic.TargetAnisotropy, Is.GreaterThanOrEqualTo(0.22f));
            Assert.That(diagnostic.CurrentAnisotropy, Is.GreaterThanOrEqualTo(0.22f));
            // The bars are axis-aligned, so the undirected axis difference is
            // exactly 90 degrees regardless of which sign Atan2 reports.
            float rawDifference = Math.Abs(diagnostic.TargetPrincipalAxisAngle - diagnostic.CurrentPrincipalAxisAngle);
            float undirectedDifference = rawDifference > 90f ? 180f - rawDifference : rawDifference;
            Assert.That(undirectedDifference, Is.EqualTo(90f).Within(1e-4f));
        }

        [Test]
        public void SymmetricShapesNeverReportRotate()
        {
            // A square block and a rotated (diamond) symmetric shape both
            // have anisotropy 0, so the Rotate precondition never holds; the
            // smaller diamond instead reports BringForward.
            var target = Buffer(5, 5, Rectangle(5, 5, Red, 1, 1, 3, 3));
            var diamond = new uint[25];
            diamond[1 * 5 + 2] = Red;
            diamond[2 * 5 + 1] = Red;
            diamond[2 * 5 + 2] = Red;
            diamond[2 * 5 + 3] = Red;
            diamond[3 * 5 + 2] = Red;
            var current = Buffer(5, 5, diamond);

            var result = CompositionDiagnostics.Analyze(target, current, new[] { Red });

            var diagnostic = result[0];
            Assert.That(diagnostic.TargetAnisotropy, Is.LessThan(0.22f));
            Assert.That(diagnostic.CurrentAnisotropy, Is.LessThan(0.22f));
            Assert.That(diagnostic.Guidance, Is.Not.EqualTo(VisualGuidanceKind.Rotate));
            Assert.That(diagnostic.Guidance, Is.EqualTo(VisualGuidanceKind.BringForward));
        }

        [Test]
        public void ReconsiderOcclusionWhenIoUIsLowWithoutOtherHint()
        {
            // A 3x3 block vs an equal-area cross keeps centroid and area in
            // the neutral zone but drops IoU well below 0.70.
            var target = Buffer(5, 5, Rectangle(5, 5, Red, 1, 1, 3, 3));
            var cross = new uint[25];
            for (int y = 0; y < 5; y++)
                cross[y * 5 + 2] = Red;
            for (int x = 0; x < 5; x++)
                cross[2 * 5 + x] = Red;
            var current = Buffer(5, 5, cross);

            var result = CompositionDiagnostics.Analyze(target, current, new[] { Red });

            var diagnostic = result[0];
            Assert.That(diagnostic.Guidance, Is.EqualTo(VisualGuidanceKind.ReconsiderOcclusion));
            Assert.That(diagnostic.IoU, Is.EqualTo(5f / 13f).Within(1e-6f));
            Assert.That(diagnostic.TargetCoverage, Is.EqualTo(5f / 9f).Within(1e-6f));
            Assert.That(diagnostic.AreaRatio, Is.EqualTo(1f).Within(1e-6f));
        }

        [Test]
        public void NoneWhenIoUIsAboveOcclusionThreshold()
        {
            // A block with two opposite corners removed stays centered with
            // IoU 7/9, so no rule fires.
            var target = Buffer(5, 5, Rectangle(5, 5, Red, 1, 1, 3, 3));
            var nearlyAligned = Rectangle(5, 5, Red, 1, 1, 3, 3);
            nearlyAligned[1 * 5 + 1] = 0;
            nearlyAligned[3 * 5 + 3] = 0;
            var current = Buffer(5, 5, nearlyAligned);

            var result = CompositionDiagnostics.Analyze(target, current, new[] { Red });

            var diagnostic = result[0];
            Assert.That(diagnostic.Guidance, Is.EqualTo(VisualGuidanceKind.None));
            Assert.That(diagnostic.IoU, Is.EqualTo(7f / 9f).Within(1e-6f));
        }

        [Test]
        public void MissingCurrentPieceYieldsBringForwardWithZeroedMetrics()
        {
            // Green is absent from the current buffer entirely; it must not
            // fabricate a movement direction. Present Red still reports
            // NearlyAligned.
            var targetPixels = Rectangle(5, 5, Red, 1, 1, 3, 3);
            targetPixels[0] = Green;
            var target = Buffer(5, 5, targetPixels);
            var current = Buffer(5, 5, Rectangle(5, 5, Red, 1, 1, 3, 3));

            var result = CompositionDiagnostics.Analyze(target, current, new[] { Red, Green });

            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result[0].Guidance, Is.EqualTo(VisualGuidanceKind.NearlyAligned));

            var missing = result[1];
            Assert.That(missing.Id, Is.EqualTo(Green));
            Assert.That(missing.Guidance, Is.EqualTo(VisualGuidanceKind.BringForward));
            Assert.That(missing.CurrentPixelArea, Is.Zero);
            Assert.That(missing.AreaRatio, Is.Zero);
            Assert.That(missing.IoU, Is.Zero);
            Assert.That(missing.TargetCoverage, Is.Zero);
            Assert.That(missing.CurrentCentroidX, Is.EqualTo(missing.TargetCentroidX).Within(1e-6f));
            Assert.That(missing.CurrentCentroidY, Is.EqualTo(missing.TargetCentroidY).Within(1e-6f));
            Assert.That(missing.CentroidDeltaX, Is.Zero);
            Assert.That(missing.CentroidDeltaY, Is.Zero);
            Assert.That(missing.CurrentPrincipalAxisAngle, Is.Zero);
            Assert.That(missing.CurrentAnisotropy, Is.Zero);
        }

        [Test]
        public void PrincipalAxisIsInvariantUnder180DegreeRotation()
        {
            // An asymmetric shape centered on the buffer and its 180-degree
            // rotation have the same central moments, so the undirected axis
            // difference is 0 and Rotate never fires even though both shapes
            // are clearly elongated.
            var shapeA = new uint[25];
            shapeA[1 * 5 + 1] = Red;
            shapeA[1 * 5 + 2] = Red;
            shapeA[2 * 5 + 2] = Red;
            shapeA[2 * 5 + 3] = Red;
            shapeA[4 * 5 + 2] = Red;
            var shapeB = new uint[25];
            shapeB[3 * 5 + 3] = Red;
            shapeB[3 * 5 + 2] = Red;
            shapeB[2 * 5 + 2] = Red;
            shapeB[2 * 5 + 1] = Red;
            shapeB[0 * 5 + 2] = Red;

            var result = CompositionDiagnostics.Analyze(
                Buffer(5, 5, shapeA),
                Buffer(5, 5, shapeB),
                new[] { Red });

            var diagnostic = result[0];
            Assert.That(diagnostic.TargetPrincipalAxisAngle,
                Is.EqualTo(diagnostic.CurrentPrincipalAxisAngle).Within(1e-4f));
            Assert.That(diagnostic.TargetAnisotropy, Is.GreaterThanOrEqualTo(0.22f));
            Assert.That(diagnostic.CurrentAnisotropy, Is.GreaterThanOrEqualTo(0.22f));
            Assert.That(diagnostic.Guidance, Is.Not.EqualTo(VisualGuidanceKind.Rotate));
            Assert.That(diagnostic.Guidance, Is.EqualTo(VisualGuidanceKind.ReconsiderOcclusion));
        }

        [Test]
        public void CentroidsAreNormalizedByBufferExtent()
        {
            // A single pixel at (1, 2) in a 4x3 buffer maps to
            // (1.5/4, 2.5/3), exercising independent X and Y normalization.
            var target = Buffer(4, 3, Rectangle(4, 3, Red, 1, 2, 1, 1));

            var result = CompositionDiagnostics.Analyze(target, target, new[] { Red });

            var diagnostic = result[0];
            Assert.That(diagnostic.TargetCentroidX, Is.EqualTo(1.5f / 4f).Within(1e-6f));
            Assert.That(diagnostic.TargetCentroidY, Is.EqualTo(2.5f / 3f).Within(1e-6f));
            Assert.That(diagnostic.CurrentCentroidX, Is.EqualTo(diagnostic.TargetCentroidX).Within(1e-6f));
            Assert.That(diagnostic.CurrentCentroidY, Is.EqualTo(diagnostic.TargetCentroidY).Within(1e-6f));
            Assert.That(diagnostic.CentroidDeltaX, Is.Zero);
            Assert.That(diagnostic.CentroidDeltaY, Is.Zero);
        }

        [Test]
        public void ResultsFollowRequiredPieceOrder()
        {
            var targetPixels = new uint[25];
            targetPixels[1 * 5 + 1] = Red;
            targetPixels[1 * 5 + 3] = Green;
            targetPixels[3 * 5 + 2] = Blue;
            var target = Buffer(5, 5, targetPixels);

            var result = CompositionDiagnostics.Analyze(target, target, new[] { Blue, Green, Red });

            Assert.That(result.Count, Is.EqualTo(3));
            Assert.That(result[0].Id, Is.EqualTo(Blue));
            Assert.That(result[1].Id, Is.EqualTo(Green));
            Assert.That(result[2].Id, Is.EqualTo(Red));
        }

        [Test]
        public void ResultIsReadOnly()
        {
            var target = Buffer(5, 5, Rectangle(5, 5, Red, 1, 1, 1, 1));

            var result = CompositionDiagnostics.Analyze(target, target, new[] { Red });

            Assert.That(result, Is.InstanceOf<ReadOnlyCollection<PieceVisualDiagnostic>>());
        }
    }
}
