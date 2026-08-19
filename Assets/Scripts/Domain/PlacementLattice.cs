using System;

namespace PerspectivePuzzle.Domain
{
    /// <summary>A snapped position on the physical composition table.</summary>
    public readonly struct PlacementLatticePoint
    {
        public int Column { get; }
        public int DepthRow { get; }
        public float X { get; }
        public float Z { get; }

        public PlacementLatticePoint(int column, int depthRow, float x, float z)
        {
            Column = column;
            DepthRow = depthRow;
            X = x;
            Z = z;
        }
    }

    /// <summary>
    /// Pure deterministic table lattice. Columns describe left/right
    /// composition and rows describe physical near/far placement, so the
    /// player manipulates one readable coordinate system instead of a free
    /// plane plus a separate continuous depth axis.
    /// </summary>
    public sealed class PlacementLattice
    {
        public int Columns { get; }
        public int DepthRows { get; }
        public float XMin { get; }
        public float XMax { get; }
        public float ZMin { get; }
        public float ZMax { get; }
        public float ColumnSpacing { get; }
        public float DepthSpacing { get; }

        public PlacementLattice(int columns, int depthRows, float xMin, float xMax, float zMin, float zMax)
            : this(columns, depthRows, xMin, xMax, zMin, zMax, 0f, 0f)
        {
        }

        public PlacementLattice(int columns, int depthRows, float xMin, float xMax, float zMin, float zMax,
            float columnSpacing, float depthSpacing)
        {
            if (columns < 2) throw new ArgumentOutOfRangeException(nameof(columns));
            if (depthRows < 2) throw new ArgumentOutOfRangeException(nameof(depthRows));
            if (!(xMax > xMin)) throw new ArgumentOutOfRangeException(nameof(xMax));
            if (!(zMax > zMin)) throw new ArgumentOutOfRangeException(nameof(zMax));
            Columns = columns;
            DepthRows = depthRows;
            XMin = xMin;
            XMax = xMax;
            ZMin = zMin;
            ZMax = zMax;
            ColumnSpacing = columnSpacing > 0f ? columnSpacing : (XMax - XMin) / (Columns - 1);
            DepthSpacing = depthSpacing > 0f ? depthSpacing : (ZMax - ZMin) / (DepthRows - 1);
        }

        public PlacementLatticePoint Snap(float x, float z)
        {
            int column = NearestIndex(x, XMin, XMax, Columns);
            int row = NearestIndex(z, ZMin, ZMax, DepthRows);
            return At(column, row);
        }

        /// <summary>
        /// Snaps to lattice intervals measured from a piece-specific authored
        /// anchor. This preserves the shared spacing while guaranteeing that
        /// every piece's valid visual solution is itself a grid position.
        /// </summary>
        public PlacementLatticePoint SnapAround(float anchorX, float anchorZ, float x, float z)
        {
            float stepX = ColumnSpacing;
            float stepZ = DepthSpacing;
            int columnOffset = (int)Math.Round((x - anchorX) / stepX, MidpointRounding.AwayFromZero);
            int rowOffset = (int)Math.Round((z - anchorZ) / stepZ, MidpointRounding.AwayFromZero);
            float snappedX = Clamp(anchorX + columnOffset * stepX, XMin, XMax);
            float snappedZ = Clamp(anchorZ + rowOffset * stepZ, ZMin, ZMax);
            return new PlacementLatticePoint(columnOffset, rowOffset, snappedX, snappedZ);
        }

        public PlacementLatticePoint At(int column, int depthRow)
        {
            if (column < 0 || column >= Columns) throw new ArgumentOutOfRangeException(nameof(column));
            if (depthRow < 0 || depthRow >= DepthRows) throw new ArgumentOutOfRangeException(nameof(depthRow));
            float x = Lerp(XMin, XMax, column / (float)(Columns - 1));
            float z = Lerp(ZMin, ZMax, depthRow / (float)(DepthRows - 1));
            return new PlacementLatticePoint(column, depthRow, x, z);
        }

        public int StepDepthRow(int currentRow, int direction)
        {
            if (currentRow < 0 || currentRow >= DepthRows) throw new ArgumentOutOfRangeException(nameof(currentRow));
            if (direction == 0) return currentRow;
            return Clamp(currentRow + Math.Sign(direction), 0, DepthRows - 1);
        }

        private static int NearestIndex(float value, float min, float max, int count)
        {
            float normalized = Clamp01((value - min) / (max - min));
            return Clamp((int)Math.Round(normalized * (count - 1), MidpointRounding.AwayFromZero), 0, count - 1);
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
        private static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;
        private static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
    }
}
