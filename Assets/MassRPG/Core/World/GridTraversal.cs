using System;

namespace MassRPG.Core.World
{
    public static class GridTraversal
    {
        /// <summary>
        /// Validates one logical step. Diagonal movement is supported but cannot cut corners:
        /// both orthogonal cells and all four cardinal edges around the corner must be usable.
        /// Diagonal steps never change logical elevation; elevation changes use explicit cardinal
        /// ramps/stairs or separate traversal connections.
        /// </summary>
        public static bool CanStep(IGridTraversalMap map, GridLocation from, GridLocation to)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (!from.SameLayer(to)) return false;
            if (!GridMath.IsSingleStep(from.Tile, to.Tile)) return false;
            if (!map.IsWalkable(to)) return false;

            if (!GridMath.IsDiagonalStep(from.Tile, to.Tile))
                return map.CanTraverseCardinalEdge(from, to);

            if (map.GetLogicalElevation(from) != map.GetLogicalElevation(to)) return false;

            GridMath.GetDiagonalCornerCells(from.Tile, to.Tile, out var firstTile, out var secondTile);
            var first = new GridLocation(firstTile, from.Plane, from.Storey);
            var second = new GridLocation(secondTile, from.Plane, from.Storey);

            if (!map.IsWalkable(first) || !map.IsWalkable(second)) return false;

            // Requiring both L-shaped routes to be open prevents slipping through the corner of
            // walls, fences, trees, cliff edges, closed doors, or other occupied cells.
            return map.CanTraverseCardinalEdge(from, first)
                && map.CanTraverseCardinalEdge(first, to)
                && map.CanTraverseCardinalEdge(from, second)
                && map.CanTraverseCardinalEdge(second, to);
        }
    }
}
