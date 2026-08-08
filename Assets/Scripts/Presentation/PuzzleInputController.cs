using System;
using System.Collections.Generic;
using UnityEngine;
using PerspectivePuzzle.Domain;

namespace PerspectivePuzzle.Presentation
{
    /// <summary>
    /// Turns pointer and keyboard input into puzzle actions for the playable
    /// scene: snapped hover preview, left-click place, right-click remove,
    /// number keys 1/2/3 to select the depth layer, Z for one-step undo, R to
    /// reset the current puzzle in place, and Space to advance to the next
    /// puzzle after a reveal. Progression is delegated to
    /// <see cref="PuzzleProgression"/> over the project-owned
    /// <see cref="PuzzleContent"/> — no controller branch knows any specific
    /// target. Every mutation goes through <see cref="PuzzleSession"/>, so the
    /// domain occupancy grid stays the single source of truth and piece
    /// GameObjects always mirror it: the action methods mutate the session
    /// first and then synchronize piece views, and a view still animating out
    /// is reinstated rather than duplicated when the same cell is placed
    /// again, so rapid valid input never creates duplicate or orphaned views.
    /// During a reveal the locked session refuses all mutations, and the
    /// transition is a single synchronous clear-and-rebuild, so rapid input
    /// during reveal/transition cannot corrupt state.
    /// </summary>
    public sealed class PuzzleInputController : MonoBehaviour
    {
        private const float MaxRayDistance = 100f;

        [SerializeField] private Camera pointerCamera;
        [SerializeField] private GridCoordinateMapper mapper;
        [SerializeField] private PuzzleSession session;
        [SerializeField] private PlacementPreview preview;
        [SerializeField] private ProjectionBoardView board;
        [SerializeField] private LayerIndicator layerIndicator;
        [SerializeField] private PuzzleSessionSource sessionSource;
        [SerializeField] private MatchReveal reveal;
        [SerializeField] private Transform pieceRoot;
        [SerializeField] private PieceView piecePrefab;
        [SerializeField] private ProgressIndicator progressIndicator;
        [SerializeField] private ControlLegend legend;
        [SerializeField] private LayerMask surfaceMask = ~0; // scene builder restricts this to the placement surface

        private readonly Dictionary<GridCoordinate, PieceView> _pieces = new Dictionary<GridCoordinate, PieceView>();
        private PuzzleProgression _progression;
        private int _activeLayerZ;
        private bool _revealRaised;

        /// <summary>Fired once per exact match when input locks (the reveal signal); re-armed by reset and transition.</summary>
        public event Action Revealed;

        /// <summary>Active depth layer as a 0-based grid Z coordinate (number keys are 1/2/3).</summary>
        public int ActiveLayerZ => _activeLayerZ;

        /// <summary>Number of live piece views, including views still animating out.</summary>
        public int PieceViewCount => _pieces.Count;

        /// <summary>0-based index of the puzzle currently being played (progression state).</summary>
        public int CurrentPuzzleIndex => _progression != null ? _progression.CurrentIndex : 0;

        /// <summary>True while a later puzzle exists (the Space-next hint is shown).</summary>
        public bool HasNextPuzzle => _progression != null && _progression.HasNext;

        /// <summary>
        /// Builds the ordered progression coordinator from the project-owned
        /// content. The content constructor validates all three targets, so a
        /// broken content edit fails at scene start.
        /// </summary>
        private void Awake()
        {
            _progression = new PuzzleProgression(PuzzleContent.Puzzles);
        }

        /// <summary>
        /// Binds the runtime-only puzzle state. <see cref="PuzzleSession"/> and
        /// <see cref="GridCoordinateMapper"/> are plain C# objects, so they cannot
        /// be serialized into the scene; <see cref="PuzzleSessionSource"/> creates
        /// them and calls this during Awake, before any Update runs.
        /// </summary>
        public void Bind(GridCoordinateMapper gridMapper, PuzzleSession puzzleSession)
        {
            mapper = gridMapper;
            session = puzzleSession;
        }

