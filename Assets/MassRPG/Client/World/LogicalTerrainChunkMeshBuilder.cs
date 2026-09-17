using System;
using System.Collections.Generic;
using MassRPG.Core.World;
using MassRPG.Data.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace MassRPG.Client.World
{
    /// <summary>
    /// Production-bound visual mesh builder for exact authored logical terrain. It renders one top
    /// surface per loaded 1x1 cell, slopes lower cells toward legal +1 elevation-transition edges,
    /// and renders vertical cliff faces where a higher cell borders a lower non-ramp neighbour.
    /// Logical elevation/pathing remain data; visual meshes are disposable chunk views.
    /// </summary>
    public static class LogicalTerrainChunkMeshBuilder
    {
        public static Mesh Build(
            AuthoredWorldPageStore store,
            int renderChunkX,
            int renderChunkY,
            int plane,
            int storey,
            float tileSize,
            float elevationStepHeight,
            int renderChunkSize = WorldConstants.DefaultRenderChunkSize,
            bool colorizeForPreview = false)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            if (renderChunkSize <= 0) throw new ArgumentOutOfRangeException(nameof(renderChunkSize));
            tileSize = Mathf.Max(0.01f, tileSize);
            elevationStepHeight = Mathf.Max(0.01f, elevationStepHeight);

            var vertices = new List<Vector3>(renderChunkSize * renderChunkSize * 4);
            var triangles = new List<int>(renderChunkSize * renderChunkSize * 6);
            var uvs = new List<Vector2>(renderChunkSize * renderChunkSize * 4);
            var colors = colorizeForPreview ? new List<Color32>(renderChunkSize * renderChunkSize * 4) : null;
            var startX = renderChunkX * renderChunkSize;
            var startY = renderChunkY * renderChunkSize;

            for (var localY = 0; localY < renderChunkSize; localY++)
            {
                for (var localX = 0; localX < renderChunkSize; localX++)
                {
                    var tile = new GridCoord(startX + localX, startY + localY);
                    if (!WorldConstants.IsInsideWorld(tile)) continue;
                    var location = new GridLocation(tile, plane, storey);
                    if (!store.TryGetCell(location, out var cell)) continue;

                    var baseHeight = cell.Elevation * elevationStepHeight;
                    var cornerHeights = GetTopCornerHeights(store, location, cell, baseHeight, elevationStepHeight);
                    AddTop(
                        vertices,
                        triangles,
                        uvs,
                        localX * tileSize,
                        localY * tileSize,
                        tileSize,
                        cornerHeights.NorthWest,
                        cornerHeights.SouthWest,
                        cornerHeights.SouthEast,
                        cornerHeights.NorthEast);

                    AddCliffIfLower(store, vertices, triangles, uvs, location, cell,
                        new GridCoord(tile.X, tile.Y - 1), localX, localY, CardinalEdgeMask.North,
                        tileSize, elevationStepHeight);
                    AddCliffIfLower(store, vertices, triangles, uvs, location, cell,
                        new GridCoord(tile.X + 1, tile.Y), localX, localY, CardinalEdgeMask.East,
                        tileSize, elevationStepHeight);
                    AddCliffIfLower(store, vertices, triangles, uvs, location, cell,
                        new GridCoord(tile.X, tile.Y + 1), localX, localY, CardinalEdgeMask.South,
                        tileSize, elevationStepHeight);
                    AddCliffIfLower(store, vertices, triangles, uvs, location, cell,
                        new GridCoord(tile.X - 1, tile.Y), localX, localY, CardinalEdgeMask.West,
                        tileSize, elevationStepHeight);
                    if (colors != null)
                    {
                        var color = PreviewColor(cell);
                        while (colors.Count < vertices.Count) colors.Add(color);
                    }
                }
            }

            var mesh = new Mesh { name = $"MassRPG Terrain {renderChunkX},{renderChunkY} p{plane}" };
            if (vertices.Count > ushort.MaxValue) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0, true);
            mesh.SetUVs(0, uvs);
            if (colors != null) mesh.SetColors(colors);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Color32 PreviewColor(AuthoredTileCell cell)
        {
            if ((cell.Flags & TileFlags.DeepWater) != 0) return new Color32(18, 55, 101, 255);
            if ((cell.Flags & TileFlags.Water) != 0) return new Color32(28, 105, 155, 255);
            var id = cell.GroundId.Value ?? string.Empty;
            if (id.Contains("snow")) return new Color32(205, 216, 220, 255);
            if (id.Contains("sand")) return new Color32(189, 166, 109, 255);
            if (id.Contains("swamp")) return new Color32(73, 94, 60, 255);
            if (id.Contains("stone") || id.Contains("rock")) return new Color32(112, 112, 108, 255);
            if (id.Contains("dirt")) return new Color32(125, 90, 61, 255);
            if (id.Contains("grass")) return new Color32(79, 133, 70, 255);
            if (id.Contains("unpainted")) return new Color32(48, 50, 53, 255);
            unchecked
            {
                uint hash = 2166136261;
                foreach (var c in id) { hash ^= c; hash *= 16777619; }
                return (Color32)Color.HSVToRGB((hash % 1000) / 1000f, 0.4f, 0.7f);
            }
        }

        private static TopCornerHeights GetTopCornerHeights(
            AuthoredWorldPageStore store,
            GridLocation current,
            AuthoredTileCell cell,
            float baseHeight,
            float elevationStepHeight)
        {
            var heights = new TopCornerHeights(baseHeight);
            RaiseTransitionEdge(store, current, cell, CardinalEdgeMask.North, new GridCoord(current.Tile.X, current.Tile.Y - 1), elevationStepHeight, ref heights);
            RaiseTransitionEdge(store, current, cell, CardinalEdgeMask.East, new GridCoord(current.Tile.X + 1, current.Tile.Y), elevationStepHeight, ref heights);
            RaiseTransitionEdge(store, current, cell, CardinalEdgeMask.South, new GridCoord(current.Tile.X, current.Tile.Y + 1), elevationStepHeight, ref heights);
            RaiseTransitionEdge(store, current, cell, CardinalEdgeMask.West, new GridCoord(current.Tile.X - 1, current.Tile.Y), elevationStepHeight, ref heights);
            return heights;
        }

        private static void RaiseTransitionEdge(
            AuthoredWorldPageStore store,
            GridLocation current,
            AuthoredTileCell currentCell,
            CardinalEdgeMask edge,
            GridCoord neighbourTile,
            float elevationStepHeight,
            ref TopCornerHeights heights)
        {
            if ((currentCell.ElevationTransitionEdges & edge) == 0) return;
            if (!WorldConstants.IsInsideWorld(neighbourTile)) return;
            var neighbour = new GridLocation(neighbourTile, current.Plane, current.Storey);
            if (!store.TryGetCell(neighbour, out var neighbourCell)) return;
            if (neighbourCell.Elevation != currentCell.Elevation + 1) return;
            if ((neighbourCell.ElevationTransitionEdges & CardinalEdges.Opposite(edge)) == 0) return;

            var raised = neighbourCell.Elevation * elevationStepHeight;
            switch (edge)
            {
                case CardinalEdgeMask.North:
                    heights.NorthWest = Mathf.Max(heights.NorthWest, raised);
                    heights.NorthEast = Mathf.Max(heights.NorthEast, raised);
                    break;
                case CardinalEdgeMask.East:
                    heights.NorthEast = Mathf.Max(heights.NorthEast, raised);
                    heights.SouthEast = Mathf.Max(heights.SouthEast, raised);
                    break;
                case CardinalEdgeMask.South:
                    heights.SouthWest = Mathf.Max(heights.SouthWest, raised);
                    heights.SouthEast = Mathf.Max(heights.SouthEast, raised);
                    break;
                case CardinalEdgeMask.West:
                    heights.NorthWest = Mathf.Max(heights.NorthWest, raised);
                    heights.SouthWest = Mathf.Max(heights.SouthWest, raised);
                    break;
            }
        }

        private static void AddTop(
            List<Vector3> vertices,
            List<int> triangles,
            List<Vector2> uvs,
            float centerX,
            float centerZ,
            float tileSize,
            float northWest,
            float southWest,
            float southEast,
            float northEast)
        {
            var half = tileSize * 0.5f;
            var first = vertices.Count;
            vertices.Add(new Vector3(centerX - half, northWest, centerZ - half));
            vertices.Add(new Vector3(centerX - half, southWest, centerZ + half));
            vertices.Add(new Vector3(centerX + half, southEast, centerZ + half));
            vertices.Add(new Vector3(centerX + half, northEast, centerZ - half));
            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(0f, 1f));
            uvs.Add(new Vector2(1f, 1f));
            uvs.Add(new Vector2(1f, 0f));
            triangles.Add(first);
            triangles.Add(first + 1);
            triangles.Add(first + 2);
            triangles.Add(first);
            triangles.Add(first + 2);
            triangles.Add(first + 3);
        }

        private static void AddCliffIfLower(
            AuthoredWorldPageStore store,
            List<Vector3> vertices,
            List<int> triangles,
            List<Vector2> uvs,
            GridLocation current,
            AuthoredTileCell currentCell,
            GridCoord neighbourTile,
            int localX,
            int localY,
            CardinalEdgeMask edge,
            float tileSize,
            float elevationStepHeight)
        {
            if (!WorldConstants.IsInsideWorld(neighbourTile)) return;
            var neighbour = new GridLocation(neighbourTile, current.Plane, current.Storey);
            if (!store.TryGetCell(neighbour, out var neighbourCell)) return;
            if (neighbourCell.Elevation >= currentCell.Elevation) return;

            // A legal authored +1 transition is drawn as a ramp on the lower tile, not as a cliff on
            // the higher tile. Require transition bits on both sides so corrupt half-edges still show
            // a visible cliff instead of creating a misleading traversable-looking seam.
            var opposite = CardinalEdges.Opposite(edge);
            var isOneStepRamp = currentCell.Elevation == neighbourCell.Elevation + 1
                && (currentCell.ElevationTransitionEdges & edge) != 0
                && (neighbourCell.ElevationTransitionEdges & opposite) != 0;
            if (isOneStepRamp) return;

            var upper = currentCell.Elevation * elevationStepHeight;
            var lower = neighbourCell.Elevation * elevationStepHeight;
            AddCliffFace(vertices, triangles, uvs, localX * tileSize, localY * tileSize, tileSize, lower, upper, edge);
        }

        private static void AddCliffFace(
            List<Vector3> vertices,
            List<int> triangles,
            List<Vector2> uvs,
            float centerX,
            float centerZ,
            float tileSize,
            float lower,
            float upper,
            CardinalEdgeMask edge)
        {
            var half = tileSize * 0.5f;
            Vector3 a;
            Vector3 b;
            Vector3 c;
            Vector3 d;

            switch (edge)
            {
                case CardinalEdgeMask.North:
                    a = new Vector3(centerX + half, lower, centerZ - half);
                    b = new Vector3(centerX - half, lower, centerZ - half);
                    c = new Vector3(centerX - half, upper, centerZ - half);
                    d = new Vector3(centerX + half, upper, centerZ - half);
                    break;
                case CardinalEdgeMask.East:
                    a = new Vector3(centerX + half, lower, centerZ + half);
                    b = new Vector3(centerX + half, lower, centerZ - half);
                    c = new Vector3(centerX + half, upper, centerZ - half);
                    d = new Vector3(centerX + half, upper, centerZ + half);
                    break;
                case CardinalEdgeMask.South:
                    a = new Vector3(centerX - half, lower, centerZ + half);
                    b = new Vector3(centerX + half, lower, centerZ + half);
                    c = new Vector3(centerX + half, upper, centerZ + half);
                    d = new Vector3(centerX - half, upper, centerZ + half);
                    break;
                case CardinalEdgeMask.West:
                    a = new Vector3(centerX - half, lower, centerZ - half);
                    b = new Vector3(centerX - half, lower, centerZ + half);
                    c = new Vector3(centerX - half, upper, centerZ + half);
                    d = new Vector3(centerX - half, upper, centerZ - half);
                    break;
                default:
                    return;
            }

            var first = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
            var verticalUv = Mathf.Max(1f, (upper - lower) / tileSize);
            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(1f, 0f));
            uvs.Add(new Vector2(1f, verticalUv));
            uvs.Add(new Vector2(0f, verticalUv));
            triangles.Add(first);
            triangles.Add(first + 1);
            triangles.Add(first + 2);
            triangles.Add(first);
            triangles.Add(first + 2);
            triangles.Add(first + 3);
        }

        private struct TopCornerHeights
        {
            public TopCornerHeights(float height)
            {
                NorthWest = height;
                SouthWest = height;
                SouthEast = height;
                NorthEast = height;
            }

            public float NorthWest;
            public float SouthWest;
            public float SouthEast;
            public float NorthEast;
        }
    }
}
