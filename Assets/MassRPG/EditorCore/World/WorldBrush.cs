using System;
using System.Collections.Generic;
using MassRPG.Core.World;

namespace MassRPG.EditorCore.World
{
    public static class WorldBrush
    {
        public static IReadOnlyList<GridLocation> Cells(GridLocation center, int size, BrushShape shape = BrushShape.Square)
        {
            if (size <= 0 || size % 2 == 0)
                throw new ArgumentOutOfRangeException(nameof(size), "Brush sizes must be positive odd values such as 1, 3, 5 or 7.");

            var radius = size / 2;
            var cells = new List<GridLocation>(size * size);
            for (var dy = -radius; dy <= radius; dy++)
            {
                for (var dx = -radius; dx <= radius; dx++)
                {
                    if (shape == BrushShape.Circle && dx * dx + dy * dy > radius * radius) continue;
                    var tile = new GridCoord(center.Tile.X + dx, center.Tile.Y + dy);
                    if (!WorldConstants.IsInsideWorld(tile)) continue;
                    cells.Add(new GridLocation(tile, center.Plane, center.Storey));
                }
            }

            return cells;
        }
    }
}
