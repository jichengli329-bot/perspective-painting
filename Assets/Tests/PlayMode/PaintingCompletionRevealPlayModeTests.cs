using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PerspectivePuzzle.Presentation;

namespace PerspectivePuzzle.PlayMode.Tests
{
    public sealed class PaintingCompletionRevealPlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/PaintingPrototype.unity";

        [UnityTest]
        public IEnumerator RevealLocksInputVisitsPaintingViewThenShowsPerspective()
        {
            SceneManager.LoadScene(ScenePath, LoadSceneMode.Single);
            yield return null;

            var reveal = Object.FindFirstObjectByType<PaintingCompletionReveal>();
            var manipulation = Object.FindFirstObjectByType<PaintingManipulationController>();
            Camera build = GameObject.Find("Build Camera").GetComponent<Camera>();
            Camera composition = GameObject.Find("Composition Camera").GetComponent<Camera>();
            Assert.IsNotNull(reveal);
            Assert.IsTrue(reveal.IsConfigured);

            Vector3 initialPosition = build.transform.position;
            reveal.BeginReveal();
            Assert.IsTrue(manipulation.InputLocked);
            Assert.AreEqual(PaintingCompletionReveal.RevealPhase.ToPainting, reveal.Phase);

            yield return WaitForPhase(reveal, PaintingCompletionReveal.RevealPhase.HoldPainting, 3f);
            Assert.Less(Vector3.Distance(build.transform.position, composition.transform.position), 0.0001f);
            Assert.Less(Quaternion.Angle(build.transform.rotation, composition.transform.rotation), 0.001f);
            Assert.AreEqual(composition.fieldOfView, build.fieldOfView, 0.0001f);

            yield return WaitForPhase(reveal, PaintingCompletionReveal.RevealPhase.Complete, 5f);
            Assert.IsTrue(reveal.HasCompleted);
            Assert.IsTrue(manipulation.InputLocked, "The completed composition must remain protected.");
            Assert.Greater(Vector3.Distance(build.transform.position, initialPosition), 0.5f);
            Assert.Greater(Vector3.Distance(build.transform.position, composition.transform.position), 0.5f,
                "The final camera must reveal the physical perspective rather than remain inside the flat painting view.");
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
    }
}
