using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PerspectivePuzzle.Domain;
using PerspectivePuzzle.Presentation;

namespace PerspectivePuzzle.PlayMode.Tests
{
    /// <summary>
    /// T-009B2 composition calibration tests: load the deterministically built
    /// Painting Prototype scene and verify the evaluator and the eight ordered
    /// piece IDs are wired, that the untouched solved arrangement passes with a
    /// strong score, and that moving the Arch Bridge measurably degrades the
    /// total score and the bridge's piece metrics before an exact restore
    /// recovers the strong score. Evaluations are requested explicitly (never
    /// relying on the automatic 6 Hz cadence) and waited for with bounded
    /// real-time timeouts.
    /// </summary>
    public class PaintingCompositionPlayModeTests
    {
        private const string PaintingPrototypeScenePath = "Assets/Scenes/PaintingPrototype.unity";
        private const float SampleTimeoutSeconds = 10f;
        private const float StrongScoreThreshold = 0.97f;
        private const float MaterialScoreDrop = 0.05f;
        private const int ArchBridgeIndex = 6;
        private const int PavilionIndex = 5;
        private const int ForegroundRockIndex = 7;

        /// <summary>Required piece roots in evaluator order.</summary>
        private static readonly string[] RequiredPieces =
        {
            "Sun", "Far Mountain", "Middle Mountain", "Tree Cluster Left",
            "Tree Cluster Right", "Pavilion", "Arch Bridge", "Foreground Rock",
        };

        /// <summary>Required packed 24-bit Object-ID per piece, in the same order.</summary>
        private static readonly uint[] RequiredPieceIds =
        {
            0xFF4040u, 0x40FF40u, 0x4040FFu, 0xFFFF40u, 0xFF40FFu, 0x40FFFFu, 0xFF8040u, 0x8040FFu,
        };

        [UnityTest]
        public IEnumerator UntouchedSolvedArrangementPassesWithStrongScore()
        {
            SceneManager.LoadScene(PaintingPrototypeScenePath, LoadSceneMode.Single);
            yield return null;

            var evaluator = FindEvaluator();
            FindOrderedPieceRoots();
            SolveAllPieces();

            yield return WaitForEvaluation(evaluator, "solved arrangement",
                r => true);

            var result = evaluator.LatestResult;
            Assert.IsTrue(result.PassesPolicy && result.WeightedScore >= StrongScoreThreshold,
                "The untouched solved arrangement must pass with a strong score; WeightedScore="
                + result.WeightedScore.ToString("F4") + ", MinimumPieceCoverage=" + result.MinimumPieceCoverage.ToString("F4")
                + ", SilhouetteIoU=" + result.SilhouetteIoU.ToString("F4")
                + ", IdentityAccuracy=" + result.IdentityAccuracy.ToString("F4")
                + ", Target/CurrentForeground=" + result.TargetForegroundPixels + "/" + result.CurrentForegroundPixels
                + ", CurrentPixelsById=" + CurrentPixelSummary(result) + ".");

            yield return UnloadPrototypeScene();
        }