        private void Start()
        {
            if (session == null || mapper == null)
                return;

            // Show the initial projection board state (target cells as Missing)
            // before the first action, and present the slice's initial
            // progression: step one highlighted, Space-next hint visible.
            RefreshBoard();
            if (_progression != null)
            {
                if (legend != null)
                    legend.SetNextHintVisible(_progression.HasNext);
                if (progressIndicator != null)
                    progressIndicator.SetCurrentStep(_progression.CurrentIndex);
            }
        }

        private void Update()
        {
            if (session == null)
                return;

            // R works even while locked: it is the way out of the reveal state
            // for the current puzzle.
            if (Input.GetKeyDown(KeyCode.R))
            {
                ResetPuzzle();
                return;
            }

            if (session.IsLocked)
            {
                if (preview != null)
                    preview.SetVisible(false);
                if (!_revealRaised)
                {
                    _revealRaised = true;
                    Revealed?.Invoke();
                }
                // Space advances 1 → 2 → 3 after a reveal; the final puzzle
                // stays in its locked hold (TryAdvancePuzzle returns false).
                if (Input.GetKeyDown(KeyCode.Space))
                    TryAdvancePuzzle();
                return;
            }

            UpdateHoverPreview();

            if (Input.GetMouseButtonDown(0))
            {
                if (TryGetHoveredCell(out var hovered))
                    PlaceAt(new GridCoordinate(hovered.X, hovered.Y, _activeLayerZ));
            }
            else if (Input.GetMouseButtonDown(1))
            {
                if (TryGetHoveredCell(out var hovered))
                    RemoveTopmost(hovered);
            }

            if (Input.GetKeyDown(KeyCode.Alpha1))
                SetActiveLayer(0);
            else if (Input.GetKeyDown(KeyCode.Alpha2))
                SetActiveLayer(1);
            else if (Input.GetKeyDown(KeyCode.Alpha3))
                SetActiveLayer(2);

            if (Input.GetKeyDown(KeyCode.Z))
                Undo();
        }

        private void UpdateHoverPreview()
        {
            if (preview == null)
                return;

            if (!TryGetHoveredCell(out var hovered))
            {
                preview.SetVisible(false);
                return;
            }

            var cell = new GridCoordinate(hovered.X, hovered.Y, _activeLayerZ);
            preview.SetVisible(true);
            preview.SetWorldPosition(mapper.WorldFromCell(cell));
            preview.SetValid(session.CanPlaceAt(cell));
        }

        /// <summary>
        /// Places a piece at <paramref name="cell"/>: the session mutates first,
        /// then the piece view is spawned (or a view still animating out is
        /// reinstated) and the projection board repaints. Returns false when the
        /// session refuses; nothing changes then.
        /// </summary>
        public bool PlaceAt(GridCoordinate cell)
        {
            if (session == null || !session.TryPlace(cell))
                return false;

            SpawnPiece(cell);
            RefreshBoard();
            return true;
        }

        /// <summary>
        /// Removes the piece at <paramref name="cell"/>: the session mutates
        /// first, then the piece view plays its removal animation. Returns
        /// false when the session refuses; nothing changes then.
        /// </summary>
        public bool RemoveAt(GridCoordinate cell)
        {
            if (session == null || !session.TryRemove(cell))
                return false;

            RemovePieceView(cell);
            RefreshBoard();
            return true;
        }

        /// <summary>
        /// Undoes the most recent placement or removal and synchronizes the
        /// piece view. Returns false when there is nothing to undo.
        /// </summary>
        public bool Undo()
        {
            if (session == null)
                return false;

            var last = session.History.LastCommand;
            if (!last.HasValue || !session.TryUndo())
                return false;

            if (last.Value.WasPlacement)
                RemovePieceView(last.Value.Cell);
            else
                SpawnPiece(last.Value.Cell);

            RefreshBoard();
            return true;
        }

        /// <summary>
        /// Restores only the current puzzle in place, without reloading the
        /// application or the scene: the session grid, target and history are
        /// rebuilt by <see cref="PuzzleSessionSource"/> around the current
        /// puzzle's target (progression index is untouched), all piece views
        /// are torn down immediately (animations included), the projection
        /// board repaints its initial target-only state, the reveal camera
        /// returns to its resting pose and re-arms, and the active layer,
        /// preview and reveal flags reset so input works from the initial
        /// state again.
        /// </summary>
        public void ResetPuzzle()
        {
            if (sessionSource == null || session == null)
                return;

            var currentTarget = session.Target;
            TeardownPuzzle();
            sessionSource.RebuildWith(currentTarget);
            RefreshBoard();
        }

