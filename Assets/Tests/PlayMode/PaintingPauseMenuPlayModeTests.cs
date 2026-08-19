using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PerspectivePuzzle.Presentation;

namespace PerspectivePuzzle.PlayMode.Tests
{
    public sealed class PaintingPauseMenuPlayModeTests
    {
        [UnityTest]
        public IEnumerator PauseOnlyUnlocksAnActiveGallery()
        {
            SceneManager.LoadScene("Assets/Scenes/PaintingPrototype.unity", LoadSceneMode.Single);
            yield return null;

            var flow = Object.FindFirstObjectByType<PaintingLevelFlow>();
            var menu = Object.FindFirstObjectByType<PaintingPauseMenu>();
            var controller = Object.FindFirstObjectByType<PaintingManipulationController>();
            Assert.IsNotNull(flow);
            Assert.IsNotNull(menu);
            Assert.IsNotNull(controller);

            menu.SetPaused(true);
            Assert.IsFalse(menu.IsPaused, "The opening screen must remain the input owner.");
            Assert.IsTrue(controller.InputLocked);

            flow.ConfigureForTests();
            flow.SetTestConfirm(true);
            yield return null;
            flow.SetTestConfirm(false);
            Assert.IsTrue(flow.CanManipulate);

            menu.SetPaused(true);
            Assert.IsTrue(menu.IsPaused);
            Assert.IsTrue(controller.InputLocked);
            menu.SetPaused(false);
            Assert.IsFalse(menu.IsPaused);
            Assert.IsFalse(controller.InputLocked);
        }
    }
}