        [UnityTest]
        public IEnumerator MovingArchBridgeDegradesThenRestoringRecovers()
        {
            SceneManager.LoadScene(PaintingPrototypeScenePath, LoadSceneMode.Single);
            yield return null;

            var evaluator = FindEvaluator();
            var pieces = FindOrderedPieceRoots();
            SolveAllPieces();
            var bridge = pieces[ArchBridgeIndex].transform;

            // Baseline: the untouched solved arrangement must pass strongly.
            yield return WaitForEvaluation(evaluator, "baseline solved arrangement",
                r => true);
            var baseline = evaluator.LatestResult;
            Assert.IsTrue(baseline.PassesPolicy && baseline.WeightedScore >= StrongScoreThreshold,
                "The baseline solved arrangement must pass with a strong score; WeightedScore="
                + baseline.WeightedScore.ToString("F4") + ".");

            // Move only the Arch Bridge horizontally out of its target
            // position, far enough for a clear composition error.
            var savedPosition = bridge.position;
            var savedRotation = bridge.rotation;
            var savedLocalScale = bridge.localScale;
            bridge.position = savedPosition + new Vector3(2f, 0f, 0f);

            // The moved arrangement must fail clearly: the total score and the
            // bridge's piece metrics/identity evidence must drop materially.
            // The manual request is silently skipped while an automatic sample
            // is in flight, so the wait re-requests until a result reflecting
            // the move arrives.
            yield return WaitForEvaluation(evaluator, "moved-bridge arrangement",
                r => r.Pieces[ArchBridgeIndex].IoU < 0.5f);

            var moved = evaluator.LatestResult;
            Assert.Less(moved.WeightedScore, baseline.WeightedScore - MaterialScoreDrop,
                "Moving the Arch Bridge must materially lower the total score (baseline "
                + baseline.WeightedScore.ToString("F4") + " -> moved " + moved.WeightedScore.ToString("F4") + ").");
            Assert.Less(moved.Pieces[ArchBridgeIndex].IoU, baseline.Pieces[ArchBridgeIndex].IoU - 0.3f,
                "Moving the Arch Bridge must materially lower its piece IoU (baseline "
                + baseline.Pieces[ArchBridgeIndex].IoU.ToString("F4") + " -> moved "
                + moved.Pieces[ArchBridgeIndex].IoU.ToString("F4") + ").");
            Assert.Less(moved.Pieces[ArchBridgeIndex].TargetCoverage, 0.5f,
                "Moving the Arch Bridge must lose most of its target coverage; TargetCoverage="
                + moved.Pieces[ArchBridgeIndex].TargetCoverage.ToString("F4") + ".");
            Assert.Less(moved.IdentityAccuracy, baseline.IdentityAccuracy,
                "Moving the Arch Bridge must lower identity accuracy (baseline "
                + baseline.IdentityAccuracy.ToString("F4") + " -> moved "
                + moved.IdentityAccuracy.ToString("F4") + ").");

            // Restore the exact transform and verify the strong score recovers.
            bridge.position = savedPosition;
            bridge.rotation = savedRotation;
            bridge.localScale = savedLocalScale;
            yield return WaitForEvaluation(evaluator, "restored arrangement",
                r => r.PassesPolicy && r.WeightedScore >= StrongScoreThreshold);
            var recovered = evaluator.LatestResult;
            Assert.IsTrue(recovered.PassesPolicy && recovered.WeightedScore >= StrongScoreThreshold,
                "Restoring the Arch Bridge must recover the strong score; WeightedScore="
                + recovered.WeightedScore.ToString("F4") + ".");

            yield return UnloadPrototypeScene();
        }

