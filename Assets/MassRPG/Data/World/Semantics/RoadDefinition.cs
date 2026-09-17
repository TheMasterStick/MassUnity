using System;
using System.Collections.Generic;
using MassRPG.Core.Content;
using MassRPG.Core.World;

namespace MassRPG.Data.World.Semantics
{
    /// <summary>
    /// Authored road polyline. Roads are public navigation/world-guidance data, not fast-travel
    /// links. The polyline is semantic; visual meshes/decals and pathfinding preference are derived
    /// from it so the road can be edited without painting thousands of independent scene objects.
    /// </summary>
    public sealed class RoadDefinition
    {
        private readonly List<GridCoord> _points;

        public RoadDefinition(ContentId id, string displayName, IEnumerable<GridCoord> points, int plane = WorldConstants.SurfacePlane)
        {
            if (id.IsEmpty) throw new ArgumentException("Road id cannot be empty.", nameof(id));
            if (points == null) throw new ArgumentNullException(nameof(points));
            _points = new List<GridCoord>(points);
            if (_points.Count < 2) throw new ArgumentException("A road requires at least two points.", nameof(points));
            for (var i = 0; i < _points.Count; i++)
                if (!WorldConstants.IsInsideWorld(_points[i])) throw new ArgumentOutOfRangeException(nameof(points), "Road point is outside the authored world.");
            Id = id;
            DisplayName = displayName ?? string.Empty;
            Plane = plane;
        }

        public ContentId Id { get; }
        public string DisplayName { get; set; }
        public IReadOnlyList<GridCoord> Points => _points;
        public int Plane { get; }

        /// <summary>Logical visual/path corridor width centred on the polyline.</summary>
        public int WidthTiles
        {
            get => _widthTiles;
            set
            {
                if (value < 1 || value > 64) throw new ArgumentOutOfRangeException(nameof(value));
                _widthTiles = value;
            }
        }

        /// <summary>Optional authored terrain surface used by visual generation under the road.</summary>
        public ContentId? SurfaceGroundId { get; set; }
        public bool VisibleOnPlayerMap { get; set; } = true;

        /// <summary>Potential slight traversal bonus. 1.0 means no physical movement-speed bonus.</summary>
        public double MovementSpeedMultiplier
        {
            get => _movementSpeedMultiplier;
            set
            {
                if (value < 1.0 || value > 2.0) throw new ArgumentOutOfRangeException(nameof(value));
                _movementSpeedMultiplier = value;
            }
        }

        /// <summary>Route planner preference multiplier. Values below 1 make a road path cheaper/more attractive.</summary>
        public double RoutePreferenceWeight
        {
            get => _routePreferenceWeight;
            set
            {
                if (value <= 0.0 || value > 4.0) throw new ArgumentOutOfRangeException(nameof(value));
                _routePreferenceWeight = value;
            }
        }

        /// <summary>Spawn systems may use this as authored guidance; roads do not themselves delete spawns.</summary>
        public bool ReduceAggressiveSpawns { get; set; } = true;

        private int _widthTiles = 3;
        private double _movementSpeedMultiplier = 1.0;
        private double _routePreferenceWeight = 0.92;
    }
}
