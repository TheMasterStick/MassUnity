using System;
using System.Collections.Generic;
using MassRPG.Core.Content;
using MassRPG.Core.World;
using MassRPG.Data.World.Semantics;

namespace MassRPG.Data.Creatures
{
    public sealed class CreatureSpawnRegionDocument
    {
        public const int CurrentFormatVersion = 1;
        public int FormatVersion { get; set; } = CurrentFormatVersion;
        public string Id { get; set; } = string.Empty;
        public string CreatureDefinitionId { get; set; } = string.Empty;
        public int Plane { get; set; }
        public int Storey { get; set; }
        public int PopulationCap { get; set; }
        public long RespawnIntervalMilliseconds { get; set; }
        public CreatureRoamingMode RoamingMode { get; set; }
        public ShapeDocument Area { get; set; }
        public List<RoadPointDocument> PatrolRoute { get; set; } = new List<RoadPointDocument>();
    }

    public static class CreatureSpawnRegionDocumentCodec
    {
        public static CreatureSpawnRegionDocument Encode(CreatureSpawnRegionDefinition region)
        {
            if (region == null) throw new ArgumentNullException(nameof(region));
            var document = new CreatureSpawnRegionDocument
            {
                Id = region.Id.Value,
                CreatureDefinitionId = region.CreatureDefinitionId.Value,
                Plane = region.Plane,
                Storey = region.Storey,
                PopulationCap = region.PopulationCap,
                RespawnIntervalMilliseconds = region.RespawnIntervalMilliseconds,
                RoamingMode = region.RoamingMode,
                Area = EncodeShape(region.Area)
            };
            for (var i = 0; i < region.PatrolRoute.Count; i++)
                document.PatrolRoute.Add(new RoadPointDocument { X = region.PatrolRoute[i].X, Y = region.PatrolRoute[i].Y });
            return document;
        }

        public static CreatureSpawnRegionDefinition Decode(CreatureSpawnRegionDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (document.FormatVersion != CreatureSpawnRegionDocument.CurrentFormatVersion)
                throw new InvalidOperationException($"Unsupported creature spawn format version {document.FormatVersion}.");
            if (!ContentId.TryCreate(document.Id, out var id)) throw new InvalidOperationException("Spawn region has invalid ID.");
            if (!ContentId.TryCreate(document.CreatureDefinitionId, out var creatureId)) throw new InvalidOperationException("Spawn region has invalid creature definition ID.");
            var route = new List<GridCoord>();
            if (document.PatrolRoute != null)
                for (var i = 0; i < document.PatrolRoute.Count; i++) route.Add(new GridCoord(document.PatrolRoute[i].X, document.PatrolRoute[i].Y));
            return new CreatureSpawnRegionDefinition(
                id,
                creatureId,
                DecodeShape(document.Area),
                document.Plane,
                document.Storey,
                document.PopulationCap,
                document.RespawnIntervalMilliseconds,
                document.RoamingMode,
                route);
        }

        private static ShapeDocument EncodeShape(WorldAreaShape shape)
        {
            if (shape is CircleAreaShape circle)
                return new ShapeDocument { Kind = WorldAreaShapeKind.Circle, CenterX = circle.Center.X, CenterY = circle.Center.Y, RadiusTiles = circle.RadiusTiles };
            if (shape is PolygonAreaShape polygon)
            {
                var document = new ShapeDocument { Kind = WorldAreaShapeKind.Polygon };
                for (var i = 0; i < polygon.Points.Count; i++) document.Points.Add(new RoadPointDocument { X = polygon.Points[i].X, Y = polygon.Points[i].Y });
                return document;
            }
            throw new NotSupportedException("Creature spawn area shape is not persistable.");
        }

        private static WorldAreaShape DecodeShape(ShapeDocument document)
        {
            if (document == null) throw new InvalidOperationException("Creature spawn region has no area shape.");
            if (document.Kind == WorldAreaShapeKind.Circle)
                return new CircleAreaShape(new GridCoord(document.CenterX, document.CenterY), document.RadiusTiles);
            if (document.Points == null || document.Points.Count < 3)
                throw new InvalidOperationException("Creature spawn polygon requires at least three points.");
            var points = new List<GridCoord>();
            for (var i = 0; i < document.Points.Count; i++) points.Add(new GridCoord(document.Points[i].X, document.Points[i].Y));
            return new PolygonAreaShape(points);
        }
    }
}
