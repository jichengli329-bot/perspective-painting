using System;

namespace PerspectivePuzzle.Domain
{
    /// <summary>
    /// Immutable 2D X/Y occupancy map produced by projecting an
    /// <see cref="OccupancyGrid3D"/>. Instances are created fully populated by
    /// <see cref="ZProjection"/>; the internal storage is never exposed and
    /// cannot change after construction.
    /// </summary>
    public sealed class ProjectionMap2D
    {
        private readonly bool[] _cells;

        /// <summary>Extent along the X axis. Always positive.</summary>
        public int Width { get; }

        /// <summary>Extent along the Y axis. Always positive.</summary>
        public int Height { get; }

        /// <summary>Number of occupied cells in this map.</summary>
        public int OccupiedCount { get; }

        /// <summary>
        /// Creates an immutable map from row-major X/Y cell data. The input is
        /// copied, so later changes to the caller's array cannot change the map.
        /// </summary>
        public static ProjectionMap2D FromCells(int width, int height, bool[] cells)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), width, "Map width must be positive.");
            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height), height, "Map height must be positive.");
            if (cells == null)
                throw new ArgumentNullException(nameof(cells));
            if (cells.Length != width * height)
                throw new ArgumentException("Cell array length must equal width x height.", nameof(cells));

            var copy = new bool[cells.Length];
            Array.Copy(cells, copy, cells.Length);

            int occupiedCount = 0;
            for (int i = 0; i < copy.Length; i++)
                if (copy[i])
                    occupiedCount++;

            return new ProjectionMap2D(width, height, copy, occupiedCount);
        }

        /// <summary>
        /// Builds the map from a fully populated cell array. Only
        /// <see cref="ZProjection"/> creates instances.
        /// </summary>
        internal ProjectionMap2D(int width, int height, bool[] cells, int occupiedCount)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), width, "Map width must be positive.");
            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height), height, "Map height must be positive.");
            if (cells == null)
                throw new ArgumentNullException(nameof(cells));
            if (cells.Length != width * height)
                throw new ArgumentException("Cell array length must equal width x height.", nameof(cells));
            if (occupiedCount < 0 || occupiedCount > cells.Length)
                throw new ArgumentOutOfRangeException(nameof(occupiedCount), occupiedCount, "Occupied count must be within [0, cell count].");

            Width = width;
            Height = height;
            _cells = cells;
            OccupiedCount = occupiedCount;
        }

        /// <summary>
        /// True when the cell is occupied. Out-of-bounds coordinates report
        /// false.
        /// </summary>
        public bool IsOccupied(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
                return false;

            return _cells[y * Width + x];
        }
    }
}
