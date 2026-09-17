using System;

namespace MassRPG.Core.World
{
    /// <summary>
    /// Shared logical-grid math. Range uses Chebyshev distance so a range of N reaches
    /// N tiles horizontally, vertically or diagonally, matching the settled gameplay rules.
    /// </summary>
    public static class GridMath
    {
        public static int RangeDistance(GridCoord a, GridCoord b)
        {
            return Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
        }

        public static bool IsSameOrAdjacent(GridCoord a, GridCoord b) => RangeDistance(a, b) <= 1;

        public static bool IsAdjacent(GridCoord a, GridCoord b)
        {
            return a != b && RangeDistance(a, b) == 1;
        }

        public static bool IsSingleStep(GridCoord from, GridCoord to) => IsAdjacent(from, to);

        public static bool IsDiagonalStep(GridCoord from, GridCoord to)
        {
            return Math.Abs(to.X - from.X) == 1 && Math.Abs(to.Y - from.Y) == 1;
        }

        /// <summary>
        /// For a diagonal step, returns the two orthogonal cells that form the corner.
        /// Movement/melee/projectile systems can require the relevant corner to be open
        /// instead of allowing diagonal squeezing through two blocked edges.
        /// </summary>
        public static void GetDiagonalCornerCells(
            GridCoord from,
            GridCoord to,
            out GridCoord first,
            out GridCoord second)
        {
            if (!IsDiagonalStep(from, to))
                throw new ArgumentException("The supplied coordinates do not form a diagonal single-tile step.");

            first = new GridCoord(to.X, from.Y);
            second = new GridCoord(from.X, to.Y);
        }
    }
}
