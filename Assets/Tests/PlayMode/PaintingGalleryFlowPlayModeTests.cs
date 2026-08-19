using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PerspectivePuzzle.Presentation;

namespace PerspectivePuzzle.PlayMode.Tests
{
    /// <summary>
    /// T-017D gallery-flow tests for the three-gallery exhibition flow. They
    /// cover the opening that locks manipulation until the player confirms,
    /// the exactly-once <see cref="PaintingCompletionReveal.RevealCompleted"/>
    /// signal when the reveal reaches Complete, and the final-gallery
    /// collection-complete state that never attempts scene navigation. Confirm
    /// input is driven through the tiny <see cref="PaintingLevelFlow"/>
    /// test-only configuration seam (a scripted confirm instead of live legacy
    /// Input); the reveal is driven through its public deterministic
    /// <see cref="PaintingCompletionReveal.BeginReveal"/> entry. No synthetic
    /// mouse/keyboard and no reflection; frames are advanced with the same
    /// real-time-bounded wait pattern as the other painting PlayMode tests.
    /// </summary>
    public sealed class PaintingGalleryFlowPlayModeTests
    {
        private const string PrototypeScenePath = "Assets/Scenes/PaintingPrototype.unity";
        private const string FinalGalleryScenePath = "Assets/Scenes/PaintingTwinSeal.unity";
        private const float RevealTimeoutSeconds = 8f;

        [UnityTest]
        public IEnumerator IntroLocksManipulationUntilConfirm()
        {
            SceneManager.LoadScene(PrototypeScenePath, LoadSceneMode.Single);
            yield return null;

            var flow = UnityEngine.Object.FindFirstObjectByType<PaintingLevelFlow>();
            var manipulation = UnityEngine.Object.FindFirstObjectByType<PaintingManipulationController>();
            Assert.IsNotNull(flow, "PaintingLevelFlow missing from " + PrototypeScenePath + ".");
            Assert.IsNotNull(manipulation, "PaintingManipulationController missing.");
            flow.ConfigureForTests();

            var introGroup = GameObject.Find("Exhibition Introduction").GetComponent<CanvasGroup>();
            var continueGroup = GameObject.Find("Gallery Continue").GetComponent<CanvasGroup>();
            Assert.IsNotNull(introGroup, "Exhibition Introduction CanvasGroup missing.");
            Assert.IsNotNull(continueGroup, "Gallery Continue CanvasGroup missing.");

            // The museum opening owns the scene before any confirm:
            // manipulation is locked and the intro is visible.
            Assert.IsTrue(manipulation.InputLocked, "Manipulation must be locked by the intro.");
            Assert.AreEqual(1f, introGroup.alpha, 0.001f, "The intro must be visible before confirm.");
            Assert.IsFalse(continueGroup.interactable, "The continue prompt must stay hidden before confirm.");

            // Later frames without a confirm change nothing: still locked and
            // the intro still covers the gallery.
            yield return null;
            yield return null;
            Assert.IsTrue(manipulation.InputLocked, "Manipulation must stay locked until confirm.");

            // A single scripted confirm dismisses the intro and unlocks.
            flow.SetTestConfirm(true);
            yield return null;
            flow.SetTestConfirm(false);

            Assert.IsFalse(manipulation.InputLocked, "Confirm must unlock manipulation.");
            Assert.AreEqual(0f, introGroup.alpha, 0.001f, "Confirm must dismiss the intro.");
            Assert.IsFalse(continueGroup.interactable,
                "Confirm must only enter the gallery, not reveal the continue prompt yet.");

            yield return UnloadScene();
        }

