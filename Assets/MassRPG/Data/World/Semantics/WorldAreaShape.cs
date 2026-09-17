using System;
using System.Collections.Generic;
using MassRPG.Core.World;

namespace MassRPG.Data.World.Semantics
{
    public abstract class WorldAreaShape
    {
        public abstract bool Contains(GridCoord tile);
    }

    public sealed class CircleAreaShape : WorldAreaShape
    {
        public CircleAreaShape(GridCoord center, int radiusTiles)
        {
            if (radiusTiles < 0) throw new ArgumentOutOfRangeException(nameof(radiusTiles));
            Center = center;
            RadiusTiles = radiusTiles;
        }

        public GridCoord Center { get; }
        public int RadiusTiles { get; }

        public override bool Contains(GridCoord tile)
        {
            var dx = (long)tile.X - Center.X;
            var dy = (long)tile.Y - Center.Y;
            var radius = (long)RadiusTiles;
            return dx * dx + dy * dy <= radius * radius;
        }
    }

    public sealed class PolygonAreaShape : WorldAreaShape
    {
        private readonly List<GridCoord> _points;

        public PolygonAreaShape(IEnumerable<GridCoord> points)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            _points = new List<GridCoord>(points);
            if (_points.Count < 3) throw new ArgumentException("A polygon requires at least three points.", nameof(points));
        }

        public IReadOnlyList<GridCoord> Points => _points;

        public override bool Contains(GridCoord tile)
        {
            var inside = false;
            for (int i = 0, j = _points.Count - 1; i < _points.Count; j = i++)
            {
                var a = _points[j];
                var b = _points[i];
                if (PointOnSegment(tile, a, b)) return true;

                var crosses = (a.Y > tile.Y) != (b.Y > tile.Y);
                if (!crosses) continue;
                var intersectionX = a.X + (double)(b.X - a.X) * (tile.Y - a.Y) / (b.Y - a.Y);
                if (tile.X < intersectionX) inside = !inside;
            }
            return inside;
        }

        private static bool PointOnSegment(GridCoord p, GridCoord a, GridCoord b)
        {
            var cross = ((long)p.Y - a.Y) * (b.X - a.X) - ((long)p.X - a.X) * (b.Y - a.Y);
            if (cross != 0) return false;
            return p.X >= Math.Min(a.X, b.X) && p.X <= Math.Max(a.X, b.X)
                && p.Y >= Math.Min(a.Y, b.Y) && p.Y <= Math.Max(a.Y, b.Y);
        }
    }
}
