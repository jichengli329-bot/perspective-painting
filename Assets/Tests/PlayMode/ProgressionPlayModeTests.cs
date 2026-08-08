using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PerspectivePuzzle.Domain;
using PerspectivePuzzle.Presentation;

namespace PerspectivePuzzle.PlayMode.Tests
{
    /// <summary>
    /// T-007 progression tests: load the playable prototype scene and drive the
    /// controller's own action path (PlaceAt / TryAdvancePuzzle / ResetPuzzle,
    /// the same methods Update maps input keys to) to verify ordered
    /// 1 → 2 → 3 progression, per-puzzle reset, transition cleanup, the final
    /// non-text hold without wrapping, and that rapid input during
    /// reveal/transition cannot mutate the locked session or leave orphaned
    /// views.
    /// </summary>
    public class ProgressionPlayModeTests
    {
        private const string PlayablePrototypeScenePath = "Assets/Scenes/PlayablePrototype.unity";
        private const float RevealWaitSeconds = 2f;
        private const float CameraMoveWaitSeconds = 4f;
        private const float CameraArrivalTolerance = 0.05f;

        [UnityTest]
        public IEnumerator SpaceAdvanceTransitionsToNextPuzzleAndCleansEverything()
        {
            SceneManager.LoadScene(PlayablePrototypeScenePath, LoadSceneMode.Single);
            yield return null;

            var source = UnityEngine.Object.FindFirstObjectByType<PuzzleSessionSource>();
            var controller = UnityEngine.Object.FindFirstObjectByType<PuzzleInputController>();
            var reveal = UnityEngine.Object.FindFirstObjectByType<MatchReveal>();
            var board = UnityEngine.Object.FindFirstObjectByType<ProjectionBoardView>();
            var indicator = UnityEngine.Object.FindFirstObjectByType<ProgressIndicator>();
            var legend = UnityEngine.Object.FindFirstObjectByType<ControlLegend>();
            Assert.IsNotNull(controller, "PuzzleInputController missing.");
            Assert.IsNotNull(indicator, "ProgressIndicator missing.");
            Assert.IsNotNull(legend, "ControlLegend missing.");

            var camera = ReadField<Camera>(reveal, "targetCamera");
            var initialPosition = camera.transform.position;
            var initialRotation = camera.transform.rotation;
            var initialOrthographicSize = camera.orthographicSize;

            // Slice starts on puzzle one: step one highlighted, hint visible.
            Assert.AreEqual(0, controller.CurrentPuzzleIndex, "Progression must start on puzzle one.");
            Assert.IsTrue(controller.HasNextPuzzle, "Puzzle one must have a next puzzle.");
            Assert.AreEqual(0, indicator.CurrentStep, "Progress indicator must start on step one.");
            Assert.IsTrue(legend.NextHintVisible, "Legend must mention Space while a next puzzle exists.");

            // Solve puzzle one through the controller.
            var revealed = new RevealFlag();
            controller.Revealed += () => revealed.Raised = true;
            Assert.IsTrue(Solve(controller, PuzzleContent.Puzzles[0]), "Solving puzzle one failed.");
            yield return WaitForReveal(revealed);
            Assert.IsTrue(revealed.Raised, "Puzzle one reveal not raised.");

            // Space advance (Update maps the Space key to this method).
            Assert.IsTrue(controller.TryAdvancePuzzle(), "Space advance from puzzle one refused.");
            yield return null;
            yield return null;

            // Ordered progression: now puzzle two.
            Assert.AreEqual(1, controller.CurrentPuzzleIndex, "Progression did not advance to puzzle two.");
            Assert.IsFalse(source.Session.IsLocked, "Transition must unlock the fresh session.");
            Assert.AreEqual(0, source.Session.Grid.OccupiedCount, "Transition must clear the occupancy grid.");
            Assert.IsFalse(source.Session.History.CanUndo, "Transition must clear undo history.");
            Assert.AreEqual(0, controller.PieceViewCount, "Transition must clear piece views.");
            Assert.AreEqual(0, CountPieceViewsUnderRoot(), "Transition must leave no orphaned piece views.");
            Assert.AreEqual(0, controller.ActiveLayerZ, "Transition must restore layer 1.");
            Assert.AreEqual(initialOrthographicSize, camera.orthographicSize, "Transition must restore camera size.");
            Assert.Less(Vector3.Distance(camera.transform.position, initialPosition), CameraArrivalTolerance,
                "Transition must restore the camera position.");
            Assert.AreEqual(initialRotation, camera.transform.rotation, "Transition must restore the camera rotation.");

            // Board repaints the puzzle-two target (all Missing, none placed).
            AssertTargetEquals(board.Target, PuzzleContent.Puzzles[1], "Board must show the puzzle-two target.");
            foreach (var cell in PuzzleContent.Puzzles[1])
                Assert.AreEqual(ProjectionCellState.Missing, board.StateAt(cell.x, cell.y),
                    "Transition must repaint puzzle-two target cell (" + cell.x + ", " + cell.y + ") as Missing.");

            // The restrained step indicator and legend follow the progression.
            Assert.AreEqual(1, indicator.CurrentStep, "Progress indicator must advance to step two.");
            Assert.IsTrue(legend.NextHintVisible, "Puzzle two must still mention Space.");

            // Input works from the fresh state again.
            Assert.IsTrue(controller.PlaceAt(new GridCoordinate(0, 0, 0)), "Transition must allow placing again.");

            yield return UnloadPlayableScene();
        }

