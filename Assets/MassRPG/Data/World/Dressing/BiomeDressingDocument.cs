using System;
using System.Collections.Generic;
using MassRPG.Core.Content;
using MassRPG.Core.World;
using MassRPG.Data.World.Placements;
using MassRPG.Data.World.Semantics;

namespace MassRPG.Data.World.Dressing
{
    public sealed class BiomeDressingEntryDocument
    {
        public string Id { get; set; } = string.Empty;
        public string DefinitionId { get; set; } = string.Empty;
        public WorldPlacementKind PlacementKind { get; set; }
        public double DensityPerTile { get; set; }
        public int MinimumSpacingTiles { get; set; }
        public DressingOccupancyChannel Channel { get; set; }
        public int Priority { get; set; }
        public uint SeedSalt { get; set; }
    }

    public sealed class BiomeDressingOverrideDocument
    {
        public string Id { get; set; } = string.Empty;
        public double DensityMultiplier { get; set; } = 1.0;
        public string TargetEntryId { get; set; } = string.Empty;
        public WorldAreaShapeKind ShapeKind { get; set; } = WorldAreaShapeKind.Polygon;
        public int CenterX { get; set; }
        public int CenterY { get; set; }
        public int RadiusTiles { get; set; }
        public List<RoadPointDocument> Points { get; set; } = new List<RoadPointDocument>();
    }

    public sealed class BiomeDressingProfileDocument
    {
        public const int CurrentFormatVersion = 1;
        public int FormatVersion { get; set; } = CurrentFormatVersion;
        public string Id { get; set; } = string.Empty;
        public string BiomeAreaId { get; set; } = string.Empty;
        public uint Seed { get; set; }
        public List<BiomeDressingEntryDocument> Entries { get; set; } = new List<BiomeDressingEntryDocument>();
        public List<BiomeDressingOverrideDocument> DensityOverrides { get; set; } = new List<BiomeDressingOverrideDocument>();
    }

    public static class BiomeDressingProfileDocumentCodec
    {
        public static BiomeDressingProfileDocument Encode(BiomeDressingProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            var document = new BiomeDressingProfileDocument
            {
                Id = profile.Id.Value,
                BiomeAreaId = profile.BiomeAreaId.Value,
                Seed = profile.Seed
            };

            for (var i = 0; i < profile.Entries.Count; i++)
            {
                var entry = profile.Entries[i];
                document.Entries.Add(new BiomeDressingEntryDocument
                {
                    Id = entry.Id.Value,
                    DefinitionId = entry.DefinitionId.Value,
                    PlacementKind = entry.PlacementKind,
                    DensityPerTile = entry.DensityPerTile,
                    MinimumSpacingTiles = entry.MinimumSpacingTiles,
                    Channel = entry.Channel,
                    Priority = entry.Priority,
                    SeedSalt = entry.SeedSalt
                });
            }

            for (var i = 0; i < profile.DensityOverrides.Count; i++)
                document.DensityOverrides.Add(EncodeOverride(profile.DensityOverrides[i]));
            return document;
        }

        public static BiomeDressingProfile Decode(BiomeDressingProfileDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (document.FormatVersion != BiomeDressingProfileDocument.CurrentFormatVersion)
                throw new InvalidOperationException($"Unsupported biome dressing format version {document.FormatVersion}.");
            if (!ContentId.TryCreate(document.Id, out var id))
                throw new InvalidOperationException("Biome dressing profile has an invalid stable ID.");
            if (!ContentId.TryCreate(document.BiomeAreaId, out var biomeAreaId))
                throw new InvalidOperationException("Biome dressing profile has an invalid biome area ID.");

            var profile = new BiomeDressingProfile(id, biomeAreaId, document.Seed);
            if (document.Entries != null)
            {
                for (var i = 0; i < document.Entries.Count; i++)
                {
                    var item = document.Entries[i];
                    if (!ContentId.TryCreate(item.Id, out var entryId))
                        throw new InvalidOperationException("Biome dressing entry has an invalid stable ID.");
                    if (!ContentId.TryCreate(item.DefinitionId, out var definitionId))
                        throw new InvalidOperationException($"Biome dressing entry '{item.Id}' has an invalid definition ID.");
                    profile.AddEntry(new BiomeDressingEntry(
                        entryId,
                        definitionId,
                        item.PlacementKind,
                        item.DensityPerTile,
                        item.MinimumSpacingTiles,
                        item.Channel,
                        item.Priority,
                        item.SeedSalt));
                }
            }

            if (document.DensityOverrides != null)
            {
                for (var i = 0; i < document.DensityOverrides.Count; i++)
                    profile.AddDensityOverride(DecodeOverride(document.DensityOverrides[i]));
            }
            return profile;
        }

