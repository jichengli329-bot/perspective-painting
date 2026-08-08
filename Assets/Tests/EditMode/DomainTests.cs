using System;
using System.Collections.Generic;
using NUnit.Framework;
using PerspectivePuzzle.Domain;

namespace PerspectivePuzzle.Domain.Tests
{
    [TestFixture]
    public class GridCoordinateTests
    {
        [Test]
        public void EqualValuesAreEqual()
        {
            var a = new GridCoordinate(1, 2, 3);
            var b = new GridCoordinate(1, 2, 3);

            Assert.That(a, Is.EqualTo(b));
            Assert.That(a.Equals(b), Is.True);
            Assert.That(a == b, Is.True);
            Assert.That(a != b, Is.False);
        }

        [TestCase(0, 2, 3)]
        [TestCase(1, 0, 3)]
        [TestCase(1, 2, 0)]
        [TestCase(-1, 2, 3)]
        public void AnyComponentDifferenceIsUnequal(int x, int y, int z)
        {
            var a = new GridCoordinate(1, 2, 3);
            var b = new GridCoordinate(x, y, z);

            Assert.That(a.Equals(b), Is.False);
            Assert.That(a == b, Is.False);
            Assert.That(a != b, Is.True);
        }

        [Test]
        public void HashIsStableAcrossIndependentlyConstructedInstances()
        {
            var a = new GridCoordinate(4, 5, 6);
            var b = new GridCoordinate(4, 5, 6);

            Assert.That(b.GetHashCode(), Is.EqualTo(a.GetHashCode()));
        }

        [Test]
        public void HashDependsOnAllComponents()
        {
            var a = new GridCoordinate(1, 2, 3);
            var b = new GridCoordinate(3, 2, 1);

            Assert.That(b.GetHashCode(), Is.Not.EqualTo(a.GetHashCode()));
        }

        [Test]
        public void WorksAsDictionaryKey()
        {
            var dict = new Dictionary<GridCoordinate, string>();
            dict[new GridCoordinate(2, 3, 4)] = "cell";

            Assert.That(dict[new GridCoordinate(2, 3, 4)], Is.EqualTo("cell"));
            Assert.That(dict.ContainsKey(new GridCoordinate(2, 3, 4)), Is.True);

            dict.Remove(new GridCoordinate(2, 3, 4));
            Assert.That(dict.Count, Is.Zero);
        }
    }

