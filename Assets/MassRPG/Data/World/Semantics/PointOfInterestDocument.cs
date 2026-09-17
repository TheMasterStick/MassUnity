using System;
using System.Collections.Generic;
using MassRPG.Core.Content;
using MassRPG.Core.World;

namespace MassRPG.Data.World.Semantics
{
    public sealed class PointOfInterestDocument
    {
        public const int CurrentFormatVersion = 1;
        public int FormatVersion { get; set; } = CurrentFormatVersion;
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public PointOfInterestKind Kind { get; set; }
        public MapMarkerCategory MarkerCategory { get; set; }
        public PlayerMapVisibility MapVisibility { get; set; } = PlayerMapVisibility.Public;
        public int X { get; set; }
        public int Y { get; set; }
        public int Plane { get; set; } = WorldConstants.SurfacePlane;
        public int Storey { get; set; }
        public ShapeDocument VisibleFootprint { get; set; }
        public ShapeDocument ProtectionFootprint { get; set; }
        public List<string> ServiceTags { get; set; } = new List<string>();
    }

    public sealed class ShapeDocument
    {
        public WorldAreaShapeKind Kind { get; set; }
        public int CenterX { get; set; }
        public int CenterY { get; set; }
        public int RadiusTiles { get; set; }
        public List<RoadPointDocument> Points { get; set; } = new List<RoadPointDocument>();
    }

    public static class PointOfInterestDocumentCodec
    {
        public static PointOfInterestDocument Encode(PointOfInterestDefinition poi)
        {
            if (poi == null) throw new ArgumentNullException(nameof(poi));
            var document = new PointOfInterestDocument
            {
                Id = poi.Id.Value,
                DisplayName = poi.DisplayName,
                Kind = poi.Kind,
                MarkerCategory = poi.MarkerCategory,
                MapVisibility = poi.MapVisibility,
                X = poi.Center.Tile.X,
                Y = poi.Center.Tile.Y,
                Plane = poi.Center.Plane,
                Storey = poi.Center.Storey,
                VisibleFootprint = EncodeShape(poi.VisibleFootprint),
                ProtectionFootprint = EncodeShape(poi.ProtectionFootprint)
            };
            for (var i = 0; i < poi.ServiceTags.Count; i++) document.ServiceTags.Add(poi.ServiceTags[i].Value);
            return document;
        }

        public static PointOfInterestDefinition Decode(PointOfInterestDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (document.FormatVersion != PointOfInterestDocument.CurrentFormatVersion)
                throw new InvalidOperationException($"Unsupported POI format version {document.FormatVersion}.");
            if (!ContentId.TryCreate(document.Id, out var id))
                throw new InvalidOperationException("POI document has an invalid stable ID.");
            var center = new GridLocation(new GridCoord(document.X, document.Y), document.Plane, document.Storey);
            if (!WorldConstants.IsInsideWorld(center.Tile)) throw new InvalidOperationException("POI centre is outside the authored world.");

            var poi = new PointOfInterestDefinition(
                id,
                document.DisplayName,
                document.Kind,
                center,
                document.MarkerCategory,
                document.MapVisibility)
            {
                VisibleFootprint = DecodeShape(document.VisibleFootprint),
                ProtectionFootprint = DecodeShape(document.ProtectionFootprint)
            };

            if (document.ServiceTags != null)
            {
                for (var i = 0; i < document.ServiceTags.Count; i++)
                {
                    if (!ContentId.TryCreate(document.ServiceTags[i], out var tag))
                        throw new InvalidOperationException($"POI service tag '{document.ServiceTags[i]}' is invalid.");
                    poi.AddServiceTag(tag);
                }
            }
            return poi;
        }

        private static ShapeDocument EncodeShape(WorldAreaShape shape)
        {
            if (shape == null) return null;
            if (shape is CircleAreaShape circle)
            {
                return new ShapeDocument
                {
                    Kind = WorldAreaShapeKind.Circle,
                    CenterX = circle.Center.X,
                    CenterY = circle.Center.Y,
                    RadiusTiles = circle.RadiusTiles
                };
            }
            if (shape is PolygonAreaShape polygon)
            {
                var document = new ShapeDocument { Kind = WorldAreaShapeKind.Polygon };
                for (var i = 0; i < polygon.Points.Count; i++)
                    document.Points.Add(new RoadPointDocument { X = polygon.Points[i].X, Y = polygon.Points[i].Y });
                return document;
            }
            throw new NotSupportedException($"POI shape '{shape.GetType().Name}' is not persistable yet.");
        }

        private static WorldAreaShape DecodeShape(ShapeDocument document)
        {
            if (document == null) return null;
            if (document.Kind == WorldAreaShapeKind.Circle)
                return new CircleAreaShape(new GridCoord(document.CenterX, document.CenterY), document.RadiusTiles);
            if (document.Kind != WorldAreaShapeKind.Polygon || document.Points == null || document.Points.Count < 3)
                throw new InvalidOperationException("POI polygon footprint requires at least three points.");
            var points = new List<GridCoord>(document.Points.Count);
            for (var i = 0; i < document.Points.Count; i++)
                points.Add(new GridCoord(document.Points[i].X, document.Points[i].Y));
            return new PolygonAreaShape(points);
        }
    }
}