        private static BiomeDressingOverrideDocument EncodeOverride(BiomeDressingDensityOverride item)
        {
            var document = new BiomeDressingOverrideDocument
            {
                Id = item.Id.Value,
                DensityMultiplier = item.DensityMultiplier,
                TargetEntryId = item.TargetEntryId.HasValue ? item.TargetEntryId.Value.Value : string.Empty
            };

            var circle = item.Shape as CircleAreaShape;
            if (circle != null)
            {
                document.ShapeKind = WorldAreaShapeKind.Circle;
                document.CenterX = circle.Center.X;
                document.CenterY = circle.Center.Y;
                document.RadiusTiles = circle.RadiusTiles;
                return document;
            }

            var polygon = item.Shape as PolygonAreaShape;
            if (polygon != null)
            {
                document.ShapeKind = WorldAreaShapeKind.Polygon;
                for (var i = 0; i < polygon.Points.Count; i++)
                    document.Points.Add(new RoadPointDocument { X = polygon.Points[i].X, Y = polygon.Points[i].Y });
                return document;
            }

            throw new NotSupportedException($"Dressing override shape '{item.Shape.GetType().Name}' is not persistable yet.");
        }

        private static BiomeDressingDensityOverride DecodeOverride(BiomeDressingOverrideDocument document)
        {
            if (!ContentId.TryCreate(document.Id, out var id))
                throw new InvalidOperationException("Biome dressing override has an invalid stable ID.");

            ContentId? target = null;
            if (!string.IsNullOrWhiteSpace(document.TargetEntryId))
            {
                if (!ContentId.TryCreate(document.TargetEntryId, out var targetId))
                    throw new InvalidOperationException($"Biome dressing override '{document.Id}' has an invalid target entry ID.");
                target = targetId;
            }

            WorldAreaShape shape;
            switch (document.ShapeKind)
            {
                case WorldAreaShapeKind.Circle:
                    shape = new CircleAreaShape(new GridCoord(document.CenterX, document.CenterY), document.RadiusTiles);
                    break;
                case WorldAreaShapeKind.Polygon:
                    if (document.Points == null || document.Points.Count < 3)
                        throw new InvalidOperationException($"Biome dressing override '{document.Id}' polygon needs at least three points.");
                    var points = new List<GridCoord>(document.Points.Count);
                    for (var i = 0; i < document.Points.Count; i++)
                        points.Add(new GridCoord(document.Points[i].X, document.Points[i].Y));
                    shape = new PolygonAreaShape(points);
                    break;
                default:
                    throw new InvalidOperationException("Unknown biome dressing override shape kind.");
            }
            return new BiomeDressingDensityOverride(id, shape, document.DensityMultiplier, target);
        }
    }

    public sealed class BiomeDressingSuppressionDocument
    {
        public string EntryId { get; set; } = string.Empty;
        public int X { get; set; }
        public int Y { get; set; }
        public int Plane { get; set; }
        public int Storey { get; set; }
    }

    public sealed class BiomeDressingExceptionDocument
    {
        public const int CurrentFormatVersion = 1;
        public int FormatVersion { get; set; } = CurrentFormatVersion;
        public string ProfileId { get; set; } = string.Empty;
        public List<BiomeDressingSuppressionDocument> Suppressed { get; set; } = new List<BiomeDressingSuppressionDocument>();
    }

    public static class BiomeDressingExceptionDocumentCodec
    {
        public static BiomeDressingExceptionDocument Encode(ContentId profileId, BiomeDressingExceptionSet exceptions)
        {
            if (profileId.IsEmpty) throw new ArgumentException("Profile id cannot be empty.", nameof(profileId));
            if (exceptions == null) throw new ArgumentNullException(nameof(exceptions));
            var document = new BiomeDressingExceptionDocument { ProfileId = profileId.Value };
            foreach (var key in exceptions.Suppressed)
            {
                if (key.ProfileId != profileId) continue;
                document.Suppressed.Add(new BiomeDressingSuppressionDocument
                {
                    EntryId = key.EntryId.Value,
                    X = key.Anchor.Tile.X,
                    Y = key.Anchor.Tile.Y,
                    Plane = key.Anchor.Plane,
                    Storey = key.Anchor.Storey
                });
            }
            return document;
        }

        public static BiomeDressingExceptionSet Decode(BiomeDressingExceptionDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (document.FormatVersion != BiomeDressingExceptionDocument.CurrentFormatVersion)
                throw new InvalidOperationException($"Unsupported biome dressing exception format version {document.FormatVersion}.");
            if (!ContentId.TryCreate(document.ProfileId, out var profileId))
                throw new InvalidOperationException("Biome dressing exception document has an invalid profile ID.");

            var result = new BiomeDressingExceptionSet();
            if (document.Suppressed == null) return result;
            for (var i = 0; i < document.Suppressed.Count; i++)
            {
                var item = document.Suppressed[i];
                if (!ContentId.TryCreate(item.EntryId, out var entryId))
                    throw new InvalidOperationException("Biome dressing suppression has an invalid entry ID.");
                var tile = new GridCoord(item.X, item.Y);
                if (!WorldConstants.IsInsideWorld(tile))
                    throw new InvalidOperationException("Biome dressing suppression is outside the world.");
                result.Suppress(new BiomeDressingPlacementKey(
                    profileId,
                    entryId,
                    new GridLocation(tile, item.Plane, item.Storey)));
            }
            return result;
        }
    }
}