    [TestFixture]
    public class OccupancyGrid3DTests
    {
        [TestCase(0, 2, 2)]
        [TestCase(2, 0, 2)]
        [TestCase(2, 2, 0)]
        [TestCase(-1, 2, 2)]
        [TestCase(2, -1, 2)]
        [TestCase(2, 2, -1)]
        public void ConstructionRejectsNonPositiveDimensions(int width, int height, int depth)
        {
            Assert.That(() => new OccupancyGrid3D(width, height, depth),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void ConstructionAcceptsValidDimensionsAndStartsEmpty()
        {
            var grid = new OccupancyGrid3D(3, 4, 5);

            Assert.That(grid.Width, Is.EqualTo(3));
            Assert.That(grid.Height, Is.EqualTo(4));
            Assert.That(grid.Depth, Is.EqualTo(5));
            Assert.That(grid.OccupiedCount, Is.Zero);
        }

        [Test]
        public void BoundsIncludeEdgesAndExcludeOutside()
        {
            var grid = new OccupancyGrid3D(3, 4, 5);

            Assert.That(grid.IsInBounds(new GridCoordinate(0, 0, 0)), Is.True);
            Assert.That(grid.IsInBounds(new GridCoordinate(2, 3, 4)), Is.True);
            Assert.That(grid.IsInBounds(new GridCoordinate(1, 2, 3)), Is.True);

            Assert.That(grid.IsInBounds(new GridCoordinate(-1, 0, 0)), Is.False);
            Assert.That(grid.IsInBounds(new GridCoordinate(0, -1, 0)), Is.False);
            Assert.That(grid.IsInBounds(new GridCoordinate(0, 0, -1)), Is.False);
            Assert.That(grid.IsInBounds(new GridCoordinate(3, 0, 0)), Is.False);
            Assert.That(grid.IsInBounds(new GridCoordinate(0, 4, 0)), Is.False);
            Assert.That(grid.IsInBounds(new GridCoordinate(0, 0, 5)), Is.False);
        }

        [Test]
        public void BoundaryCellsCanBePlacedAndRemoved()
        {
            var grid = new OccupancyGrid3D(3, 4, 5);

            Assert.That(grid.TryPlace(new GridCoordinate(0, 0, 0)), Is.True);
            Assert.That(grid.TryPlace(new GridCoordinate(2, 3, 4)), Is.True);
            Assert.That(grid.OccupiedCount, Is.EqualTo(2));
            Assert.That(grid.TryRemove(new GridCoordinate(0, 0, 0)), Is.True);
            Assert.That(grid.OccupiedCount, Is.EqualTo(1));
        }

        [TestCase(-1, 0, 0)]
        [TestCase(0, -1, 0)]
        [TestCase(0, 0, -1)]
        [TestCase(3, 0, 0)]
        [TestCase(0, 4, 0)]
        [TestCase(0, 0, 5)]
        public void PlacementOutsideBoundsFailsAndCountIsUnchanged(int x, int y, int z)
        {
            var grid = new OccupancyGrid3D(3, 4, 5);

            Assert.That(grid.TryPlace(new GridCoordinate(x, y, z)), Is.False);
            Assert.That(grid.OccupiedCount, Is.Zero);
        }

        [Test]
        public void QueryReflectsPlacement()
        {
            var grid = new OccupancyGrid3D(2, 2, 2);
            var cell = new GridCoordinate(1, 1, 1);

            Assert.That(grid.IsOccupied(cell), Is.False);
            grid.TryPlace(cell);
            Assert.That(grid.IsOccupied(cell), Is.True);
        }

        [Test]
        public void DuplicatePlacementIsPrevented()
        {
            var grid = new OccupancyGrid3D(2, 2, 2);
            var cell = new GridCoordinate(0, 0, 0);

            Assert.That(grid.TryPlace(cell), Is.True);
            Assert.That(grid.TryPlace(cell), Is.False);
            Assert.That(grid.OccupiedCount, Is.EqualTo(1));
        }

        [Test]
        public void RemovalFreesCellAndUpdatesCount()
        {
            var grid = new OccupancyGrid3D(2, 2, 2);
            var cell = new GridCoordinate(1, 0, 1);

            grid.TryPlace(cell);
            grid.TryPlace(new GridCoordinate(0, 0, 0));
            Assert.That(grid.OccupiedCount, Is.EqualTo(2));

            Assert.That(grid.TryRemove(cell), Is.True);
            Assert.That(grid.IsOccupied(cell), Is.False);
            Assert.That(grid.OccupiedCount, Is.EqualTo(1));

            Assert.That(grid.TryRemove(cell), Is.False);
        }

        [TestCase(-1, 0, 0)]
        [TestCase(2, 0, 0)]
        [TestCase(0, 2, 0)]
        [TestCase(0, 0, 2)]
        public void RemovalOutsideBoundsFailsAndCountIsUnchanged(int x, int y, int z)
        {
            var grid = new OccupancyGrid3D(2, 2, 2);
            grid.TryPlace(new GridCoordinate(0, 0, 0));

            Assert.That(grid.TryRemove(new GridCoordinate(x, y, z)), Is.False);
            Assert.That(grid.OccupiedCount, Is.EqualTo(1));
        }

        [Test]
        public void ClearEmptiesTheWholeGrid()
        {
            var grid = new OccupancyGrid3D(3, 3, 3);
            grid.TryPlace(new GridCoordinate(0, 0, 0));
            grid.TryPlace(new GridCoordinate(1, 1, 1));
            grid.TryPlace(new GridCoordinate(2, 2, 2));

            grid.Clear();

            Assert.That(grid.OccupiedCount, Is.Zero);
            Assert.That(grid.IsOccupied(new GridCoordinate(0, 0, 0)), Is.False);
            Assert.That(grid.IsOccupied(new GridCoordinate(1, 1, 1)), Is.False);
            Assert.That(grid.IsOccupied(new GridCoordinate(2, 2, 2)), Is.False);
        }

        [Test]
        public void OccupiedCountTracksPlaceAndRemoveSequence()
        {
            var grid = new OccupancyGrid3D(2, 2, 2);

            grid.TryPlace(new GridCoordinate(0, 0, 0));
            grid.TryPlace(new GridCoordinate(0, 1, 0));
            grid.TryPlace(new GridCoordinate(1, 0, 1));
            Assert.That(grid.OccupiedCount, Is.EqualTo(3));

            grid.TryPlace(new GridCoordinate(0, 0, 0)); // duplicate: no effect
            grid.TryRemove(new GridCoordinate(0, 1, 0));
            grid.TryRemove(new GridCoordinate(0, 0, 1)); // not occupied: no effect
            Assert.That(grid.OccupiedCount, Is.EqualTo(2));

            grid.TryPlace(new GridCoordinate(0, 1, 0)); // re-occupy after removal
            Assert.That(grid.OccupiedCount, Is.EqualTo(3));
        }
    }

    [TestFixture]
    public class ZProjectionTests
    {
        [Test]
        public void ProjectNullThrows()
        {
            Assert.That(() => ZProjection.Project(null), Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void EmptyGridProjectsToEmptyMap()
        {
            var grid = new OccupancyGrid3D(3, 2, 4);
            var map = ZProjection.Project(grid);

            Assert.That(map.Width, Is.EqualTo(3));
            Assert.That(map.Height, Is.EqualTo(2));
            Assert.That(map.OccupiedCount, Is.Zero);

            for (int x = 0; x < map.Width; x++)
                for (int y = 0; y < map.Height; y++)
                    Assert.That(map.IsOccupied(x, y), Is.False, "cell " + x + "," + y + " must be empty");
        }

        [Test]
        public void SingleDepthCollapsesOntoXY()
        {
            var grid = new OccupancyGrid3D(3, 3, 3);
            grid.TryPlace(new GridCoordinate(1, 1, 0));

            var map = ZProjection.Project(grid);

            Assert.That(map.OccupiedCount, Is.EqualTo(1));
            Assert.That(map.IsOccupied(1, 1), Is.True);
            Assert.That(map.IsOccupied(0, 0), Is.False);
        }

        [Test]
        public void AnyOccupiedDepthMarksTheColumnOccupied()
        {
            var grid = new OccupancyGrid3D(2, 2, 3);
            grid.TryPlace(new GridCoordinate(0, 0, 0));
            grid.TryPlace(new GridCoordinate(0, 0, 2)); // same column, deeper depth
            grid.TryPlace(new GridCoordinate(1, 1, 1));

            var map = ZProjection.Project(grid);

            Assert.That(map.OccupiedCount, Is.EqualTo(2));
            Assert.That(map.IsOccupied(0, 0), Is.True);
            Assert.That(map.IsOccupied(1, 1), Is.True);
            Assert.That(map.IsOccupied(1, 0), Is.False);
            Assert.That(map.IsOccupied(0, 1), Is.False);
        }
    }

    [TestFixture]
    public class TargetComparisonTests
    {
        private static ProjectionMap2D ProjectWith(params GridCoordinate[] occupied)
        {
            var grid = new OccupancyGrid3D(3, 3, 2);
            foreach (var cell in occupied)
                grid.TryPlace(cell);
            return ZProjection.Project(grid);
        }

        [Test]
        public void CompareNullArgumentsThrow()
        {
            var map = ProjectWith();

            Assert.That(() => TargetComparison.Compare(null, map), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => TargetComparison.Compare(map, null), Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void DimensionMismatchFailsExplicitly()
        {
            var map2x3 = ZProjection.Project(new OccupancyGrid3D(2, 3, 1));
            var map3x3 = ProjectWith();

            Assert.That(() => TargetComparison.Compare(map2x3, map3x3),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void ExactMatchReportsFullAgreement()
        {
            var current = ProjectWith(new GridCoordinate(0, 0, 0), new GridCoordinate(1, 1, 1), new GridCoordinate(2, 2, 0));
            var target = ProjectWith(new GridCoordinate(0, 0, 0), new GridCoordinate(1, 1, 1), new GridCoordinate(2, 2, 0));

            var result = TargetComparison.Compare(current, target);

            Assert.That(result.MatchingCells, Is.EqualTo(9));
            Assert.That(result.TotalCells, Is.EqualTo(9));
            Assert.That(result.NormalizedMatchRatio, Is.EqualTo(1f));
            Assert.That(result.IsExactMatch, Is.True);
        }

        [Test]
        public void TwoEmptyProjectionsMatchExactly()
        {
            var current = ProjectWith();
            var target = ProjectWith();

            var result = TargetComparison.Compare(current, target);

            Assert.That(result.IsExactMatch, Is.True);
            Assert.That(result.NormalizedMatchRatio, Is.EqualTo(1f));
        }

        [Test]
        public void PartialMatchReportsPartialAgreement()
        {
            var current = ProjectWith(new GridCoordinate(0, 0, 0));
            var target = ProjectWith(new GridCoordinate(1, 0, 0));

            var result = TargetComparison.Compare(current, target);

            Assert.That(result.MatchingCells, Is.EqualTo(7)); // 9 cells, 2 disagree
            Assert.That(result.TotalCells, Is.EqualTo(9));
            Assert.That(result.NormalizedMatchRatio, Is.EqualTo(7f / 9f));
            Assert.That(result.IsExactMatch, Is.False);
        }

        [Test]
        public void ExtraCellsBeyondTargetCountAsMismatches()
        {
            var current = ProjectWith(new GridCoordinate(0, 0, 0));
            var target = ProjectWith();

            var result = TargetComparison.Compare(current, target);

            Assert.That(result.MatchingCells, Is.EqualTo(8));
            Assert.That(result.IsExactMatch, Is.False);
        }

        [Test]
        public void MatchResultValidatesItsInputs()
        {
            Assert.That(() => new MatchResult(-1, 9), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new MatchResult(10, 9), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new MatchResult(0, 0), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void MatchResultComputesRatioAndExactState()
        {
            var partial = new MatchResult(3, 4);
            Assert.That(partial.NormalizedMatchRatio, Is.EqualTo(0.75f));
            Assert.That(partial.IsExactMatch, Is.False);

            var exact = new MatchResult(4, 4);
            Assert.That(exact.NormalizedMatchRatio, Is.EqualTo(1f));
            Assert.That(exact.IsExactMatch, Is.True);
        }
    }

    [TestFixture]
    public class ProjectionMap2DTests
    {
        [Test]
        public void FromCellsCreatesTargetAndCountsOccupiedCells()
        {
            var map = ProjectionMap2D.FromCells(3, 2, new[]
            {
                true, false, true,
                false, true, false
            });

            Assert.That(map.Width, Is.EqualTo(3));
            Assert.That(map.Height, Is.EqualTo(2));
            Assert.That(map.OccupiedCount, Is.EqualTo(3));
            Assert.That(map.IsOccupied(0, 0), Is.True);
            Assert.That(map.IsOccupied(2, 0), Is.True);
            Assert.That(map.IsOccupied(1, 1), Is.True);
        }

        [Test]
        public void FromCellsDefensivelyCopiesInput()
        {
            var cells = new[] { true, false };
            var map = ProjectionMap2D.FromCells(2, 1, cells);

            cells[0] = false;
            cells[1] = true;

            Assert.That(map.IsOccupied(0, 0), Is.True);
            Assert.That(map.IsOccupied(1, 0), Is.False);
        }

        [Test]
        public void FromCellsValidatesArguments()
        {
            Assert.That(() => ProjectionMap2D.FromCells(0, 1, new bool[0]),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => ProjectionMap2D.FromCells(1, 0, new bool[0]),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => ProjectionMap2D.FromCells(1, 1, null),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => ProjectionMap2D.FromCells(2, 2, new bool[3]),
                Throws.TypeOf<ArgumentException>());
        }
    }
}
