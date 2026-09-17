using System;

namespace MassRPG.Core.World
{
    public readonly struct WorldPageCoord : IEquatable<WorldPageCoord>
    {
        public WorldPageCoord(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }
        public int Y { get; }

        public bool Equals(WorldPageCoord other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is WorldPageCoord other && Equals(other);
        public override int GetHashCode() => unchecked((X * 397) ^ Y);
        public override string ToString() => $"({X}, {Y})";
        public static bool operator ==(WorldPageCoord left, WorldPageCoord right) => left.Equals(right);
        public static bool operator !=(WorldPageCoord left, WorldPageCoord right) => !left.Equals(right);
    }

    public readonly struct WorldPageKey : IEquatable<WorldPageKey>
    {
        public WorldPageKey(WorldPageCoord page, int plane, int storey)
        {
            Page = page;
            Plane = plane;
            Storey = storey;
        }

        public WorldPageCoord Page { get; }
        public int Plane { get; }
        public int Storey { get; }

        public bool Equals(WorldPageKey other)
            => Page == other.Page && Plane == other.Plane && Storey == other.Storey;
        public override bool Equals(object obj) => obj is WorldPageKey other && Equals(other);
        public override int GetHashCode() => unchecked((Page.GetHashCode() * 397) ^ (Plane * 31) ^ Storey);
        public override string ToString() => $"page {Page} p{Plane} f{Storey}";
        public static bool operator ==(WorldPageKey left, WorldPageKey right) => left.Equals(right);
        public static bool operator !=(WorldPageKey left, WorldPageKey right) => !left.Equals(right);
    }

    public readonly struct WorldPageAddress
    {
        public WorldPageAddress(WorldPageKey key, int localX, int localY, int pageSize)
        {
            Key = key;
            LocalX = localX;
            LocalY = localY;
            PageSize = pageSize;
        }

        public WorldPageKey Key { get; }
        public int LocalX { get; }
        public int LocalY { get; }
        public int PageSize { get; }
        public int LinearIndex => LocalY * PageSize + LocalX;
    }

    public static class WorldAddressing
    {
        public static bool TryResolve(GridLocation location, out WorldPageAddress address, int pageSize = WorldConstants.DefaultStoragePageSize)
        {
            if (pageSize <= 0) throw new ArgumentOutOfRangeException(nameof(pageSize));
            if (!WorldConstants.IsInsideWorld(location.Tile))
            {
                address = default;
                return false;
            }

            var pageX = location.Tile.X / pageSize;
            var pageY = location.Tile.Y / pageSize;
            var localX = location.Tile.X - pageX * pageSize;
            var localY = location.Tile.Y - pageY * pageSize;
            address = new WorldPageAddress(
                new WorldPageKey(new WorldPageCoord(pageX, pageY), location.Plane, location.Storey),
                localX,
                localY,
                pageSize);
            return true;
        }
    }
}
