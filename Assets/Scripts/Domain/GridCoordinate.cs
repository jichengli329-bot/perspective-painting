using System;

namespace PerspectivePuzzle.Domain
{
    /// <summary>
    /// Immutable integer cell coordinate in a 3D grid. A value type with value
    /// equality and a deterministic (stable) hash, so instances can be compared
    /// and used as dictionary keys without depending on object identity.
    /// </summary>
    public readonly struct GridCoordinate : IEquatable<GridCoordinate>
    {
        public int X { get; }
        public int Y { get; }
        public int Z { get; }

        public GridCoordinate(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public bool Equals(GridCoordinate other)
        {
            return X == other.X && Y == other.Y && Z == other.Z;
        }

        public override bool Equals(object obj)
        {
            return obj is GridCoordinate other && Equals(other);
        }

        /// <summary>
        /// Deterministic across runs and platforms; derived only from the
        /// component values, never from object identity.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + X;
                hash = hash * 31 + Y;
                hash = hash * 31 + Z;
                return hash;
            }
        }

        public override string ToString()
        {
            return "(" + X + ", " + Y + ", " + Z + ")";
        }

        public static bool operator ==(GridCoordinate a, GridCoordinate b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(GridCoordinate a, GridCoordinate b)
        {
            return !a.Equals(b);
        }
    }
}
