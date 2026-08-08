namespace PerspectivePuzzle.Presentation
{
    /// <summary>
    /// Per-cell display state of the projection board after comparing the
    /// current projection with the target.
    /// </summary>
    public enum ProjectionCellState
    {
        /// <summary>Both projections are empty at this cell.</summary>
        Empty,

        /// <summary>Target occupied, current empty: a cell the player still needs.</summary>
        Missing,

        /// <summary>Current occupied, target empty: an extra piece.</summary>
        Extra,

        /// <summary>Both occupied: a correct overlap.</summary>
        Matched,
    }
}
