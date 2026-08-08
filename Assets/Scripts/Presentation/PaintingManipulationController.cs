using System;
using System.Collections.Generic;
using UnityEngine;

namespace PerspectivePuzzle.Presentation
{
    /// <summary>
    /// T-010G1: turns pointer and keyboard input into one physical placement
    /// transaction for the ordered <see cref="PaintingManipulablePiece"/>s
    /// wired by the Painting Prototype builder. All pieces rest on one
    /// horizontal surface at <see cref="SurfaceY"/>. A left-mouse press on a
    /// configured piece root collider picks the piece up; while carrying, the
    /// pointer ray is intersected with the horizontal plane at SurfaceY and
    /// the landing candidate is driven by the cursor with the grab offset
    /// preserved, while the actual piece follows the lifted target
    /// (candidate + <see cref="Vector3.up"/> * lift height) with
    /// <see cref="Vector3.SmoothDamp"/> so it feels weighted, not dead-
    /// attached. A persistent translucent ghost preview
    /// (<see cref="PaintingPlacementPreview"/>) clones the piece's visible
    /// meshes at the candidate pose and uses the configured valid/invalid
    /// materials. The candidate is valid only when the piece's root
    /// BoxCollider XZ footprint at that pose lies fully inside the shared
    /// world-XZ placement rectangle (touching within a small epsilon is
    /// allowed) and does not overlap another configured piece's root
    /// collider; the carried piece itself is ignored, and no Rigidbody or
    /// physics simulation is used. Releasing with a valid candidate settles
    /// from the lifted pose to the candidate over the configured settle
    /// duration with smooth easing and a restrained vertical bounce, then
    /// stores one undo step restoring the exact pre-pickup pose; releasing
    /// with an invalid candidate (or pressing Escape while carrying) settles
    /// back to the exact pre-pickup pose and restores the undo state that
    /// existed before the pickup, so an invalid placement never consumes or
    /// replaces history. Input is ignored while settling. Q/E rotate the
    /// carried root by exactly the configured step with the same
    /// quantization and clamp as the selected-piece rotation and refresh
    /// preview validity; outside a pickup the existing manipulation stays
    /// (Z undo, R reset, Q/E rotate, Escape deselect). The public T-010
    /// compatibility APIs (<see cref="Pieces"/>, <see cref="Bridge"/>,
    /// <see cref="SelectedPiece"/>, <see cref="SelectPiece()"/>,
    /// <see cref="DeselectPiece"/>, <see cref="TryTranslate"/>,
    /// <see cref="TryAdjustDepth"/>, <see cref="TryRotate"/>,
    /// <see cref="Undo"/>, <see cref="ResetToAuthored"/>) are preserved and
    /// keep their direct deterministic paths; only pointer placement uses the
    /// physical transaction, and the Composition Camera is retained only for
    /// that public compatibility. Read-only diagnostics
    /// (<see cref="IsCarrying"/>, <see cref="IsSettling"/>,
    /// <see cref="PlacementCandidate"/>, <see cref="IsPlacementCandidateValid"/>,
    /// <see cref="PlacementRectangle"/>, <see cref="SurfaceY"/>) and the
    /// public transaction helpers (<see cref="BeginPlacement"/>,
    /// <see cref="UpdatePlacementTarget"/>, <see cref="ReleasePlacement"/>,
    /// <see cref="CancelPlacement"/>) let PlayMode tests drive the exact same
    /// transaction without synthetic mouse input. On disable/destroy the
    /// preview is destroyed and a carried piece is restored safely; no
    /// runtime objects or material instances leak. Uses the existing legacy
    /// Input API; no package or tween changes.
    /// </summary>
    public sealed class PaintingManipulationController : MonoBehaviour
    {
        private const float MaxRayDistance = 100f;

        /// <summary>Overlap/containment tolerance in world units; touching within this is allowed.</summary>
        private const float PlacementEpsilon = 0.01f;

        /// <summary>Peak amplitude of the restrained vertical landing bounce, in world units.</summary>
        private const float LandingBounceAmplitude = 0.1f;

        [SerializeField] private Camera _buildCamera;
        [SerializeField] private Camera _compositionCamera;
        [SerializeField] private PaintingManipulablePiece[] _pieces = Array.Empty<PaintingManipulablePiece>();
        [SerializeField] private PaintingManipulablePiece _bridge;
        [SerializeField] private LayerMask _selectionMask;
        [SerializeField] private Bounds _movementBounds;
        [SerializeField] private Rect _compositionViewportBounds = new Rect(0.05f, 0.05f, 0.9f, 0.9f);
        [SerializeField] private Vector2 _compositionDepthRange = new Vector2(4.5f, 12f);
        [SerializeField, Min(0.01f)] private float _wheelSensitivity = 0.25f;
        [SerializeField, Min(0.05f)] private float _wheelBurstWindowSeconds = 0.45f;
        [SerializeField, Min(1f)] private float _rotationStepDegrees = 15f;
        [SerializeField, Min(1f)] private float _maxRotationOffsetDegrees = 45f;

        // Physical placement configuration: shared world-XZ rectangle, surface
        // Y, pickup lift, follow/settle timings and preview materials.
        [SerializeField] private Rect _placementRectangle;
        [SerializeField] private float _surfaceY;
        [SerializeField, Min(0f)] private float _liftHeight = 0.6f;
        [SerializeField, Min(0.01f)] private float _followSmoothTime = 0.08f;
        [SerializeField, Min(0.01f)] private float _settleDuration = 0.25f;
        [SerializeField] private Material _validPreviewMaterial;
        [SerializeField] private Material _invalidPreviewMaterial;