        [UnityTest]
        public IEnumerator DepthVerticalAndOcclusionErrorsReduceRelevantMetrics()
        {
            SceneManager.LoadScene(PaintingPrototypeScenePath, LoadSceneMode.Single);
            yield return null;

            var evaluator = FindEvaluator();
            var pieces = FindOrderedPieceRoots();
            SolveAllPieces();
            var bridge = pieces[ArchBridgeIndex].transform;
            var pavilion = pieces[PavilionIndex].transform;
            var rock = pieces[ForegroundRockIndex].transform;

            yield return WaitForEvaluation(evaluator, "additional-error baseline", r => true);
            var baseline = evaluator.LatestResult;
            Assert.IsTrue(baseline.PassesPolicy, "Additional-error baseline must start solved; score="
                + baseline.WeightedScore.ToString("F4") + ".");

            Vector3 bridgePosition = bridge.position;
            bridge.position = bridgePosition + new Vector3(0f, 0.8f, 0f);
            yield return WaitForEvaluation(evaluator, "vertical bridge error",
                r => r.Pieces[ArchBridgeIndex].IoU < 0.65f);
            var vertical = evaluator.LatestResult;
            Assert.Less(vertical.Pieces[ArchBridgeIndex].TargetCoverage,
                baseline.Pieces[ArchBridgeIndex].TargetCoverage - 0.25f,
                "Vertical error must reduce bridge target coverage.");
            bridge.position = bridgePosition;
            yield return WaitForEvaluation(evaluator, "vertical restore", r => r.PassesPolicy);

            bridge.position = bridgePosition + new Vector3(0f, 0f, 1.5f);
            yield return WaitForEvaluation(evaluator, "depth bridge error",
                r => r.Pieces[ArchBridgeIndex].IoU < 0.75f);
            var depth = evaluator.LatestResult;
            Assert.Less(depth.WeightedScore, baseline.WeightedScore - 0.03f,
                "Near/far error must reduce total score (baseline " + baseline.WeightedScore.ToString("F4")
                + " -> depth " + depth.WeightedScore.ToString("F4") + ").");
            bridge.position = bridgePosition;
            yield return WaitForEvaluation(evaluator, "depth restore", r => r.PassesPolicy);

            Vector3 rockPosition = rock.position;
            rock.position = pavilion.position + new Vector3(0f, 0.15f, 0.8f);
            yield return WaitForEvaluation(evaluator, "pavilion occlusion error",
                r => r.WeightedScore < 0.95f);
            var occluded = evaluator.LatestResult;
            Assert.Less(occluded.Pieces[PavilionIndex].TargetCoverage,
                baseline.Pieces[PavilionIndex].TargetCoverage - 0.10f,
                "Wrong foreground occlusion must reduce pavilion coverage (baseline "
                + baseline.Pieces[PavilionIndex].TargetCoverage.ToString("F4") + " -> occluded "
                + occluded.Pieces[PavilionIndex].TargetCoverage.ToString("F4") + ").");
            Assert.Less(occluded.IdentityAccuracy, baseline.IdentityAccuracy,
                "Wrong foreground occlusion must reduce identity accuracy.");
            rock.position = rockPosition;
            yield return WaitForEvaluation(evaluator, "occlusion restore", r => r.PassesPolicy);

            yield return UnloadPrototypeScene();
        }

        /// <summary>
        /// Finds the single evaluator the scene must deserialize and wire from
        /// its saved references.
        /// </summary>
        private static PaintingCompositionEvaluator FindEvaluator()
        {
            var evaluator = UnityEngine.Object.FindFirstObjectByType<PaintingCompositionEvaluator>();
            Assert.IsNotNull(evaluator, "PaintingCompositionEvaluator missing from " + PaintingPrototypeScenePath + ".");
            return evaluator;
        }

        /// <summary>
        /// Finds "Solved Scenery" and returns its eight direct children in the
        /// required order, asserting each carries exactly one PaintingPieceId
        /// with the required packed ID and no PaintingPieceId on its renderer
        /// children.
        /// </summary>
        private static PaintingPieceId[] FindOrderedPieceRoots()
        {
            var scenery = GameObject.Find("Solved Scenery");
            Assert.IsNotNull(scenery, "Solved Scenery missing.");
            Assert.AreEqual(RequiredPieces.Length, scenery.transform.childCount,
                "Solved Scenery must contain exactly " + RequiredPieces.Length + " direct piece roots.");

            var roots = new PaintingPieceId[RequiredPieces.Length];
            for (int i = 0; i < RequiredPieces.Length; i++)
            {
                var pieceRoot = scenery.transform.Find(RequiredPieces[i]);
                Assert.IsNotNull(pieceRoot, "Solved Scenery/" + RequiredPieces[i] + " missing.");
                var ids = pieceRoot.GetComponents<PaintingPieceId>();
                Assert.AreEqual(1, ids.Length,
                    "Solved Scenery/" + RequiredPieces[i] + " must carry exactly one PaintingPieceId.");
                Assert.AreEqual(RequiredPieceIds[i], ids[0].Id,
                    "Solved Scenery/" + RequiredPieces[i] + " must carry ID 0x" + RequiredPieceIds[i].ToString("X6") + ".");
                Assert.AreEqual(1, pieceRoot.GetComponentsInChildren<PaintingPieceId>(true).Length,
                    "Solved Scenery/" + RequiredPieces[i] + " must carry no PaintingPieceId on its renderer children.");
                roots[i] = ids[0];
            }
            return roots;
        }

