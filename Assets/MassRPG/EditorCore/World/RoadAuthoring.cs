using System;
using System.Collections.Generic;
using MassRPG.Core.Content;
using MassRPG.Core.World;
using MassRPG.Data.World.Semantics;

namespace MassRPG.EditorCore.World
{
    /// <summary>
    /// Mutable road draft used only while authoring. Published/runtime roads remain immutable-point
    /// RoadDefinition values so editor gestures never leak into live world state.
    /// </summary>
    public sealed class RoadAuthoringDraft
    {
        private readonly List<GridCoord> _points = new List<GridCoord>();

        public RoadAuthoringDraft(ContentId id, string displayName, int plane = WorldConstants.SurfacePlane)
        {
            if (id.IsEmpty) throw new ArgumentException("Road id cannot be empty.", nameof(id));
            Id = id;
            DisplayName = displayName ?? string.Empty;
            Plane = plane;
        }

        public ContentId Id { get; }
        public string DisplayName { get; set; }
        public int Plane { get; }
        public int WidthTiles { get; set; } = 3;
        public ContentId? SurfaceGroundId { get; set; }
        public bool VisibleOnPlayerMap { get; set; } = true;
        public double MovementSpeedMultiplier { get; set; } = 1.0;
        public double RoutePreferenceWeight { get; set; } = 0.92;
        public bool ReduceAggressiveSpawns { get; set; } = true;
        public IReadOnlyList<GridCoord> Points => _points;
        public bool CanBuild => _points.Count >= 2;

        public void AddPoint(GridCoord point)
        {
            if (!WorldConstants.IsInsideWorld(point)) throw new ArgumentOutOfRangeException(nameof(point));
            if (_points.Count > 0 && _points[_points.Count - 1] == point) return;
            _points.Add(point);
        }

        public void InsertPoint(int index, GridCoord point)
        {
            if (!WorldConstants.IsInsideWorld(point)) throw new ArgumentOutOfRangeException(nameof(point));
            if (index < 0 || index > _points.Count) throw new ArgumentOutOfRangeException(nameof(index));
            _points.Insert(index, point);
        }

        public void MovePoint(int index, GridCoord point)
        {
            if (!WorldConstants.IsInsideWorld(point)) throw new ArgumentOutOfRangeException(nameof(point));
            if (index < 0 || index >= _points.Count) throw new ArgumentOutOfRangeException(nameof(index));
            _points[index] = point;
        }

        public bool RemovePoint(int index)
        {
            if (index < 0 || index >= _points.Count) return false;
            _points.RemoveAt(index);
            return true;
        }

        public void ClearPoints() => _points.Clear();

        public RoadDefinition Build()
        {
            if (!CanBuild) throw new InvalidOperationException("A road needs at least two control points.");
            var road = new RoadDefinition(Id, DisplayName, _points, Plane)
            {
                WidthTiles = WidthTiles,
                SurfaceGroundId = SurfaceGroundId,
                VisibleOnPlayerMap = VisibleOnPlayerMap,
                MovementSpeedMultiplier = MovementSpeedMultiplier,
                RoutePreferenceWeight = RoutePreferenceWeight,
                ReduceAggressiveSpawns = ReduceAggressiveSpawns
            };
            return road;
        }

        public static RoadAuthoringDraft FromDefinition(RoadDefinition road)
        {
            if (road == null) throw new ArgumentNullException(nameof(road));
            var draft = new RoadAuthoringDraft(road.Id, road.DisplayName, road.Plane)
            {
                WidthTiles = road.WidthTiles,
                SurfaceGroundId = road.SurfaceGroundId,
                VisibleOnPlayerMap = road.VisibleOnPlayerMap,
                MovementSpeedMultiplier = road.MovementSpeedMultiplier,
                RoutePreferenceWeight = road.RoutePreferenceWeight,
                ReduceAggressiveSpawns = road.ReduceAggressiveSpawns
            };
            for (var i = 0; i < road.Points.Count; i++) draft.AddPoint(road.Points[i]);
            return draft;
        }
    }

    public static class RoadRasterizer
    {
        /// <summary>
        /// Returns logical tiles touched by a road corridor. This is for editor preview, terrain
        /// surface derivation and route/spawn queries; it does not convert roads into thousands of
        /// independent persisted tile objects.
        /// </summary>
        public static IReadOnlyCollection<GridCoord> Rasterize(RoadDefinition road)
        {
            if (road == null) throw new ArgumentNullException(nameof(road));
            var result = new HashSet<GridCoord>();
            var radius = (road.WidthTiles - 1) * 0.5;
            var integerRadius = (int)Math.Ceiling(radius);

            for (var segment = 1; segment < road.Points.Count; segment++)
            {
                var line = WorldTileGeometry.Line(road.Points[segment - 1], road.Points[segment]);
                for (var i = 0; i < line.Count; i++)
                {
                    var center = line[i];
                    for (var y = center.Y - integerRadius; y <= center.Y + integerRadius; y++)
                    {
                        for (var x = center.X - integerRadius; x <= center.X + integerRadius; x++)
                        {
                            var tile = new GridCoord(x, y);
                            if (!WorldConstants.IsInsideWorld(tile)) continue;
                            var dx = x - center.X;
                            var dy = y - center.Y;
                            // Square-ish corridor with gently clipped corners; easy to read on the 1x1 grid.
                            if (Math.Sqrt(dx * dx + dy * dy) <= radius + 0.55) result.Add(tile);
                        }
                    }
                }
            }
            return result;
        }
    }
}
