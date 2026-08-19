using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PerspectivePuzzle.Presentation;

namespace PerspectivePuzzle.PlayMode.Tests
{
    /// <summary>
    /// T-022D curriculum coverage: the first gallery intentionally exposes
    /// only Pavilion and Arch Bridge with depth disabled, rotation enabled, and locked scenery
    /// colliders disabled; Moon Garden grows to four active pieces with depth
    /// enabled but rotation still disabled; Red Cliffs is the first full
    /// eight-piece scene with both depth and rotation enabled. Each scene
    /// still ships all eight scenery roots; the inactive roots simply have
    /// their selection colliders disabled and sit on the Default layer.
    /// </summary>
    public sealed class PaintingCurriculumPlayModeTests
    {
        private const string MistValleyScenePath = "Assets/Scenes/PaintingPrototype.unity";
        private const string MoonGardenScenePath = "Assets/Scenes/PaintingMoonGarden.unity";
        private const string RedCliffsScenePath = "Assets/Scenes/PaintingRedCliffs.unity";

        /// <summary>Required piece roots in evaluator/controller order.</summary>
        private static readonly string[] RequiredPieces =
        {
            "Sun", "Far Mountain", "Middle Mountain", "Tree Cluster Left",
            "Tree Cluster Right", "Pavilion", "Arch Bridge", "Foreground Rock",
        };

        [UnityTest]
        public IEnumerator MistValleyExposesTwoPiecesWithRotationAndImmediatePavilionAccess()
        {
            SceneManager.LoadScene(MistValleyScenePath, LoadSceneMode.Single);
            yield return null;

            var controller = FindController(MistValleyScenePath);
            AssertCurriculum(controller, MistValleyScenePath,
                new[] { "Pavilion", "Arch Bridge" },
                allowDepth: false, allowRotation: true, initiallyLocked: System.Array.Empty<string>());
            Assert.IsTrue(controller.UsesPlacementLattice);
            Assert.AreEqual(5, controller.LatticeColumns);
            Assert.AreEqual(3, controller.LatticeDepthRows);
            var inspection = Object.FindFirstObjectByType<PaintingInspectionCamera>();
            Assert.IsNotNull(inspection, "Mist Valley must provide a bounded high-angle board inspection view.");
            Assert.IsTrue(inspection.IsConfigured);

            var sequence = Object.FindFirstObjectByType<PaintingTutorialSequence>();
            Assert.IsNotNull(sequence, "Mist Valley must provide a real two-step tutorial gate.");
            Assert.IsTrue(sequence.IsConfigured);
            Assert.AreEqual(2, sequence.CurrentStep);
            Assert.AreEqual(PaintingTutorialSequence.AssistanceLevel.Normal, sequence.AssistanceForElapsed(0f));
            Assert.AreEqual(PaintingTutorialSequence.AssistanceLevel.Warm, sequence.AssistanceForElapsed(20f));
            Assert.AreEqual(PaintingTutorialSequence.AssistanceLevel.Rescue, sequence.AssistanceForElapsed(45f));
            var pavilion = controller.Pieces[0];
            var bridge = controller.Pieces[1];
            Assert.IsTrue(controller.IsPieceAvailable(pavilion), "Pavilion must be movable from the start.");
            Assert.IsTrue(controller.IsPieceAvailable(bridge));
            Assert.DoesNotThrow(() => controller.SelectPiece(pavilion));
            Assert.IsTrue(controller.TryRotate(15f), "Mist Valley answer requires Q/E rotation.");
            Assert.IsTrue(controller.BeginPlacement(pavilion), "Pavilion must accept direct placement from the start.");
            controller.CancelPlacement();

            yield return UnloadScene();
        }

