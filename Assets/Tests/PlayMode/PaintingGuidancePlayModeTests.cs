using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PerspectivePuzzle.Presentation;

namespace PerspectivePuzzle.PlayMode.Tests
{
    public sealed class PaintingGuidancePlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/PaintingPrototype.unity";

        [UnityTest]
        public IEnumerator GuidanceShowsTargetLiveViewAndRespondsWithoutMachineUi()
        {
            SceneManager.LoadScene(ScenePath, LoadSceneMode.Single);
            yield return null;

            var presenter = Object.FindFirstObjectByType<PaintingGuidancePresenter>();
            var evaluator = Object.FindFirstObjectByType<PaintingCompositionEvaluator>();
            var controller = Object.FindFirstObjectByType<PaintingManipulationController>();
            Assert.IsNotNull(presenter);
            Assert.IsNotNull(evaluator);
            Assert.IsNotNull(controller);
            Assert.IsTrue(presenter.IsConfigured);
            Assert.IsNotNull(presenter.TargetTexture, "The target painting must be visible.");
            Assert.IsNotNull(presenter.LiveTexture, "The Composition Camera must feed a live beauty view.");
            Assert.IsTrue(presenter.LiveTexture.IsCreated());
            Assert.AreSame(presenter.LiveTexture, GameObject.Find("Composition Camera").GetComponent<Camera>().targetTexture);

            yield return WaitForEvaluation(evaluator);
            Assert.IsFalse(string.IsNullOrWhiteSpace(presenter.Status));
            Assert.IsFalse(string.IsNullOrWhiteSpace(presenter.Focus));
            Assert.IsFalse(presenter.Status.Contains("%") || presenter.Focus.Contains("%"),
                "Guidance must not expose a raw percentage.");
            Assert.That(presenter.WorstPieceIndex, Is.InRange(0, controller.Pieces.Count - 1));

            for (int i = 0; i < controller.Pieces.Count; i++)
            {
                controller.SelectPiece(controller.Pieces[i]);
                controller.ResetToAuthored();
            }
            yield return WaitForEvaluation(evaluator, true);
            Assert.AreEqual("Painting aligned", presenter.Status);
            Assert.AreEqual("Hold the composition", presenter.Focus);
        }

        private static IEnumerator WaitForEvaluation(PaintingCompositionEvaluator evaluator, bool requirePass = false)
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (evaluator.LatestResult == null || (requirePass && !evaluator.LatestResult.PassesPolicy))
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, "Guidance evaluation timed out.");
                evaluator.RequestEvaluationNow();
                yield return null;
            }
            yield return null; // allow the presenter event and smoothed UI update to run
        }
    }
}
