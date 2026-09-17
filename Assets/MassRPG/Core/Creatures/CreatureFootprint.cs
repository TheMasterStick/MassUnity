using System;
using System.Collections.Generic;
using MassRPG.Core.World;

namespace MassRPG.Core.Creatures
{
    /// <summary>
    /// Logical rectangular footprint for an actor. Most creatures are 1x1; large creatures may
    /// occupy 2x2, 3x3, etc. Visual meshes may extend beyond the footprint without changing it.
    /// </summary>
    public readonly struct CreatureFootprint : IEquatable<CreatureFootprint>
    {
        public CreatureFootprint(int width, int height)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            Width = width;
            Height = height;
        }

        public int Width { get; }
        public int Height { get; }
        public int TileCount => checked(Width * Height);

        public IReadOnlyList<GridLocation> OccupiedTiles(GridLocation anchor)
        {
            var result = new List<GridLocation>(TileCount);
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    result.Add(new GridLocation(
                        new GridCoord(anchor.Tile.X + x, anchor.Tile.Y + y),
                        anchor.Plane,
                        anchor.Storey));
                }
            }
            return result;
        }

        public bool Contains(GridLocation anchor, GridLocation location)
        {
            if (!anchor.SameLayer(location)) return false;
            return location.Tile.X >= anchor.Tile.X
                && location.Tile.X < anchor.Tile.X + Width
                && location.Tile.Y >= anchor.Tile.Y
                && location.Tile.Y < anchor.Tile.Y + Height;
        }

        public int RangeDistanceTo(GridLocation anchor, GridLocation location)
        {
            if (!anchor.SameLayer(location)) return int.MaxValue;
            var minX = anchor.Tile.X;
            var maxX = anchor.Tile.X + Width - 1;
            var minY = anchor.Tile.Y;
            var maxY = anchor.Tile.Y + Height - 1;
            var dx = location.Tile.X < minX ? minX - location.Tile.X : location.Tile.X > maxX ? location.Tile.X - maxX : 0;
            var dy = location.Tile.Y < minY ? minY - location.Tile.Y : location.Tile.Y > maxY ? location.Tile.Y - maxY : 0;
            return Math.Max(dx, dy);
        }

        public bool Equals(CreatureFootprint other) => Width == other.Width && Height == other.Height;
        public override bool Equals(object obj) => obj is CreatureFootprint other && Equals(other);
        public override int GetHashCode() => unchecked((Width * 397) ^ Height);
        public override string ToString() => $"{Width}x{Height}";
        public static bool operator ==(CreatureFootprint left, CreatureFootprint right) => left.Equals(right);
        public static bool operator !=(CreatureFootprint left, CreatureFootprint right) => !left.Equals(right);
    }
}
