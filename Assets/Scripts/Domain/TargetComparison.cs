using System;

namespace PerspectivePuzzle.Domain
{
    /// <summary>
    /// Compares a current projection against a target projection.
    /// </summary>
    public static class TargetComparison
    {
        /// <summary>
        /// Counts cells where <paramref name="current"/> and
        /// <paramref name="target"/> agree (both empty or both occupied).
        /// </summary>
        /// <exception cref="ArgumentNullException">Either argument is null.</exception>
        /// <exception cref="ArgumentException">The two maps have different X/Y dimensions.</exception>
        public static MatchResult Compare(ProjectionMap2D current, ProjectionMap2D target)
        {
            if (current == null)
                throw new ArgumentNullException(nameof(current));
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (current.Width != target.Width || current.Height != target.Height)
                throw new ArgumentException(
                    "Projection dimensions must match: current is " + current.Width + "x" + current.Height
                    + ", target is " + target.Width + "x" + target.Height + ".",
                    nameof(target));

            int matching = 0;
            for (int x = 0; x < current.Width; x++)
            {
                for (int y = 0; y < current.Height; y++)
                {
                    if (current.IsOccupied(x, y) == target.IsOccupied(x, y))
                        matching++;
                }
            }

            return new MatchResult(matching, current.Width * current.Height);
        }
    }
}