        /// <summary>
        /// T-010C saves the scene intentionally unsolved. T-009 scorer tests
        /// recover their canonical baseline through the public manipulation
        /// API before applying their own controlled transform perturbations.
        /// </summary>
        private static void SolveAllPieces()
        {
            var controller = UnityEngine.Object.FindFirstObjectByType<PaintingManipulationController>();
            Assert.IsNotNull(controller, "PaintingManipulationController missing.");
            Assert.AreEqual(RequiredPieces.Length, controller.Pieces.Count,
                "The controller must expose all ordered painting pieces.");
            for (int i = 0; i < controller.Pieces.Count; i++)
            {
                controller.SelectPiece(controller.Pieces[i]);
                Assert.IsTrue(controller.ResetToAuthored(),
                    "ResetToAuthored must recover " + RequiredPieces[i] + " for scorer calibration.");
            }
            controller.DeselectPiece();
        }

        /// <summary>
        /// Requests an explicit evaluation and waits for the next Evaluated
        /// event whose result satisfies <paramref name="accepted"/>, within a
        /// bounded real-time timeout. The manual request is silently skipped
        /// when the automatic 6 Hz sampling already has a request in flight;
        /// in that case the in-flight sample's event is inspected (it may
        /// predate a scene mutation) and another request is issued until an
        /// accepted result arrives, so the wait never depends on the sampling
        /// cadence. The event subscription is cleaned up on every exit path.
        /// </summary>
        private static IEnumerator WaitForEvaluation(
            PaintingCompositionEvaluator evaluator,
            string description,
            Func<CompositionScoreResult, bool> accepted)
        {
            Assert.IsTrue(evaluator.IsConfigured,
                "Composition Evaluator must configure itself from the saved scene (is the target Object-ID texture readable?).");

            var flag = new EvaluationFlag();
            Action<CompositionScoreResult> handler = result => flag.Raised = true;
            evaluator.Evaluated += handler;
            try
            {
                float deadline = Time.realtimeSinceStartup + SampleTimeoutSeconds;
                CompositionScoreResult last = evaluator.LatestResult;
                while (Time.realtimeSinceStartup < deadline)
                {
                    flag.Raised = false;
                    evaluator.RequestEvaluationNow();
                    while (!flag.Raised && Time.realtimeSinceStartup < deadline)
                        yield return null;
                    if (!flag.Raised)
                        break;
                    last = evaluator.LatestResult;
                    if (accepted(last))
                        yield break;
                }
                Assert.Fail("No accepted " + description + " evaluation arrived within " + SampleTimeoutSeconds
                    + " seconds; last WeightedScore="
                    + (last != null ? last.WeightedScore.ToString("F4") : "none") + ".");
                yield break;
            }
            finally
            {
                evaluator.Evaluated -= handler;
            }
        }

        /// <summary>Mutable flag so the Evaluated event can be observed across coroutine frames.</summary>
        private sealed class EvaluationFlag
        {
            public bool Raised;
        }

        private static string CurrentPixelSummary(CompositionScoreResult result)
        {
            var values = new string[result.Pieces.Count];
            for (int i = 0; i < values.Length; i++)
                values[i] = result.Pieces[i].Id.ToString("X6") + ":" + result.Pieces[i].CurrentPixels;
            return string.Join(",", values);
        }

        private static IEnumerator UnloadPrototypeScene()
        {
            var prototypeScene = SceneManager.GetActiveScene();
            var cleanupScene = SceneManager.CreateScene("T009BCleanup");
            SceneManager.SetActiveScene(cleanupScene);
            yield return SceneManager.UnloadSceneAsync(prototypeScene);
        }
    }
}