        [UnityTest]
        public IEnumerator ResetOnlyResetsTheCurrentPuzzle()
        {
            SceneManager.LoadScene(PlayablePrototypeScenePath, LoadSceneMode.Single);
            yield return null;

            var source = UnityEngine.Object.FindFirstObjectByType<PuzzleSessionSource>();
            var controller = UnityEngine.Object.FindFirstObjectByType<PuzzleInputController>();
            var board = UnityEngine.Object.FindFirstObjectByType<ProjectionBoardView>();
            var indicator = UnityEngine.Object.FindFirstObjectByType<ProgressIndicator>();

            // Reach puzzle two.
            var revealed = new RevealFlag();
            controller.Revealed += () => revealed.Raised = true;
            Assert.IsTrue(Solve(controller, PuzzleContent.Puzzles[0]), "Solving puzzle one failed.");
            yield return WaitForReveal(revealed);
            Assert.IsTrue(revealed.Raised, "Puzzle one reveal not raised.");
            Assert.IsTrue(controller.TryAdvancePuzzle(), "Advance to puzzle two refused.");

            // Play some of puzzle two, including a layer change and an undo.
            Assert.IsTrue(controller.PlaceAt(new GridCoordinate(0, 0, 0)), "Puzzle-two place refused.");
            Assert.IsTrue(controller.PlaceAt(new GridCoordinate(1, 1, 1)), "Puzzle-two layer-2 place refused.");
            Assert.IsTrue(controller.PlaceAt(new GridCoordinate(2, 2, 2)), "Puzzle-two layer-3 place refused.");
            Assert.IsTrue(controller.RemoveAt(new GridCoordinate(0, 0, 0)), "Puzzle-two remove refused.");

            // R resets only the current puzzle: still puzzle two, fresh state.
            controller.ResetPuzzle();
            yield return null;
            yield return null;

            Assert.AreEqual(1, controller.CurrentPuzzleIndex, "R reset must not move the progression back to puzzle one.");
            Assert.IsFalse(source.Session.IsLocked, "Reset must unlock the session.");
            Assert.AreEqual(0, source.Session.Grid.OccupiedCount, "Reset must empty the domain grid.");
            Assert.IsFalse(source.Session.History.CanUndo, "Reset must clear undo history.");
            Assert.AreEqual(0, controller.PieceViewCount, "Reset must remove every piece view.");
            Assert.AreEqual(0, CountPieceViewsUnderRoot(), "Reset must leave no orphaned piece views.");
            Assert.AreEqual(1, indicator.CurrentStep, "R reset must not reset the step indicator.");
            AssertTargetEquals(source.Session.Target, PuzzleContent.Puzzles[1],
                "R reset must keep puzzle two's target.");
            foreach (var cell in PuzzleContent.Puzzles[1])
                Assert.AreEqual(ProjectionCellState.Missing, board.StateAt(cell.x, cell.y),
                    "Reset must repaint puzzle-two target cell (" + cell.x + ", " + cell.y + ") as Missing.");

            // And puzzle two remains solvable after the reset.
            var revealedAgain = new RevealFlag();
            controller.Revealed += () => revealedAgain.Raised = true;
            Assert.IsTrue(Solve(controller, PuzzleContent.Puzzles[1]), "Puzzle two not solvable after reset.");
            yield return WaitForReveal(revealedAgain);
            Assert.IsTrue(revealedAgain.Raised, "Puzzle two reveal not raised after reset.");

            yield return UnloadPlayableScene();
        }

