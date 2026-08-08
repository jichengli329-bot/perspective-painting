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
    /// T-010C manipulation tests: load the deterministically built Painting
    /// Prototype scene — which now saves the authored unsolved start layout —
    /// and drive the eight manipulable pieces exclusively through the public
    /// APIs of <see cref="PaintingManipulationController"/> — no synthetic
    /// mouse input and no reflection-based transform mutation. The
    /// scene-load baseline verifies the unsolved start (a non-passing
    /// composition score, every piece displaced from its authored pose but
    /// inside the movement bounds and the +/-2 Composition Camera depth
    /// constraint with a quantized yaw), then <see cref="SolveAllPieces"/>
    /// restores the solved composition through SelectPiece + ResetToAuthored
    /// and the strong score returns. Verifies the wiring (one handle and one
    /// collider on every piece root, PaintingPiece layer, authored transform
    /// captured, ordered controller references), that all eight pieces are
    /// selectable through the public API and resolvable by Build Camera
    /// raycasting with the nearest-visible overlap rule, that a bounded
    /// plane translation and a bounded depth adjustment on two different
    /// pieces both measurably degrade their own evaluated piece metric
    /// without changing scale, that undo restores the stored historical
    /// piece after the selection switched, and that the Q/E-equivalent
    /// public rotation is quantized to 15-degree steps, clamped to +/-45
    /// degrees of authored yaw, preserves scale, lowers the score, and is
    /// recovered exactly by undo and reset. Evaluations are requested
    /// explicitly (never relying on the automatic 6 Hz cadence) and waited
    /// for with bounded real-time timeouts; event subscriptions are cleaned
    /// up on every path.
    /// </summary>
    public class PaintingManipulationPlayModeTests
    {
        private const string PaintingPrototypeScenePath = "Assets/Scenes/PaintingPrototype.unity";
        private const float SampleTimeoutSeconds = 10f;
        private const float StrongScoreThreshold = 0.97f;
        private const float MaterialScoreDrop = 0.05f;
        private const float DepthScoreDrop = 0.03f;
        private const int FarMountainIndex = 1;
        private const int MiddleMountainIndex = 2;
        private const int PavilionIndex = 5;
        private const int ArchBridgeIndex = 6;
        private const int ForegroundRockIndex = 7;

        /// <summary>T-010C start-layout rotation step/clamp in degrees.</summary>
        private const float RotationStepDegrees = 15f;
        private const float MaxRotationOffsetDegrees = 45f;

        /// <summary>Required piece roots in evaluator/controller order.</summary>
        private static readonly string[] RequiredPieces =
        {
            "Sun", "Far Mountain", "Middle Mountain", "Tree Cluster Left",
            "Tree Cluster Right", "Pavilion", "Arch Bridge", "Foreground Rock",
        };

        [UnityTest]
        public IEnumerator SceneStartsUnsolvedThenSolvingAllPiecesRecoversStrongScore()
        {
            SceneManager.LoadScene(PaintingPrototypeScenePath, LoadSceneMode.Single);
            yield return null;

            var evaluator = FindEvaluator();
            var controller = FindController();
            VerifyManipulationWiring(controller);

            // T-010C scene-load baseline: the saved start layout must be
            // materially unsolved — the composition must not pass and the
            // score must be weak.
            yield return WaitForEvaluation(evaluator, "authored unsolved start layout", r => true);
            var start = evaluator.LatestResult;
            Assert.IsFalse(start.PassesPolicy,
                "The scene must load unsolved; PassesPolicy=" + start.PassesPolicy
                + " (WeightedScore=" + start.WeightedScore.ToString("F4") + ").");
            Assert.Less(start.WeightedScore, StrongScoreThreshold,
                "The unsolved start must not score strongly; WeightedScore="
                + start.WeightedScore.ToString("F4") + ".");

            // The start layout must be readable and selectable from the
            // Build Camera: a Build Camera ray toward each piece's collider
            // center must resolve to a configured piece on the PaintingPiece
            // layer.
            for (int i = 0; i < controller.Pieces.Count; i++)
            {
                var resolved = ResolveFromBuildCamera(controller, controller.Pieces[i].SelectionCollider.bounds.center);
                Assert.IsNotNull(resolved,
                    "A Build Camera ray toward the unsolved " + RequiredPieces[i]
                    + " must hit a configured piece on the PaintingPiece layer.");
            }

            // Every piece must start displaced from its authored pose but
            // inside the movement bounds and within +/-2 Composition Camera
            // depth of it, with a root yaw quantized to 15-degree steps and
            // clamped to +/-45 degrees.
            var compositionCamera = GameObject.Find("Composition Camera").GetComponent<Camera>();
            for (int i = 0; i < controller.Pieces.Count; i++)
            {
                var piece = controller.Pieces[i];
                var pieceRoot = piece.Root;
                Assert.Greater(Vector3.Distance(pieceRoot.position, piece.AuthoredPosition), 0.001f,
                    RequiredPieces[i] + " must start displaced from its authored position.");
                Bounds colliderBounds = piece.SelectionCollider.bounds;
                Assert.IsTrue(controller.MovementBounds.Contains(colliderBounds.min)
                    && controller.MovementBounds.Contains(colliderBounds.max),
                    RequiredPieces[i] + " start collider must stay inside the movement bounds.");
                Vector3 viewport = compositionCamera.WorldToViewportPoint(pieceRoot.position);
                Assert.IsTrue(controller.CompositionViewportBounds.Contains(new Vector2(viewport.x, viewport.y)),
                    RequiredPieces[i] + " start pose must stay inside the shared composition canvas.");
                Assert.That(viewport.z, Is.InRange(
                    controller.CompositionDepthRange.x, controller.CompositionDepthRange.y),
                    RequiredPieces[i] + " start pose must stay inside the shared composition depth range.");
                float yawOffset = piece.AuthoredSignedYawOffset(pieceRoot.rotation);
                float quantized = Mathf.Round(yawOffset / RotationStepDegrees) * RotationStepDegrees;
                Assert.LessOrEqual(Mathf.Abs(yawOffset), MaxRotationOffsetDegrees + 0.01f,
                    RequiredPieces[i] + " start yaw must stay within +/-" + MaxRotationOffsetDegrees + " degrees.");
                Assert.LessOrEqual(Mathf.Abs(yawOffset - quantized), 0.01f,
                    RequiredPieces[i] + " start yaw must be a multiple of " + RotationStepDegrees
                    + " degrees; yaw=" + yawOffset.ToString("F2") + ".");
            }

            // Recover the solved composition through the public APIs only:
            // select every ordered piece and reset it to its authored pose.
            SolveAllPieces(controller);

            // Each piece must be back at its exact authored position/rotation/scale.
            for (int i = 0; i < controller.Pieces.Count; i++)
            {
                var piece = controller.Pieces[i];
                var pieceRoot = piece.Root;
                Assert.AreEqual(piece.AuthoredPosition, pieceRoot.position,
                    "Solving must restore the authored position of " + RequiredPieces[i] + ".");
                Assert.AreEqual(piece.AuthoredRotation, pieceRoot.rotation,
                    "Solving must restore the authored rotation of " + RequiredPieces[i] + ".");
                Assert.AreEqual(piece.AuthoredLocalScale, pieceRoot.localScale,
                    "Solving must restore the authored local scale of " + RequiredPieces[i] + ".");
            }

            // The solved arrangement must pass strongly again.
            yield return WaitForEvaluation(evaluator, "solved arrangement",
                r => r.PassesPolicy && r.WeightedScore >= StrongScoreThreshold);
            var solved = evaluator.LatestResult;
            Assert.IsTrue(solved.PassesPolicy && solved.WeightedScore >= StrongScoreThreshold,
                "Solving all pieces must recover the strong score; WeightedScore="
                + solved.WeightedScore.ToString("F4") + ".");

            yield return UnloadPrototypeScene();
        }

        [UnityTest]
        public IEnumerator BoundedTranslationLowersScoreThenUndoRecoversExactly()
        {
            SceneManager.LoadScene(PaintingPrototypeScenePath, LoadSceneMode.Single);
            yield return null;

            var evaluator = FindEvaluator();
            var controller = FindController();
            VerifyManipulationWiring(controller);
            SolveAllPieces(controller);

            // Baseline after recovering the hidden authored solution.
            yield return WaitForEvaluation(evaluator, "baseline solved arrangement", r => true);
            var baseline = evaluator.LatestResult;
            Assert.IsTrue(baseline.PassesPolicy && baseline.WeightedScore >= StrongScoreThreshold,
                "The baseline solved arrangement must pass with a strong score; WeightedScore="
                + baseline.WeightedScore.ToString("F4") + ".");

            var bridge = controller.Bridge.Root;
            var authoredPosition = bridge.position;
            var authoredRotation = bridge.rotation;
            var authoredLocalScale = bridge.localScale;

            // Select through the public API and perform a bounded plane
            // translation far enough for a clear composition error.
            controller.SelectPiece();
            Assert.IsTrue(controller.IsSelected, "SelectPiece must select the bridge.");
            Assert.IsTrue(controller.TryTranslate(new Vector3(1.6f, 0f, 0f)),
                "A bounded world translation must succeed.");
            Assert.AreNotEqual(authoredPosition, bridge.position,
                "A successful translation must move the bridge.");
            Assert.IsTrue(controller.CanUndo, "A completed translation must be undoable.");

            // The translated arrangement must fail clearly: total score and
            // the bridge's piece IoU must drop materially.
            yield return WaitForEvaluation(evaluator, "translated bridge arrangement",
                r => r.Pieces[ArchBridgeIndex].IoU < 0.5f);
            var moved = evaluator.LatestResult;
            Assert.Less(moved.WeightedScore, baseline.WeightedScore - MaterialScoreDrop,
                "Translating the Arch Bridge must materially lower the total score (baseline "
                + baseline.WeightedScore.ToString("F4") + " -> moved " + moved.WeightedScore.ToString("F4") + ").");
            Assert.Less(moved.Pieces[ArchBridgeIndex].IoU, baseline.Pieces[ArchBridgeIndex].IoU - 0.3f,
                "Translating the Arch Bridge must materially lower its piece IoU (baseline "
                + baseline.Pieces[ArchBridgeIndex].IoU.ToString("F4") + " -> moved "
                + moved.Pieces[ArchBridgeIndex].IoU.ToString("F4") + ").");

            // Undo must restore the exact authored transform and consume the
            // stored operation, and the passing score must recover.
            Assert.IsTrue(controller.Undo(), "Undo after a completed translation must succeed.");
            Assert.IsFalse(controller.CanUndo, "Undo must consume the stored operation.");
            Assert.AreEqual(authoredPosition, bridge.position, "Undo must restore the authored position.");
            Assert.AreEqual(authoredRotation, bridge.rotation, "Undo must restore the authored rotation.");
            Assert.AreEqual(authoredLocalScale, bridge.localScale, "Undo must restore the authored local scale.");

            yield return WaitForEvaluation(evaluator, "restored arrangement",
                r => r.PassesPolicy && r.WeightedScore >= StrongScoreThreshold);
            var recovered = evaluator.LatestResult;
            Assert.IsTrue(recovered.PassesPolicy && recovered.WeightedScore >= StrongScoreThreshold,
                "Undoing the translation must recover the strong score; WeightedScore="
                + recovered.WeightedScore.ToString("F4") + ".");

            // An oversized move reaches the shared target-canvas edge rather
            // than an authored-relative invisible cage.
            Camera compositionCamera = GameObject.Find("Composition Camera").GetComponent<Camera>();
            Assert.IsTrue(controller.TryTranslate(compositionCamera.transform.right * 50f),
                "An oversized translation delta must still succeed through clamping.");
            float bridgeViewportX = compositionCamera.WorldToViewportPoint(bridge.position).x;
            Assert.AreEqual(controller.CompositionViewportBounds.xMax, bridgeViewportX, 0.0001f,
                "An oversized translation must clamp to the shared composition-canvas edge.");
            Assert.IsTrue(controller.Undo(), "Undo after the clamped translation must succeed.");
            Assert.AreEqual(authoredPosition, bridge.position, "Undo must restore the authored position after clamping.");

            yield return UnloadPrototypeScene();
        }

        [UnityTest]
        public IEnumerator BoundedDepthAdjustmentLowersScoreThenResetRecovers()
        {
            SceneManager.LoadScene(PaintingPrototypeScenePath, LoadSceneMode.Single);
            yield return null;

            var evaluator = FindEvaluator();
            var controller = FindController();
            VerifyManipulationWiring(controller);
            SolveAllPieces(controller);

            yield return WaitForEvaluation(evaluator, "depth baseline solved arrangement", r => true);
            var baseline = evaluator.LatestResult;
            Assert.IsTrue(baseline.PassesPolicy && baseline.WeightedScore >= StrongScoreThreshold,
                "The depth baseline must pass with a strong score; WeightedScore="
                + baseline.WeightedScore.ToString("F4") + ".");

            var bridge = controller.Bridge.Root;
            var authoredPosition = bridge.position;
            var authoredRotation = bridge.rotation;
            var authoredLocalScale = bridge.localScale;

            // Select and adjust depth along the Composition Camera forward
            // (negative = toward the camera) through the public API.
            controller.SelectPiece();
            Assert.IsTrue(controller.IsSelected, "SelectPiece must select the bridge.");
            Assert.IsTrue(controller.TryAdjustDepth(-1.5f),
                "A bounded depth adjustment must succeed.");
            Assert.AreEqual(authoredLocalScale, bridge.localScale,
                "A depth adjustment must never change scale.");
            Assert.AreNotEqual(authoredPosition, bridge.position,
                "A successful depth adjustment must move the bridge.");

            // The depth-adjusted arrangement must fail clearly, without any
            // scale change: total score and bridge piece IoU must drop.
            yield return WaitForEvaluation(evaluator, "depth-adjusted bridge arrangement",
                r => r.Pieces[ArchBridgeIndex].IoU < 0.75f);
            var adjusted = evaluator.LatestResult;
            Assert.Less(adjusted.WeightedScore, baseline.WeightedScore - DepthScoreDrop,
                "A depth adjustment must lower the total score (baseline "
                + baseline.WeightedScore.ToString("F4") + " -> adjusted " + adjusted.WeightedScore.ToString("F4") + ").");
            Assert.Less(adjusted.Pieces[ArchBridgeIndex].IoU, 0.75f,
                "A depth adjustment must lower the bridge piece IoU; IoU="
                + adjusted.Pieces[ArchBridgeIndex].IoU.ToString("F4") + ".");
            Assert.IsTrue(controller.CanUndo, "A completed depth operation must be undoable.");

            // Reset must restore the exact authored transform (and the
            // passing score) and is itself undoable.
            Assert.IsTrue(controller.ResetToAuthored(), "ResetToAuthored must succeed.");
            Assert.AreEqual(authoredPosition, bridge.position, "Reset must restore the authored position.");
            Assert.AreEqual(authoredRotation, bridge.rotation, "Reset must restore the authored rotation.");
            Assert.AreEqual(authoredLocalScale, bridge.localScale, "Reset must restore the authored local scale.");
            Assert.IsTrue(controller.CanUndo, "Reset itself must be undoable.");

            yield return WaitForEvaluation(evaluator, "reset arrangement",
                r => r.PassesPolicy && r.WeightedScore >= StrongScoreThreshold);
            var recovered = evaluator.LatestResult;
            Assert.IsTrue(recovered.PassesPolicy && recovered.WeightedScore >= StrongScoreThreshold,
                "Resetting the depth adjustment must recover the strong score; WeightedScore="
                + recovered.WeightedScore.ToString("F4") + ".");

            // Undo consumes the reset operation and returns to the
            // pre-reset (depth-adjusted) pose; deselection clears the
            // selection state without leaving undo state behind.
            Assert.IsTrue(controller.Undo(), "Undo after reset must succeed.");
            Assert.IsFalse(controller.CanUndo, "Undo must consume the stored state.");
            Assert.AreNotEqual(authoredPosition, bridge.position,
                "Undoing a reset must return to the pre-reset (depth-adjusted) pose.");

            controller.DeselectPiece();
            Assert.IsFalse(controller.IsSelected, "DeselectPiece must clear the selection.");
            Assert.IsNull(controller.SelectedPiece, "DeselectPiece must clear the selected piece.");
            Assert.IsFalse(controller.CanUndo, "Deselection must not leave undo state.");

            yield return UnloadPrototypeScene();
        }

        [UnityTest]
        public IEnumerator AllPiecesSelectableThroughPublicApiAndBuildCameraRaycasting()
        {
            SceneManager.LoadScene(PaintingPrototypeScenePath, LoadSceneMode.Single);
            yield return null;

            var evaluator = FindEvaluator();
            var controller = FindController();
            VerifyManipulationWiring(controller);

            yield return WaitForEvaluation(evaluator, "saved unsolved starting arrangement", r => true);
            var unsolved = evaluator.LatestResult;
            Assert.IsFalse(unsolved.PassesPolicy,
                "The saved starting layout must be materially unsolved; WeightedScore="
                + unsolved.WeightedScore.ToString("F4") + ".");

            SolveAllPieces(controller);
            yield return WaitForEvaluation(evaluator, "all-piece authored recovery",
                r => r.PassesPolicy && r.WeightedScore >= StrongScoreThreshold);
            Assert.IsTrue(evaluator.LatestResult.PassesPolicy
                && evaluator.LatestResult.WeightedScore >= StrongScoreThreshold,
                "Resetting all eight pieces through public APIs must recover the hidden solution.");

            var pieces = new PaintingManipulablePiece[RequiredPieces.Length];
            for (int i = 0; i < RequiredPieces.Length; i++)
                pieces[i] = controller.Pieces[i];

            // Every piece is selectable through the public API, and the
            // selection highlight switches with the selection.
            for (int i = 0; i < pieces.Length; i++)
            {
                controller.SelectPiece(pieces[i]);
                Assert.AreSame(pieces[i], controller.SelectedPiece,
                    "SelectPiece must select " + RequiredPieces[i] + ".");
                Assert.IsTrue(controller.IsSelected, "SelectPiece must leave a piece selected.");
                Assert.IsTrue(AnyRendererHasPropertyBlock(pieces[i]),
                    "Selecting " + RequiredPieces[i] + " must highlight its renderers.");
                if (i > 0)
                    Assert.IsFalse(AnyRendererHasPropertyBlock(pieces[i - 1]),
                        "Selecting another piece must clear the previous highlight.");

                var compositionCamera = GameObject.Find("Composition Camera").GetComponent<Camera>();
                Vector3 towardUpperRight = (compositionCamera.transform.right
                    + compositionCamera.transform.up) * 50f;
                Assert.IsTrue(controller.TryTranslate(towardUpperRight),
                    RequiredPieces[i] + " must reach the upper-right canvas corner.");
                Vector3 upperRight = compositionCamera.WorldToViewportPoint(pieces[i].Root.position);
                Assert.AreEqual(controller.CompositionViewportBounds.xMax, upperRight.x, 0.0001f,
                    RequiredPieces[i] + " must reach the common right canvas edge.");
                Assert.AreEqual(controller.CompositionViewportBounds.yMax, upperRight.y, 0.0001f,
                    RequiredPieces[i] + " must reach the common upper canvas edge.");
                Assert.IsTrue(controller.Undo(), "Undo must restore " + RequiredPieces[i] + ".");
                Assert.IsTrue(controller.TryTranslate(-towardUpperRight),
                    RequiredPieces[i] + " must reach the lower-left canvas corner.");
                Vector3 lowerLeft = compositionCamera.WorldToViewportPoint(pieces[i].Root.position);
                Assert.AreEqual(controller.CompositionViewportBounds.xMin, lowerLeft.x, 0.0001f,
                    RequiredPieces[i] + " must reach the common left canvas edge.");
                Assert.AreEqual(controller.CompositionViewportBounds.yMin, lowerLeft.y, 0.0001f,
                    RequiredPieces[i] + " must reach the common lower canvas edge.");
                Assert.IsTrue(controller.Undo(), "Undo must restore " + RequiredPieces[i] + ".");
            }
            controller.DeselectPiece();
            Assert.IsFalse(controller.IsSelected, "DeselectPiece must clear the selection.");
            Assert.IsNull(controller.SelectedPiece, "DeselectPiece must clear the selected piece.");
            Assert.IsFalse(AnyRendererHasPropertyBlock(pieces[pieces.Length - 1]),
                "Deselecting must restore the property blocks.");

            // SolveAllPieces just moved every static collider; sync the
            // physics scene so the Build Camera raycasts below deterministically
            // see the solved poses instead of whatever the last physics step
            // cached.
            Physics.SyncTransforms();

            // Build Camera raycasting: a ray toward a visible point of each
            // piece resolves to that piece. The Far Mountain sits behind the
            // Middle Mountain from the Build Camera's three-quarter view, so
            // that ray resolves to the nearest
            // visible configured piece instead — the deterministic overlap
            // priority of the pointer selection.
            for (int i = 0; i < pieces.Length; i++)
            {
                var resolved = ResolveFromBuildCamera(controller, BuildCameraAimPoint(pieces[i], i));
                Assert.IsNotNull(resolved,
                    "A Build Camera ray toward " + RequiredPieces[i] + " must hit a configured piece.");
                if (i == FarMountainIndex)
                    Assert.AreSame(pieces[MiddleMountainIndex], resolved,
                        "A Build Camera ray toward occluded " + RequiredPieces[i]
                        + " must resolve to the nearest visible piece (the Middle Mountain).");
                else
                    Assert.AreSame(pieces[i], resolved,
                        "A Build Camera ray toward " + RequiredPieces[i] + " must resolve to it.");
            }

            yield return UnloadPrototypeScene();
        }

        [UnityTest]
        public IEnumerator TranslateAndDepthOnOtherPiecesReduceOwnMetricsThenUndoRestoresHistoricalPiece()
        {
            SceneManager.LoadScene(PaintingPrototypeScenePath, LoadSceneMode.Single);
            yield return null;

            var evaluator = FindEvaluator();
            var controller = FindController();
            VerifyManipulationWiring(controller);
            SolveAllPieces(controller);

            yield return WaitForEvaluation(evaluator, "other-piece baseline solved arrangement", r => true);
            var baseline = evaluator.LatestResult;
            Assert.IsTrue(baseline.PassesPolicy && baseline.WeightedScore >= StrongScoreThreshold,
                "The baseline solved arrangement must pass with a strong score; WeightedScore="
                + baseline.WeightedScore.ToString("F4") + ".");

            var rock = controller.Pieces[ForegroundRockIndex];
            var pavilion = controller.Pieces[PavilionIndex];
            var rockRoot = rock.Root;
            var rockAuthoredPosition = rockRoot.position;
            var rockAuthoredRotation = rockRoot.rotation;
            var rockAuthoredLocalScale = rockRoot.localScale;

            // Select the Foreground Rock through the public API and translate
            // it far enough for a clear composition error.
            controller.SelectPiece(rock);
            Assert.AreSame(rock, controller.SelectedPiece, "SelectPiece must select the Foreground Rock.");
            Assert.IsTrue(controller.IsSelected, "SelectPiece must leave a piece selected.");
            Assert.IsTrue(controller.TryTranslate(new Vector3(1.6f, 0f, 0f)),
                "A bounded world translation of the Foreground Rock must succeed.");
            Assert.AreNotEqual(rockAuthoredPosition, rockRoot.position,
                "A successful translation must move the Foreground Rock.");
            Assert.AreEqual(rockAuthoredLocalScale, rockRoot.localScale,
                "A translation must never change scale.");

            // The translated rock must fail clearly: its own piece IoU must
            // drop materially.
            yield return WaitForEvaluation(evaluator, "translated rock arrangement",
                r => r.Pieces[ForegroundRockIndex].IoU < 0.5f);
            var moved = evaluator.LatestResult;
            Assert.Less(moved.Pieces[ForegroundRockIndex].IoU, baseline.Pieces[ForegroundRockIndex].IoU - 0.3f,
                "Translating the Foreground Rock must materially lower its own piece IoU (baseline "
                + baseline.Pieces[ForegroundRockIndex].IoU.ToString("F4") + " -> moved "
                + moved.Pieces[ForegroundRockIndex].IoU.ToString("F4") + ").");

            // Switch selection to the Pavilion: the highlight must move with
            // the selection and the previous piece must keep its moved pose.
            controller.SelectPiece(pavilion);
            Assert.AreSame(pavilion, controller.SelectedPiece,
                "Selecting another piece must switch the selection.");
            Assert.IsTrue(AnyRendererHasPropertyBlock(pavilion),
                "The newly selected piece must be highlighted.");
            Assert.IsFalse(AnyRendererHasPropertyBlock(rock),
                "The previously selected piece highlight must be cleared.");
            Assert.AreNotEqual(rockAuthoredPosition, rockRoot.position,
                "Switching selection must not move either piece.");

            // Undo must restore the piece stored by the rock translation even
            // though the selection has since changed to the Pavilion.
            Assert.IsTrue(controller.Undo(), "Undo after the rock translation must succeed.");
            Assert.IsFalse(controller.CanUndo, "Undo must consume the stored operation.");
            Assert.AreEqual(rockAuthoredPosition, rockRoot.position,
                "Undo must restore the Foreground Rock's authored position after a selection switch.");
            Assert.AreEqual(rockAuthoredRotation, rockRoot.rotation,
                "Undo must restore the Foreground Rock's authored rotation.");
            Assert.AreEqual(rockAuthoredLocalScale, rockRoot.localScale,
                "Undo must restore the Foreground Rock's authored local scale.");
            Assert.AreEqual(pavilion.Root.position, pavilion.AuthoredPosition,
                "Undoing the rock must not touch the Pavilion.");

            // Depth adjustment on the Pavilion reduces its own metric.
            var pavilionAuthoredPosition = pavilion.Root.position;
            var pavilionAuthoredRotation = pavilion.Root.rotation;
            var pavilionAuthoredLocalScale = pavilion.Root.localScale;
            Assert.IsTrue(controller.TryAdjustDepth(-1.5f),
                "A bounded depth adjustment of the Pavilion must succeed.");
            Assert.AreEqual(pavilionAuthoredLocalScale, pavilion.Root.localScale,
                "A depth adjustment must never change scale.");
            Assert.AreNotEqual(pavilionAuthoredPosition, pavilion.Root.position,
                "A successful depth adjustment must move the Pavilion.");

            yield return WaitForEvaluation(evaluator, "depth-adjusted pavilion arrangement",
                r => r.Pieces[PavilionIndex].IoU < 0.75f);
            var adjusted = evaluator.LatestResult;
            Assert.Less(adjusted.Pieces[PavilionIndex].IoU, 0.75f,
                "A depth adjustment must lower the Pavilion's own piece IoU; IoU="
                + adjusted.Pieces[PavilionIndex].IoU.ToString("F4") + ".");
            Assert.IsTrue(controller.CanUndo, "A completed depth operation must be undoable.");

            // Reset restores the exact authored transform of the Pavilion and
            // is itself undoable back to the depth-adjusted pose.
            Assert.IsTrue(controller.ResetToAuthored(), "ResetToAuthored must succeed.");
            Assert.AreEqual(pavilionAuthoredPosition, pavilion.Root.position,
                "Reset must restore the Pavilion's authored position.");
            Assert.AreEqual(pavilionAuthoredRotation, pavilion.Root.rotation,
                "Reset must restore the Pavilion's authored rotation.");
            Assert.AreEqual(pavilionAuthoredLocalScale, pavilion.Root.localScale,
                "Reset must restore the Pavilion's authored local scale.");
            Assert.IsTrue(controller.CanUndo, "Reset itself must be undoable.");
            Assert.IsTrue(controller.Undo(), "Undo after reset must succeed.");
            Assert.IsFalse(controller.CanUndo, "Undo must consume the stored state.");
            Assert.AreNotEqual(pavilionAuthoredPosition, pavilion.Root.position,
                "Undoing a reset must return to the pre-reset (depth-adjusted) pose.");

            controller.DeselectPiece();
            Assert.IsFalse(controller.IsSelected, "DeselectPiece must clear the selection.");
            Assert.IsNull(controller.SelectedPiece, "DeselectPiece must clear the selected piece.");
            Assert.IsFalse(controller.CanUndo, "Deselection must not leave undo state.");

            yield return UnloadPrototypeScene();
        }

        [UnityTest]
        public IEnumerator ConstrainedRotationIsQuantizedClampedAndRecoveredByUndoAndReset()
        {
            SceneManager.LoadScene(PaintingPrototypeScenePath, LoadSceneMode.Single);
            yield return null;

            var evaluator = FindEvaluator();
            var controller = FindController();
            VerifyManipulationWiring(controller);
            SolveAllPieces(controller);

            yield return WaitForEvaluation(evaluator, "rotation baseline solved arrangement", r => true);
            var baseline = evaluator.LatestResult;
            Assert.IsTrue(baseline.PassesPolicy && baseline.WeightedScore >= StrongScoreThreshold,
                "The rotation baseline must pass with a strong score; WeightedScore="
                + baseline.WeightedScore.ToString("F4") + ".");

            var bridge = controller.Bridge;
            var root = bridge.Root;
            var authoredPosition = root.position;
            var authoredRotation = root.rotation;
            var authoredLocalScale = root.localScale;

            controller.SelectPiece();
            Assert.IsTrue(controller.IsSelected, "SelectPiece must select the bridge.");

            // +15 per successful call, quantized to 15-degree increments.
            Assert.IsTrue(controller.TryRotate(15f), "TryRotate(15) must succeed.");
            Assert.AreEqual(15f, bridge.AuthoredSignedYawOffset(root.rotation), 0.01f,
                "TryRotate(15) must rotate exactly 15 degrees around world Y.");
            Assert.AreEqual(authoredPosition, root.position, "Rotation must never move the piece.");
            Assert.AreEqual(authoredLocalScale, root.localScale, "Rotation must never change scale.");
            Assert.IsTrue(controller.CanUndo, "A completed rotation must be undoable.");

            Assert.IsTrue(controller.TryRotate(8f), "TryRotate(8) must succeed.");
            Assert.AreEqual(30f, bridge.AuthoredSignedYawOffset(root.rotation), 0.01f,
                "The yaw offset must quantize to 15-degree increments (15 + 8 -> 30).");

            Assert.IsTrue(controller.TryRotate(-45f), "TryRotate(-45) must succeed.");
            Assert.AreEqual(-15f, bridge.AuthoredSignedYawOffset(root.rotation), 0.01f,
                "The quantized yaw must be -15 degrees.");

            Assert.IsTrue(controller.TryRotate(90f), "TryRotate(90) must succeed through clamping.");
            Assert.AreEqual(45f, bridge.AuthoredSignedYawOffset(root.rotation), 0.01f,
                "The signed yaw offset must clamp at +45 degrees.");

            Assert.IsFalse(controller.TryRotate(15f), "A rotation fully clamped away must fail.");
            Assert.AreEqual(45f, bridge.AuthoredSignedYawOffset(root.rotation), 0.01f,
                "A clamped rotation must not change the yaw.");
            Assert.IsTrue(controller.CanUndo, "A failed rotation must not consume the undo state.");

            Assert.IsTrue(controller.TryRotate(-90f), "TryRotate(-90) must succeed through clamping.");
            Assert.AreEqual(-45f, bridge.AuthoredSignedYawOffset(root.rotation), 0.01f,
                "The signed yaw offset must clamp at -45 degrees.");
            Assert.IsFalse(controller.TryRotate(-15f), "A rotation fully clamped away must fail.");

            // The rotated bridge must fail clearly: total score and the
            // bridge's piece IoU must drop materially.
            yield return WaitForEvaluation(evaluator, "rotated bridge arrangement",
                r => r.Pieces[ArchBridgeIndex].IoU < 0.5f);
            var rotated = evaluator.LatestResult;
            Assert.Less(rotated.WeightedScore, baseline.WeightedScore - MaterialScoreDrop,
                "Rotating the Arch Bridge must materially lower the total score (baseline "
                + baseline.WeightedScore.ToString("F4") + " -> rotated " + rotated.WeightedScore.ToString("F4") + ").");
            Assert.Less(rotated.Pieces[ArchBridgeIndex].IoU, baseline.Pieces[ArchBridgeIndex].IoU - 0.3f,
                "Rotating the Arch Bridge must materially lower its piece IoU (baseline "
                + baseline.Pieces[ArchBridgeIndex].IoU.ToString("F4") + " -> rotated "
                + rotated.Pieces[ArchBridgeIndex].IoU.ToString("F4") + ").");

            // Reset restores the exact authored rotation and the passing score.
            Assert.IsTrue(controller.ResetToAuthored(), "ResetToAuthored must succeed.");
            Assert.AreEqual(authoredRotation, root.rotation,
                "Reset must restore the exact authored rotation.");
            Assert.AreEqual(authoredPosition, root.position, "Reset must restore the authored position.");
            Assert.AreEqual(authoredLocalScale, root.localScale, "Reset must restore the authored local scale.");

            yield return WaitForEvaluation(evaluator, "rotation-reset arrangement",
                r => r.PassesPolicy && r.WeightedScore >= StrongScoreThreshold);
            var recovered = evaluator.LatestResult;
            Assert.IsTrue(recovered.PassesPolicy && recovered.WeightedScore >= StrongScoreThreshold,
                "Resetting the rotation must recover the strong score; WeightedScore="
                + recovered.WeightedScore.ToString("F4") + ".");

            // T-010 uses one-step undo: undoing reset returns to the pose
            // immediately before reset, then consumes the single history slot.
            Assert.IsTrue(controller.Undo(), "Undo after reset must succeed.");
            Assert.AreEqual(-45f, bridge.AuthoredSignedYawOffset(root.rotation), 0.01f,
                "Undoing the reset must return to the rotated pose.");
            Assert.IsFalse(controller.Undo(), "Undo must consume the last stored operation.");
            Assert.IsFalse(controller.CanUndo, "No operation remains to undo.");
            Assert.AreEqual(authoredPosition, root.position, "Undo must leave the authored position.");
            Assert.AreEqual(authoredLocalScale, root.localScale, "Undo must leave the authored local scale.");

            controller.DeselectPiece();
            yield return UnloadPrototypeScene();
        }

        /// <summary>
        /// Recovers the hidden accepted composition exclusively through the
        /// same public per-piece reset path available to gameplay.
        /// </summary>
        private static void SolveAllPieces(PaintingManipulationController controller)
        {
            for (int i = 0; i < controller.Pieces.Count; i++)
            {
                PaintingManipulablePiece piece = controller.Pieces[i];
                controller.SelectPiece(piece);
                Assert.IsTrue(controller.ResetToAuthored(),
                    "ResetToAuthored must solve " + RequiredPieces[i] + ".");
                Assert.AreEqual(piece.AuthoredPosition, piece.Root.position,
                    RequiredPieces[i] + " must recover its exact authored position.");
                Assert.AreEqual(piece.AuthoredRotation, piece.Root.rotation,
                    RequiredPieces[i] + " must recover its exact authored rotation.");
                Assert.AreEqual(piece.AuthoredLocalScale, piece.Root.localScale,
                    RequiredPieces[i] + " must preserve its authored scale.");
            }
            controller.DeselectPiece();
        }

        /// <summary>
        /// Verifies the T-010B/T-010C scene wiring: one controller that
        /// self-configured from the saved scene, exactly eight manipulable
        /// handles and eight enabled selection colliders — one per piece root
        /// in the required order — with the authored transform captured, all
        /// roots on the PaintingPiece layer with no Rigidbody and their
        /// renderer children still on Default, and the bridge compatibility
        /// reference resolved explicitly. The scene starts at the authored
        /// unsolved layout, so every root must be displaced from its
        /// authored (solved) position while its local scale stays authored.
        /// </summary>
        private static void VerifyManipulationWiring(PaintingManipulationController controller)
        {
            Assert.IsTrue(controller.IsConfigured,
                "PaintingManipulationController must self-configure from the saved scene wiring.");
            Assert.IsNotNull(controller.Bridge, "The controller must reference the bridge piece.");
            Assert.IsTrue(controller.Bridge.IsConfigured,
                "The bridge PaintingManipulablePiece must be configured (authored transform captured).");
            Assert.AreEqual(RequiredPieces.Length, controller.Pieces.Count,
                "The controller must reference exactly " + RequiredPieces.Length + " ordered pieces.");
            Assert.IsFalse(controller.CanUndo, "No operation has run yet; CanUndo must be false.");
            Assert.IsFalse(controller.IsSelected, "Nothing is selected at load; IsSelected must be false.");
            Assert.IsNull(controller.SelectedPiece, "Nothing is selected at load; SelectedPiece must be null.");

            var scenery = GameObject.Find("Solved Scenery");
            Assert.IsNotNull(scenery, "Solved Scenery missing.");
            Assert.AreEqual(RequiredPieces.Length, scenery.transform.childCount,
                "Solved Scenery must contain exactly " + RequiredPieces.Length + " piece roots.");

            int layer = LayerMask.NameToLayer("PaintingPiece");
            Assert.AreNotEqual(-1, layer, "The project must define a 'PaintingPiece' layer.");

            for (int i = 0; i < RequiredPieces.Length; i++)
            {
                var pieceRoot = scenery.transform.Find(RequiredPieces[i]);
                Assert.IsNotNull(pieceRoot, "Solved Scenery/" + RequiredPieces[i] + " missing.");

                var handles = pieceRoot.GetComponents<PaintingManipulablePiece>();
                Assert.AreEqual(1, handles.Length,
                    RequiredPieces[i] + " must carry exactly one PaintingManipulablePiece.");
                Assert.AreSame(controller.Pieces[i], handles[0],
                    "Controller piece " + i + " must be Solved Scenery/" + RequiredPieces[i] + ".");
                Assert.IsTrue(handles[0].IsConfigured,
                    RequiredPieces[i] + " PaintingManipulablePiece must be configured.");
                Assert.AreEqual(layer, pieceRoot.gameObject.layer,
                    RequiredPieces[i] + " must be on the 'PaintingPiece' layer.");
                Assert.IsNull(pieceRoot.GetComponent<Rigidbody>(),
                    RequiredPieces[i] + " must carry no Rigidbody.");

                var colliders = pieceRoot.GetComponents<Collider>();
                Assert.AreEqual(1, colliders.Length,
                    RequiredPieces[i] + " must carry exactly one selection collider.");
                Assert.IsInstanceOf<BoxCollider>(colliders[0],
                    RequiredPieces[i] + " selection collider must be a BoxCollider.");
                Assert.IsTrue(colliders[0].enabled,
                    RequiredPieces[i] + " selection collider must be enabled.");

                for (int c = 0; c < pieceRoot.childCount; c++)
                    Assert.AreEqual(0, pieceRoot.GetChild(c).gameObject.layer,
                        RequiredPieces[i] + " renderer children must stay on the Default layer.");

                Assert.IsTrue(pieceRoot.position != handles[0].AuthoredPosition
                    || pieceRoot.rotation != handles[0].AuthoredRotation,
                    RequiredPieces[i] + " must start away from its hidden authored solution.");
                Assert.AreEqual(pieceRoot.localScale, handles[0].AuthoredLocalScale,
                    RequiredPieces[i] + " start layout must preserve authored local scale.");
            }

            Assert.AreSame(controller.Pieces[ArchBridgeIndex], controller.Bridge,
                "The compatibility Bridge helper must resolve to the Arch Bridge piece.");
        }

        /// <summary>
        /// A point on each piece's selection collider that is visible from the
        /// fixed Build Camera: the Middle Mountain is aimed above the
        /// Pavilion's cover, the Arch Bridge on its right side (the Foreground
        /// Rock overlaps its center), everything else at the collider center.
        /// </summary>
        private static Vector3 BuildCameraAimPoint(PaintingManipulablePiece piece, int index)
        {
            Bounds bounds = piece.SelectionCollider.bounds;
            if (index == MiddleMountainIndex)
                return new Vector3(bounds.center.x, bounds.max.y - 0.1f, bounds.center.z);
            if (index == ArchBridgeIndex)
                return new Vector3(bounds.max.x - 0.2f, bounds.center.y, bounds.center.z);
            return bounds.center;
        }

        /// <summary>
        /// Replicates the pointer-selection rule of the controller: casts the
        /// Build Camera ray against the PaintingPiece layer and resolves the
        /// closest valid configured piece collider, ignoring hits not
        /// belonging directly to a configured piece.
        /// </summary>
        private static PaintingManipulablePiece ResolveFromBuildCamera(
            PaintingManipulationController controller, Vector3 aimPoint)
        {
            var buildCamera = GameObject.Find("Build Camera").GetComponent<Camera>();
            int layer = LayerMask.NameToLayer("PaintingPiece");
            Ray ray = new Ray(buildCamera.transform.position, (aimPoint - buildCamera.transform.position).normalized);
            RaycastHit[] hits = Physics.RaycastAll(ray, 100f, 1 << layer);

            float closest = float.PositiveInfinity;
            PaintingManipulablePiece resolved = null;
            foreach (RaycastHit hit in hits)
            {
                PaintingManipulablePiece piece = null;
                foreach (var candidate in controller.Pieces)
                {
                    if (candidate.SelectionCollider == hit.collider)
                    {
                        piece = candidate;
                        break;
                    }
                }
                if (piece == null)
                    continue;
                if (hit.distance < closest)
                {
                    closest = hit.distance;
                    resolved = piece;
                }
            }
            return resolved;
        }

        /// <summary>True when any cached renderer below the piece currently carries a property block (the selection highlight).</summary>
        private static bool AnyRendererHasPropertyBlock(PaintingManipulablePiece piece)
        {
            foreach (var renderer in piece.Renderers)
            {
                if (renderer != null && renderer.HasPropertyBlock())
                    return true;
            }
            return false;
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
        /// Finds the single manipulation controller the scene must deserialize
        /// and wire from its saved references.
        /// </summary>
        private static PaintingManipulationController FindController()
        {
            var controller = UnityEngine.Object.FindFirstObjectByType<PaintingManipulationController>();
            Assert.IsNotNull(controller, "PaintingManipulationController missing from " + PaintingPrototypeScenePath + ".");
            return controller;
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

        private static IEnumerator UnloadPrototypeScene()
        {
            var prototypeScene = SceneManager.GetActiveScene();
            var cleanupScene = SceneManager.CreateScene("T010BCleanup");
            SceneManager.SetActiveScene(cleanupScene);
            yield return SceneManager.UnloadSceneAsync(prototypeScene);
        }
    }
}
