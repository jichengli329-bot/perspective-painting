using System;
using PerspectivePuzzle.Domain;

namespace PerspectivePuzzle.Presentation
{
    /// <summary>
    /// Records the most recent successful placement or removal and can reverse
    /// it against the same <see cref="OccupancyGrid3D"/>. Only the last action
    /// is undoable (one-step undo): any successful mutation replaces the slot,
    /// and failed mutations (duplicate placement, out-of-bounds, empty removal)
    /// never touch it. The grid stays the source of truth — mutations are only
    /// performed through the grid's own Try methods.
    /// </summary>
    public sealed class PlacementHistory
    {
        private readonly OccupancyGrid3D _grid;
        private PlacementCommand? _last;

        /// <exception cref="ArgumentNullException"><paramref name="grid"/> is null.</exception>
        public PlacementHistory(OccupancyGrid3D grid)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
        }

        /// <summary>True when the last action can be undone.</summary>
        public bool CanUndo => _last.HasValue;

        /// <summary>The last successful action, or null when there is none.</summary>
        public PlacementCommand? LastCommand => _last;

        /// <summary>
        /// Places a piece and records it as the undoable action. Returns false
        /// when the grid rejects the placement; the recorded action is unchanged.
        /// </summary>
        public bool TryPlace(GridCoordinate cell)
        {
            if (!_grid.TryPlace(cell))
                return false;

            _last = new PlacementCommand(cell, true);
            return true;
        }

        /// <summary>
        /// Removes a piece and records it as the undoable action. Returns false
        /// when the grid rejects the removal; the recorded action is unchanged.
        /// </summary>
        public bool TryRemove(GridCoordinate cell)
        {
            if (!_grid.TryRemove(cell))
                return false;

            _last = new PlacementCommand(cell, false);
            return true;
        }

        /// <summary>
        /// Reverses the recorded action: a placement is removed, a removal is
        /// re-placed. Returns false when there is nothing to undo.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The reverse mutation failed, meaning the grid was changed outside
        /// this history between the action and the undo.
        /// </exception>
        public bool TryUndo()
        {
            if (!_last.HasValue)
                return false;

            var command = _last.Value;
            _last = null;

            bool reversed = command.WasPlacement
                ? _grid.TryRemove(command.Cell)
                : _grid.TryPlace(command.Cell);

            if (!reversed)
                throw new InvalidOperationException(
                    "Undo could not reverse " + command + "; the grid was mutated outside the history.");

            return true;
        }
    }
}