        private bool _configured;
        private bool _placementConfigured;
        private PaintingManipulablePiece _selectedPiece;
        private PieceOperationState? _undoState;
        private float _depthOffset;
        private float _lastDepthChangeTime;

        // Pickup -> preview -> release transaction state.
        private PaintingManipulablePiece _carriedPiece;
        private bool _settling;
        private bool _settleLanding;
        private float _settleTimer;
        private Vector3 _settleFrom;
        private Vector3 _settleTo;
        private Quaternion _settleFromRotation;
        private Vector3 _candidate;
        private Vector3 _grabOffset;
        private Vector3 _carryVelocity;
        private Vector3 _pickupPosition;
        private Quaternion _pickupRotation;
        private Vector3 _pickupLocalScale;
        private float _pickupDepthOffset;
        private PieceOperationState? _prePickupUndoState;
        private Plane _surfacePlane;
        private PaintingPlacementPreview _placementPreview;

        /// <summary>True once <see cref="Configure"/> succeeded; all input and manipulation is gated on this.</summary>
        public bool IsConfigured => _configured;

        /// <summary>True while a piece is selected.</summary>
        public bool IsSelected => _configured && _selectedPiece != null;

        /// <summary>True while one completed operation can be undone.</summary>
        public bool CanUndo => _configured && _undoState.HasValue;

        /// <summary>True once the physical placement was configured (rectangle, surface, timings, materials).</summary>
        public bool IsPlacementConfigured => _configured && _placementConfigured;

        /// <summary>True while a piece is picked up and the landing candidate is being previewed.</summary>
        public bool IsCarrying => _carriedPiece != null && !_settling;

        /// <summary>True while a release/return settle animation runs; all input is ignored.</summary>
        public bool IsSettling => _settling;

        /// <summary>
        /// Current landing candidate in world space; its Y is always
        /// <see cref="SurfaceY"/> while carrying. Holds the last value when
        /// idle.
        /// </summary>
        public Vector3 PlacementCandidate => _candidate;

        /// <summary>True when the current candidate passes the rectangle and overlap checks.</summary>
        public bool IsPlacementCandidateValid => IsCarrying && ComputePlacementValidity(_carriedPiece, _candidate);

        /// <summary>Shared world-XZ placement rectangle; x maps to world X, y to world Z.</summary>
        public Rect PlacementRectangle => _placementRectangle;

        /// <summary>The horizontal surface Y every piece ultimately rests on.</summary>
        public float SurfaceY => _surfaceY;

        /// <summary>The Arch Bridge piece kept as the explicit compatibility reference; null until configured.</summary>
        public PaintingManipulablePiece Bridge => _bridge;

        /// <summary>The currently selected piece; null when unconfigured or nothing is selected.</summary>
        public PaintingManipulablePiece SelectedPiece => _configured ? _selectedPiece : null;

        /// <summary>The ordered configured manipulable pieces.</summary>
        public IReadOnlyList<PaintingManipulablePiece> Pieces => _pieces;

        /// <summary>Authored world-space movement bounds every compatibility translation is clamped to.</summary>
        public Bounds MovementBounds => _movementBounds;

        /// <summary>Shared normalized Composition Camera canvas (compatibility property).</summary>
        public Rect CompositionViewportBounds => _compositionViewportBounds;

        /// <summary>Shared absolute Composition Camera depth interval (compatibility property).</summary>
        public Vector2 CompositionDepthRange => _compositionDepthRange;

        private void Awake()
        {
            // Convenience path for scenes wired by the deterministic builder:
            // fully serialized references configure at startup; tests and the
            // builder can also call Configure/ConfigurePlacement explicitly
            // with any values. Placement is wired only when its serialized
            // fields are present, so existing scenes degrade to
            // selection-only pointer input.
            if (_buildCamera != null && _compositionCamera != null && _pieces != null && _pieces.Length > 0
                && _bridge != null && _selectionMask.value != 0
                && IsValidViewportBounds(_compositionViewportBounds)
                && IsValidDepthRange(_compositionDepthRange) && _wheelSensitivity > 0f
                && _rotationStepDegrees > 0f && _maxRotationOffsetDegrees > 0f)
            {
                Configure(_buildCamera, _compositionCamera, _pieces, _bridge, _selectionMask,
                    _movementBounds, _compositionViewportBounds, _compositionDepthRange,
                    _wheelSensitivity, _wheelBurstWindowSeconds,
                    _rotationStepDegrees, _maxRotationOffsetDegrees);

                if (_placementRectangle.width > 0f && _placementRectangle.height > 0f
                    && _liftHeight >= 0f && _followSmoothTime > 0f && _settleDuration > 0f
                    && _validPreviewMaterial != null && _invalidPreviewMaterial != null)
                {
                    ConfigurePlacement(_placementRectangle, _surfaceY, _liftHeight,
                        _followSmoothTime, _settleDuration, _validPreviewMaterial, _invalidPreviewMaterial);
                }
            }
        }

        private void OnDisable()
        {
            CleanupCarry();
        }

        private void OnDestroy()
        {
            CleanupCarry();
        }

