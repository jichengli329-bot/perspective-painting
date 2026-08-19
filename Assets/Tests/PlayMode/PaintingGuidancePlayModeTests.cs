using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PerspectivePuzzle.Presentation;

namespace PerspectivePuzzle.PlayMode.Tests
{
    public sealed class PaintingGuidancePlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/PaintingPrototype.unity";
        private const string RedCliffsScenePath = "Assets/Scenes/PaintingRedCliffs.unity";

        [UnityTest]
        public IEnumerator RedCliffsPavilionSelectionShowsTargetRegionAndVisibleOverlay()
        {
            SceneManager.LoadScene(RedCliffsScenePath, LoadSceneMode.Single);
            yield return null;

            var presenter = Object.FindFirstObjectByType<PaintingGuidancePresenter>();
            var controller = Object.FindFirstObjectByType<PaintingManipulationController>();
            Assert.IsNotNull(presenter);
            Assert.IsNotNull(controller);
            PaintingManipulablePiece pavilion = null;
            for (int i = 0; i < controller.Pieces.Count; i++)
                if (controller.Pieces[i].Root.name == "Pavilion") pavilion = controller.Pieces[i];
            Assert.IsNotNull(pavilion);
            controller.SelectPiece(pavilion);
            yield return null;

            StringAssert.Contains("已选“凉亭”：目标在", presenter.Focus,
                "The crowded third gallery must state where the selected pavilion belongs.");
            RawImage overlay = GameObject.Find("Selected Target Outline").GetComponent<RawImage>();
            Assert.IsTrue(overlay.enabled);
            Assert.IsNotNull(overlay.texture,
                "The selected pavilion must be visibly filled and outlined on the target painting.");
        }

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
            controller.SetInputLocked(false); // enter gallery before driving the public assist path
            Assert.IsTrue(presenter.IsConfigured);
            CanvasScaler scaler = presenter.GetComponent<CanvasScaler>();
            Assert.IsNotNull(scaler);
            Assert.AreEqual(CanvasScaler.ScaleMode.ScaleWithScreenSize, scaler.uiScaleMode);
            Assert.AreEqual(0.5f, scaler.matchWidthOrHeight, 0.001f,
                "The curator rail must balance width and height across 4:3, 16:9 and ultrawide displays.");
            Assert.IsNotNull(presenter.TargetTexture, "The target painting must be visible.");
            Assert.IsNotNull(presenter.LiveTexture, "The Composition Camera must feed a live beauty view.");
            Assert.IsTrue(presenter.LiveTexture.IsCreated());
            Camera liveCamera = GameObject.Find("Composition Camera").GetComponent<Camera>();
            Assert.AreSame(presenter.LiveTexture, liveCamera.targetTexture);
            Assert.IsTrue(liveCamera.enabled,
                "A RenderTexture is not a live preview unless its camera continuously renders into it.");
            var comparison = GameObject.Find("构图对照放大").GetComponent<CanvasGroup>();
            Assert.IsNotNull(comparison);
            Assert.AreEqual(0f, comparison.alpha, 0.001f);
            Assert.AreSame(presenter.TargetTexture, GameObject.Find("放大目标画面").GetComponent<RawImage>().texture);
            Assert.AreSame(presenter.LiveTexture, GameObject.Find("放大当前画面").GetComponent<RawImage>().texture);

            yield return WaitForEvaluation(evaluator);
            Assert.IsFalse(string.IsNullOrWhiteSpace(presenter.Status));
            Assert.IsFalse(string.IsNullOrWhiteSpace(presenter.Focus));
            Assert.IsFalse(presenter.Status.Contains("%") || presenter.Focus.Contains("%"),
                "Guidance must not expose a raw percentage.");
            StringAssert.Contains("入门提示", presenter.Focus,
                "The tutorial must identify the action without exposing an obsolete staged gate.");
            StringAssert.Contains("自动吸附", presenter.Focus,
                "The tutorial must explain the forgiving lattice behavior.");
            // WorstPieceIndex is an evaluator index over all eight target
            // pieces, not an index into the tutorial controller's two active
            // handles; it must also never name a locked (inactive) tutorial
            // piece, because the presenter's hint-eligibility filter skips them.
            Assert.That(presenter.WorstPieceIndex, Is.InRange(0, evaluator.Pieces.Count - 1),
                "WorstPieceIndex must index the evaluator's target pieces, not the tutorial controller's active handles.");
            var activeNames = new List<string>();
            for (int i = 0; i < controller.Pieces.Count; i++)
                activeNames.Add(controller.Pieces[i].Root.name);
            CollectionAssert.Contains(activeNames, evaluator.Pieces[presenter.WorstPieceIndex].name,
                "The worst-piece focus must never identify a locked tutorial piece.");