        /// <summary>
        /// Advances to the next puzzle after a reveal, reusing the same room:
        /// the current session must be locked (the reveal state), and the
        /// progression must still have a next puzzle — on the final puzzle this
        /// returns false and the locked hold stays (no wrap to puzzle one).
        /// The transition clears occupancy, history, piece views, preview and
        /// prior reveal state, restores the camera and input, rebuilds the
        /// session around the next target, refreshes the projection board, and
        /// advances the physical three-step indicator and the legend hint.
        /// Everything happens synchronously, so rapid input during the
        /// transition cannot observe or create an intermediate state.
        /// </summary>
        public bool TryAdvancePuzzle()
        {
            if (sessionSource == null || session == null || _progression == null)
                return false;
            if (!session.IsLocked)
                return false; // only from the reveal state
            if (!_progression.TryAdvance(out var next))
                return false; // final puzzle: non-text locked hold, never wraps

            TeardownPuzzle();
            sessionSource.RebuildWith(next);
            if (legend != null)
                legend.SetNextHintVisible(_progression.HasNext);
            if (progressIndicator != null)
                progressIndicator.SetCurrentStep(_progression.CurrentIndex);
            RefreshBoard();
            return true;
        }

        /// <summary>
        /// Clears everything a puzzle leaves behind before the session is
        /// rebuilt: piece views (animations included), the active layer, the
        /// reveal flag, the preview, the layer indicator and the reveal
        /// camera. The session itself is rebuilt by the caller.
        /// </summary>
        private void TeardownPuzzle()
        {
            foreach (var view in new List<PieceView>(_pieces.Values))
            {
                if (view != null)
                    view.TeardownImmediate();
            }
            _pieces.Clear();

            _activeLayerZ = 0;
            _revealRaised = false;

            if (preview != null)
                preview.SetVisible(false);
            if (layerIndicator != null)
                layerIndicator.ResetIndicator();
            if (reveal != null)
                reveal.ResetReveal();
        }

        private void RemoveTopmost(GridCoordinate hovered)
        {
            if (!session.TryGetTopmostOccupied(hovered.X, hovered.Y, out var pointed))
                return;
            RemoveAt(pointed);
        }

        private void SetActiveLayer(int gridZ)
        {
            _activeLayerZ = Mathf.Clamp(gridZ, 0, 2);
            if (layerIndicator != null)
                layerIndicator.SetActiveLayer(_activeLayerZ);
        }

        private bool TryGetHoveredCell(out GridCoordinate cell)
        {
            cell = default;
            if (pointerCamera == null || mapper == null)
                return false;

            var ray = pointerCamera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out var hit, MaxRayDistance, surfaceMask))
                return false;

            return mapper.TryCellFromWorld(hit.point, out cell);
        }

        private void SpawnPiece(GridCoordinate cell)
        {
            if (piecePrefab == null || pieceRoot == null || mapper == null)
                return;

            // A view still animating out is reinstated instead of duplicated:
            // the cell keeps exactly one live view through rapid input.
            if (_pieces.TryGetValue(cell, out var existing) && existing != null)
            {
                existing.PlaceAt(cell, mapper);
                return;
            }

            var view = Instantiate(piecePrefab, pieceRoot);
            view.RemovalFinished += () => OnViewRemovalFinished(view);
            view.PlaceAt(cell, mapper);
            _pieces[cell] = view;
        }

        private void RemovePieceView(GridCoordinate cell)
        {
            if (!_pieces.TryGetValue(cell, out var view))
                return;
            if (view != null)
                view.BeginRemove();
            // The entry is kept while the removal animation runs so a rapid
            // re-placement of the same cell reinstates the very same view.
        }

        private void OnViewRemovalFinished(PieceView view)
        {
            if (_pieces.TryGetValue(view.Cell, out var current) && current == view)
                _pieces.Remove(view.Cell);
        }

        private void RefreshBoard()
        {
            if (board != null)
                board.Refresh(session.CurrentProjection, session.Target);
        }
    }
}
