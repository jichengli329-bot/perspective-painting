using System;
using UnityEngine;
using NUnit.Framework;
using PerspectivePuzzle.Domain;
using PerspectivePuzzle.Presentation;

namespace PerspectivePuzzle.Presentation.Tests
{
    using Assert = NUnit.Framework.Assert; // UnityEngine.Assertions.Assert also defines "Assert"

    /// <summary>
    /// T-007 content tests: the project-owned <see cref="PuzzleContent"/> must
    /// define exactly three distinct, non-empty, in-bounds 5x5 target patterns
    /// with a deliberate difficulty curve, and the <see cref="PuzzleProgression"/>
    /// coordinator must advance through them in order without ever wrapping.
    /// </summary>
    [TestFixture]
    public class PuzzleContentTests
    {
        [Test]
        public void ContentDefinesExactlyThreePuzzles()
        {
            Assert.That(PuzzleContent.Puzzles, Is.Not.Null, "PuzzleContent.Puzzles is null.");
            Assert.That(PuzzleContent.Puzzles.Length, Is.EqualTo(PuzzleProgression.PuzzleCount),
                "The slice must ship exactly " + PuzzleProgression.PuzzleCount + " puzzles.");
        }

        [Test]
        public void EveryTargetIsNonEmptyAndInsideTheFiveByFiveBoard()
        {
            foreach (var pattern in PuzzleContent.Puzzles)
            {
                Assert.That(pattern, Is.Not.Null, "A puzzle defines no cells.");
                Assert.That(pattern.Length, Is.GreaterThan(0), "A puzzle target is empty.");

                var seen = new bool[PuzzleSession.GridWidth * PuzzleSession.GridHeight];
                foreach (var cell in pattern)
                {
                    Assert.That(cell.x, Is.InRange(0, PuzzleSession.GridWidth - 1),
                        "Cell (" + cell.x + ", " + cell.y + ") is outside the board on X.");
                    Assert.That(cell.y, Is.InRange(0, PuzzleSession.GridHeight - 1),
                        "Cell (" + cell.x + ", " + cell.y + ") is outside the board on Y.");
                    int index = cell.y * PuzzleSession.GridWidth + cell.x;
                    Assert.That(seen[index], Is.False,
                        "Cell (" + cell.x + ", " + cell.y + ") appears twice in one puzzle.");
                    seen[index] = true;
                }
            }
        }

        [Test]
        public void AllThreeTargetsArePairwiseDistinct()
        {
            for (int i = 0; i < PuzzleContent.Puzzles.Length; i++)
            {
                for (int j = i + 1; j < PuzzleContent.Puzzles.Length; j++)
                {
                    Assert.That(Bitmask(PuzzleContent.Puzzles[i]), Is.Not.EqualTo(Bitmask(PuzzleContent.Puzzles[j])),
                        "Puzzle " + (i + 1) + " and puzzle " + (j + 1) + " are identical targets.");
                }
            }
        }

        [Test]
        public void DifficultyRisesFromPuzzleToPuzzle()
        {
            var counts = new int[PuzzleContent.Puzzles.Length];
            for (int i = 0; i < PuzzleContent.Puzzles.Length; i++)
                counts[i] = PuzzleContent.Puzzles[i].Length;

            Assert.That(counts[0], Is.LessThan(counts[1]), "Puzzle two must not be easier than puzzle one.");
            Assert.That(counts[1], Is.LessThan(counts[2]), "Puzzle three must not be easier than puzzle two.");
        }

        [Test]
        public void FirstPatternMatchesTheSerializedSceneTarget()
        {
            // The deterministic scene builder serializes puzzle one into the
            // session source; the runtime session for puzzle one must therefore
            // equal the content definition's first pattern.
            var progression = new PuzzleProgression(PuzzleContent.Puzzles);
            AssertTargetEquals(progression.Current, PuzzleContent.Puzzles[0]);
        }

        internal static ulong Bitmask(Vector2Int[] cells)
        {
            ulong mask = 0;
            foreach (var cell in cells)
                mask |= 1UL << (cell.y * PuzzleSession.GridWidth + cell.x);
            return mask;
        }

        internal static void AssertTargetEquals(ProjectionMap2D target, Vector2Int[] cells)
        {
            Assert.That(target.Width, Is.EqualTo(PuzzleSession.GridWidth));
            Assert.That(target.Height, Is.EqualTo(PuzzleSession.GridHeight));
            Assert.That(target.OccupiedCount, Is.EqualTo(cells.Length));

            var mask = 0UL;
            for (int y = 0; y < target.Height; y++)
                for (int x = 0; x < target.Width; x++)
                    if (target.IsOccupied(x, y))
                        mask |= 1UL << (y * target.Width + x);

            Assert.That(mask, Is.EqualTo(Bitmask(cells)), "Target does not match the pattern cells.");
        }
    }