            controller.SelectPiece(controller.Bridge);
            yield return null;
            StringAssert.Contains("向右旋转：按 E 2 次", presenter.Focus,
                "Selected scenery must expose a concrete Q/E count instead of asking the player to guess its yaw.");

            var sequence = Object.FindFirstObjectByType<PaintingTutorialSequence>();
            PaintingManipulablePiece bridge = controller.Bridge;
            Assert.IsTrue(controller.AssistPlace(bridge));
            yield return WaitForTutorialStep(sequence, 2, evaluator);
            PaintingManipulablePiece pavilion = controller.Pieces[0];
            Assert.IsTrue(controller.AssistPlace(pavilion));
            yield return WaitForEvaluation(evaluator, true);
            Assert.AreEqual("画面已经重合", presenter.Status);
            StringAssert.StartsWith("保持这个构图", presenter.Focus);
            StringAssert.Contains("当前：中景层", presenter.Focus);
        }

        [UnityTest]
        public IEnumerator OnePieceAssistIsUndoableAndCanCompleteTutorialWithoutSkippingPuzzle()
        {
            SceneManager.LoadScene(ScenePath, LoadSceneMode.Single);
            yield return null;

            var evaluator = Object.FindFirstObjectByType<PaintingCompositionEvaluator>();
            var controller = Object.FindFirstObjectByType<PaintingManipulationController>();
            controller.SetInputLocked(false); // enter the gallery without synthetic legacy input
            yield return WaitForEvaluation(evaluator);

            PaintingManipulablePiece first = controller.Bridge;
            Vector3 unsolved = first.Root.position;
            Assert.IsTrue(controller.AssistPlace(first));
            Assert.That(Vector3.Distance(first.Root.position, first.AuthoredPosition), Is.LessThan(0.001f));
            Assert.IsTrue(controller.Undo(), "A player must be able to undo an unwanted assist.");
            Assert.That(Vector3.Distance(first.Root.position, unsolved), Is.LessThan(0.001f));

            Assert.IsTrue(controller.AssistPlace(first));
            var sequence = Object.FindFirstObjectByType<PaintingTutorialSequence>();
            yield return WaitForTutorialStep(sequence, 2, evaluator);
            var reveal = Object.FindFirstObjectByType<PaintingCompletionReveal>();
            yield return new WaitForSecondsRealtime(1f);
            Assert.AreEqual(PaintingCompletionReveal.RevealPhase.Idle, reveal.Phase,
                "A forgiving overall score after the bridge must not complete the tutorial before the pavilion.");
            Assert.IsFalse(controller.InputLocked,
                "Pavilion must remain operable after the bridge unlocks it.");
            for (int i = 0; i < controller.Pieces.Count; i++)
                if (controller.Pieces[i] != first)
                    Assert.IsTrue(controller.AssistPlace(controller.Pieces[i]));
            yield return WaitForEvaluation(evaluator, true);
            Assert.IsTrue(evaluator.LatestResult.PassesPolicy,
                "Repeated optional one-piece assists must guarantee that the tutorial remains completable.");
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

        private static IEnumerator WaitForTutorialStep(PaintingTutorialSequence sequence, int step,
            PaintingCompositionEvaluator evaluator)
        {
            Assert.IsNotNull(sequence);
            float deadline = Time.realtimeSinceStartup + 10f;
            while (sequence.CurrentStep != step)
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, "Tutorial step transition timed out.");
                evaluator.RequestEvaluationNow();
                yield return null;
            }
        }
    }
}
