using System;

namespace MassRPG.Core.World
{
    /// <summary>
    /// Logical-grid line of sight for ranged/magic combat. Elevation is intentionally ignored;
    /// only explicit LOS-blocking geometry matters. Diagonal corner gaps are rejected when neither
    /// orthogonal route around that corner is visually open.
    /// </summary>
    public static class GridLineOfSight
    {
        public static bool HasLineOfSight(IRangedLineOfSightMap map, GridLocation start, GridLocation goal)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (!start.SameLayer(goal)) return false;
            if (start == goal) return true;

            var x = start.Tile.X;
            var y = start.Tile.Y;
            var targetX = goal.Tile.X;
            var targetY = goal.Tile.Y;
            var dx = Math.Abs(targetX - x);
            var sx = x < targetX ? 1 : -1;
            var dy = -Math.Abs(targetY - y);
            var sy = y < targetY ? 1 : -1;
            var error = dx + dy;
            var current = start;

            while (x != targetX || y != targetY)
            {
                var e2 = 2 * error;
                var nextX = x;
                var nextY = y;
                if (e2 >= dy)
                {
                    error += dy;
                    nextX += sx;
                }
                if (e2 <= dx)
                {
                    error += dx;
                    nextY += sy;
                }

                var next = new GridLocation(new GridCoord(nextX, nextY), start.Plane, start.Storey);
                if (!CanSeeStep(map, current, next)) return false;
                current = next;
                x = nextX;
                y = nextY;
            }

            return true;
        }

        private static bool CanSeeStep(IRangedLineOfSightMap map, GridLocation from, GridLocation to)
        {
            var dx = Math.Abs(to.Tile.X - from.Tile.X);
            var dy = Math.Abs(to.Tile.Y - from.Tile.Y);
            if (dx > 1 || dy > 1 || dx + dy == 0) return false;
            if (map.IsRangedLineOfSightBlockingTile(to)) return false;

            if (dx + dy == 1)
                return !map.IsRangedLineOfSightBlockedCardinalEdge(from, to);

            GridMath.GetDiagonalCornerCells(from.Tile, to.Tile, out var firstTile, out var secondTile);
            var first = new GridLocation(firstTile, from.Plane, from.Storey);
            var second = new GridLocation(secondTile, from.Plane, from.Storey);

            var firstRouteOpen = !map.IsRangedLineOfSightBlockingTile(first)
                && !map.IsRangedLineOfSightBlockedCardinalEdge(from, first)
                && !map.IsRangedLineOfSightBlockedCardinalEdge(first, to);
            var secondRouteOpen = !map.IsRangedLineOfSightBlockingTile(second)
                && !map.IsRangedLineOfSightBlockedCardinalEdge(from, second)
                && !map.IsRangedLineOfSightBlockedCardinalEdge(second, to);

            return firstRouteOpen || secondRouteOpen;
        }
    }
}
