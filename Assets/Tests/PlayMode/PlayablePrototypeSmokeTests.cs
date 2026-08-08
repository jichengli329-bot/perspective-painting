using System.Collections;
using System.Collections.Generic;
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
    /// T-005 smoke test: loads the playable prototype scene and verifies the
    /// whole playable wiring end to end. The source's Awake builds the session
    /// and mapper, the controller's Start initializes the projection board, all
    /// 25 layer-0 cell centers project vertically onto the placement surface
    /// collider, the seven serialized target cells are placed through the
    /// session at Z = 0, the projection locks on an exact match, the controller
    /// raises its reveal signal and the reveal camera moves to the reveal
    /// position, and the scene is cleaned back up.
    /// </summary>
    public class PlayablePrototypeSmokeTests
    {
        private const string PlayablePrototypeScenePath = "Assets/Scenes/PlayablePrototype.unity";
        private const int GridWidth = PuzzleSession.GridWidth;
        private const int GridHeight = PuzzleSession.GridHeight;
        private const int GridDepth = PuzzleSession.GridDepth;
        private const int PlacementSurfaceLayer = 3; // "PlacementSurface", set by the scene builder
        private const int PlacementSurfaceMask = 1 << PlacementSurfaceLayer;
        private const float SurfaceRayDistance = 5f;
        private const int ExpectedTargetCellCount = 7;
        private const float RevealWaitSeconds = 2f;
        private const float CameraMoveWaitSeconds = 4f;
        private const float CameraArrivalTolerance = 0.05f;

        [UnityTest]
        public IEnumerator PlayablePrototypeSceneSmokeTest()
        {
            // Load the scene; Awake of every component runs synchronously on load.
            SceneManager.LoadScene(PlayablePrototypeScenePath, LoadSceneMode.Single);
            yield return null;

            var source = UnityEngine.Object.FindFirstObjectByType<PuzzleSessionSource>();
            Assert.IsNotNull(source, "PlayablePrototype: PuzzleSessionSource missing after scene load.");
            Assert.IsNotNull(source.Session, "PlayablePrototype: PuzzleSessionSource.Session not created by Awake.");
            Assert.IsNotNull(source.Mapper, "PlayablePrototype: PuzzleSessionSource.Mapper not created by Awake.");
            Assert.AreEqual(GridWidth, source.Session.Grid.Width, "Session grid width is not " + GridWidth + ".");
            Assert.AreEqual(GridHeight, source.Session.Grid.Height, "Session grid height is not " + GridHeight + ".");
            Assert.AreEqual(GridDepth, source.Session.Grid.Depth, "Session grid depth is not " + GridDepth + ".");

            var controller = UnityEngine.Object.FindFirstObjectByType<PuzzleInputController>();
            var board = UnityEngine.Object.FindFirstObjectByType<ProjectionBoardView>();
            var reveal = UnityEngine.Object.FindFirstObjectByType<MatchReveal>();
            Assert.IsNotNull(controller, "PlayablePrototype: PuzzleInputController missing.");
            Assert.IsNotNull(board, "PlayablePrototype: ProjectionBoardView missing.");
            Assert.IsNotNull(reveal, "PlayablePrototype: MatchReveal missing.");
            Assert.IsNotNull(Camera.main, "PlayablePrototype: main camera missing.");

            // The scene builder wires these serialized references; assert the
            // controller and reveal are fully connected. The fields are private
            // [SerializeField] members, so they are read with reflection, which
            // works in runtime PlayMode where SerializedObject does not.
            Assert.IsNotNull(ReadField<Camera>(controller, "pointerCamera"),
                "PlayablePrototype: input controller has no pointer camera.");
            Assert.IsNotNull(ReadField<ProjectionBoardView>(controller, "board"),
                "PlayablePrototype: input controller has no projection board.");
            Assert.AreEqual(PlacementSurfaceMask, (int)ReadField<LayerMask>(controller, "surfaceMask"),
                "PlayablePrototype: input controller surface raycast mask is not the placement surface layer.");

            Assert.IsNotNull(ReadField<PuzzleInputController>(reveal, "controller"),
                "PlayablePrototype: MatchReveal has no input controller.");
            Assert.IsNotNull(ReadField<Camera>(reveal, "targetCamera"),
                "PlayablePrototype: MatchReveal has no target camera.");
            Assert.IsNotNull(ReadField<Transform>(reveal, "boardTarget"),
                "PlayablePrototype: MatchReveal has no board target.");

            // The controller's Start refreshes the board; wait until it ran.
            int frames = 0;
            while (frames < 30 && (board.Width != GridWidth || board.Height != GridHeight))
            {
                yield return null;
                frames++;
            }
            Assert.AreEqual(GridWidth, board.Width, "Projection board not initialized to 5 columns.");
            Assert.AreEqual(GridHeight, board.Height, "Projection board not initialized to 5 rows.");
            Assert.IsNotNull(board.Target, "Projection board has no target after initialization.");

            // Every layer-0 cell center must project vertically (world Y down)
            // onto the placement surface collider, and the surface point must
            // snap back to the very cell it projects from.
            for (int x = 0; x < GridWidth; x++)
            {
                for (int y = 0; y < GridHeight; y++)
                {
                    var expected = new GridCoordinate(x, y, 0);
                    var center = source.Mapper.WorldFromCell(expected);
                    bool hit = Physics.Raycast(
                        center, Vector3.down, out RaycastHit surfaceHit, SurfaceRayDistance, PlacementSurfaceMask);
                    Assert.IsTrue(hit, "No placement-surface collider below layer-0 center of " + expected + ".");
                    Assert.AreEqual(PlacementSurfaceLayer, surfaceHit.collider.gameObject.layer,
                        "Surface hit at " + expected + " is not on the placement surface layer.");
                    Assert.IsTrue(source.Mapper.TryCellFromWorld(surfaceHit.point, out GridCoordinate snapped),
                        "Surface point below " + expected + " does not map to a grid cell.");
                    Assert.AreEqual(expected, snapped,
                        "Surface point below " + expected + " snapped to " + snapped + " instead.");
                }
            }

            // Read the seven serialized target cells the scene builder wrote.
            var targetCells = ReadTargetCells(source);
            Assert.AreEqual(ExpectedTargetCellCount, targetCells.Length,
                "PlayablePrototype must serialize exactly " + ExpectedTargetCellCount + " target cells.");

            // Solve the puzzle through the session at Z = 0, the layer the
            // target pattern is drawn on.
            bool revealed = false;
            controller.Revealed += () => revealed = true;
            foreach (var cell in targetCells)
            {
                var placed = source.Session.TryPlace(new GridCoordinate(cell.x, cell.y, 0));
                Assert.IsTrue(placed, "TryPlace rejected target cell (" + cell.x + ", " + cell.y + ", 0).");
            }

            Assert.IsTrue(source.Session.Comparison.IsExactMatch,
                "Projection does not exactly match the target after placing all seven cells.");
            Assert.IsTrue(source.Session.IsLocked, "Session not locked on exact match.");
            Assert.AreEqual(targetCells.Length, source.Session.CurrentProjection.OccupiedCount,
                "Current projection does not contain exactly the seven placed cells.");

            // The controller raises its reveal signal on the frame after the
            // lock; wait for it with a timeout.
            float revealDeadline = Time.realtimeSinceStartup + RevealWaitSeconds;
            while (!revealed && Time.realtimeSinceStartup < revealDeadline)
                yield return null;
            Assert.IsTrue(revealed, "PuzzleInputController.Revealed not raised after the exact match.");

            // MatchReveal eases the camera to the serialized reveal position;
            // wait until it arrives.
            var camera = ReadField<Camera>(reveal, "targetCamera");
            var revealPosition = ReadField<Vector3>(reveal, "revealPosition");
            float cameraDeadline = Time.realtimeSinceStartup + CameraMoveWaitSeconds;
            while (Vector3.Distance(camera.transform.position, revealPosition) > CameraArrivalTolerance
                   && Time.realtimeSinceStartup < cameraDeadline)
                yield return null;
            Assert.Less(Vector3.Distance(camera.transform.position, revealPosition), CameraArrivalTolerance,
                "Reveal camera never reached the reveal position.");

            // Clean up: swap the prototype scene for a fresh empty one so no
            // playable-scene objects leak into later tests or the editor session.
            var prototypeScene = SceneManager.GetActiveScene();
            var cleanupScene = SceneManager.CreateScene("T005Cleanup");
            SceneManager.SetActiveScene(cleanupScene);
            yield return SceneManager.UnloadSceneAsync(prototypeScene);
        }

        /// <summary>
        /// T-006 reset test: solves the puzzle through the controller's own
        /// action path, waits for the reveal, then presses the R-reset path
        /// (ResetPuzzle) and verifies that the session grid/history/lock, piece
        /// views, projection board, reveal camera, layer and input all return to
        /// their initial state without any scene or application reload.
        /// </summary>
        [UnityTest]
        public IEnumerator ResetRestoresSessionPiecesBoardCameraAndInput()
        {
            SceneManager.LoadScene(PlayablePrototypeScenePath, LoadSceneMode.Single);
            yield return null;

            var source = UnityEngine.Object.FindFirstObjectByType<PuzzleSessionSource>();
            var controller = UnityEngine.Object.FindFirstObjectByType<PuzzleInputController>();
            var reveal = UnityEngine.Object.FindFirstObjectByType<MatchReveal>();
            var board = UnityEngine.Object.FindFirstObjectByType<ProjectionBoardView>();
            Assert.IsNotNull(controller, "PlayablePrototype: PuzzleInputController missing for reset test.");
            Assert.IsNotNull(source, "PlayablePrototype: PuzzleSessionSource missing for reset test.");
            Assert.IsNotNull(reveal, "PlayablePrototype: MatchReveal missing for reset test.");

            var camera = ReadField<Camera>(reveal, "targetCamera");
            var revealPosition = ReadField<Vector3>(reveal, "revealPosition");
            var initialPosition = camera.transform.position;
            var initialRotation = camera.transform.rotation;
            var initialOrthographicSize = camera.orthographicSize;

            bool revealed = false;
            controller.Revealed += () => revealed = true;

            // Solve the whole puzzle through the controller's own action path.
            foreach (var cell in ReadTargetCells(source))
            {
                Assert.IsTrue(controller.PlaceAt(new GridCoordinate(cell.x, cell.y, 0)),
                    "Controller refused target cell (" + cell.x + ", " + cell.y + ", 0).");
                yield return null;
            }
            Assert.IsTrue(source.Session.IsLocked, "Session not locked after solving through the controller.");
            Assert.AreEqual(ExpectedTargetCellCount, controller.PieceViewCount,
                "Piece view count does not match the placed cells after solving.");

            float revealDeadline = Time.realtimeSinceStartup + RevealWaitSeconds;
            while (!revealed && Time.realtimeSinceStartup < revealDeadline)
                yield return null;
            Assert.IsTrue(revealed, "Controller.Revealed not raised before reset.");

            float cameraDeadline = Time.realtimeSinceStartup + CameraMoveWaitSeconds;
            while (Vector3.Distance(camera.transform.position, revealPosition) > CameraArrivalTolerance
                   && Time.realtimeSinceStartup < cameraDeadline)
                yield return null;
            Assert.Less(Vector3.Distance(camera.transform.position, revealPosition), CameraArrivalTolerance,
                "Reveal camera never reached the reveal position before reset.");

            // R reset: session, pieces, board, camera, layer and input.
            controller.ResetPuzzle();
            yield return null;
            yield return null;

            Assert.IsFalse(source.Session.IsLocked, "Reset must unlock the session.");
            Assert.AreEqual(0, source.Session.Grid.OccupiedCount, "Reset must empty the domain grid.");
            Assert.IsFalse(source.Session.History.CanUndo, "Reset must clear undo history.");
            Assert.AreEqual(0, controller.PieceViewCount, "Reset must remove every piece view.");
            Assert.AreEqual(0, CountPieceViewsUnderRoot(), "Reset must leave no orphaned piece views.");
            Assert.AreEqual(0, controller.ActiveLayerZ, "Reset must return to layer 1 (grid Z 0).");
            Assert.AreEqual(initialOrthographicSize, camera.orthographicSize, "Reset must restore camera size.");
            Assert.Less(Vector3.Distance(camera.transform.position, initialPosition), CameraArrivalTolerance,
                "Reset must restore the camera position.");
            Assert.AreEqual(initialRotation, camera.transform.rotation, "Reset must restore the camera rotation.");

            // The board repaints its initial target-only state.
            Assert.AreEqual(GridWidth, board.Width, "Board not refreshed after reset.");
            Assert.AreEqual(GridHeight, board.Height, "Board not refreshed after reset.");
            foreach (var cell in ReadTargetCells(source))
                Assert.AreEqual(ProjectionCellState.Missing, board.StateAt(cell.x, cell.y),
                    "Reset must repaint target cell (" + cell.x + ", " + cell.y + ") as Missing.");

            // Input works from the initial state again.
            Assert.IsTrue(controller.PlaceAt(new GridCoordinate(0, 0, 0)), "Reset must allow placing again.");
            Assert.IsTrue(controller.RemoveAt(new GridCoordinate(0, 0, 0)), "Reset must allow removing again.");
            Assert.AreEqual(0, source.Session.Grid.OccupiedCount, "Grid not empty after place/remove following reset.");
            float settleDeadline = Time.realtimeSinceStartup + 1f;
            while (controller.PieceViewCount != 0 && Time.realtimeSinceStartup < settleDeadline)
                yield return null;
            Assert.AreEqual(0, controller.PieceViewCount, "Piece view did not settle after reset place/remove.");

            yield return UnloadPlayableScene();
        }

        /// <summary>
        /// T-006 rapid-input test: hammers place/remove (and reinstate-during-
        /// removal) on the same cell and across distinct cells while the direct
        /// coroutine animations are still running, then asserts the domain grid
        /// and the piece view collection stay consistent: no duplicates, no
        /// orphans, and every live view maps one-to-one to an occupied cell.
        /// </summary>
        [UnityTest]
        public IEnumerator RapidPlaceRemoveSynchronizesPieceViews()
        {
            SceneManager.LoadScene(PlayablePrototypeScenePath, LoadSceneMode.Single);
            yield return null;

            var source = UnityEngine.Object.FindFirstObjectByType<PuzzleSessionSource>();
            var controller = UnityEngine.Object.FindFirstObjectByType<PuzzleInputController>();
            Assert.IsNotNull(controller, "PlayablePrototype: PuzzleInputController missing for rapid-input test.");
            Assert.IsNotNull(source, "PlayablePrototype: PuzzleSessionSource missing for rapid-input test.");

            // Rapid place/remove cycles on one cell: exactly one live view per
            // cell at every frame, including mid-animation frames.
            var cell = new GridCoordinate(2, 2, 0);
            for (int cycle = 0; cycle < 5; cycle++)
            {
                Assert.IsTrue(controller.PlaceAt(cell), "Cycle " + cycle + ": place refused.");
                Assert.AreEqual(1, controller.PieceViewCount,
                    "Cycle " + cycle + ": duplicate piece view after place.");
                Assert.AreEqual(1, CountPieceViewsUnderRoot(),
                    "Cycle " + cycle + ": orphaned piece views after place.");
                yield return null;

                Assert.IsTrue(controller.RemoveAt(cell), "Cycle " + cycle + ": remove refused.");
                yield return null;
            }
            Assert.AreEqual(0, source.Session.Grid.OccupiedCount, "Grid not empty after rapid cycles.");

            // Remove and immediately re-place the same cell while the removal
            // animation is still running: the animating-out view is reinstated,
            // never duplicated.
            Assert.IsTrue(controller.PlaceAt(cell), "Reinstate setup place refused.");
            Assert.IsTrue(controller.RemoveAt(cell), "Reinstate setup remove refused.");
            Assert.IsTrue(controller.PlaceAt(cell), "Reinstating placement refused.");
            Assert.AreEqual(1, controller.PieceViewCount, "Reinstate duplicated the piece view.");
            Assert.AreEqual(1, CountPieceViewsUnderRoot(), "Reinstate left orphaned views.");
            yield return null;
            Assert.AreEqual(1, CountPieceViewsUnderRoot(), "Reinstate left orphaned views a frame later.");

            Assert.IsTrue(controller.RemoveAt(cell), "Final remove refused.");
            float settleDeadline = Time.realtimeSinceStartup + 2f;
            while ((controller.PieceViewCount != 0 || CountPieceViewsUnderRoot() != 0)
                   && Time.realtimeSinceStartup < settleDeadline)
                yield return null;
            Assert.AreEqual(0, controller.PieceViewCount, "Piece views did not settle to zero.");

            // Rapid alternation across distinct cells with animations running,
            // then settle and verify a strict one-view-per-cell invariant.
            for (int i = 0; i < 6; i++)
            {
                var alternate = new GridCoordinate(i % 3, 3 + (i % 2), 0);
                Assert.IsTrue(controller.PlaceAt(alternate), "Alternating place refused at " + alternate + ".");
                yield return null;
            }
            Assert.AreEqual(6, controller.PieceViewCount, "Alternating places did not produce six views.");
            for (int i = 0; i < 6; i++)
            {
                var alternate = new GridCoordinate(i % 3, 3 + (i % 2), 0);
                Assert.IsTrue(controller.RemoveAt(alternate), "Alternating remove refused at " + alternate + ".");
                yield return null;
            }
            settleDeadline = Time.realtimeSinceStartup + 2f;
            while ((controller.PieceViewCount != 0 || CountPieceViewsUnderRoot() != 0)
                   && Time.realtimeSinceStartup < settleDeadline)
                yield return null;
            Assert.AreEqual(0, controller.PieceViewCount, "Piece views did not settle after alternation.");
            Assert.AreEqual(0, CountPieceViewsUnderRoot(), "Orphaned views remained after settling.");
            Assert.AreEqual(0, source.Session.Grid.OccupiedCount, "Domain grid not empty after settling.");

            var seen = new HashSet<GridCoordinate>();
            foreach (var view in CountPieceViewsUnderRoot(GameObject.Find("Pieces")))
                Assert.IsTrue(seen.Add(view.Cell), "Duplicate live view for cell " + view.Cell + ".");
            Assert.AreEqual(0, seen.Count, "Live views exist after everything settled.");

            yield return UnloadPlayableScene();
        }

        private static IEnumerator UnloadPlayableScene()
        {
            var prototypeScene = SceneManager.GetActiveScene();
            var cleanupScene = SceneManager.CreateScene("T006Cleanup");
            SceneManager.SetActiveScene(cleanupScene);
            yield return SceneManager.UnloadSceneAsync(prototypeScene);
        }

        private static int CountPieceViewsUnderRoot()
        {
            var root = GameObject.Find("Pieces");
            return root != null ? CountPieceViewsUnderRoot(root).Length : 0;
        }

        private static PieceView[] CountPieceViewsUnderRoot(GameObject root)
        {
            if (root == null)
                return new PieceView[0];
            return root.GetComponentsInChildren<PieceView>(true);
        }

        private static Vector2Int[] ReadTargetCells(PuzzleSessionSource source)
        {
            var cells = ReadField<Vector2Int[]>(source, "targetCells");
            Assert.IsNotNull(cells, "PuzzleSessionSource.targetCells field is null.");
            return cells;
        }

        /// <summary>
        /// Reads a private serialized field by name at runtime. SerializedObject
        /// is an editor-only API and is unavailable in PlayMode test runs, so the
        /// scene-builder-written fields are read via reflection instead.
        /// </summary>
        private static TField ReadField<TField>(object target, string fieldName)
        {
            var field = target.GetType().GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(field, target.GetType().Name + "." + fieldName + " field not found.");
            return (TField)field.GetValue(target);
        }
    }
}
