using PerspectivePuzzle.Domain;

namespace PerspectivePuzzle.Presentation
{
    /// <summary>
    /// Immutable record of one successful grid mutation, used by
    /// <see cref="PlacementHistory"/> to reverse it on undo.
    /// </summary>
    public readonly struct PlacementCommand
    {
        /// <summary>The cell the action affected.</summary>
        public GridCoordinate Cell { get; }

        /// <summary>True when the action placed a piece; false when it removed one.</summary>
        public bool WasPlacement { get; }

        public PlacementCommand(GridCoordinate cell, bool wasPlacement)
        {
            Cell = cell;
            WasPlacement = wasPlacement;
        }

        public override string ToString()
        {
            return (WasPlacement ? "Place " : "Remove ") + Cell;
        }
    }
}
