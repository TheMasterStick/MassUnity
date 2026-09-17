using System;
using System.Collections.Generic;
using MassRPG.Core.World;
using MassRPG.Data.World;

namespace MassRPG.EditorCore.World
{
    /// <summary>
    /// Engine-independent raster helpers for tactile map tools. These operate on logical tile
    /// coordinates, so the Unity UI can use the same exact geometry for previews and committed edits.
    /// </summary>
    public static class WorldTileGeometry
    {
        /// <summary>Inclusive Bresenham line. Every returned coordinate is unique and 8-connected.</summary>
        public static IReadOnlyList<GridCoord> Line(GridCoord from, GridCoord to)
        {
            var result = new List<GridCoord>();
            var x0 = from.X;
            var y0 = from.Y;
            var x1 = to.X;
            var y1 = to.Y;
            var dx = Math.Abs(x1 - x0);
            var sx = x0 < x1 ? 1 : -1;
            var dy = -Math.Abs(y1 - y0);
            var sy = y0 < y1 ? 1 : -1;
            var error = dx + dy;

            while (true)
            {
                result.Add(new GridCoord(x0, y0));
                if (x0 == x1 && y0 == y1) break;
                var twice = 2 * error;
                if (twice >= dy) { error += dy; x0 += sx; }
                if (twice <= dx) { error += dx; y0 += sy; }
            }
            return result;
        }

        /// <summary>Inclusive filled axis-aligned rectangle regardless of drag direction.</summary>
        public static IReadOnlyList<GridCoord> FilledRectangle(GridCoord a, GridCoord b)
        {
            var minX = Math.Min(a.X, b.X);
            var maxX = Math.Max(a.X, b.X);
            var minY = Math.Min(a.Y, b.Y);
            var maxY = Math.Max(a.Y, b.Y);
            var width = checked(maxX - minX + 1);
            var height = checked(maxY - minY + 1);
            var result = new List<GridCoord>(checked(width * height));
            for (var y = minY; y <= maxY; y++)
                for (var x = minX; x <= maxX; x++)
                    result.Add(new GridCoord(x, y));
            return result;
        }

        /// <summary>Inclusive rectangle outline, useful for walls, borders and region blocking passes.</summary>
        public static IReadOnlyList<GridCoord> RectangleOutline(GridCoord a, GridCoord b)
        {
            var minX = Math.Min(a.X, b.X);
            var maxX = Math.Max(a.X, b.X);
            var minY = Math.Min(a.Y, b.Y);
            var maxY = Math.Max(a.Y, b.Y);
            var result = new List<GridCoord>();

            for (var x = minX; x <= maxX; x++) result.Add(new GridCoord(x, minY));
            if (maxY != minY)
                for (var x = minX; x <= maxX; x++) result.Add(new GridCoord(x, maxY));
            for (var y = minY + 1; y < maxY; y++)
            {
                result.Add(new GridCoord(minX, y));
                if (maxX != minX) result.Add(new GridCoord(maxX, y));
            }
            return result;
        }
    }

    /// <summary>
    /// Reusable rectangular logical-tile stamp. Surface data is copied by default; edge masks are
    /// optional because copying a border edge without its neighbour can create asymmetric barriers.
    /// </summary>
    public sealed class WorldTileStamp
    {
        private readonly AuthoredTileCell[] _cells;

        private WorldTileStamp(int width, int height, AuthoredTileCell[] cells, bool includesEdges)
        {
            Width = width;
            Height = height;
            _cells = cells;
            IncludesEdges = includesEdges;
        }

        public int Width { get; }
        public int Height { get; }
        public bool IncludesEdges { get; }
        public int CellCount => _cells.Length;

        public AuthoredTileCell GetCell(int x, int y)
        {
            if (x < 0 || x >= Width) throw new ArgumentOutOfRangeException(nameof(x));
            if (y < 0 || y >= Height) throw new ArgumentOutOfRangeException(nameof(y));
            return _cells[y * Width + x];
        }

        public static WorldTileStamp Capture(
            AuthoredWorldPageStore store,
            GridLocation a,
            GridLocation b,
            bool includeEdges = false)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            if (!a.SameLayer(b)) throw new ArgumentException("A stamp selection must stay on one plane/storey.");
            var minX = Math.Min(a.Tile.X, b.Tile.X);
            var maxX = Math.Max(a.Tile.X, b.Tile.X);
            var minY = Math.Min(a.Tile.Y, b.Tile.Y);
            var maxY = Math.Max(a.Tile.Y, b.Tile.Y);
            var width = checked(maxX - minX + 1);
            var height = checked(maxY - minY + 1);
            var cells = new AuthoredTileCell[checked(width * height)];

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var location = new GridLocation(new GridCoord(minX + x, minY + y), a.Plane, a.Storey);
                    if (!store.TryGetCell(location, out var cell))
                        throw new InvalidOperationException($"Cannot capture unloaded world cell {location}.");
                    cells[y * width + x] = includeEdges ? cell : WithoutEdges(cell);
                }
            }
            return new WorldTileStamp(width, height, cells, includeEdges);
        }

        public int Paste(WorldEditSession session, GridLocation topLeft, string label = "Paste tile stamp")
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            var edits = new List<WorldCellEdit>(_cells.Length);
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    var tile = new GridCoord(topLeft.Tile.X + x, topLeft.Tile.Y + y);
                    if (!WorldConstants.IsInsideWorld(tile)) continue;
                    edits.Add(new WorldCellEdit(new GridLocation(tile, topLeft.Plane, topLeft.Storey), GetCell(x, y)));
                }
            }
            return session.Apply(label, edits);
        }

        private static AuthoredTileCell WithoutEdges(AuthoredTileCell cell)
            => new AuthoredTileCell(
                cell.GroundId,
                cell.Elevation,
                cell.Flags,
                CardinalEdgeMask.None,
                CardinalEdgeMask.None,
                CardinalEdgeMask.None);
    }
}
