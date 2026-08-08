using System;

namespace PerspectivePuzzle.Domain
{
    /// <summary>
    /// Immutable result of comparing a current projection against a target.
    /// A cell "matches" when both maps agree: both empty or both occupied.
    /// </summary>
    public readonly struct MatchResult
    {
        /// <summary>Number of cells where current and target agree.</summary>
        public int MatchingCells { get; }

        /// <summary>Total number of compared cells (map width x height).</summary>
        public int TotalCells { get; }

        /// <summary>Matching cells divided by total cells, in [0, 1].</summary>
        public float NormalizedMatchRatio => (float)MatchingCells / TotalCells;

        /// <summary>True when every cell matches (ratio is exactly 1).</summary>
        public bool IsExactMatch => MatchingCells == TotalCells;

        public MatchResult(int matchingCells, int totalCells)
        {
            if (totalCells <= 0)
                throw new ArgumentOutOfRangeException(nameof(totalCells), totalCells, "Total cells must be positive.");
            if (matchingCells < 0 || matchingCells > totalCells)
                throw new ArgumentOutOfRangeException(nameof(matchingCells), matchingCells, "Matching cells must be within [0, totalCells].");

            MatchingCells = matchingCells;
            TotalCells = totalCells;
        }
    }
}
