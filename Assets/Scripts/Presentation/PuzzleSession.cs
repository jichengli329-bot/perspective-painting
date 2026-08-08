using System;
using PerspectivePuzzle.Domain;

namespace PerspectivePuzzle.Presentation
{
    /// <summary>
    /// Orchestrates the single 5x5x3 puzzle of the playable scene. Owns the
    /// domain occupancy grid and target, routes every mutation through
    /// <see cref="PlacementHistory"/>, recomputes the Z projection and the
    /// target comparison after each successful change, and locks input plus
    /// raises a reveal signal when the projection exactly matches the target.
    /// The <see cref="OccupancyGrid3D"/> remains the source of truth; this
    /// class never duplicates projection or comparison rules.
    /// </summary>
    public sealed class PuzzleSession
    {
        /// <summary>Puzzle grid width (X).</summary>
        public const int GridWidth = 5;

        /// <summary>Puzzle grid height (Y).</summary>
        public const int GridHeight = 5;

        /// <summary>Puzzle grid depth (Z layers).</summary>
        public const int GridDepth = 3;

        /// <summary>The domain occupancy grid; the source of truth for state.</summary>
        public OccupancyGrid3D Grid { get; }

        /// <summary>The immutable target projection the player must recreate.</summary>
        public ProjectionMap2D Target { get; }

        /// <summary>One-step undo history over the grid.</summary>
        public PlacementHistory History { get; }

        /// <summary>Z projection of the grid, recomputed after every mutation.</summary>
        public ProjectionMap2D CurrentProjection { get; private set; }

        /// <summary>Comparison of the current projection against the target.</summary>
        public MatchResult Comparison { get; private set; }

        /// <summary>True once the projection exactly matches the target; mutations are then refused.</summary>
        public bool IsLocked { get; private set; }

        /// <summary>Fired after every successful mutation and undo, once projection and comparison are recomputed.</summary>
        public event Action StateChanged;

        /// <summary>Fired once, when the puzzle first reaches an exact match (the reveal signal).</summary>
        public event Action ExactMatchReached;

        /// <exception cref="ArgumentNullException">Either argument is null.</exception>
        /// <exception cref="ArgumentException">The grid is not 5x5x3 or the target is not 5x5.</exception>
        public PuzzleSession(OccupancyGrid3D grid, ProjectionMap2D target)
        {
            if (grid == null)
                throw new ArgumentNullException(nameof(grid));
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (grid.Width != GridWidth || grid.Height != GridHeight || grid.Depth != GridDepth)
                throw new ArgumentException(
                    "The puzzle grid must be " + GridWidth + "x" + GridHeight + "x" + GridDepth + " but was "
                    + grid.Width + "x" + grid.Height + "x" + grid.Depth + ".", nameof(grid));
            if (target.Width != GridWidth || target.Height != GridHeight)
                throw new ArgumentException(
                    "The target projection must be " + GridWidth + "x" + GridHeight + " but was "
                    + target.Width + "x" + target.Height + ".", nameof(target));

            Grid = grid;
            Target = target;
            History = new PlacementHistory(grid);
            Refresh();
        }

        /// <summary>
        /// True when placing at <paramref name="cell"/> is currently allowed:
        /// the session is not locked, the cell is inside the grid, and it is empty.
        /// </summary>
        public bool CanPlaceAt(GridCoordinate cell)
        {
            return !IsLocked && Grid.IsInBounds(cell) && !Grid.IsOccupied(cell);
        }

        /// <summary>
        /// Places a piece at <paramref name="cell"/>. Returns false when the
        /// session is locked or the grid rejects the placement (out of bounds
        /// or already occupied); nothing changes then.
        /// </summary>
        public bool TryPlace(GridCoordinate cell)
        {
            if (IsLocked)
                return false;
            if (!History.TryPlace(cell))
                return false;

            Refresh();
            return true;
        }

        /// <summary>
        /// Removes the piece at <paramref name="cell"/>. Returns false when the
        /// session is locked or the cell holds no piece.
        /// </summary>
        public bool TryRemove(GridCoordinate cell)
        {
            if (IsLocked)
                return false;
            if (!History.TryRemove(cell))
                return false;

            Refresh();
            return true;
        }

        /// <summary>
        /// Reverses the most recent placement or removal. Returns false when
        /// the session is locked or there is nothing to undo.
        /// </summary>
        public bool TryUndo()
        {
            if (IsLocked)
                return false;
            if (!History.TryUndo())
                return false;

            Refresh();
            return true;
        }

        /// <summary>
        /// Finds the highest occupied cell in the column at (x, y) — the piece a
        /// right-click at that position points at when layers stack upward.
        /// </summary>
        public bool TryGetTopmostOccupied(int x, int y, out GridCoordinate cell)
        {
            if (x < 0 || x >= Grid.Width || y < 0 || y >= Grid.Height)
            {
                cell = default;
                return false;
            }

            for (int z = Grid.Depth - 1; z >= 0; z--)
            {
                cell = new GridCoordinate(x, y, z);
                if (Grid.IsOccupied(cell))
                    return true;
            }

            cell = default;
            return false;
        }

        private void Refresh()
        {
            CurrentProjection = ZProjection.Project(Grid);
            Comparison = TargetComparison.Compare(CurrentProjection, Target);

            if (!IsLocked && Comparison.IsExactMatch)
            {
                IsLocked = true;
                ExactMatchReached?.Invoke();
            }

            StateChanged?.Invoke();
        }
    }
}