        [UnityTest]
        public IEnumerator FinalPuzzleLocksAndNeverWraps()
        {
            SceneManager.LoadScene(PlayablePrototypeScenePath, LoadSceneMode.Single);
            yield return null;

            var source = UnityEngine.Object.FindFirstObjectByType<PuzzleSessionSource>();
            var controller = UnityEngine.Object.FindFirstObjectByType<PuzzleInputController>();
            var reveal = UnityEngine.Object.FindFirstObjectByType<MatchReveal>();
            var board = UnityEngine.Object.FindFirstObjectByType<ProjectionBoardView>();
            var indicator = UnityEngine.Object.FindFirstObjectByType<ProgressIndicator>();
            var legend = UnityEngine.Object.FindFirstObjectByType<ControlLegend>();

            var camera = ReadField<Camera>(reveal, "targetCamera");
            var revealPosition = ReadField<Vector3>(reveal, "revealPosition");

            // Advance 1 → 2 → 3 through the reveal states.
            var revealed = new RevealFlag();
            controller.Revealed += () => revealed.Raised = true;
            for (int puzzle = 0; puzzle < 2; puzzle++)
            {
                Assert.IsTrue(Solve(controller, PuzzleContent.Puzzles[puzzle]), "Solving puzzle " + (puzzle + 1) + " failed.");
                yield return WaitForReveal(revealed);
                Assert.IsTrue(revealed.Raised, "Puzzle " + (puzzle + 1) + " reveal not raised.");
                Assert.IsTrue(controller.TryAdvancePuzzle(), "Advance from puzzle " + (puzzle + 1) + " refused.");
                revealed.Raised = false;
            }

            // Puzzle three: solve and wait for the reveal camera to arrive.
            Assert.IsTrue(Solve(controller, PuzzleContent.Puzzles[2]), "Solving puzzle three failed.");
            yield return WaitForReveal(revealed);
            Assert.IsTrue(revealed.Raised, "Puzzle three reveal not raised.");
            float cameraDeadline = Time.realtimeSinceStartup + CameraMoveWaitSeconds;
            while (Vector3.Distance(camera.transform.position, revealPosition) > CameraArrivalTolerance
                   && Time.realtimeSinceStartup < cameraDeadline)
                yield return null;

            // Final locked non-text hold: no wrap, no mutation, hint hidden.
            Assert.AreEqual(2, controller.CurrentPuzzleIndex, "Progression must sit on the final puzzle.");
            Assert.IsFalse(controller.HasNextPuzzle, "The final puzzle must have no next.");
            Assert.IsFalse(controller.TryAdvancePuzzle(), "Advancing past the final puzzle must fail.");
            Assert.IsFalse(controller.TryAdvancePuzzle(), "Advancing past the final puzzle must fail repeatedly.");
            Assert.AreEqual(2, controller.CurrentPuzzleIndex, "The final puzzle must never wrap to puzzle one.");
            Assert.IsTrue(source.Session.IsLocked, "The final puzzle must stay locked.");
            Assert.AreEqual(9, source.Session.Grid.OccupiedCount, "The final hold must keep the solved grid.");
            Assert.IsFalse(controller.PlaceAt(new GridCoordinate(0, 0, 0)), "The locked final session must refuse input.");
            Assert.Less(Vector3.Distance(camera.transform.position, revealPosition), CameraArrivalTolerance,
                "The final hold must keep the reveal camera.");
            Assert.AreEqual(2, indicator.CurrentStep, "The step indicator must rest on step three.");
            Assert.IsFalse(legend.NextHintVisible, "The legend must drop the Space hint on the final puzzle.");
            foreach (var cell in PuzzleContent.Puzzles[2])
                Assert.AreEqual(ProjectionCellState.Matched, board.StateAt(cell.x, cell.y),
                    "The final hold must keep puzzle-three target cell (" + cell.x + ", " + cell.y + ") matched.");

            // R still resets only the final puzzle (no wrap, fresh puzzle three).
            controller.ResetPuzzle();
            yield return null;
            Assert.AreEqual(2, controller.CurrentPuzzleIndex, "R reset must keep the final puzzle.");
            Assert.IsFalse(source.Session.IsLocked, "R reset must unlock the final puzzle.");
            AssertTargetEquals(source.Session.Target, PuzzleContent.Puzzles[2],
                "R reset must keep puzzle three's target.");

            yield return UnloadPlayableScene();
        }