        private void Update()
        {
            if (!_configured || !isActiveAndEnabled)
                return;

            if (_settling)
            {
                // Input is ignored while settling; only the animation moves
                // the piece, and no destructive state changes happen.
                UpdateSettle();
                return;
            }

            if (_carriedPiece != null)
            {
                if (Input.GetMouseButtonUp(0))
                    ReleasePlacement();
                else if (Input.GetKeyDown(KeyCode.Escape))
                    CancelPlacement();
                else if (Input.GetMouseButton(0))
                    UpdateCarryFromPointer();

                if (Input.GetKeyDown(KeyCode.Q))
                    TryRotate(-_rotationStepDegrees);
                else if (Input.GetKeyDown(KeyCode.E))
                    TryRotate(_rotationStepDegrees);
            }
            else
            {
                if (Input.GetMouseButtonDown(0))
                {
                    if (TryHitPiece(out PaintingManipulablePiece piece, out _))
                    {
                        // A press on a configured root collider begins the
                        // pickup, preserving the grab offset from the press
                        // ray; without placement configuration it is a
                        // selection-only click.
                        if (BeginPlacement(piece))
                            CapturePointerGrabOffset();
                        else
                            SelectPiece(piece);
                    }
                    else
                    {
                        DeselectPiece();
                    }
                }
                else if (Input.GetKeyDown(KeyCode.Z))
                    Undo();
                else if (Input.GetKeyDown(KeyCode.R))
                    ResetToAuthored();
                if (Input.GetKeyDown(KeyCode.Q))
                    TryRotate(-_rotationStepDegrees);
                else if (Input.GetKeyDown(KeyCode.E))
                    TryRotate(_rotationStepDegrees);
                if (Input.GetKeyDown(KeyCode.Escape))
                    DeselectPiece();
            }

            if (_carriedPiece != null && !_settling)
                UpdateCarry();
        }

        /// <summary>
        /// Wires the controller to the Build Camera, the Composition Camera
        /// (retained only for scoring-compatible depth direction and public
        /// compatibility), the ordered nonempty manipulable pieces plus an
        /// explicit Arch Bridge compatibility reference, the selectable layer
        /// mask, the authored world-space movement bounds, the max signed
        /// depth offset and the rotation step/clamp, then resets all
        /// interaction state. Validates that every piece is configured and
        /// unique, that the movement bounds contain every piece's collider
        /// and that the mask includes every piece's layer, so a miswired
        /// scene fails loudly at configuration time. The physical placement
        /// configuration is wired separately by
        /// <see cref="ConfigurePlacement"/> (the Awake convenience path wires
        /// it from serialized fields).
        /// </summary>
        public void Configure(
            Camera buildCamera,
            Camera compositionCamera,
            PaintingManipulablePiece[] pieces,
            PaintingManipulablePiece bridge,
            LayerMask selectionMask,
            Bounds movementBounds,
            Rect compositionViewportBounds,
            Vector2 compositionDepthRange,
            float wheelSensitivity,
            float wheelBurstWindowSeconds,
            float rotationStepDegrees,
            float maxRotationOffsetDegrees)
        {
            if (buildCamera == null)
                throw new ArgumentNullException(nameof(buildCamera));
            if (compositionCamera == null)
                throw new ArgumentNullException(nameof(compositionCamera));
            if (pieces == null)
                throw new ArgumentNullException(nameof(pieces));
            if (pieces.Length == 0)
                throw new ArgumentException("At least one manipulable piece is required.", nameof(pieces));
            if (bridge == null)
                throw new ArgumentNullException(nameof(bridge));
            if (selectionMask.value == 0)
                throw new ArgumentException("The selection mask must select at least one layer.", nameof(selectionMask));
            if (!IsValidViewportBounds(compositionViewportBounds))
                throw new ArgumentOutOfRangeException(nameof(compositionViewportBounds), compositionViewportBounds,
                    "Composition viewport bounds must be a positive rectangle inside normalized viewport space.");
            if (!IsValidDepthRange(compositionDepthRange))
                throw new ArgumentOutOfRangeException(nameof(compositionDepthRange), compositionDepthRange,
                    "Composition depth range must be positive and ordered.");
            if (wheelSensitivity <= 0f)
                throw new ArgumentOutOfRangeException(nameof(wheelSensitivity), wheelSensitivity, "Wheel sensitivity must be positive.");
            if (wheelBurstWindowSeconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(wheelBurstWindowSeconds), wheelBurstWindowSeconds, "Wheel burst window must be positive.");
            if (rotationStepDegrees <= 0f)
                throw new ArgumentOutOfRangeException(nameof(rotationStepDegrees), rotationStepDegrees, "Rotation step must be positive.");
            if (maxRotationOffsetDegrees <= 0f)
                throw new ArgumentOutOfRangeException(nameof(maxRotationOffsetDegrees), maxRotationOffsetDegrees, "Max rotation offset must be positive.");

            for (int i = 0; i < pieces.Length; i++)
            {
                PaintingManipulablePiece piece = pieces[i];
                if (piece == null)
                    throw new ArgumentException("Piece " + i + " must not be null.", nameof(pieces));
                if (!piece.IsConfigured)
                    throw new InvalidOperationException(
                        $"PaintingManipulationController requires configured pieces; configure '{piece.name}' first.");
                for (int j = 0; j < i; j++)
                {
                    if (pieces[j] == piece)
                        throw new ArgumentException($"Piece '{piece.name}' appears more than once.", nameof(pieces));
                }

                piece.EnsureCached();
                Bounds colliderBounds = piece.SelectionCollider.bounds;
                if (!movementBounds.Contains(colliderBounds.min) || !movementBounds.Contains(colliderBounds.max))
                    throw new ArgumentException(
                        "Movement bounds must contain the authored '" + piece.name + "' collider.", nameof(movementBounds));
                if ((selectionMask.value & (1 << piece.gameObject.layer)) == 0)
                    throw new ArgumentException(
                        "The selection mask must include piece '" + piece.name + "' layer.", nameof(selectionMask));
            }

            if (Array.IndexOf(pieces, bridge) < 0)
                throw new ArgumentException("The bridge piece must be one of the configured manipulable pieces.", nameof(bridge));

            _buildCamera = buildCamera;
            _compositionCamera = compositionCamera;
            _pieces = pieces;
            _bridge = bridge;
            _selectionMask = selectionMask;
            _movementBounds = movementBounds;
            _compositionViewportBounds = compositionViewportBounds;
            _compositionDepthRange = compositionDepthRange;
            _wheelSensitivity = wheelSensitivity;
            _wheelBurstWindowSeconds = wheelBurstWindowSeconds;
            _rotationStepDegrees = rotationStepDegrees;
            _maxRotationOffsetDegrees = maxRotationOffsetDegrees;

            // Start from a clean interaction state.
            if (_selectedPiece != null)
                _selectedPiece.SetSelected(false);
            _selectedPiece = null;
            CleanupCarry();
            _depthOffset = 0f;
            _undoState = null;
            _lastDepthChangeTime = float.NegativeInfinity;
            _candidate = Vector3.zero;
            _placementConfigured = false;
            _configured = true;
        }

