using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PerspectivePuzzle.Domain;
using PerspectivePuzzle.Presentation;

namespace PerspectivePuzzle.PlayMode.Tests
{
    /// <summary>Audits every shipped answer asset against its scene's authored visual solution.</summary>
    public sealed class PaintingAnswerIntegrityPlayModeTests
    {
        private static readonly string[] Scenes =
        {
            "Assets/Scenes/PaintingPrototype.unity",
            "Assets/Scenes/PaintingMoonGarden.unity",
            "Assets/Scenes/PaintingRedCliffs.unity",
            "Assets/Scenes/PaintingTwinSeal.unity",
        };

        [UnityTest]
        public IEnumerator EveryAuthoredSolutionMatchesItsPrimaryAnswer()
        {
            foreach (string scenePath in Scenes)
            {
                SceneManager.LoadScene(scenePath, LoadSceneMode.Single);
                yield return null;
                RestoreEveryAuthoredPiece(scenePath);
                PaintingCompositionEvaluator primary = GameObject.Find("Composition Evaluator")
                    ?.GetComponent<PaintingCompositionEvaluator>();
                Assert.IsNotNull(primary, "Primary evaluator missing from " + scenePath);
                yield return WaitForFreshEvaluation(primary);
                var result = primary.LatestResult;
                Assert.IsTrue(result.PassesPolicy,
                    scenePath + " authored solution does not pass its answer: weighted="
                    + result.WeightedScore.ToString("F4") + ", silhouette="
                    + result.SilhouetteIoU.ToString("F4") + ", minimumCoverage="
                    + result.MinimumPieceCoverage.ToString("F4") + ".");
                Assert.That(result.WeightedScore, Is.GreaterThanOrEqualTo(0.97f),
                    scenePath + " answer has drifted from the authored solution.");
            }
        }

        [UnityTest]
        public IEnumerator TwinSealAuthoredSolutionMatchesSecondaryAnswer()
        {
            const string scenePath = "Assets/Scenes/PaintingTwinSeal.unity";
            SceneManager.LoadScene(scenePath, LoadSceneMode.Single);
            yield return null;
            RestoreEveryAuthoredPiece(scenePath);
            PaintingCompositionEvaluator secondary = GameObject.Find("Secondary Composition Evaluator")
                ?.GetComponent<PaintingCompositionEvaluator>();
            Assert.IsNotNull(secondary);
            yield return WaitForFreshEvaluation(secondary);
            Assert.That(secondary.LatestResult.SilhouetteIoU, Is.GreaterThanOrEqualTo(0.97f),
                "Twin Seal secondary silhouette answer has drifted from its authored solution.");
            var gate = Object.FindFirstObjectByType<PaintingGoalGate>();
            Assert.IsNotNull(gate);
            PaintingCompositionEvaluator primary = gate.Primary;
            yield return WaitForFreshEvaluation(primary);
            Assert.IsTrue(gate.IsSatisfied,
                "Twin Seal authored solution must satisfy both primary painting and secondary seal answers.");
        }

        private static void RestoreEveryAuthoredPiece(string scenePath)
        {
            var scenery = GameObject.Find("Solved Scenery");
            Assert.IsNotNull(scenery, "Solved Scenery missing from " + scenePath);
            var pieces = scenery.GetComponentsInChildren<PaintingManipulablePiece>(true);
            Assert.AreEqual(8, pieces.Length, scenePath + " must contain eight authored answer pieces.");
            foreach (PaintingManipulablePiece piece in pieces) piece.RestoreAuthored();
        }

        private static IEnumerator WaitForFreshEvaluation(PaintingCompositionEvaluator evaluator)
        {
            float deadline = Time.realtimeSinceStartup + 12f;
            CompositionScoreResult before = evaluator.LatestResult;
            while (evaluator.LatestResult == null || evaluator.LatestResult == before)
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, "Answer evaluation timed out.");
                evaluator.RequestEvaluationNow();
                yield return null;
            }
        }
    }
}
