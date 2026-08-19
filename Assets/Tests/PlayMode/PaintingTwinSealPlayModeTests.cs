using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PerspectivePuzzle.Presentation;

namespace PerspectivePuzzle.PlayMode.Tests
{
    /// <summary>
    /// T-022D Twin Seal compound-goal wiring: the final gallery scene must
    /// contain the primary and secondary composition evaluators, a goal gate
    /// with exactly two goals, a secondary reveal camera, the secondary target
    /// UI, and a completion reveal that references the compound goal gate.
    /// </summary>
    public sealed class PaintingTwinSealPlayModeTests
    {
        private const string TwinSealScenePath = "Assets/Scenes/PaintingTwinSeal.unity";

        [UnityTest]
        public IEnumerator TwinSealSceneWiresCompoundGoalGateAndReveal()
        {
            SceneManager.LoadScene(TwinSealScenePath, LoadSceneMode.Single);
            yield return null;

            var primaryGo = GameObject.Find("Composition Evaluator");
            var secondaryGo = GameObject.Find("Secondary Composition Evaluator");
            Assert.IsNotNull(primaryGo, "Primary Composition Evaluator missing from " + TwinSealScenePath + ".");
            Assert.IsNotNull(secondaryGo, "Secondary Composition Evaluator missing from " + TwinSealScenePath + ".");
            var primary = primaryGo.GetComponent<PaintingCompositionEvaluator>();
            var secondary = secondaryGo.GetComponent<PaintingCompositionEvaluator>();
            Assert.IsNotNull(primary, "Composition Evaluator must carry a PaintingCompositionEvaluator.");
            Assert.IsNotNull(secondary, "Secondary Composition Evaluator must carry a PaintingCompositionEvaluator.");
            Assert.IsTrue(primary.IsConfigured, "The primary evaluator must self-configure from the saved wiring.");
            Assert.IsTrue(secondary.IsConfigured, "The secondary evaluator must self-configure from the saved wiring.");

            var gateGo = GameObject.Find("Painting Goal Gate");
            Assert.IsNotNull(gateGo, "Painting Goal Gate missing from " + TwinSealScenePath + ".");
            var gate = gateGo.GetComponent<PaintingGoalGate>();
            Assert.IsNotNull(gate, "Painting Goal Gate must carry a PaintingGoalGate.");
            Assert.IsTrue(gate.IsConfigured, "The goal gate must reference its evaluators.");
            Assert.AreSame(primary, gate.Primary, "The goal gate must reference the primary evaluator.");
            Assert.AreEqual(2, gate.GoalCount, "Twin Seal must present exactly two goals.");

            var secondaryCameraGo = GameObject.Find("Secondary Composition Camera");
            Assert.IsNotNull(secondaryCameraGo, "Secondary Composition Camera missing from " + TwinSealScenePath + ".");
            Assert.IsNotNull(secondaryCameraGo.GetComponent<Camera>(),
                "Secondary Composition Camera must carry a Camera.");

            var secondaryStatus = GameObject.Find("Secondary Status");
            Assert.IsNotNull(secondaryStatus, "Secondary Status UI missing from " + TwinSealScenePath + ".");
            Assert.IsNotNull(secondaryStatus.GetComponent<Text>(), "Secondary Status must carry a Text.");
            var sealImage = GameObject.Find("Secondary Seal Target");
            Assert.IsNotNull(sealImage, "Secondary Seal Target UI missing from " + TwinSealScenePath + ".");
            Assert.IsNotNull(sealImage.GetComponent<RawImage>(), "Secondary Seal Target must carry a RawImage.");

            var reveal = Object.FindFirstObjectByType<PaintingCompletionReveal>();
            Assert.IsNotNull(reveal, "PaintingCompletionReveal missing from " + TwinSealScenePath + ".");
            Assert.IsTrue(reveal.IsConfigured, "The completion reveal must self-configure from the saved wiring.");
            Assert.AreSame(gate, ReadGoalGateReference(reveal),
                "The completion reveal must reference the compound goal gate.");

            yield return UnloadScene();
        }

        /// <summary>
        /// The reveal's goal-gate reference is a serialized field with no
        /// public accessor; reading it directly keeps this PlayMode test free
        /// of UnityEditor dependencies. The GoalGate EditMode tests still drive
        /// the gate exclusively through the public <see cref="PaintingGoalGate.Configure"/>
        /// seam.
        /// </summary>
        private static PaintingGoalGate ReadGoalGateReference(PaintingCompletionReveal reveal)
        {
            var field = typeof(PaintingCompletionReveal).GetField("_goalGate",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "PaintingCompletionReveal._goalGate field not found.");
            return (PaintingGoalGate)field.GetValue(reveal);
        }

        private static IEnumerator UnloadScene()
        {
            var current = SceneManager.GetActiveScene();
            var cleanup = SceneManager.CreateScene("T022DTwinSealCleanup");
            SceneManager.SetActiveScene(cleanup);
            yield return SceneManager.UnloadSceneAsync(current);
        }
    }
}
