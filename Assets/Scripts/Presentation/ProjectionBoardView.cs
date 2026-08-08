using System;
using UnityEngine;
using PerspectivePuzzle.Domain;

namespace PerspectivePuzzle.Presentation
{
    /// <summary>
    /// Unity-facing view of the physical projection board. It computes the
    /// per-cell display state (target-only, current-only, overlap, empty) from
    /// the domain projection and target after every mutation; the scene builder
    /// renders those states with the accepted restrained palette.
    /// </summary>
    public sealed class ProjectionBoardView : MonoBehaviour
    {
        private ProjectionCellState[] _states;

        /// <summary>Board width in cells, as of the last refresh.</summary>
        public int Width { get; private set; }

        /// <summary>Board height in cells, as of the last refresh.</summary>
        public int Height { get; private set; }

        /// <summary>The target projection the board displays.</summary>
        public ProjectionMap2D Target { get; private set; }

        /// <summary>Fired at the end of each <see cref="Refresh"/> once cell states are computed.</summary>
        public event Action StatesChanged;

        /// <summary>
        /// Recomputes the board cell states from <paramref name="current"/> and
        /// the puzzle target. The maps must share X/Y dimensions.
        /// </summary>
        public void Refresh(ProjectionMap2D current, ProjectionMap2D target)
        {
            if (current == null)
                throw new ArgumentNullException(nameof(current));
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (current.Width != target.Width || current.Height != target.Height)
                throw new ArgumentException(
                    "Projection dimensions must match: current is " + current.Width + "x" + current.Height
                    + ", target is " + target.Width + "x" + target.Height + ".", nameof(target));

            Target = target;
            Width = current.Width;
            Height = current.Height;
            _states = new ProjectionCellState[Width * Height];

            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    bool currentOccupied = current.IsOccupied(x, y);
                    bool targetOccupied = target.IsOccupied(x, y);

                    ProjectionCellState state = currentOccupied && targetOccupied
                        ? ProjectionCellState.Matched
                        : currentOccupied
                            ? ProjectionCellState.Extra
                            : targetOccupied
                                ? ProjectionCellState.Missing
                                : ProjectionCellState.Empty;

                    _states[y * Width + x] = state;
                }
            }

            StatesChanged?.Invoke();
        }

        /// <summary>Display state of the cell at (x, y) after the last refresh.</summary>
        /// <exception cref="InvalidOperationException">No refresh has happened yet.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The cell is outside the board.</exception>
        public ProjectionCellState StateAt(int x, int y)
        {
            if (_states == null)
                throw new InvalidOperationException("ProjectionBoardView.Refresh must be called before StateAt.");
            if (x < 0 || x >= Width)
                throw new ArgumentOutOfRangeException(nameof(x), x, "Cell is outside the board.");
            if (y < 0 || y >= Height)
                throw new ArgumentOutOfRangeException(nameof(y), y, "Cell is outside the board.");

            return _states[y * Width + x];
        }
    }
}
