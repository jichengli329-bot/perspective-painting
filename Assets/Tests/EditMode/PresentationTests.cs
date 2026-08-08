using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using NUnit.Framework;
using PerspectivePuzzle.Domain;
using PerspectivePuzzle.Presentation;

namespace PerspectivePuzzle.Presentation.Tests
{
    using Assert = NUnit.Framework.Assert; // UnityEngine.Assertions.Assert also defines "Assert"

    [TestFixture]
    public class GridCoordinateMapperTests
    {
        private static readonly Vector3 Origin = new Vector3(1f, 2f, 3f);
        private const float SpacingX = 0.5f;
        private const float SpacingY = 0.7f;
        private const float LayerHeight = 0.4f;

        private static GridCoordinateMapper Mapper()
        {
            return GridCoordinateMapper.ForPuzzle5x5x3(Origin, SpacingX, SpacingY, LayerHeight);
        }

        private static void AssertVectorEqual(Vector3 expected, Vector3 actual)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(1e-5f), "x");
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(1e-5f), "y");
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(1e-5f), "z");
        }

        [Test]
        public void PuzzleFactoryProducesTheFiveByFiveByThreeGrid()
        {
            var mapper = Mapper();

            Assert.That(mapper.Width, Is.EqualTo(5));
            Assert.That(mapper.Height, Is.EqualTo(5));
            Assert.That(mapper.Depth, Is.EqualTo(3));
        }

        [Test]
        public void WorldFromCellMapsGridAxesToWorldAxes()
        {
            var world = Mapper().WorldFromCell(new GridCoordinate(2, 3, 1));

            // Grid X -> world X, grid Z (layer) -> world Y, grid Y -> world Z.
            AssertVectorEqual(new Vector3(
                Origin.x + 2f * SpacingX,
                Origin.y + 1f * LayerHeight,
                Origin.z + 3f * SpacingY), world);
        }

        [TestCase(5, 0, 0)]
        [TestCase(0, 5, 0)]
        [TestCase(0, 0, 3)]
        [TestCase(-1, 0, 0)]
        public void WorldFromCellRejectsOutOfBoundsCells(int x, int y, int z)
        {
            Assert.That(() => Mapper().WorldFromCell(new GridCoordinate(x, y, z)),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void RoundTripPreservesEveryCellInThePuzzleGrid()
        {
            var mapper = Mapper();

            for (int x = 0; x < mapper.Width; x++)
            {
                for (int y = 0; y < mapper.Height; y++)
                {
                    for (int z = 0; z < mapper.Depth; z++)
                    {
                        var cell = new GridCoordinate(x, y, z);
                        Assert.That(mapper.TryCellFromWorld(mapper.WorldFromCell(cell), out var snapped), Is.True);
                        Assert.That(snapped, Is.EqualTo(cell));
                    }
                }
            }
        }

        [Test]
        public void TryCellFromWorldSnapsOffCenterWorldPositionsToNearestCell()
        {
            var world = Origin + new Vector3(2.2f * SpacingX, 1.1f * LayerHeight, 3.3f * SpacingY);

            Assert.That(Mapper().TryCellFromWorld(world, out var cell), Is.True);
            Assert.That(cell, Is.EqualTo(new GridCoordinate(2, 3, 1)));
        }

        [Test]
        public void TryCellFromWorldFailsBeyondTheGridEdges()
        {
            var mapper = Mapper();

            Assert.That(mapper.TryCellFromWorld(Origin + new Vector3(4.6f * SpacingX, 0f, 0f), out _), Is.False);
            Assert.That(mapper.TryCellFromWorld(Origin + new Vector3(0f, 3f * LayerHeight, 0f), out _), Is.False);
            Assert.That(mapper.TryCellFromWorld(Origin + new Vector3(0f, 0f, -1f), out _), Is.False);
        }

        [TestCase(0, 1, 1)]
        [TestCase(1, 0, 1)]
        [TestCase(1, 1, 0)]
        public void ConstructorRejectsNonPositiveDimensions(int width, int height, int depth)
        {
            Assert.That(() => new GridCoordinateMapper(width, height, depth, Origin, SpacingX, SpacingY, LayerHeight),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [TestCase(0f, 1f, 1f)]
        [TestCase(1f, 0f, 1f)]
        [TestCase(1f, 1f, 0f)]
        [TestCase(-1f, 1f, 1f)]
        public void ConstructorRejectsNonPositiveSpacings(float spacingX, float spacingY, float layerHeight)
        {
            Assert.That(() => new GridCoordinateMapper(5, 5, 3, Origin, spacingX, spacingY, layerHeight),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }
    }

    [TestFixture]
    public class PlacementHistoryTests
    {
        private static OccupancyGrid3D PuzzleGrid()
        {
            return new OccupancyGrid3D(5, 5, 3);
        }

        [Test]
        public void NullGridThrows()
        {
            Assert.That(() => new PlacementHistory(null), Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void UndoWithoutHistoryReturnsFalse()
        {
            var history = new PlacementHistory(PuzzleGrid());

            Assert.That(history.CanUndo, Is.False);
            Assert.That(history.TryUndo(), Is.False);
        }

        [Test]
        public void PlaceThenUndoRestoresTheEmptyCell()
        {
            var grid = PuzzleGrid();
            var history = new PlacementHistory(grid);
            var cell = new GridCoordinate(1, 1, 1);

            Assert.That(history.TryPlace(cell), Is.True);
            Assert.That(grid.IsOccupied(cell), Is.True);
            Assert.That(history.CanUndo, Is.True);
            Assert.That(history.LastCommand.HasValue, Is.True);
            Assert.That(history.LastCommand.Value.Cell, Is.EqualTo(cell));
            Assert.That(history.LastCommand.Value.WasPlacement, Is.True);

            Assert.That(history.TryUndo(), Is.True);
            Assert.That(grid.IsOccupied(cell), Is.False);
            Assert.That(history.CanUndo, Is.False);
        }

        [Test]
        public void RemoveThenUndoReplacesThePiece()
        {
            var grid = PuzzleGrid();
            var history = new PlacementHistory(grid);
            var cell = new GridCoordinate(2, 2, 0);

            history.TryPlace(cell);
            Assert.That(history.TryRemove(cell), Is.True);
            Assert.That(grid.IsOccupied(cell), Is.False);
            Assert.That(history.LastCommand.Value.WasPlacement, Is.False);

            Assert.That(history.TryUndo(), Is.True);
            Assert.That(grid.IsOccupied(cell), Is.True);
        }

        [Test]
        public void FailedPlacementDoesNotReplaceTheRecordedAction()
        {
            var grid = PuzzleGrid();
            var history = new PlacementHistory(grid);
            var cell = new GridCoordinate(0, 0, 0);

            history.TryPlace(cell);
            Assert.That(history.TryPlace(cell), Is.False); // duplicate: rejected by the grid

            Assert.That(history.LastCommand.Value.Cell, Is.EqualTo(cell));
            Assert.That(history.LastCommand.Value.WasPlacement, Is.True);

            Assert.That(history.TryUndo(), Is.True);
            Assert.That(grid.IsOccupied(cell), Is.False);
        }

        [Test]
        public void FailedRemovalDoesNotReplaceTheRecordedAction()
        {
            var grid = PuzzleGrid();
            var history = new PlacementHistory(grid);
            var cell = new GridCoordinate(0, 0, 0);

            history.TryPlace(cell);
            Assert.That(history.TryRemove(new GridCoordinate(4, 4, 2)), Is.False); // empty cell

            Assert.That(history.TryUndo(), Is.True);
            Assert.That(grid.IsOccupied(cell), Is.False);
        }

        [Test]
        public void OnlyTheLastActionIsUndoable()
        {
            var grid = PuzzleGrid();
            var history = new PlacementHistory(grid);
            var first = new GridCoordinate(0, 0, 0);
            var second = new GridCoordinate(1, 1, 1);

            history.TryPlace(first);
            history.TryPlace(second);

            Assert.That(history.TryUndo(), Is.True);
            Assert.That(grid.IsOccupied(second), Is.False);
            Assert.That(grid.IsOccupied(first), Is.True);
            Assert.That(history.CanUndo, Is.False); // the earlier placement is not undoable
        }

        [Test]
        public void UndoOfRemovalReplacesPieceAfterEarlierPlacement()
        {
            var grid = PuzzleGrid();
            var history = new PlacementHistory(grid);
            var first = new GridCoordinate(0, 0, 0);
            var second = new GridCoordinate(1, 1, 1);

            history.TryPlace(first);
            history.TryPlace(second);
            history.TryRemove(second);

            Assert.That(history.TryUndo(), Is.True);
            Assert.That(grid.IsOccupied(second), Is.True);
            Assert.That(grid.IsOccupied(first), Is.True);
            Assert.That(grid.OccupiedCount, Is.EqualTo(2));
            Assert.That(history.TryUndo(), Is.False);
        }

        [Test]
        public void PlacementAfterUndoWorksNormally()
        {
            var grid = PuzzleGrid();
            var history = new PlacementHistory(grid);
            var cell = new GridCoordinate(3, 3, 2);

            history.TryPlace(cell);
            history.TryUndo();
            Assert.That(history.TryPlace(cell), Is.True);
            Assert.That(grid.IsOccupied(cell), Is.True);
        }
    }

    [TestFixture]
    public class PuzzleSessionTests
    {
        private static OccupancyGrid3D PuzzleGrid()
        {
            return new OccupancyGrid3D(5, 5, 3);
        }

        private static ProjectionMap2D TargetWith(params (int x, int y)[] cells)
        {
            var map = new bool[25];
            foreach (var cell in cells)
                map[cell.y * 5 + cell.x] = true;
            return ProjectionMap2D.FromCells(5, 5, map);
        }

        private static PuzzleSession Session()
        {
            return new PuzzleSession(PuzzleGrid(), TargetWith((0, 0), (1, 1)));
        }

        [Test]
        public void ConstructionValidatesGridAndTargetArguments()
        {
            var target = TargetWith((0, 0), (1, 1));

            Assert.That(() => new PuzzleSession(null, target), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => new PuzzleSession(PuzzleGrid(), null), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => new PuzzleSession(new OccupancyGrid3D(5, 5, 2), target), Throws.TypeOf<ArgumentException>());
            Assert.That(() => new PuzzleSession(PuzzleGrid(), ProjectionMap2D.FromCells(4, 5, new bool[20])),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void ConstructionComputesEmptyProjectionAndComparison()
        {
            var session = Session();

            Assert.That(session.CurrentProjection.Width, Is.EqualTo(5));
            Assert.That(session.CurrentProjection.Height, Is.EqualTo(5));
            Assert.That(session.CurrentProjection.OccupiedCount, Is.Zero);
            Assert.That(session.Comparison.MatchingCells, Is.EqualTo(23)); // 25 cells, 2 target cells missing
            Assert.That(session.IsLocked, Is.False);
        }

        [Test]
        public void PlacementUpdatesProjectionAndComparison()
        {
            var session = Session();

            Assert.That(session.TryPlace(new GridCoordinate(0, 0, 1)), Is.True);

            Assert.That(session.CurrentProjection.IsOccupied(0, 0), Is.True);
            Assert.That(session.CurrentProjection.IsOccupied(1, 1), Is.False);
            Assert.That(session.Comparison.MatchingCells, Is.EqualTo(24)); // one target cell now matched
            Assert.That(session.IsLocked, Is.False);
        }

        [Test]
        public void ExactMatchLocksTheSessionAndFiresTheRevealSignalOnce()
        {
            var session = Session();
            int revealSignals = 0;
            session.ExactMatchReached += () => revealSignals++;

            Assert.That(session.TryPlace(new GridCoordinate(0, 0, 0)), Is.True);
            Assert.That(session.IsLocked, Is.False);
            Assert.That(revealSignals, Is.Zero);

            Assert.That(session.TryPlace(new GridCoordinate(1, 1, 2)), Is.True);
            Assert.That(session.Comparison.IsExactMatch, Is.True);
            Assert.That(session.IsLocked, Is.True);
            Assert.That(revealSignals, Is.EqualTo(1));

            // Locked: every further mutation and undo is refused.
            Assert.That(session.TryPlace(new GridCoordinate(2, 2, 0)), Is.False);
            Assert.That(session.TryRemove(new GridCoordinate(1, 1, 2)), Is.False);
            Assert.That(session.TryUndo(), Is.False);
            Assert.That(revealSignals, Is.EqualTo(1));
        }

        [Test]
        public void UndoRestoresProjectionAndComparison()
        {
            var session = Session();

            session.TryPlace(new GridCoordinate(0, 0, 0));
            Assert.That(session.TryUndo(), Is.True);

            Assert.That(session.CurrentProjection.OccupiedCount, Is.Zero);
            Assert.That(session.Comparison.MatchingCells, Is.EqualTo(23));
            Assert.That(session.History.CanUndo, Is.False);
        }

        [Test]
        public void TryGetTopmostOccupiedFindsThePointedPieceInAColumn()
        {
            var session = Session();

            session.TryPlace(new GridCoordinate(2, 2, 0));
            session.TryPlace(new GridCoordinate(2, 2, 2));

            Assert.That(session.TryGetTopmostOccupied(2, 2, out var top), Is.True);
            Assert.That(top, Is.EqualTo(new GridCoordinate(2, 2, 2)));

            session.TryRemove(top);
            Assert.That(session.TryGetTopmostOccupied(2, 2, out top), Is.True);
            Assert.That(top, Is.EqualTo(new GridCoordinate(2, 2, 0)));

            Assert.That(session.TryGetTopmostOccupied(0, 0, out _), Is.False);
            Assert.That(session.TryGetTopmostOccupied(5, 0, out _), Is.False);
        }

        [Test]
        public void CanPlaceAtReportsBoundsAndOccupancy()
        {
            var session = Session();
            var occupied = new GridCoordinate(0, 0, 0);

            Assert.That(session.CanPlaceAt(occupied), Is.True);
            session.TryPlace(occupied);

            Assert.That(session.CanPlaceAt(occupied), Is.False); // occupied
            Assert.That(session.CanPlaceAt(new GridCoordinate(5, 0, 0)), Is.False); // out of bounds
            Assert.That(session.CanPlaceAt(new GridCoordinate(4, 4, 2)), Is.True);
        }

        [Test]
        public void EmptyTargetLocksTheSessionAtConstruction()
        {
            var session = new PuzzleSession(PuzzleGrid(), TargetWith());

            Assert.That(session.IsLocked, Is.True);
            Assert.That(session.TryPlace(new GridCoordinate(0, 0, 0)), Is.False);
        }
    }

    [TestFixture]
    public class PuzzleSessionSourceResetTests
    {
        /// <summary>
        /// The R reset must rebuild the session — fresh grid, history and lock —
        /// around the current puzzle's target and rebind the controller, all
        /// without any scene or application reload. The source's Awake is not
        /// invoked for edit-mode components, so Initialize is called
        /// explicitly, exactly as Awake would.
        /// </summary>
        [Test]
        public void RebuildWithRebuildsGridHistoryAndRebindsController()
        {
            var controllerGo = new GameObject("Test Controller");
            var controller = controllerGo.AddComponent<PuzzleInputController>();
            var sourceGo = new GameObject("Test Source");
            var source = sourceGo.AddComponent<PuzzleSessionSource>();

            try
            {
                var serialized = new SerializedObject(source);
                serialized.FindProperty("controller").objectReferenceValue = controller;
                var targetProperty = serialized.FindProperty("targetCells");
                targetProperty.arraySize = 2;
                targetProperty.GetArrayElementAtIndex(0).vector2IntValue = new Vector2Int(1, 3);
                targetProperty.GetArrayElementAtIndex(1).vector2IntValue = new Vector2Int(2, 2);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                source.Initialize();

                var firstSession = source.Session;
                Assert.That(firstSession, Is.Not.Null, "Initialize did not build a session.");
                Assert.That(firstSession.TryPlace(new GridCoordinate(0, 0, 0)), Is.True);
                Assert.That(firstSession.TryPlace(new GridCoordinate(1, 1, 1)), Is.True);

                // The controller's R reset rebuilds around the current target,
                // never falling back to puzzle one.
                source.RebuildWith(firstSession.Target);

                var secondSession = source.Session;
                Assert.That(secondSession, Is.Not.SameAs(firstSession), "Reset must build a fresh session.");
                Assert.That(secondSession.Target, Is.SameAs(firstSession.Target),
                    "Reset must keep the current puzzle's target.");
                Assert.That(secondSession.Grid.OccupiedCount, Is.Zero, "Reset must empty the grid.");
                Assert.That(secondSession.History.CanUndo, Is.False, "Reset must clear undo history.");
                Assert.That(secondSession.IsLocked, Is.False, "Reset must unlock the session.");
                Assert.That(secondSession.TryPlace(new GridCoordinate(0, 0, 0)), Is.True,
                    "Reset must allow placing again.");

                Assert.That(ReadSessionField(controller), Is.SameAs(secondSession),
                    "Reset must rebind the controller to the fresh session.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sourceGo);
                UnityEngine.Object.DestroyImmediate(controllerGo);
            }
        }

        private static PuzzleSession ReadSessionField(PuzzleInputController controller)
        {
            var field = typeof(PuzzleInputController).GetField(
                "session", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "PuzzleInputController.session field not found.");
            return (PuzzleSession)field.GetValue(controller);
        }
    }
}