        /// <summary>
        /// Wires the physical placement configuration: the shared world-XZ
        /// placement rectangle (x maps to world X, y to world Z), the
        /// horizontal surface Y all pieces rest on, the pickup lift height,
        /// the SmoothDamp follow time, the settle duration and the valid/
        /// invalid preview materials. Must be called after
        /// <see cref="Configure"/>; any active carry or settle is safely
        /// cleaned up first.
        /// </summary>
        public void ConfigurePlacement(
            Rect placementRectangle,
            float surfaceY,
            float liftHeight,
            float followSmoothTime,
            float settleDuration,
            Material validPreviewMaterial,
            Material invalidPreviewMaterial)
        {
            if (!_configured)
                throw new InvalidOperationException("Placement must be configured after the manipulation controller itself.");
            if (placementRectangle.width <= 0f || placementRectangle.height <= 0f)
                throw new ArgumentOutOfRangeException(nameof(placementRectangle), placementRectangle,
                    "The placement rectangle must have positive width and height.");
            if (liftHeight < 0f)
                throw new ArgumentOutOfRangeException(nameof(liftHeight), liftHeight, "The pickup lift height must not be negative.");
            if (followSmoothTime <= 0f)
                throw new ArgumentOutOfRangeException(nameof(followSmoothTime), followSmoothTime, "The follow smooth time must be positive.");
            if (settleDuration <= 0f)
                throw new ArgumentOutOfRangeException(nameof(settleDuration), settleDuration, "The settle duration must be positive.");
            if (validPreviewMaterial == null)
                throw new ArgumentNullException(nameof(validPreviewMaterial));
            if (invalidPreviewMaterial == null)
                throw new ArgumentNullException(nameof(invalidPreviewMaterial));

            CleanupCarry();
            _placementRectangle = placementRectangle;
            _surfaceY = surfaceY;
            _liftHeight = liftHeight;
            _followSmoothTime = followSmoothTime;
            _settleDuration = settleDuration;
            _validPreviewMaterial = validPreviewMaterial;
            _invalidPreviewMaterial = invalidPreviewMaterial;
            _surfacePlane = new Plane(Vector3.up, new Vector3(0f, surfaceY, 0f));
            _placementConfigured = true;
        }

        /// <summary>Selects the Arch Bridge piece (compatibility helper for the bridge-focused tests).</summary>
        public void SelectPiece()
        {
            if (!_configured || _carriedPiece != null || _settling)
                return;
            SelectPiece(_bridge);
        }

        /// <summary>
        /// Selects one configured piece, applying its selected visual and
        /// clearing the previous selection; no transform change. Ignored
        /// during an active carry or settle. Throws when the piece is not one
        /// of the configured pieces.
        /// </summary>
        public void SelectPiece(PaintingManipulablePiece piece)
        {
            if (piece == null)
                throw new ArgumentNullException(nameof(piece));
            if (!_configured || _carriedPiece != null || _settling)
                return;
            if (Array.IndexOf(_pieces, piece) < 0)
                throw new ArgumentException("The piece must be one of the configured manipulable pieces.", nameof(piece));
            if (_selectedPiece == piece)
                return;

            if (_selectedPiece != null)
                _selectedPiece.SetSelected(false);
            _selectedPiece = piece;
            piece.SetSelected(true);
            // A new selection starts a fresh depth burst so the first wheel
            // tick opens its own undoable operation.
            _lastDepthChangeTime = float.NegativeInfinity;
            SynchronizeDepthOffset();
        }

        /// <summary>Deselects the current piece, restoring its visual. Ignored during an active carry or settle.</summary>
        public void DeselectPiece()
        {
            if (!_configured || _carriedPiece != null || _settling)
                return;
            if (_selectedPiece != null)
            {
                _selectedPiece.SetSelected(false);
                _selectedPiece = null;
            }
        }

        /// <summary>
        /// Direct bounded world translation of the selected piece: moves it by
        /// <paramref name="worldDelta"/>, clamped to the movement bounds, and
        /// stores one undoable operation before the move (only when the move
        /// actually changes the pose). Shares the clamp/history path with the
        /// public test APIs, so tests exercise the same behavior without
        /// synthesizing input. When nothing is selected the Arch Bridge is
        /// selected first (T-010A compatibility). Returns false when
        /// unconfigured, while a carry or settle is active, or the move is
        /// fully clamped away.
        /// </summary>
        public bool TryTranslate(Vector3 worldDelta)
        {
            if (!_configured || _carriedPiece != null || _settling)
                return false;
            if (_selectedPiece == null)
                SelectPiece(_bridge); // T-010A compatibility: operations default to the Arch Bridge

            Vector3 current = _selectedPiece.Root.position;
            Vector3 target = ClampPosition(current + worldDelta);
            if (target == current)
                return false;

            BeginUndoableOperation();
            _selectedPiece.Root.position = target;
            SynchronizeDepthOffset();
            return true;
        }

