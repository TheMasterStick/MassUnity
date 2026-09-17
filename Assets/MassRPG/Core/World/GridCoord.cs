using System;

namespace MassRPG.Core.World
{
    /// <summary>
    /// Integer coordinate on MassRPG's canonical 1x1 logical world grid.
    /// Rendering may offset meshes inside a tile; gameplay ownership remains grid based.
    /// </summary>
    public readonly struct GridCoord : IEquatable<GridCoord>
    {
        public int X { get; }
        public int Y { get; }

        public GridCoord(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(GridCoord other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is GridCoord other && Equals(other);
        public override int GetHashCode() => unchecked((X * 397) ^ Y);
        public override string ToString() => $"({X}, {Y})";

        public static bool operator ==(GridCoord left, GridCoord right) => left.Equals(right);
        public static bool operator !=(GridCoord left, GridCoord right) => !left.Equals(right);
    }
}
