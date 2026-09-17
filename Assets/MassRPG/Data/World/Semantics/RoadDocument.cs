using System;
using System.Collections.Generic;
using MassRPG.Core.Content;
using MassRPG.Core.World;

namespace MassRPG.Data.World.Semantics
{
    /// <summary>Versionable persistence DTO for a semantic authored road.</summary>
    public sealed class RoadDocument
    {
        public const int CurrentFormatVersion = 1;
        public int FormatVersion { get; set; } = CurrentFormatVersion;
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int Plane { get; set; } = WorldConstants.SurfacePlane;
        public int WidthTiles { get; set; } = 3;
        public string SurfaceGroundId { get; set; } = string.Empty;
        public bool VisibleOnPlayerMap { get; set; } = true;
        public double MovementSpeedMultiplier { get; set; } = 1.0;
        public double RoutePreferenceWeight { get; set; } = 0.92;
        public bool ReduceAggressiveSpawns { get; set; } = true;
        public List<RoadPointDocument> Points { get; set; } = new List<RoadPointDocument>();
    }

    public sealed class RoadPointDocument
    {
        public int X { get; set; }
        public int Y { get; set; }
    }

    public static class RoadDocumentCodec
    {
        public static RoadDocument Encode(RoadDefinition road)
        {
            if (road == null) throw new ArgumentNullException(nameof(road));
            var document = new RoadDocument
            {
                Id = road.Id.Value,
                DisplayName = road.DisplayName,
                Plane = road.Plane,
                WidthTiles = road.WidthTiles,
                SurfaceGroundId = road.SurfaceGroundId.HasValue ? road.SurfaceGroundId.Value.Value : string.Empty,
                VisibleOnPlayerMap = road.VisibleOnPlayerMap,
                MovementSpeedMultiplier = road.MovementSpeedMultiplier,
                RoutePreferenceWeight = road.RoutePreferenceWeight,
                ReduceAggressiveSpawns = road.ReduceAggressiveSpawns
            };
            for (var i = 0; i < road.Points.Count; i++)
                document.Points.Add(new RoadPointDocument { X = road.Points[i].X, Y = road.Points[i].Y });
            return document;
        }

        public static RoadDefinition Decode(RoadDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (document.FormatVersion != RoadDocument.CurrentFormatVersion)
                throw new InvalidOperationException($"Unsupported road format version {document.FormatVersion}.");
            if (!ContentId.TryCreate(document.Id, out var id))
                throw new InvalidOperationException("Road document has an invalid stable ID.");
            if (document.Points == null || document.Points.Count < 2)
                throw new InvalidOperationException("Road document requires at least two points.");

            var points = new List<GridCoord>(document.Points.Count);
            for (var i = 0; i < document.Points.Count; i++)
                points.Add(new GridCoord(document.Points[i].X, document.Points[i].Y));

            var road = new RoadDefinition(id, document.DisplayName, points, document.Plane)
            {
                WidthTiles = document.WidthTiles,
                VisibleOnPlayerMap = document.VisibleOnPlayerMap,
                MovementSpeedMultiplier = document.MovementSpeedMultiplier,
                RoutePreferenceWeight = document.RoutePreferenceWeight,
                ReduceAggressiveSpawns = document.ReduceAggressiveSpawns
            };
            if (!string.IsNullOrWhiteSpace(document.SurfaceGroundId))
            {
                if (!ContentId.TryCreate(document.SurfaceGroundId, out var surface))
                    throw new InvalidOperationException("Road document has an invalid surface ground ID.");
                road.SurfaceGroundId = surface;
            }
            return road;
        }
    }
}