        /// <summary>
        /// Direct bounded depth adjustment of the selected piece: moves it
        /// along the Composition Camera forward vector by
        /// <paramref name="signedDelta"/> world units (positive = along
        /// forward, negative = toward the camera), clamped to the max signed
        /// authored depth offset and the movement bounds relative to the
        /// piece's own authored transform. The piece's scale is never
        /// touched. A short burst of wheel input is one undoable operation:
        /// the pre-burst state is stored once and later ticks extend the
        /// operation instead of overwriting the undo state every frame. When
        /// nothing is selected the Arch Bridge is selected first (T-010A
        /// compatibility). Returns false when unconfigured, while a carry or
        /// settle is active, or the adjustment is fully clamped away.
        /// </summary>
        public bool TryAdjustDepth(float signedDelta)
        {
            if (!_configured || _carriedPiece != null || _settling)
                return false;
            if (Mathf.Approximately(signedDelta, 0f))
                return false;
            if (_selectedPiece == null)
                SelectPiece(_bridge); // T-010A compatibility: operations default to the Arch Bridge

            SynchronizeDepthOffset();
            float newDepth = Mathf.Clamp(
                _depthOffset + signedDelta, _compositionDepthRange.x, _compositionDepthRange.y);
            if (Mathf.Approximately(newDepth, _depthOffset))
                return false;

            bool burstActive = _undoState.HasValue
                && Time.time - _lastDepthChangeTime <= _wheelBurstWindowSeconds;
            if (!burstActive)
                BeginUndoableOperation();
            _lastDepthChangeTime = Time.time;

            float depthDelta = newDepth - _depthOffset;
            _selectedPiece.Root.position = ClampPosition(
                _selectedPiece.Root.position + _compositionCamera.transform.forward * depthDelta);
            SynchronizeDepthOffset();
            return true;
        }

        /// <summary>
        /// Direct constrained world-Y rotation: while carrying, rotates the
        /// carried root by <paramref name="signedDegrees"/> with the same
        /// quantization and clamp as the selected-piece path (no separate
        /// undo step is written — the whole pickup is one transaction, and
        /// the preview/validity refresh on the next carry update); otherwise
        /// rotates the selected root and stores one undoable operation.
        /// Rotation stays quantized to the configured step and clamped so the
        /// signed yaw offset from the piece's authored rotation stays within
        /// +/- the max rotation offset; scale and position are never touched.
        /// When nothing is selected the Arch Bridge is selected first (T-010A
        /// compatibility). Returns false when unconfigured, while settling,
        /// or the target yaw is quantized/clamped back to the current yaw.
        /// </summary>
        public bool TryRotate(float signedDegrees)
        {
            if (!_configured)
                return false;
            if (_carriedPiece != null && !_settling)
                return RotateCarried(signedDegrees);
            if (_settling)
                return false;
            if (_selectedPiece == null)
                SelectPiece(_bridge); // T-010A compatibility: operations default to the Arch Bridge

            float currentYaw = _selectedPiece.AuthoredSignedYawOffset(_selectedPiece.Root.rotation);
            float targetYaw = Mathf.Clamp(
                Mathf.RoundToInt((currentYaw + signedDegrees) / _rotationStepDegrees) * _rotationStepDegrees,
                -_maxRotationOffsetDegrees, _maxRotationOffsetDegrees);
            if (Mathf.Approximately(targetYaw, currentYaw))
                return false;

            BeginUndoableOperation();
            _selectedPiece.Root.rotation = _selectedPiece.AuthoredRotation * Quaternion.Euler(0f, targetYaw, 0f);
            return true;
        }

        /// <summary>
        /// One-step undo: restores the complete pre-operation position/
        /// rotation/local scale (and depth offset) of the piece stored when
        /// the last completed drag, depth burst, rotation or reset began —
        /// even if the selection has since changed — then consumes the stored
        /// state. After a valid placement it restores the exact pre-pickup
        /// pose; an invalid placement never reaches undo. Returns false when
        /// there is nothing to undo or a carry/settle is active.
        /// </summary>
        public bool Undo()
        {
            if (!_configured || _carriedPiece != null || _settling || !_undoState.HasValue)
                return false;

            PieceOperationState state = _undoState.Value;
            state.Piece.Root.position = state.Position;
            state.Piece.Root.rotation = state.Rotation;
            state.Piece.Root.localScale = state.LocalScale;
            _depthOffset = state.DepthOffset;
            _undoState = null;
            _lastDepthChangeTime = float.NegativeInfinity;
            if (_selectedPiece != null)
                SynchronizeDepthOffset();
            return true;
        }

        /// <summary>
        /// Restores the exact authored transform of the selected piece (the
        /// Arch Bridge is selected first when nothing is selected, for
        /// T-010A compatibility). Reset itself is undoable: the pre-reset
        /// state is stored first, and Undo returns to it. Returns false when
        /// unconfigured or a carry/settle is active.
        /// </summary>
        public bool ResetToAuthored()
        {
            if (!_configured || _carriedPiece != null || _settling)
                return false;
            if (_selectedPiece == null)
                SelectPiece(_bridge); // T-010A compatibility: operations default to the Arch Bridge

            BeginUndoableOperation();
            _selectedPiece.RestoreAuthored();
            SynchronizeDepthOffset();
            return true;
        }