        [UnityTest]
        public IEnumerator RevealCompletedFiresExactlyOnceWhenRevealReachesComplete()
        {
            SceneManager.LoadScene(PrototypeScenePath, LoadSceneMode.Single);
            yield return null;

            var reveal = UnityEngine.Object.FindFirstObjectByType<PaintingCompletionReveal>();
            Assert.IsNotNull(reveal, "PaintingCompletionReveal missing from " + PrototypeScenePath + ".");
            Assert.IsTrue(reveal.IsConfigured, "PaintingCompletionReveal must self-configure from the scene wiring.");

            int completed = 0;
            Action onCompleted = () => completed++;
            reveal.RevealCompleted += onCompleted;
            try
            {
                reveal.BeginReveal();
                yield return WaitForPhase(reveal, PaintingCompletionReveal.RevealPhase.Complete, RevealTimeoutSeconds);

                Assert.IsTrue(reveal.HasCompleted, "The reveal must reach Complete.");
                Assert.AreEqual(1, completed, "RevealCompleted must fire exactly once when the reveal completes.");

                // The Complete hold is terminal: staying on it must not re-emit.
                yield return null;
                yield return null;
                Assert.AreEqual(1, completed, "RevealCompleted must not re-fire while held on Complete.");
            }
            finally
            {
                reveal.RevealCompleted -= onCompleted;
            }

            yield return UnloadScene();
        }

        [UnityTest]
        public IEnumerator FinalGalleryCompletionShowsCollectionCompleteWithoutSceneNavigation()
        {
            SceneManager.LoadScene(FinalGalleryScenePath, LoadSceneMode.Single);
            yield return null;

            var flow = UnityEngine.Object.FindFirstObjectByType<PaintingLevelFlow>();
            var reveal = UnityEngine.Object.FindFirstObjectByType<PaintingCompletionReveal>();
            var manipulation = UnityEngine.Object.FindFirstObjectByType<PaintingManipulationController>();
            Assert.IsNotNull(flow, "PaintingLevelFlow missing from " + FinalGalleryScenePath + ".");
            Assert.IsNotNull(reveal, "PaintingCompletionReveal missing.");
            Assert.IsNotNull(manipulation, "PaintingManipulationController missing.");
            flow.ConfigureForTests();

            var continueGroup = GameObject.Find("Gallery Continue").GetComponent<CanvasGroup>();
            var continueText = GameObject.Find("Continue Message").GetComponent<Text>();
            Assert.IsNotNull(continueGroup, "Gallery Continue CanvasGroup missing.");
            Assert.IsNotNull(continueText, "Continue Message Text missing.");
            Assert.IsFalse(continueGroup.interactable, "The continue prompt must be hidden before completion.");

            // Enter the gallery, then drive the completion reveal to Complete.
            flow.SetTestConfirm(true);
            yield return null;
            flow.SetTestConfirm(false);
            Assert.IsFalse(manipulation.InputLocked, "Confirm must unlock the gallery.");

            reveal.BeginReveal();
            yield return WaitForPhase(reveal, PaintingCompletionReveal.RevealPhase.Complete, RevealTimeoutSeconds);

            // The final gallery presents the collection-complete hold instead
            // of a "next gallery" prompt.
            Assert.IsTrue(reveal.HasCompleted, "The reveal must reach Complete.");
            Assert.IsTrue(continueGroup.interactable, "The continue prompt must appear after completion.");
            StringAssert.Contains("全部作品已完成", continueText.text,
                "The final gallery must present the collection-complete state.");

            // Confirming the final hold must not attempt scene navigation.
            string sceneBefore = SceneManager.GetActiveScene().name;
            int sceneCountBefore = SceneManager.sceneCount;
            flow.SetTestConfirm(true);
            yield return null;
            yield return null;
            flow.SetTestConfirm(false);

            Assert.AreEqual(sceneCountBefore, SceneManager.sceneCount,
                "Confirming the final gallery must not load another scene.");
            Assert.AreEqual(sceneBefore, SceneManager.GetActiveScene().name,
                "The final gallery must stay in place without navigating.");
            Assert.IsTrue(continueGroup.interactable, "The collection-complete hold must persist.");

            yield return UnloadScene();
        }

        private static IEnumerator WaitForPhase(PaintingCompletionReveal reveal,
            PaintingCompletionReveal.RevealPhase phase, float timeout)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (reveal.Phase != phase)
            {
                Assert.Less(Time.realtimeSinceStartup, deadline,
                    "Timed out waiting for reveal phase " + phase + "; current=" + reveal.Phase + ".");
                yield return null;
            }
        }

        private static IEnumerator UnloadScene()
        {
            var current = SceneManager.GetActiveScene();
            var cleanup = SceneManager.CreateScene("T017DCleanup");
            SceneManager.SetActiveScene(cleanup);
            yield return SceneManager.UnloadSceneAsync(current);
        }
    }
}