        [UnityTest]
        public IEnumerator RapidInputDuringRevealAndTransitionIsIgnored()
        {
            SceneManager.LoadScene(PlayablePrototypeScenePath, LoadSceneMode.Single);
            yield return null;

            var source = UnityEngine.Object.FindFirstObjectByType<PuzzleSessionSource>();
            var controller = UnityEngine.Object.FindFirstObjectByType<PuzzleInputController>();

            // Solve puzzle one as fast as the controller allows.
            foreach (var cell in PuzzleContent.Puzzles[0])
                Assert.IsTrue(controller.PlaceAt(new GridCoordinate(cell.x, cell.y, 0)),
                    "Placing puzzle-one cell (" + cell.x + ", " + cell.y + ") refused.");

            // During the reveal the locked session refuses every mutation.
            Assert.IsTrue(source.Session.IsLocked, "Session must lock on exact match.");
            Assert.IsFalse(controller.PlaceAt(new GridCoordinate(0, 0, 0)),
                "Rapid input must not mutate the locked session.");
            Assert.IsFalse(controller.RemoveAt(new GridCoordinate(0, 0, 0)),
                "Rapid input must not mutate the locked session.");

            // Hammer the advance path during the reveal animation: exactly one
            // transition happens; the next call already sees an unlocked fresh
            // session and is ignored.
            Assert.IsTrue(controller.TryAdvancePuzzle(), "First advance during reveal refused.");
            Assert.IsFalse(controller.TryAdvancePuzzle(), "Second advance during transition must be ignored.");
            Assert.IsFalse(controller.TryAdvancePuzzle(), "Third advance during transition must be ignored.");
            yield return null;
            yield return null;

            Assert.AreEqual(1, controller.CurrentPuzzleIndex, "Rapid advance must move exactly one puzzle.");
            Assert.AreEqual(0, controller.PieceViewCount, "Rapid advance must not leave piece views.");
            Assert.AreEqual(0, CountPieceViewsUnderRoot(), "Rapid advance must not leave orphaned views.");
            Assert.AreEqual(0, source.Session.Grid.OccupiedCount, "Rapid advance must not carry grid state over.");
            Assert.IsFalse(source.Session.IsLocked, "The fresh session must be unlocked and playable.");

            // Normal input works right after the transition.
            Assert.IsTrue(controller.PlaceAt(new GridCoordinate(1, 1, 0)), "Placing after rapid advance refused.");
            yield return UnloadPlayableScene();
        }

        /// <summary>Places every cell of <paramref name="pattern"/> on layer 0 through the controller.</summary>
        private static bool Solve(PuzzleInputController controller, Vector2Int[] pattern)
        {
            foreach (var cell in pattern)
            {
                if (!controller.PlaceAt(new GridCoordinate(cell.x, cell.y, 0)))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Yields frames until the controller's Update raises the reveal signal
        /// or the timeout elapses. The caller asserts the flag afterwards.
        /// </summary>
        private static IEnumerator WaitForReveal(RevealFlag revealed)
        {
            float deadline = Time.realtimeSinceStartup + RevealWaitSeconds;
            while (!revealed.Raised && Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        /// <summary>Mutable flag so the reveal event can be observed across coroutine frames.</summary>
        private sealed class RevealFlag
        {
            public bool Raised;
        }

        private static void AssertTargetEquals(ProjectionMap2D target, Vector2Int[] cells, string message)
        {
            Assert.AreEqual(cells.Length, target.OccupiedCount, message);
            for (int y = 0; y < target.Height; y++)
            {
                for (int x = 0; x < target.Width; x++)
                {
                    bool expected = System.Array.IndexOf(cells, new Vector2Int(x, y)) >= 0;
                    Assert.AreEqual(expected, target.IsOccupied(x, y), message + " (cell " + x + "," + y + ")");
                }
            }
        }

        private static IEnumerator UnloadPlayableScene()
        {
            var prototypeScene = SceneManager.GetActiveScene();
            var cleanupScene = SceneManager.CreateScene("T007Cleanup");
            SceneManager.SetActiveScene(cleanupScene);
            yield return SceneManager.UnloadSceneAsync(prototypeScene);
        }

        private static int CountPieceViewsUnderRoot()
        {
            var root = GameObject.Find("Pieces");
            return root != null ? root.GetComponentsInChildren<PieceView>(true).Length : 0;
        }

        /// <summary>Reads a private serialized field by name at runtime (SerializedObject is editor-only).</summary>
        private static TField ReadField<TField>(object target, string fieldName)
        {
            var field = target.GetType().GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(field, target.GetType().Name + "." + fieldName + " field not found.");
            return (TField)field.GetValue(target);
        }
    }
}