        /// <summary>
        /// Begins a physical placement transaction on <paramref name="piece"/>:
        /// selects it, stores its exact pre-pickup transform and the prior
        /// one-step undo state, and initializes the landing candidate to the
        /// piece root projected to <see cref="SurfaceY"/>. Pointer input
        /// captures the grab offset separately afterwards
        /// (<see cref="UpdatePlacementTarget"/> never applies it, so tests
        /// drive exact candidates). Returns false when unconfigured, placement
        /// is not configured, another transaction is active, or the piece is
        /// not one of the configured pieces.
        /// </summary>
        public bool BeginPlacement(PaintingManipulablePiece piece)
        {
            if (!_configured || !_placementConfigured || _carriedPiece != null || _settling)
                return false;
            if (piece == null || Array.IndexOf(_pieces, piece) < 0)
                return false;

            if (_selectedPiece != piece)
            {
                if (_selectedPiece != null)
                    _selectedPiece.SetSelected(false);
                piece.SetSelected(true);
                _selectedPiece = piece;
                _lastDepthChangeTime = float.NegativeInfinity;
            }

            _carriedPiece = piece;
            _pickupPosition = piece.Root.position;
            _pickupRotation = piece.Root.rotation;
            _pickupLocalScale = piece.Root.localScale;
            _pickupDepthOffset = _depthOffset;
            _prePickupUndoState = _undoState;
            _grabOffset = Vector3.zero;
            _carryVelocity = Vector3.zero;

            // The landing candidate initially equals the piece root projected to SurfaceY.
            _candidate = new Vector3(_pickupPosition.x, _surfaceY, _pickupPosition.z);

            _placementPreview = PaintingPlacementPreview.Create(piece, _validPreviewMaterial, _invalidPreviewMaterial);
            _placementPreview.Show(_candidate, piece.Root.rotation, piece.Root.localScale,
                ComputePlacementValidity(piece, _candidate));
            return true;
        }

        /// <summary>
        /// Updates the landing candidate from a world position while carrying;
        /// the candidate root Y is always <see cref="SurfaceY"/>. This is the
        /// same path pointer dragging drives after intersecting the surface
        /// plane (minus the grab offset), so PlayMode tests exercise the
        /// transaction without synthetic mouse input. No-op when no
        /// transaction is active.
        /// </summary>
        public void UpdatePlacementTarget(Vector3 worldPosition)
        {
            if (!_configured || _carriedPiece == null || _settling)
                return;
            _candidate = new Vector3(worldPosition.x, _surfaceY, worldPosition.z);
            RefreshPlacementPreview();
        }

        /// <summary>
        /// Ends the transaction: a valid candidate starts the landing settle
        /// to the candidate (on completion undo restores the exact pre-pickup
        /// pose); an invalid candidate starts the return settle to the exact
        /// pre-pickup pose and restores the undo state that existed before
        /// the pickup. The preview is hidden at settle start. Returns whether
        /// the candidate was valid; false when no transaction is active.
        /// </summary>
        public bool ReleasePlacement()
        {
            if (!_configured || _carriedPiece == null || _settling)
                return false;
            bool valid = ComputePlacementValidity(_carriedPiece, _candidate);
            BeginSettle(valid);
            return valid;
        }

        /// <summary>
        /// Cancels the transaction like an invalid release (Escape): the
        /// piece settles back to its exact pre-pickup pose and the undo state
        /// from before the pickup is restored. No-op when no transaction is
        /// active.
        /// </summary>
        public void CancelPlacement()
        {
            if (!_configured || _carriedPiece == null || _settling)
                return;
            BeginSettle(false);
        }

        private void CapturePointerGrabOffset()
        {
            _grabOffset = Vector3.zero;
            Ray pointerRay = _buildCamera.ScreenPointToRay(Input.mousePosition);
            if (_surfacePlane.Raycast(pointerRay, out float enter))
                _grabOffset = pointerRay.GetPoint(enter) - _pickupPosition;
        }

        private void UpdateCarryFromPointer()
        {
            Ray ray = _buildCamera.ScreenPointToRay(Input.mousePosition);
            if (_surfacePlane.Raycast(ray, out float enter))
                UpdatePlacementTarget(ray.GetPoint(enter) - _grabOffset);
        }

        private void UpdateCarry()
        {
            // The actual piece follows the lifted target with SmoothDamp so
            // it feels weighted, not dead-attached to the cursor.
            Vector3 followTarget = _candidate + Vector3.up * _liftHeight;
            _carriedPiece.Root.position = Vector3.SmoothDamp(
                _carriedPiece.Root.position, followTarget, ref _carryVelocity, _followSmoothTime);

            if (_placementPreview != null)
            {
                RefreshPlacementPreview();
            }
        }

        private void RefreshPlacementPreview()
        {
            if (_placementPreview == null || _carriedPiece == null)
                return;
            _placementPreview.Root.position = _candidate;
            _placementPreview.Root.rotation = _carriedPiece.Root.rotation;
            _placementPreview.SetValid(ComputePlacementValidity(_carriedPiece, _candidate));
        }

        /// <summary>
        /// Same quantization/clamp as the selected-piece rotation path, but
        /// inside the transaction: no undo step is written (the pickup owns
        /// the history), and the preview/validity refresh on the next carry
        /// update reflects the new footprint.
        /// </summary>
        private bool RotateCarried(float signedDegrees)
        {
            float currentYaw = _carriedPiece.AuthoredSignedYawOffset(_carriedPiece.Root.rotation);
            float targetYaw = Mathf.Clamp(
                Mathf.RoundToInt((currentYaw + signedDegrees) / _rotationStepDegrees) * _rotationStepDegrees,
                -_maxRotationOffsetDegrees, _maxRotationOffsetDegrees);
            if (Mathf.Approximately(targetYaw, currentYaw))
                return false;
            _carriedPiece.Root.rotation = _carriedPiece.AuthoredRotation * Quaternion.Euler(0f, targetYaw, 0f);
            RefreshPlacementPreview();
            return true;
        }

