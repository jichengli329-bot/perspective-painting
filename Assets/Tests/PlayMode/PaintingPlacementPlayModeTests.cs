using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PerspectivePuzzle.Presentation;

namespace PerspectivePuzzle.PlayMode.Tests
{
    /// <summary>Regression coverage for the physical pick-preview-place transaction.</summary>
    public sealed class PaintingPlacementPlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/PaintingRedCliffs.unity";

        [UnityTest]
        public IEnumerator PickupCreatesNonPhysicalPreviewAndOutOfBoundsDragClampsToTray()
        {
            SceneManager.LoadScene(ScenePath, LoadSceneMode.Single);
            yield return null;

            PaintingManipulationController controller = FindController();
            PaintingManipulablePiece piece = controller.Pieces[0];
            Vector3 startPosition = piece.Root.position;
            Quaternion startRotation = piece.Root.rotation;
            Vector3 startScale = piece.Root.localScale;

            Assert.IsTrue(controller.IsPlacementConfigured);
            Assert.IsTrue(controller.BeginPlacement(piece));
            Assert.IsTrue(controller.IsCarrying);
            var preview = Object.FindFirstObjectByType<PaintingPlacementPreview>();
            Assert.IsNotNull(preview, "Pickup must create a visible landing preview.");
            Assert.IsTrue(preview.IsVisible);
            Assert.AreEqual(0, preview.GetComponentsInChildren<Collider>(true).Length,
                "The landing preview must never participate in collision.");
            Assert.AreEqual(0, preview.GetComponentsInChildren<Rigidbody>(true).Length,
                "The landing preview must never participate in physics.");

            controller.UpdatePlacementTarget(new Vector3(100f, 0f, 100f));
            Assert.IsTrue(controller.IsPlacementCandidateValid,
                "Pointer overshoot must clamp to the nearest landable tray position.");
            Assert.IsTrue(preview.IsValid, "A clamped candidate must keep the landing preview valid.");
            Vector3 clamped = controller.PlacementCandidate;
            Assert.Less(clamped.x, 100f);
            Assert.Less(clamped.z, 100f);
            Assert.IsTrue(controller.ReleasePlacement(), "A clamped release must land instead of snapping back.");
            yield return WaitUntilIdle(controller);

            Assert.AreEqual(clamped.x, piece.Root.position.x, 0.0001f);
            Assert.AreEqual(clamped.z, piece.Root.position.z, 0.0001f);
            Assert.AreEqual(startRotation, piece.Root.rotation);
            Assert.AreEqual(startScale, piece.Root.localScale);
            Assert.IsTrue(controller.CanUndo);
        }

        [UnityTest]
        public IEnumerator ValidDropSettlesOnSurfaceAndUndoRestoresPickupPose()
        {
            SceneManager.LoadScene(ScenePath, LoadSceneMode.Single);
            yield return null;

            PaintingManipulationController controller = FindController();
            PaintingManipulablePiece piece = controller.Pieces[0];
            Vector3 startPosition = piece.Root.position;
            Quaternion startRotation = piece.Root.rotation;
            Vector3 startScale = piece.Root.localScale;

            Assert.IsTrue(controller.BeginPlacement(piece));
            Assert.IsTrue(TryFindValidCandidate(controller, out Vector3 candidate),
                "At least one non-overlapping tabletop landing must be available for the sun ornament.");
            controller.UpdatePlacementTarget(candidate);
            Assert.IsTrue(controller.IsPlacementCandidateValid);
            candidate = controller.PlacementCandidate;
            Assert.IsTrue(controller.ReleasePlacement());
            Assert.IsTrue(controller.IsSettling);
            yield return WaitUntilIdle(controller);

            Assert.AreEqual(candidate.x, piece.Root.position.x, 0.0001f);
            Assert.AreEqual(controller.SurfaceY, piece.Root.position.y, 0.0001f);
            Assert.AreEqual(candidate.z, piece.Root.position.z, 0.0001f);
            Assert.AreEqual(startRotation, piece.Root.rotation);
            Assert.AreEqual(startScale, piece.Root.localScale);
            Assert.IsTrue(controller.CanUndo);

            Assert.IsTrue(controller.Undo());
            Assert.AreEqual(startPosition, piece.Root.position);
            Assert.AreEqual(startRotation, piece.Root.rotation);
            Assert.AreEqual(startScale, piece.Root.localScale);
        }

        [UnityTest]
        public IEnumerator TwoPiecesMayShareCompositionAreaForOcclusion()
        {
            SceneManager.LoadScene(ScenePath, LoadSceneMode.Single);
            yield return null;

            PaintingManipulationController controller = FindController();
            Vector3 sharedTarget = new Vector3(0f, controller.SurfaceY, 0f);
            PaintingManipulablePiece first = controller.Pieces[0];
            PaintingManipulablePiece second = controller.Pieces[7];

            Assert.IsTrue(controller.BeginPlacement(first));
            controller.UpdatePlacementTarget(sharedTarget);
            Vector3 firstLanding = controller.PlacementCandidate;
            Assert.IsTrue(controller.ReleasePlacement());
            yield return WaitUntilIdle(controller);

            Assert.IsTrue(controller.BeginPlacement(second));
            controller.UpdatePlacementTarget(firstLanding);
            Assert.IsTrue(controller.IsPlacementCandidateValid,
                "Scenery overlap must remain legal because occlusion creates the target image.");
            Assert.IsTrue(controller.ReleasePlacement());
            yield return WaitUntilIdle(controller);
        }

        private static bool TryFindValidCandidate(PaintingManipulationController controller, out Vector3 candidate)
        {
            Rect area = controller.PlacementRectangle;
            for (float z = area.yMin + 0.3f; z <= area.yMax - 0.3f; z += 0.3f)
            {
                for (float x = area.xMin + 0.3f; x <= area.xMax - 0.3f; x += 0.3f)
                {
                    candidate = new Vector3(x, controller.SurfaceY, z);
                    controller.UpdatePlacementTarget(candidate);
                    if (controller.IsPlacementCandidateValid)
                        return true;
                }
            }
            candidate = default;
            return false;
        }

        private static IEnumerator WaitUntilIdle(PaintingManipulationController controller)
        {
            float deadline = Time.realtimeSinceStartup + 3f;
            while (controller.IsCarrying || controller.IsSettling)
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, "Placement transaction timed out.");
                yield return null;
            }
        }

        private static PaintingManipulationController FindController()
        {
            var controller = Object.FindFirstObjectByType<PaintingManipulationController>();
            Assert.IsNotNull(controller);
            Assert.IsTrue(controller.IsConfigured);
            // These tests exercise the placement transaction in isolation;
            // the production scene now intentionally starts behind a locked
            // exhibition introduction, covered by gallery-flow tests.
            controller.SetInputLocked(false);
            return controller;
        }
    }
}
