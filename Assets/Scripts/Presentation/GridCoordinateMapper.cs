using System;
using UnityEngine;
using PerspectivePuzzle.Domain;

namespace PerspectivePuzzle.Presentation
{
    /// <summary>
    /// Maps between grid cells and world positions for the 5x5x3 puzzle stage.
    /// Grid X maps to world X, grid Z (the depth layer) maps to world Y (up),
    /// and grid Y maps to world Z, matching the three-quarter view of the
    /// accepted prototype: the placement surface lies flat and layers stack
    /// upward. The scene builder chooses <see cref="Origin"/> and the spacings;
    /// this class never reads scene state.
    /// </summary>
    public sealed class GridCoordinateMapper
    {
        /// <summary>Extent along the grid X axis.</summary>
        public int Width { get; }

        /// <summary>Extent along the grid Y axis.</summary>
        public int Height { get; }

        /// <summary>Extent along the grid Z axis (depth layers).</summary>
        public int Depth { get; }

        /// <summary>World position of the center of cell (0, 0, 0).</summary>
        public Vector3 Origin { get; }

        /// <summary>World distance between adjacent cell centers along grid X.</summary>
        public float SpacingX { get; }

        /// <summary>World distance between adjacent cell centers along grid Y.</summary>
        public float SpacingY { get; }

        /// <summary>World height of one depth layer along grid Z.</summary>
        public float LayerHeight { get; }

        /// <summary>Creates the mapper for the T-005 5x5x3 puzzle grid.</summary>
        public static GridCoordinateMapper ForPuzzle5x5x3(Vector3 origin, float spacingX, float spacingY, float layerHeight)
        {
            return new GridCoordinateMapper(5, 5, 3, origin, spacingX, spacingY, layerHeight);
        }

        public GridCoordinateMapper(int width, int height, int depth, Vector3 origin, float spacingX, float spacingY, float layerHeight)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), width, "Grid width must be positive.");
            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height), height, "Grid height must be positive.");
            if (depth <= 0)
                throw new ArgumentOutOfRangeException(nameof(depth), depth, "Grid depth must be positive.");
            if (spacingX <= 0f)
                throw new ArgumentOutOfRangeException(nameof(spacingX), spacingX, "Spacing must be positive.");
            if (spacingY <= 0f)
                throw new ArgumentOutOfRangeException(nameof(spacingY), spacingY, "Spacing must be positive.");
            if (layerHeight <= 0f)
                throw new ArgumentOutOfRangeException(nameof(layerHeight), layerHeight, "Layer height must be positive.");

            Width = width;
            Height = height;
            Depth = depth;
            Origin = origin;
            SpacingX = spacingX;
            SpacingY = spacingY;
            LayerHeight = layerHeight;
        }

        /// <summary>True when the cell lies inside the mapped grid, including its edges.</summary>
        public bool IsInBounds(GridCoordinate cell)
        {
            return cell.X >= 0 && cell.X < Width
                && cell.Y >= 0 && cell.Y < Height
                && cell.Z >= 0 && cell.Z < Depth;
        }

        /// <summary>World position of the given cell center.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="cell"/> is outside the mapped grid.</exception>
        public Vector3 WorldFromCell(GridCoordinate cell)
        {
            if (!IsInBounds(cell))
                throw new ArgumentOutOfRangeException(nameof(cell), cell, "Cell is outside the mapped grid.");

            return Origin + new Vector3(cell.X * SpacingX, cell.Z * LayerHeight, cell.Y * SpacingY);
        }

        /// <summary>
        /// Snaps a world position to the nearest cell center. Returns false when
        /// the snapped cell is outside the grid. Exact boundary ties round to the
        /// nearest even cell (Mathf.RoundToInt), deterministically.
        /// </summary>
        public bool TryCellFromWorld(Vector3 world, out GridCoordinate cell)
        {
            int x = Mathf.RoundToInt((world.x - Origin.x) / SpacingX);
            int y = Mathf.RoundToInt((world.z - Origin.z) / SpacingY);
            int z = Mathf.RoundToInt((world.y - Origin.y) / LayerHeight);

            cell = new GridCoordinate(x, y, z);
            return IsInBounds(cell);
        }
    }
}
