using System;
using System.Collections.Generic;
using MassRPG.Core.Content;
using MassRPG.Core.World;

namespace MassRPG.Data.World.Semantics
{
    public enum WorldAreaShapeKind
    {
        Circle,
        Polygon
    }

    /// <summary>Versionable persistence DTO for named regions, biomes, spawn areas and other overlays.</summary>
    public sealed class WorldAreaDocument
    {
        public const int CurrentFormatVersion = 1;
        public int FormatVersion { get; set; } = CurrentFormatVersion;
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public WorldAreaKind Kind { get; set; }
        public PlayerMapVisibility MapVisibility { get; set; } = PlayerMapVisibility.NotPlayerMapData;
        public int Plane { get; set; } = WorldConstants.SurfacePlane;
        public WorldAreaShapeKind ShapeKind { get; set; } = WorldAreaShapeKind.Polygon;
        public int CenterX { get; set; }
        public int CenterY { get; set; }
        public int RadiusTiles { get; set; }
        public List<RoadPointDocument> Points { get; set; } = new List<RoadPointDocument>();
    }

    public static class WorldAreaDocumentCodec
    {
        public static WorldAreaDocument Encode(WorldAreaDefinition area)
        {
            if (area == null) throw new ArgumentNullException(nameof(area));
            var document = new WorldAreaDocument
            {
                Id = area.Id.Value,
                DisplayName = area.DisplayName,
                Kind = area.Kind,
                MapVisibility = area.MapVisibility,
                Plane = area.Plane
            };

            if (area.Shape is CircleAreaShape circle)
            {
                document.ShapeKind = WorldAreaShapeKind.Circle;
                document.CenterX = circle.Center.X;
                document.CenterY = circle.Center.Y;
                document.RadiusTiles = circle.RadiusTiles;
            }
            else if (area.Shape is PolygonAreaShape polygon)
            {
                document.ShapeKind = WorldAreaShapeKind.Polygon;
                for (var i = 0; i < polygon.Points.Count; i++)
                    document.Points.Add(new RoadPointDocument { X = polygon.Points[i].X, Y = polygon.Points[i].Y });
            }
            else
            {
                throw new NotSupportedException($"Area shape '{area.Shape.GetType().Name}' is not persistable yet.");
            }
            return document;
        }

        public static WorldAreaDefinition Decode(WorldAreaDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (document.FormatVersion != WorldAreaDocument.CurrentFormatVersion)
                throw new InvalidOperationException($"Unsupported world area format version {document.FormatVersion}.");
            if (!ContentId.TryCreate(document.Id, out var id))
                throw new InvalidOperationException("Area document has an invalid stable ID.");

            WorldAreaShape shape;
            switch (document.ShapeKind)
            {
                case WorldAreaShapeKind.Circle:
                    if (document.RadiusTiles < 0) throw new InvalidOperationException("Area circle has a negative radius.");
                    shape = new CircleAreaShape(new GridCoord(document.CenterX, document.CenterY), document.RadiusTiles);
                    break;
                case WorldAreaShapeKind.Polygon:
                    if (document.Points == null || document.Points.Count < 3)
                        throw new InvalidOperationException("Area polygon requires at least three points.");
                    var points = new List<GridCoord>(document.Points.Count);
                    for (var i = 0; i < document.Points.Count; i++)
                        points.Add(new GridCoord(document.Points[i].X, document.Points[i].Y));
                    shape = new PolygonAreaShape(points);
                    break;
                default:
                    throw new InvalidOperationException("Unknown world area shape kind.");
            }

            return new WorldAreaDefinition(
                id,
                document.DisplayName,
                document.Kind,
                shape,
                document.MapVisibility,
                document.Plane);
        }
    }
}
