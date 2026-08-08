using System;

namespace PerspectivePuzzle.Domain
{
    /// <summary>
    /// Projects a 3D occupancy grid along the Z axis onto an X/Y plane.
    /// </summary>
    public static class ZProjection
    {
        /// <summary>
        /// Returns a 2D X/Y map in which a cell is occupied when any depth at
        /// that X/Y position is occupied in <paramref name="grid"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="grid"/> is null.</exception>
        public static ProjectionMap2D Project(OccupancyGrid3D grid)
        {
            if (grid == null)
                throw new ArgumentNullException(nameof(grid));

            bool[] cells = new bool[grid.Width * grid.Height];
            int occupiedCount = 0;

            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    for (int z = 0; z < grid.Depth; z++)
                    {
                        if (grid.IsOccupied(new GridCoordinate(x, y, z)))
                        {
                            cells[y * grid.Width + x] = true;
                            occupiedCount++;
                            break;
                        }
                    }
                }
            }

            return new ProjectionMap2D(grid.Width, grid.Height, cells, occupiedCount);
        }
    }
}
