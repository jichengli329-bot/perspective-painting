using System;

namespace PerspectivePuzzle.Domain
{
    /// <summary>
    /// Fixed-size 3D occupancy grid. Dimensions are validated at construction;
    /// coordinates are validated through <see cref="IsInBounds"/> and the Try
    /// methods, which return false instead of throwing for out-of-bounds or
    /// invalid operations. The internal storage is never exposed.
    /// </summary>
    public sealed class OccupancyGrid3D
    {
        private readonly bool[] _cells;
        private int _occupiedCount;

        /// <summary>Extent along the X axis. Always positive.</summary>
        public int Width { get; }

        /// <summary>Extent along the Y axis. Always positive.</summary>
        public int Height { get; }

        /// <summary>Extent along the Z axis. Always positive.</summary>
        public int Depth { get; }

        /// <summary>Number of currently occupied cells.</summary>
        public int OccupiedCount => _occupiedCount;

        public OccupancyGrid3D(int width, int height, int depth)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), width, "Grid width must be positive.");
            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height), height, "Grid height must be positive.");
            if (depth <= 0)
                throw new ArgumentOutOfRangeException(nameof(depth), depth, "Grid depth must be positive.");

            Width = width;
            Height = height;
            Depth = depth;
            _cells = new bool[width * height * depth];
        }

        /// <summary>True when the coordinate lies inside the grid, including its edges.</summary>
        public bool IsInBounds(GridCoordinate coordinate)
        {
            return coordinate.X >= 0 && coordinate.X < Width
                && coordinate.Y >= 0 && coordinate.Y < Height
                && coordinate.Z >= 0 && coordinate.Z < Depth;
        }

        /// <summary>
        /// True when the cell is occupied. Out-of-bounds coordinates report
        /// false; check <see cref="IsInBounds"/> first when that distinction matters.
        /// </summary>
        public bool IsOccupied(GridCoordinate coordinate)
        {
            return IsInBounds(coordinate) && _cells[IndexOf(coordinate)];
        }

        /// <summary>
        /// Occupies the cell. Returns false when the cell is out of bounds or
        /// already occupied (duplicate placement is prevented). Returns true
        /// when the cell was newly occupied.
        /// </summary>
        public bool TryPlace(GridCoordinate coordinate)
        {
            if (!IsInBounds(coordinate))
                return false;

            int index = IndexOf(coordinate);
            if (_cells[index])
                return false;

            _cells[index] = true;
            _occupiedCount++;
            return true;
        }

        /// <summary>
        /// Frees the cell. Returns false when the cell is out of bounds or not
        /// occupied, true when the cell was freed.
        /// </summary>
        public bool TryRemove(GridCoordinate coordinate)
        {
            if (!IsInBounds(coordinate))
                return false;

            int index = IndexOf(coordinate);
            if (!_cells[index])
                return false;

            _cells[index] = false;
            _occupiedCount--;
            return true;
        }

        /// <summary>Frees every cell in the grid.</summary>
        public void Clear()
        {
            Array.Clear(_cells, 0, _cells.Length);
            _occupiedCount = 0;
        }

        private int IndexOf(GridCoordinate coordinate)
        {
            return (coordinate.Z * Height + coordinate.Y) * Width + coordinate.X;
        }
    }
}