        [UnityTest]
        public IEnumerator MoonGardenExposesFourPiecesWithDepthAndRotationEnabled()
        {
            SceneManager.LoadScene(MoonGardenScenePath, LoadSceneMode.Single);
            yield return null;

            var controller = FindController(MoonGardenScenePath);
            AssertCurriculum(controller, MoonGardenScenePath,
                new[] { "Far Mountain", "Middle Mountain", "Pavilion", "Arch Bridge" },
                allowDepth: true, allowRotation: true,
                initiallyLocked: System.Array.Empty<string>());
            Assert.AreEqual(5, controller.LatticeColumns);
            Assert.AreEqual(3, controller.LatticeDepthRows);

            var evaluator = Object.FindFirstObjectByType<PaintingCompositionEvaluator>();
            Assert.IsNotNull(evaluator, "Moon Garden evaluator is missing.");
            Assert.That(evaluator.Policy.PassThreshold, Is.EqualTo(0.82f).Within(0.001f),
                "Moon Garden must accept a visually convincing overall composition.");
            Assert.That(evaluator.Policy.MinimumCoverageThreshold, Is.EqualTo(0.40f).Within(0.001f),
                "Moon Garden must not deadlock behind a nearly-full bar because of plausible mountain occlusion.");
            var depthTutorial = Object.FindFirstObjectByType<PaintingDepthTutorialSequence>();
            Assert.IsNotNull(depthTutorial, "Moon Garden must provide a staged depth curriculum.");
            Assert.IsTrue(depthTutorial.IsConfigured);
            Assert.AreEqual(1, depthTutorial.CurrentStep);
            Assert.IsTrue(controller.IsPieceAvailable(controller.Bridge));
            Assert.IsTrue(controller.IsPieceAvailable(controller.Pieces[0]));
            Assert.IsTrue(controller.IsPieceAvailable(controller.Pieces[1]));
            Assert.IsTrue(controller.IsPieceAvailable(controller.Pieces[2]),
                "The pavilion must never appear through a surprise unlock.");
            controller.SelectPiece(controller.Bridge);
            Assert.IsTrue(controller.TryRotate(15f),
                "Moon Garden answer offsets require Q/E rotation to be available.");

            yield return UnloadScene();
        }

        [UnityTest]
        public IEnumerator MoonGardenKeepsAllPiecesAvailableWhileGuidanceAdvances()
        {
            SceneManager.LoadScene(MoonGardenScenePath, LoadSceneMode.Single);
            yield return null;

            var controller = FindController(MoonGardenScenePath);
            var evaluator = Object.FindFirstObjectByType<PaintingCompositionEvaluator>();
            var tutorial = Object.FindFirstObjectByType<PaintingDepthTutorialSequence>();
            var reveal = Object.FindFirstObjectByType<PaintingCompletionReveal>();
            Assert.IsNotNull(evaluator);
            Assert.IsNotNull(tutorial);
            Assert.IsNotNull(reveal);
            controller.SetInputLocked(false);

            Assert.IsTrue(controller.IsPieceAvailable(controller.Pieces[0]));
            Assert.IsTrue(controller.IsPieceAvailable(controller.Pieces[1]));
            Assert.IsTrue(controller.IsPieceAvailable(controller.Pieces[2]));

            Assert.IsTrue(controller.AssistPlace(controller.Bridge));
            yield return WaitForDepthStep(tutorial, evaluator, 2);

            Assert.IsTrue(controller.AssistPlace(controller.Pieces[0]));
            Assert.IsTrue(controller.AssistPlace(controller.Pieces[1]));
            Assert.IsTrue(controller.IsPieceAvailable(controller.Pieces[2]));
            var pavilion = controller.Pieces[2];
            Assert.DoesNotThrow(() => controller.SelectPiece(pavilion));
            Assert.IsTrue(controller.TryRotate(15f),
                "The pavilion must accept Q/E rotation from the start.");
            Assert.IsTrue(controller.TryAdjustDepth(0.25f),
                "The unlocked pavilion must accept depth adjustment.");
            Assert.IsTrue(controller.Undo());
            Assert.IsTrue(controller.BeginPlacement(pavilion),
                "The pavilion must remain movable while guidance advances.");
            controller.CancelPlacement();
            yield return new WaitForSecondsRealtime(1f);
            Assert.AreEqual(PaintingCompletionReveal.RevealPhase.Idle, reveal.Phase,
                "Overall score cannot complete Moon Garden before the pavilion lesson.");
            Assert.IsFalse(controller.InputLocked);

            Assert.IsTrue(controller.AssistPlace(controller.Pieces[2]));
            yield return WaitForDepthCompletion(tutorial, evaluator);
            Assert.IsTrue(tutorial.CompletionReady);

            yield return UnloadScene();
        }

        [UnityTest]
        public IEnumerator RedCliffsExposesEightPiecesWithDepthAndRotationEnabled()
        {
            SceneManager.LoadScene(RedCliffsScenePath, LoadSceneMode.Single);
            yield return null;

            var controller = FindController(RedCliffsScenePath);
            AssertCurriculum(controller, RedCliffsScenePath, RequiredPieces,
                allowDepth: true, allowRotation: true);
            Assert.AreEqual(7, controller.LatticeColumns);
            Assert.AreEqual(4, controller.LatticeDepthRows);

            yield return UnloadScene();
        }

