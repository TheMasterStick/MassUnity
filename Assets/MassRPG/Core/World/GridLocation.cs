using System;

namespace MassRPG.Core.World
{
    /// <summary>
    /// Full logical location. Terrain plane and building storey are intentionally separate.
    /// </summary>
    public readonly struct GridLocation : IEquatable<GridLocation>
    {
        public GridLocation(GridCoord tile, int plane, int storey = 0)
        {
            Tile = tile;
            Plane = plane;
            Storey = storey;
        }

        public GridCoord Tile { get; }
        public int Plane { get; }
        public int Storey { get; }

        public bool SameLayer(GridLocation other) => Plane == other.Plane && Storey == other.Storey;

        public bool Equals(GridLocation other)
            => Tile == other.Tile && Plane == other.Plane && Storey == other.Storey;

        public override bool Equals(object obj) => obj is GridLocation other && Equals(other);
        public override int GetHashCode() => unchecked((Tile.GetHashCode() * 397) ^ (Plane * 31) ^ Storey);
        public override string ToString() => $"{Tile} p{Plane} f{Storey}";

        public static bool operator ==(GridLocation left, GridLocation right) => left.Equals(right);
        public static bool operator !=(GridLocation left, GridLocation right) => !left.Equals(right);
    }
}
