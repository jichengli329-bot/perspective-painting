using System;
using System.Collections.ObjectModel;
using NUnit.Framework;
using PerspectivePuzzle.Domain;

namespace PerspectivePuzzle.Domain.Tests
{
    [TestFixture]
    public class CompositionScorerTests
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

        [Test]
        public void FromPixelsValidatesArguments()
        {
            Assert.That(() => CompositionIdBuffer.FromPixels(0, 1, new uint[0]),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => CompositionIdBuffer.FromPixels(1, 0, new uint[0]),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => CompositionIdBuffer.FromPixels(1, 1, null),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => CompositionIdBuffer.FromPixels(2, 2, new uint[3]),
                Throws.TypeOf<ArgumentException>());
            Assert.That(() => CompositionIdBuffer.FromPixels(1, 1, new[] { 0x1000000u }),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void FromPixelsDefensivelyCopiesInput()
        {
            var pixels = new uint[] { Red, 0, 0, 0 };
            var buffer = CompositionIdBuffer.FromPixels(2, 2, pixels);

            pixels[0] = 0;
            pixels[1] = Blue;

            Assert.That(buffer.GetPixel(0, 0), Is.EqualTo(Red));
            Assert.That(buffer.GetPixel(1, 0), Is.EqualTo(0));
        }

        [Test]
        public void FromPixelsExposesDimensionsAndPixelCount()
        {
            var buffer = Buffer(3, 2, 0, 0, 0, 0, 0, 0);

            Assert.That(buffer.Width, Is.EqualTo(3));
            Assert.That(buffer.Height, Is.EqualTo(2));
            Assert.That(buffer.PixelCount, Is.EqualTo(6));
        }

        [Test]
        public void GetPixelReturnsStoredValues()
        {
            var buffer = Buffer3x3(Red, 0, Green, 0, Blue, 0, 0, 0, 0);

            Assert.That(buffer.GetPixel(0, 0), Is.EqualTo(Red));
            Assert.That(buffer.GetPixel(2, 0), Is.EqualTo(Green));
            Assert.That(buffer.GetPixel(1, 1), Is.EqualTo(Blue));
            Assert.That(buffer.GetPixel(2, 2), Is.EqualTo(0));
        }

        [Test]
        public void GetPixelRejectsOutOfBoundsCoordinates()
        {
            var buffer = Buffer3x3(Red, 0, 0, 0, 0, 0, 0, 0, 0);

            Assert.That(() => buffer.GetPixel(-1, 0), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => buffer.GetPixel(0, -1), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => buffer.GetPixel(3, 0), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => buffer.GetPixel(0, 3), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void ExactMatchReportsFullAgreement()
        {
            var target = Buffer3x3(Red, 0, Green, 0, 0, 0, Blue, 0, 0);
            var current = Buffer3x3(Red, 0, Green, 0, 0, 0, Blue, 0, 0);

            var result = CompositionScorer.Compare(target, current, new[] { Red, Green, Blue });

            Assert.That(result.Width, Is.EqualTo(3));
            Assert.That(result.Height, Is.EqualTo(3));
            Assert.That(result.TotalPixels, Is.EqualTo(9));
            Assert.That(result.TargetForegroundPixels, Is.EqualTo(3));
            Assert.That(result.CurrentForegroundPixels, Is.EqualTo(3));
            Assert.That(result.SilhouetteIntersection, Is.EqualTo(3));
            Assert.That(result.SilhouetteUnion, Is.EqualTo(3));
            Assert.That(result.SilhouetteIoU, Is.EqualTo(1f));
            Assert.That(result.ExactSameIdPixels, Is.EqualTo(3));
            Assert.That(result.IdentityAccuracy, Is.EqualTo(1f));
            Assert.That(result.MeanPieceIoU, Is.EqualTo(1f));
            Assert.That(result.MinimumPieceCoverage, Is.EqualTo(1f));
            Assert.That(result.WeightedScore, Is.EqualTo(1f).Within(1e-6f));
            Assert.That(result.IsExactMatch, Is.True);
            Assert.That(result.PassesPolicy, Is.True);

            Assert.That(result.Pieces.Count, Is.EqualTo(3));
            for (int i = 0; i < 3; i++)
            {
                Assert.That(result.Pieces[i].TargetPixels, Is.EqualTo(1));
                Assert.That(result.Pieces[i].CurrentPixels, Is.EqualTo(1));
                Assert.That(result.Pieces[i].Intersection, Is.EqualTo(1));
                Assert.That(result.Pieces[i].Union, Is.EqualTo(1));
                Assert.That(result.Pieces[i].IoU, Is.EqualTo(1f));
                Assert.That(result.Pieces[i].TargetCoverage, Is.EqualTo(1f));
            }
        }

        [Test]
        public void SwappedPieceIdsKeepSilhouetteButLoseIdentity()
        {
            var target = Buffer3x3(Red, 0, 0, 0, Green, 0, 0, 0, 0);
            var current = Buffer3x3(Green, 0, 0, 0, Red, 0, 0, 0, 0);

            var result = CompositionScorer.Compare(target, current, new[] { Red, Green });

            Assert.That(result.SilhouetteIntersection, Is.EqualTo(2));
            Assert.That(result.SilhouetteUnion, Is.EqualTo(2));
            Assert.That(result.SilhouetteIoU, Is.EqualTo(1f));
            Assert.That(result.ExactSameIdPixels, Is.Zero);
            Assert.That(result.IdentityAccuracy, Is.EqualTo(0f));
            Assert.That(result.MeanPieceIoU, Is.EqualTo(0f));
            Assert.That(result.MinimumPieceCoverage, Is.EqualTo(0f));
            Assert.That(result.WeightedScore, Is.EqualTo(0.4f).Within(1e-6f));
            Assert.That(result.IsExactMatch, Is.False);
            Assert.That(result.PassesPolicy, Is.False);

            for (int i = 0; i < 2; i++)
            {
                Assert.That(result.Pieces[i].TargetPixels, Is.EqualTo(1));
                Assert.That(result.Pieces[i].CurrentPixels, Is.EqualTo(1));
                Assert.That(result.Pieces[i].Intersection, Is.Zero);
                Assert.That(result.Pieces[i].Union, Is.EqualTo(2));
                Assert.That(result.Pieces[i].IoU, Is.EqualTo(0f));
                Assert.That(result.Pieces[i].TargetCoverage, Is.EqualTo(0f));
            }
        }

        [Test]
        public void ShiftedForegroundFailsAllMetrics()
        {
            var target = Buffer3x3(Red, 0, 0, 0, 0, 0, 0, 0, 0);
            var current = Buffer3x3(0, 0, 0, 0, 0, Red, 0, 0, 0);

            var result = CompositionScorer.Compare(target, current, new[] { Red });

            Assert.That(result.TargetForegroundPixels, Is.EqualTo(1));
            Assert.That(result.CurrentForegroundPixels, Is.EqualTo(1));
            Assert.That(result.SilhouetteIntersection, Is.Zero);
            Assert.That(result.SilhouetteUnion, Is.EqualTo(2));
            Assert.That(result.SilhouetteIoU, Is.EqualTo(0f));
            Assert.That(result.ExactSameIdPixels, Is.Zero);
            Assert.That(result.IdentityAccuracy, Is.EqualTo(0f));
            Assert.That(result.MeanPieceIoU, Is.EqualTo(0f));
            Assert.That(result.MinimumPieceCoverage, Is.EqualTo(0f));
            Assert.That(result.WeightedScore, Is.EqualTo(0f));
            Assert.That(result.IsExactMatch, Is.False);
            Assert.That(result.PassesPolicy, Is.False);
        }

        [Test]
        public void EmptyCurrentLosesEveryPiece()
        {
            var target = Buffer3x3(Red, 0, 0, 0, Green, 0, 0, 0, 0);
            var current = Buffer3x3(0, 0, 0, 0, 0, 0, 0, 0, 0);

            var result = CompositionScorer.Compare(target, current, new[] { Red, Green });

            Assert.That(result.TargetForegroundPixels, Is.EqualTo(2));
            Assert.That(result.CurrentForegroundPixels, Is.Zero);
            Assert.That(result.SilhouetteIoU, Is.EqualTo(0f));
            Assert.That(result.IdentityAccuracy, Is.EqualTo(0f));
            Assert.That(result.MeanPieceIoU, Is.EqualTo(0f));
            Assert.That(result.MinimumPieceCoverage, Is.EqualTo(0f));
            Assert.That(result.WeightedScore, Is.EqualTo(0f));
            Assert.That(result.IsExactMatch, Is.False);

            Assert.That(result.Pieces.Count, Is.EqualTo(2));
            foreach (var piece in result.Pieces)
            {
                Assert.That(piece.TargetPixels, Is.EqualTo(1));
                Assert.That(piece.CurrentPixels, Is.Zero);
                Assert.That(piece.Intersection, Is.Zero);
                Assert.That(piece.Union, Is.EqualTo(1));
                Assert.That(piece.IoU, Is.EqualTo(0f));
                Assert.That(piece.TargetCoverage, Is.EqualTo(0f));
            }
        }

        [Test]
        public void CurrentMissingOnePieceScoresTheRemainingPiece()
        {
            var target = Buffer3x3(Red, 0, 0, 0, Green, 0, 0, 0, 0);
            var current = Buffer3x3(Red, 0, 0, 0, 0, 0, 0, 0, 0);

            var result = CompositionScorer.Compare(target, current, new[] { Red, Green });

            Assert.That(result.SilhouetteIntersection, Is.EqualTo(1));
            Assert.That(result.SilhouetteUnion, Is.EqualTo(2));
            Assert.That(result.SilhouetteIoU, Is.EqualTo(0.5f));
            Assert.That(result.ExactSameIdPixels, Is.EqualTo(1));
            Assert.That(result.IdentityAccuracy, Is.EqualTo(0.5f));
            Assert.That(result.MeanPieceIoU, Is.EqualTo(0.5f));
            Assert.That(result.MinimumPieceCoverage, Is.EqualTo(0f));
            Assert.That(result.WeightedScore, Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(result.IsExactMatch, Is.False);
            Assert.That(result.PassesPolicy, Is.False);

            Assert.That(result.Pieces[0].Id, Is.EqualTo(Red));
            Assert.That(result.Pieces[0].IoU, Is.EqualTo(1f));
            Assert.That(result.Pieces[0].TargetCoverage, Is.EqualTo(1f));
            Assert.That(result.Pieces[1].Id, Is.EqualTo(Green));
            Assert.That(result.Pieces[1].TargetPixels, Is.EqualTo(1));
            Assert.That(result.Pieces[1].CurrentPixels, Is.Zero);
            Assert.That(result.Pieces[1].Union, Is.EqualTo(1));
            Assert.That(result.Pieces[1].IoU, Is.EqualTo(0f));
            Assert.That(result.Pieces[1].TargetCoverage, Is.EqualTo(0f));
        }

        [Test]
        public void UnknownCurrentColorCountsAsForegroundMismatch()
        {
            var target = Buffer(2, 2, Red, 0, 0, 0);
            var current = Buffer(2, 2, Red, 0x123456, 0, 0);

            var result = CompositionScorer.Compare(target, current, new[] { Red });

            Assert.That(result.TargetForegroundPixels, Is.EqualTo(1));
            Assert.That(result.CurrentForegroundPixels, Is.EqualTo(2));
            Assert.That(result.SilhouetteIntersection, Is.EqualTo(1));
            Assert.That(result.SilhouetteUnion, Is.EqualTo(2));
            Assert.That(result.SilhouetteIoU, Is.EqualTo(0.5f));
            Assert.That(result.ExactSameIdPixels, Is.EqualTo(1));
            Assert.That(result.IdentityAccuracy, Is.EqualTo(0.5f));
            Assert.That(result.MeanPieceIoU, Is.EqualTo(1f));
            Assert.That(result.MinimumPieceCoverage, Is.EqualTo(1f));
            Assert.That(result.WeightedScore, Is.EqualTo(0.725f).Within(1e-6f));
            Assert.That(result.IsExactMatch, Is.False);
        }

        [Test]
        public void CompareRejectsNullArguments()
        {
            var buffer = Buffer3x3(Red, 0, 0, 0, 0, 0, 0, 0, 0);

            Assert.That(() => CompositionScorer.Compare(null, buffer, new[] { Red }),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => CompositionScorer.Compare(buffer, null, new[] { Red }),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => CompositionScorer.Compare(buffer, buffer, null),
                Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void CompareRejectsEmptyRequiredList()
        {
            var buffer = Buffer3x3(Red, 0, 0, 0, 0, 0, 0, 0, 0);

            Assert.That(() => CompositionScorer.Compare(buffer, buffer, new uint[0]),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void CompareRejectsDimensionMismatch()
        {
            var target2x2 = Buffer(2, 2, Red, 0, 0, 0);
            var current3x2 = Buffer(3, 2, Red, 0, 0, 0, 0, 0);

            Assert.That(() => CompositionScorer.Compare(target2x2, current3x2, new[] { Red }),
                Throws.TypeOf<ArgumentException>());
            Assert.That(() => CompositionScorer.Compare(current3x2, target2x2, new[] { Red }),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void CompareRejectsBlackOutOfRangeAndDuplicateRequiredIds()
        {
            var buffer = Buffer3x3(Red, 0, 0, 0, 0, 0, 0, 0, 0);

            Assert.That(() => CompositionScorer.Compare(buffer, buffer, new[] { 0u, Red }),
                Throws.TypeOf<ArgumentException>());
            Assert.That(() => CompositionScorer.Compare(buffer, buffer, new[] { 0x1000000u }),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => CompositionScorer.Compare(buffer, buffer, new[] { Red, Red }),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void CompareRejectsUnknownTargetIds()
        {
            var target = Buffer3x3(Red, 0xABCDEF, 0, 0, 0, 0, 0, 0, 0);
            var current = Buffer3x3(Red, 0, 0, 0, 0, 0, 0, 0, 0);

            Assert.That(() => CompositionScorer.Compare(target, current, new[] { Red }),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void CompareRejectsRequiredIdAbsentFromTarget()
        {
            var target = Buffer3x3(Red, 0, 0, 0, 0, 0, 0, 0, 0);

            Assert.That(() => CompositionScorer.Compare(target, target, new[] { Red, Green }),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void PolicyRejectsInvalidWeights()
        {
            Assert.That(() => new CompositionPolicy(0f, 0f, 0f, 0.93f, 0.8f),
                Throws.TypeOf<ArgumentException>());
            Assert.That(() => new CompositionPolicy(float.NaN, 1f, 1f, 0.93f, 0.8f),
                Throws.TypeOf<ArgumentException>());
            Assert.That(() => new CompositionPolicy(1f, float.NaN, 1f, 0.93f, 0.8f),
                Throws.TypeOf<ArgumentException>());
            Assert.That(() => new CompositionPolicy(1f, 1f, float.NaN, 0.93f, 0.8f),
                Throws.TypeOf<ArgumentException>());
            Assert.That(() => new CompositionPolicy(float.PositiveInfinity, 1f, 1f, 0.93f, 0.8f),
                Throws.TypeOf<ArgumentException>());
            Assert.That(() => new CompositionPolicy(-1f, 1f, 1f, 0.93f, 0.8f),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void PolicyRejectsInvalidThresholds()
        {
            Assert.That(() => new CompositionPolicy(1f, 1f, 1f, -0.1f, 0.8f),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new CompositionPolicy(1f, 1f, 1f, 1.1f, 0.8f),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new CompositionPolicy(1f, 1f, 1f, 0.93f, -0.1f),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new CompositionPolicy(1f, 1f, 1f, 0.93f, 1.1f),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new CompositionPolicy(1f, 1f, 1f, float.NaN, 0.8f),
                Throws.TypeOf<ArgumentException>());
            Assert.That(() => new CompositionPolicy(1f, 1f, 1f, 0.93f, float.NaN),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void PolicyNormalizesWeightsByTheirSum()
        {
            // Swapped scenario: silhouette 1, piece and identity 0, so the
            // weighted score is exactly the normalized silhouette weight.
            var target = Buffer3x3(Red, 0, 0, 0, Green, 0, 0, 0, 0);
            var current = Buffer3x3(Green, 0, 0, 0, Red, 0, 0, 0, 0);
            var ids = new[] { Red, Green };

            var sameRatio = CompositionScorer.Compare(target, current, ids, new CompositionPolicy(0.8f, 0.9f, 0.3f, 0.93f, 0.8f));
            var defaultRatio = CompositionScorer.Compare(target, current, ids, new CompositionPolicy(0.4f, 0.45f, 0.15f, 0.93f, 0.8f));

            Assert.That(sameRatio.WeightedScore, Is.EqualTo(0.4f).Within(1e-6f));
            Assert.That(defaultRatio.WeightedScore, Is.EqualTo(sameRatio.WeightedScore).Within(1e-6f));

            var skewed = CompositionScorer.Compare(target, current, ids, new CompositionPolicy(2f, 3f, 5f, 0.93f, 0.8f));
            Assert.That(skewed.WeightedScore, Is.EqualTo(0.2f).Within(1e-6f));
        }

        [Test]
        public void FailingCompositionFailsDefaultPolicy()
        {
            var target = Buffer3x3(Red, 0, 0, 0, 0, 0, 0, 0, 0);
            var current = Buffer3x3(0, 0, 0, 0, 0, Red, 0, 0, 0);

            var result = CompositionScorer.Compare(target, current, new[] { Red });

            Assert.That(result.PassesPolicy, Is.False);
        }

        [Test]
        public void PermissiveThresholdsPassAFailingComposition()
        {
            var target = Buffer3x3(Red, 0, 0, 0, 0, 0, 0, 0, 0);
            var current = Buffer3x3(0, 0, 0, 0, 0, Red, 0, 0, 0);

            var result = CompositionScorer.Compare(target, current, new[] { Red }, new CompositionPolicy(0.4f, 0.45f, 0.15f, 0f, 0f));

            Assert.That(result.WeightedScore, Is.EqualTo(0f));
            Assert.That(result.PassesPolicy, Is.True);
            Assert.That(result.IsExactMatch, Is.False);
        }

        [Test]
        public void CoverageThresholdIsIndependentOfWeightedScore()
        {
            // Swapped: weighted score 0.4 meets the pass threshold, but
            // per-piece coverage 0 does not meet the 0.5 minimum.
            var target = Buffer3x3(Red, 0, 0, 0, Green, 0, 0, 0, 0);
            var current = Buffer3x3(Green, 0, 0, 0, Red, 0, 0, 0, 0);

            var result = CompositionScorer.Compare(target, current, new[] { Red, Green }, new CompositionPolicy(0.4f, 0.45f, 0.15f, 0.4f, 0.5f));

            Assert.That(result.WeightedScore, Is.EqualTo(0.4f).Within(1e-6f));
            Assert.That(result.MinimumPieceCoverage, Is.Zero);
            Assert.That(result.PassesPolicy, Is.False);
        }

        [Test]
        public void PieceResultsFollowRequiredOrder()
        {
            var target = Buffer3x3(Red, Green, Blue, 0, 0, 0, 0, 0, 0);
            var current = Buffer3x3(Red, 0, 0, 0, Green, 0, 0, 0, 0);

            var result = CompositionScorer.Compare(target, current, new[] { Blue, Green, Red });

            Assert.That(result.Pieces.Count, Is.EqualTo(3));

            Assert.That(result.Pieces[0].Id, Is.EqualTo(Blue));
            Assert.That(result.Pieces[0].TargetPixels, Is.EqualTo(1));
            Assert.That(result.Pieces[0].CurrentPixels, Is.Zero);
            Assert.That(result.Pieces[0].Intersection, Is.Zero);
            Assert.That(result.Pieces[0].Union, Is.EqualTo(1));
            Assert.That(result.Pieces[0].IoU, Is.EqualTo(0f));
            Assert.That(result.Pieces[0].TargetCoverage, Is.EqualTo(0f));

            Assert.That(result.Pieces[1].Id, Is.EqualTo(Green));
            Assert.That(result.Pieces[1].TargetPixels, Is.EqualTo(1));
            Assert.That(result.Pieces[1].CurrentPixels, Is.EqualTo(1));
            Assert.That(result.Pieces[1].Intersection, Is.Zero);
            Assert.That(result.Pieces[1].Union, Is.EqualTo(2));
            Assert.That(result.Pieces[1].IoU, Is.EqualTo(0f));
            Assert.That(result.Pieces[1].TargetCoverage, Is.EqualTo(0f));

            Assert.That(result.Pieces[2].Id, Is.EqualTo(Red));
            Assert.That(result.Pieces[2].TargetPixels, Is.EqualTo(1));
            Assert.That(result.Pieces[2].CurrentPixels, Is.EqualTo(1));
            Assert.That(result.Pieces[2].Intersection, Is.EqualTo(1));
            Assert.That(result.Pieces[2].Union, Is.EqualTo(1));
            Assert.That(result.Pieces[2].IoU, Is.EqualTo(1f));
            Assert.That(result.Pieces[2].TargetCoverage, Is.EqualTo(1f));

            Assert.That(result.SilhouetteIntersection, Is.EqualTo(1));
            Assert.That(result.SilhouetteUnion, Is.EqualTo(4));
            Assert.That(result.SilhouetteIoU, Is.EqualTo(0.25f));
            Assert.That(result.ExactSameIdPixels, Is.EqualTo(1));
            Assert.That(result.IdentityAccuracy, Is.EqualTo(0.25f));
            Assert.That(result.MeanPieceIoU, Is.EqualTo(1f / 3f).Within(1e-6f));
            Assert.That(result.MinimumPieceCoverage, Is.EqualTo(0f));
            Assert.That(result.WeightedScore, Is.EqualTo(0.2875f).Within(1e-6f));
        }

        [Test]
        public void PieceResultComputesUnionIoUAndCoverage()
        {
            var empty = new CompositionPieceResult(Red, 0, 0, 0);
            Assert.That(empty.Union, Is.Zero);
            Assert.That(empty.IoU, Is.EqualTo(1f));
            Assert.That(empty.TargetCoverage, Is.EqualTo(1f));

            var half = new CompositionPieceResult(Red, 2, 1, 1);
            Assert.That(half.Union, Is.EqualTo(2));
            Assert.That(half.IoU, Is.EqualTo(0.5f));
            Assert.That(half.TargetCoverage, Is.EqualTo(0.5f));
        }

        [Test]
        public void PieceResultValidatesItsInputs()
        {
            Assert.That(() => new CompositionPieceResult(0, 1, 0, 0),
                Throws.TypeOf<ArgumentException>());
            Assert.That(() => new CompositionPieceResult(0x1000000u, 1, 0, 0),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new CompositionPieceResult(Red, -1, 0, 0),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new CompositionPieceResult(Red, 0, -1, 0),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new CompositionPieceResult(Red, 1, 0, 2),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new CompositionPieceResult(Red, 1, 0, 1),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void ResultPiecesAreReadOnly()
        {
            var target = Buffer3x3(Red, 0, 0, 0, 0, 0, 0, 0, 0);
            var result = CompositionScorer.Compare(target, target, new[] { Red });

            Assert.That(result.Pieces, Is.InstanceOf<ReadOnlyCollection<CompositionPieceResult>>());
            Assert.That(ReferenceEquals(result.Pieces, result.Pieces), Is.True);
        }

        [Test]
        public void CompareWithoutPolicyUsesDefaultPolicy()
        {
            var target = Buffer3x3(Red, 0, 0, 0, Green, 0, 0, 0, 0);
            var current = Buffer3x3(Red, 0, 0, 0, 0, 0, 0, 0, 0);
            var ids = new[] { Red, Green };

            var implicitPolicy = CompositionScorer.Compare(target, current, ids);
            var explicitDefault = CompositionScorer.Compare(target, current, ids, CompositionPolicy.Default);

            Assert.That(implicitPolicy.WeightedScore, Is.EqualTo(explicitDefault.WeightedScore));
            Assert.That(implicitPolicy.PassesPolicy, Is.EqualTo(explicitDefault.PassesPolicy));
            Assert.That(implicitPolicy.IsExactMatch, Is.EqualTo(explicitDefault.IsExactMatch));
        }
    }
}