        /// <summary>
        /// Verifies one scene's curriculum: the ordered active handle set, the
        /// depth/rotation ability flags, and that every piece root's selection
        /// collider and layer match whether the piece is active or locked.
        /// </summary>
        private static void AssertCurriculum(
            PaintingManipulationController controller,
            string scenePath,
            string[] activeNames,
            bool allowDepth,
            bool allowRotation,
            string[] initiallyLocked = null)
        {
            Assert.IsTrue(controller.IsConfigured,
                "PaintingManipulationController must self-configure in " + scenePath + ".");
            Assert.AreEqual(allowDepth, controller.AllowsDepthAdjustment,
                "Depth adjustment ability must match the curriculum in " + scenePath + ".");
            Assert.AreEqual(allowRotation, controller.AllowsRotation,
                "Rotation ability must match the curriculum in " + scenePath + ".");
            Assert.AreEqual(activeNames.Length, controller.Pieces.Count,
                scenePath + " must expose exactly " + activeNames.Length + " active pieces.");

            for (int i = 0; i < activeNames.Length; i++)
                Assert.AreEqual(activeNames[i], controller.Pieces[i].Root.name,
                    "Active piece " + i + " in " + scenePath + " must be " + activeNames[i] + ".");

            var active = new HashSet<string>(activeNames);
            int paintingPieceLayer = LayerMask.NameToLayer("PaintingPiece");
            Assert.AreNotEqual(-1, paintingPieceLayer,
                "The project must define a 'PaintingPiece' layer.");

            var scenery = GameObject.Find("Solved Scenery");
            Assert.IsNotNull(scenery, "Solved Scenery missing from " + scenePath + ".");
            Assert.AreEqual(RequiredPieces.Length, scenery.transform.childCount,
                scenePath + " must keep all " + RequiredPieces.Length + " piece roots.");

            for (int i = 0; i < RequiredPieces.Length; i++)
            {
                var root = scenery.transform.Find(RequiredPieces[i]);
                Assert.IsNotNull(root, "Solved Scenery/" + RequiredPieces[i] + " missing from " + scenePath + ".");
                var handle = root.GetComponent<PaintingManipulablePiece>();
                Assert.IsNotNull(handle,
                    RequiredPieces[i] + " must carry a PaintingManipulablePiece in " + scenePath + ".");

                bool isActive = active.Contains(RequiredPieces[i]);
                bool lockedInitially = initiallyLocked != null
                    && System.Array.IndexOf(initiallyLocked, RequiredPieces[i]) >= 0;
                bool initiallyAvailable = isActive && !lockedInitially;
                Assert.AreEqual(initiallyAvailable, handle.SelectionCollider.enabled,
                    RequiredPieces[i] + " selection collider must be " + (initiallyAvailable ? "enabled" : "disabled")
                    + " in " + scenePath + ".");
                Assert.AreEqual(isActive ? paintingPieceLayer : 0, root.gameObject.layer,
                    RequiredPieces[i] + " must sit on the " + (isActive ? "'PaintingPiece'" : "Default")
                    + " layer in " + scenePath + ".");
            }
        }

        private static PaintingManipulationController FindController(string scenePath)
        {
            var controller = Object.FindFirstObjectByType<PaintingManipulationController>();
            Assert.IsNotNull(controller, "PaintingManipulationController missing from " + scenePath + ".");
            return controller;
        }

        private static IEnumerator UnloadScene()
        {
            var current = SceneManager.GetActiveScene();
            var cleanup = SceneManager.CreateScene("T022DCurriculumCleanup");
            SceneManager.SetActiveScene(cleanup);
            yield return SceneManager.UnloadSceneAsync(current);
        }

        private static IEnumerator WaitForDepthStep(PaintingDepthTutorialSequence tutorial,
            PaintingCompositionEvaluator evaluator, int step)
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (tutorial.CurrentStep != step)
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, "Depth tutorial step timed out.");
                evaluator.RequestEvaluationNow();
                yield return null;
            }
        }

        private static IEnumerator WaitForDepthCompletion(PaintingDepthTutorialSequence tutorial,
            PaintingCompositionEvaluator evaluator)
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!tutorial.CompletionReady)
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, "Depth tutorial completion timed out.");
                evaluator.RequestEvaluationNow();
                yield return null;
            }
        }
    }
}