        /// <summary>
        /// The candidate is valid only when the piece's root BoxCollider XZ
        /// footprint at the candidate pose lies fully inside the configured
        /// world-XZ placement rectangle (touching within the epsilon is
        /// allowed) and does not overlap another configured piece's root
        /// collider; the carried piece itself is ignored. Pure geometry —
        /// no Rigidbody, no physics simulation.
        /// </summary>
        private bool ComputePlacementValidity(PaintingManipulablePiece piece, Vector3 candidate)
        {
            if (!_placementConfigured)
                return false;
            if (!TryGetXzFootprint(piece, candidate, piece.Root.rotation, out Vector2 min, out Vector2 max))
                return false; // only root BoxColliders are placeable; fail closed otherwise

            // Fully inside the shared world-XZ placement rectangle, touching within the epsilon allowed.
            if (min.x < _placementRectangle.xMin - PlacementEpsilon
                || max.x > _placementRectangle.xMax + PlacementEpsilon
                || min.y < _placementRectangle.yMin - PlacementEpsilon
                || max.y > _placementRectangle.yMax + PlacementEpsilon)
                return false;

            for (int i = 0; i < _pieces.Length; i++)
            {
                PaintingManipulablePiece other = _pieces[i];
                if (other == null || other == piece)
                    continue;
                if (!TryGetXzFootprint(other, other.Root.position, other.Root.rotation, out Vector2 otherMin, out Vector2 otherMax))
                    continue;
                if (min.x < otherMax.x - PlacementEpsilon && otherMin.x < max.x - PlacementEpsilon
                    && min.y < otherMax.y - PlacementEpsilon && otherMin.y < max.y - PlacementEpsilon)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// World XZ footprint (min/max) of the piece's root BoxCollider at
        /// the given root pose, computed directly from the box extents by
        /// transforming its eight corners — no Rigidbody, no physics
        /// simulation. Returns false when the root collider is not a
        /// BoxCollider.
        /// </summary>
        private static bool TryGetXzFootprint(
            PaintingManipulablePiece piece, Vector3 rootPosition, Quaternion rootRotation, out Vector2 min, out Vector2 max)
        {
            min = default;
            max = default;
            BoxCollider box = piece.SelectionCollider as BoxCollider;
            if (box == null)
                return false;

            Vector3 center = box.center;
            Vector3 half = box.size * 0.5f;
            min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        Vector3 corner = center + new Vector3(sx * half.x, sy * half.y, sz * half.z);
                        Vector3 scaledCorner = Vector3.Scale(corner, piece.Root.lossyScale);
                        Vector3 world = rootPosition + rootRotation * scaledCorner;
                        min.x = Mathf.Min(min.x, world.x);
                        min.y = Mathf.Min(min.y, world.z);
                        max.x = Mathf.Max(max.x, world.x);
                        max.y = Mathf.Max(max.y, world.z);
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Starts the deterministic settle: from the lifted actual pose to the
        /// candidate (landing) or back to the exact pre-pickup pose (return).
        /// The undo state is committed up front so an interrupted settle still
        /// leaves consistent history: a landing restores the exact pre-pickup
        /// pose, an invalid return restores the state that existed before the
        /// pickup. The preview is hidden here.
        /// </summary>
        private void BeginSettle(bool landing)
        {
            _settling = true;
            _settleLanding = landing;
            _settleTimer = 0f;
            _settleFrom = _carriedPiece.Root.position;
            _settleFromRotation = _carriedPiece.Root.rotation;
            _settleTo = landing ? _candidate : _pickupPosition;

            _undoState = landing
                ? new PieceOperationState(_carriedPiece, _pickupPosition, _pickupRotation, _pickupLocalScale, _pickupDepthOffset)
                : _prePickupUndoState;
            _lastDepthChangeTime = float.NegativeInfinity;

            if (_placementPreview != null)
            {
                _placementPreview.Dispose();
                _placementPreview = null;
            }
        }

        private void UpdateSettle()
        {
            _settleTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_settleTimer / _settleDuration);
            float ease = t * t * (3f - 2f * t); // smoothstep easing
            Vector3 position = Vector3.LerpUnclamped(_settleFrom, _settleTo, ease);
            if (_settleLanding)
                position.y += Mathf.Sin(t * Mathf.PI * 2f) * (1f - t) * LandingBounceAmplitude; // restrained vertical bounce
            _carriedPiece.Root.position = position;
            if (!_settleLanding)
                _carriedPiece.Root.rotation = Quaternion.SlerpUnclamped(_settleFromRotation, _pickupRotation, ease);
            if (t >= 1f)
                CompleteSettle();
        }

        private void CompleteSettle()
        {
            if (_settleLanding)
            {
                // Exact landing: root position = candidate; rotation and local
                // scale are untouched by the settle.
                _carriedPiece.Root.position = _settleTo;
            }
            else
            {
                // Exact return: full pre-pickup pose.
                _carriedPiece.Root.position = _pickupPosition;
                _carriedPiece.Root.rotation = _pickupRotation;
                _carriedPiece.Root.localScale = _pickupLocalScale;
            }
            _carriedPiece = null;
            _settling = false;
        }

        /// <summary>
        /// Restores a carried piece safely on disable/destroy or
        /// reconfiguration: the preview is destroyed (its renderers only ever
        /// used the configured shared materials, so nothing leaks), a running
        /// settle snaps to its committed target, and a plain carry restores
        /// the exact pre-pickup pose plus the undo state from before the
        /// pickup. Idempotent.
        /// </summary>
        private void CleanupCarry()
        {
            if (_placementPreview != null)
            {
                _placementPreview.Dispose();
                _placementPreview = null;
            }
            if (_settling && _carriedPiece != null)
            {
                _carriedPiece.Root.position = _settleTo;
                if (!_settleLanding)
                    _carriedPiece.Root.rotation = _pickupRotation;
                _settling = false;
            }
            else if (_carriedPiece != null)
            {
                _carriedPiece.Root.position = _pickupPosition;
                _carriedPiece.Root.rotation = _pickupRotation;
                _carriedPiece.Root.localScale = _pickupLocalScale;
                _undoState = _prePickupUndoState;
                _lastDepthChangeTime = float.NegativeInfinity;
            }
            _carriedPiece = null;
        }

        /// <summary>
        /// Pointer selection: raycasts all colliders on the dedicated mask,
        /// then resolves overlap by visual intent rather than raw depth. The
        /// hit whose projected renderer bounds centre is closest to the
        /// pointer wins; ray distance is only a stable tie-breaker. This keeps
        /// broad fitted mountain/tree boxes from stealing clicks aimed at a
        /// smaller visible piece behind them.
        /// </summary>
        private bool TryHitPiece(out PaintingManipulablePiece piece, out RaycastHit hit)
        {
            piece = null;
            hit = default;
            Ray ray = _buildCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit[] hits = Physics.RaycastAll(ray, MaxRayDistance, _selectionMask);
            float bestScore = float.PositiveInfinity;
            for (int i = 0; i < hits.Length; i++)
            {
                PaintingManipulablePiece candidate = PieceByCollider(hits[i].collider);
                if (candidate == null)
                    continue;
                float score = PointerIntentScore(candidate, Input.mousePosition)
                    + hits[i].distance * 0.0001f;
                if (score < bestScore)
                {
                    bestScore = score;
                    piece = candidate;
                    hit = hits[i];
                }
            }
            return piece != null;
        }

        private float PointerIntentScore(PaintingManipulablePiece piece, Vector2 pointer)
        {
            bool initialized = false;
            Vector2 min = default;
            Vector2 max = default;
            IReadOnlyList<Renderer> renderers = piece.Renderers;
            for (int i = 0; i < renderers.Count; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                    continue;
                Bounds bounds = renderer.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 world = new Vector3(
                        (corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                        (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                        (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
                    Vector3 screen = _buildCamera.WorldToScreenPoint(world);
                    if (screen.z <= 0f)
                        continue;
                    Vector2 point = new Vector2(screen.x, screen.y);
                    if (!initialized)
                    {
                        min = max = point;
                        initialized = true;
                    }
                    else
                    {
                        min = Vector2.Min(min, point);
                        max = Vector2.Max(max, point);
                    }
                }
            }

            if (!initialized)
                return float.PositiveInfinity;
            Vector2 size = Vector2.Max(max - min, Vector2.one);
            Vector2 center = (min + max) * 0.5f;
            Vector2 normalized = new Vector2(
                (pointer.x - center.x) / size.x,
                (pointer.y - center.y) / size.y);
            float score = normalized.sqrMagnitude;
            bool inside = pointer.x >= min.x && pointer.x <= max.x
                && pointer.y >= min.y && pointer.y <= max.y;
            return inside ? score : 4f + score;
        }

        private PaintingManipulablePiece PieceByCollider(Collider collider)
        {
            for (int i = 0; i < _pieces.Length; i++)
            {
                if (_pieces[i] != null && _pieces[i].SelectionCollider == collider)
                    return _pieces[i];
            }
            return null;
        }

        private void BeginUndoableOperation()
        {
            if (!_configured || _selectedPiece == null)
                return;
            _undoState = new PieceOperationState(
                _selectedPiece,
                _selectedPiece.Root.position, _selectedPiece.Root.rotation, _selectedPiece.Root.localScale,
                _depthOffset);
        }

        private Vector3 ClampPosition(Vector3 position)
        {
            Vector3 viewport = _compositionCamera.WorldToViewportPoint(position);
            viewport.x = Mathf.Clamp(viewport.x,
                _compositionViewportBounds.xMin, _compositionViewportBounds.xMax);
            viewport.y = Mathf.Clamp(viewport.y,
                _compositionViewportBounds.yMin, _compositionViewportBounds.yMax);
            viewport.z = Mathf.Clamp(viewport.z,
                _compositionDepthRange.x, _compositionDepthRange.y);

            Vector3 bounded = _compositionCamera.ViewportToWorldPoint(viewport);
            Bounds bounds = _movementBounds; // emergency numerical safety only
            return new Vector3(
                Mathf.Clamp(bounded.x, bounds.min.x, bounds.max.x),
                Mathf.Clamp(bounded.y, bounds.min.y, bounds.max.y),
                Mathf.Clamp(bounded.z, bounds.min.z, bounds.max.z));
        }

        private void SynchronizeDepthOffset()
        {
            _depthOffset = _compositionCamera.WorldToViewportPoint(_selectedPiece.Root.position).z;
        }

        private static bool IsValidViewportBounds(Rect bounds)
        {
            return bounds.width > 0f && bounds.height > 0f
                && bounds.xMin >= 0f && bounds.yMin >= 0f
                && bounds.xMax <= 1f && bounds.yMax <= 1f;
        }

        private static bool IsValidDepthRange(Vector2 range)
        {
            return range.x > 0f && range.y > range.x;
        }

        /// <summary>Complete pre-operation transform/depth state of one piece, so one-step undo can restore the right piece even after the selection changed.</summary>
        private readonly struct PieceOperationState
        {
            public readonly PaintingManipulablePiece Piece;
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public readonly Vector3 LocalScale;
            public readonly float DepthOffset;

            public PieceOperationState(PaintingManipulablePiece piece, Vector3 position, Quaternion rotation, Vector3 localScale, float depthOffset)
            {
                Piece = piece;
                Position = position;
                Rotation = rotation;
                LocalScale = localScale;
                DepthOffset = depthOffset;
            }
        }
    }
}
