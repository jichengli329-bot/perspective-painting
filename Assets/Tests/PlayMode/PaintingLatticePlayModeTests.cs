using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PerspectivePuzzle.Presentation;

namespace PerspectivePuzzle.PlayMode.Tests
{
    public sealed class PaintingLatticePlayModeTests
    {
        [UnityTest]
        public IEnumerator TutorialStrongSnapLandsAtAuthoredCellAndShowsGuides()
        {
            SceneManager.LoadScene("Assets/Scenes/PaintingPrototype.unity", LoadSceneMode.Single);
            yield return null;
            var controller = Object.FindFirstObjectByType<PaintingManipulationController>();
            var view = Object.FindFirstObjectByType<PaintingPlacementLatticeView>();
            controller.SetInputLocked(false);
            PaintingManipulablePiece bridge = controller.Bridge;

            controller.SelectPiece(bridge);
            yield return null;
            Assert.AreEqual(0, view.transform.childCount,
                "Selection at rest must keep the lake visually clean.");

            Assert.IsTrue(controller.BeginPlacement(bridge));
            yield return null;
            Assert.Greater(view.transform.childCount, 0,
                "Picking up a piece must reveal the transient placement lattice.");
            controller.UpdatePlacementTarget(bridge.AuthoredPosition + new Vector3(0.35f, 0f, 0.30f));
            Assert.That(Vector3.Distance(controller.PlacementCandidate,
                new Vector3(bridge.AuthoredPosition.x, controller.SurfaceY, bridge.AuthoredPosition.z)),
                Is.LessThan(0.001f), "Tutorial magnet radius should forgive a visibly close release.");
            Assert.IsTrue(controller.ReleasePlacement());
            while (controller.IsSettling) yield return null;
            Assert.That(Vector3.Distance(bridge.Root.position,
                new Vector3(bridge.AuthoredPosition.x, controller.SurfaceY, bridge.AuthoredPosition.z)),
                Is.LessThan(0.001f));
        }

        [UnityTest]
        public IEnumerator LaterGalleryWheelMovesExactlyOneDepthInterval()
        {
            SceneManager.LoadScene("Assets/Scenes/PaintingMoonGarden.unity", LoadSceneMode.Single);
            yield return null;
            var controller = Object.FindFirstObjectByType<PaintingManipulationController>();
            controller.SetInputLocked(false);
            controller.SelectPiece(controller.Bridge);
            float before = controller.Bridge.Root.position.z;
            var beforeBand = controller.SelectedDepthBand;
            bool moved = controller.TryAdjustDepth(-100f);
            if (!moved) moved = controller.TryAdjustDepth(100f);
            Assert.IsTrue(moved, "At least one adjacent depth row must remain reachable.");
            float offsetInRows = (controller.Bridge.Root.position.z - controller.Bridge.AuthoredPosition.z)
                / controller.LatticeDepthSpacing;
            Assert.AreEqual(Mathf.Round(offsetInRows), offsetInRows, 0.001f,
                "The first wheel action from an authored unsolved pose must land exactly on a depth row.");
            Assert.Greater(Mathf.Abs(controller.Bridge.Root.position.z - before), 0.001f);
            Assert.AreNotEqual(beforeBand, controller.SelectedDepthBand,
                "A discrete depth move must produce a readable depth-band change.");
        }
    }
}