    [TestFixture]
    public class PuzzleProgressionTests
    {
        private static readonly Vector2Int[] PuzzleOne = { new Vector2Int(0, 0), new Vector2Int(1, 1) };
        private static readonly Vector2Int[] PuzzleTwo = { new Vector2Int(0, 4), new Vector2Int(1, 4), new Vector2Int(2, 4) };
        private static readonly Vector2Int[] PuzzleThree = { new Vector2Int(0, 0), new Vector2Int(1, 1), new Vector2Int(2, 2), new Vector2Int(3, 3) };

        private static PuzzleProgression Progression()
        {
            return new PuzzleProgression(new[] { PuzzleOne, PuzzleTwo, PuzzleThree });
        }

        [Test]
        public void ProgressionStartsOnPuzzleOne()
        {
            var progression = Progression();

            Assert.That(progression.CurrentIndex, Is.Zero);
            Assert.That(progression.HasNext, Is.True);
            Assert.That(progression.IsOnFinalPuzzle, Is.False);
            AssertTargetEquals(progression.Current, PuzzleOne);
        }

        [Test]
        public void AdvanceMovesThroughAllThreePuzzlesInOrder()
        {
            var progression = Progression();

            Assert.That(progression.TryAdvance(out var next), Is.True);
            Assert.That(progression.CurrentIndex, Is.EqualTo(1));
            AssertTargetEquals(next, PuzzleTwo);
            AssertTargetEquals(progression.Current, PuzzleTwo);

            Assert.That(progression.TryAdvance(out next), Is.True);
            Assert.That(progression.CurrentIndex, Is.EqualTo(2));
            AssertTargetEquals(next, PuzzleThree);
            Assert.That(progression.HasNext, Is.False, "The final puzzle must have no next.");
            Assert.That(progression.IsOnFinalPuzzle, Is.True);
        }

        [Test]
        public void AdvanceOnTheFinalPuzzleFailsAndNeverWraps()
        {
            var progression = Progression();

            progression.TryAdvance(out _);
            progression.TryAdvance(out _);

            Assert.That(progression.TryAdvance(out var next), Is.False,
                "Advancing past the final puzzle must fail, never wrap to puzzle one.");
            Assert.That(next, Is.Null);
            Assert.That(progression.CurrentIndex, Is.EqualTo(2), "The index must stay on the final puzzle.");
            AssertTargetEquals(progression.Current, PuzzleThree);
        }

        [Test]
        public void ContentValidationRejectsWrongPuzzleCount()
        {
            Assert.That(() => new PuzzleProgression(new[] { PuzzleOne, PuzzleTwo }),
                Throws.TypeOf<ArgumentException>(), "Two puzzles must be rejected.");
            Assert.That(() => new PuzzleProgression(new[] { PuzzleOne, PuzzleTwo, PuzzleThree, PuzzleOne }),
                Throws.TypeOf<ArgumentException>(), "Four puzzles must be rejected.");
            Assert.That(() => new PuzzleProgression(null), Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void ContentValidationRejectsEmptyTargets()
        {
            Assert.That(() => new PuzzleProgression(new[] { Array.Empty<Vector2Int>(), PuzzleTwo, PuzzleThree }),
                Throws.TypeOf<ArgumentException>(), "An empty target must be rejected.");
        }

        [Test]
        public void ContentValidationRejectsOutOfBoundsCells()
        {
            var outOfBounds = new[] { new Vector2Int(5, 0) };
            Assert.That(() => new PuzzleProgression(new[] { PuzzleOne, outOfBounds, PuzzleThree }),
                Throws.TypeOf<ArgumentException>(), "An out-of-bounds cell must be rejected.");
        }

        [Test]
        public void ContentValidationRejectsDuplicateTargets()
        {
            Assert.That(() => new PuzzleProgression(new[] { PuzzleOne, PuzzleOne, PuzzleThree }),
                Throws.TypeOf<ArgumentException>(), "Duplicate targets must be rejected.");
        }

        private static void AssertTargetEquals(ProjectionMap2D target, Vector2Int[] cells)
        {
            PuzzleContentTests.AssertTargetEquals(target, cells);
        }
    }
}
