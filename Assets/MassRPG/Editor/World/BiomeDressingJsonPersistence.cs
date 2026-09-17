using System;
using System.Collections.Generic;
using System.IO;
using MassRPG.Core.Content;
using MassRPG.Data.World.Dressing;
using MassRPG.Data.World.Placements;
using MassRPG.Data.World.Semantics;
using UnityEngine;

namespace MassRPG.Editor.World
{
    /// <summary>
    /// Editor-side persistence for deterministic biome dressing profiles and sparse suppression
    /// exceptions. Generated untouched placements are never serialized.
    /// </summary>
    public static class BiomeDressingJsonPersistence
    {
        private static string _profilesRoot;
        private static string _exceptionsRoot;

        public static string ProfilesRoot
        {
            get
            {
                if (string.IsNullOrEmpty(_profilesRoot))
                    _profilesRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "WorldData", "Dressing", "Profiles"));
                return _profilesRoot;
            }
        }

        public static string ExceptionsRoot
        {
            get
            {
                if (string.IsNullOrEmpty(_exceptionsRoot))
                    _exceptionsRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "WorldData", "Dressing", "Exceptions"));
                return _exceptionsRoot;
            }
        }

        public static string ProfilePath(ContentId id)
            => Path.Combine(ProfilesRoot, SafeFileName(id.Value) + ".json").Replace('\\', '/');

        public static string ExceptionPath(ContentId id)
            => Path.Combine(ExceptionsRoot, SafeFileName(id.Value) + ".json").Replace('\\', '/');

        public static void SaveProfile(BiomeDressingProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            Directory.CreateDirectory(ProfilesRoot);
            var document = BiomeDressingProfileDocumentCodec.Encode(profile);
            File.WriteAllText(ProfilePath(profile.Id), JsonUtility.ToJson(ToDto(document), true));
        }

        public static bool TryLoadProfile(string path, out BiomeDressingProfile profile)
        {
            profile = null;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
            try
            {
                var dto = JsonUtility.FromJson<ProfileDto>(File.ReadAllText(path));
                if (dto == null) return false;
                profile = BiomeDressingProfileDocumentCodec.Decode(FromDto(dto));
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load MassRPG biome dressing profile '{path}': {ex}");
                return false;
            }
        }

        public static IEnumerable<string> EnumerateProfileFiles()
            => Directory.Exists(ProfilesRoot)
                ? Directory.EnumerateFiles(ProfilesRoot, "*.json", SearchOption.TopDirectoryOnly)
                : Array.Empty<string>();

        public static bool DeleteProfile(ContentId id)
        {
            var path = ProfilePath(id);
            if (!File.Exists(path)) return false;
            File.Delete(path);
            var exceptions = ExceptionPath(id);
            if (File.Exists(exceptions)) File.Delete(exceptions);
            return true;
        }

        public static void SaveExceptions(ContentId profileId, BiomeDressingExceptionSet exceptions)
        {
            if (exceptions == null) throw new ArgumentNullException(nameof(exceptions));
            Directory.CreateDirectory(ExceptionsRoot);
            var document = BiomeDressingExceptionDocumentCodec.Encode(profileId, exceptions);
            File.WriteAllText(ExceptionPath(profileId), JsonUtility.ToJson(ToDto(document), true));
        }

        public static BiomeDressingExceptionSet LoadExceptions(ContentId profileId)
        {
            var path = ExceptionPath(profileId);
            if (!File.Exists(path)) return new BiomeDressingExceptionSet();
            try
            {
                var dto = JsonUtility.FromJson<ExceptionDto>(File.ReadAllText(path));
                if (dto == null) return new BiomeDressingExceptionSet();
                return BiomeDressingExceptionDocumentCodec.Decode(FromDto(dto));
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load MassRPG biome dressing exceptions '{path}': {ex}");
                return new BiomeDressingExceptionSet();
            }
        }

        private static ProfileDto ToDto(BiomeDressingProfileDocument document)
        {
            var dto = new ProfileDto
            {
                formatVersion = document.FormatVersion,
                id = document.Id,
                biomeAreaId = document.BiomeAreaId,
                seed = document.Seed,
                entries = new EntryDto[document.Entries != null ? document.Entries.Count : 0],
                densityOverrides = new OverrideDto[document.DensityOverrides != null ? document.DensityOverrides.Count : 0]
            };

            for (var i = 0; i < dto.entries.Length; i++)
            {
                var item = document.Entries[i];
                dto.entries[i] = new EntryDto
                {
                    id = item.Id,
                    definitionId = item.DefinitionId,
                    placementKind = (int)item.PlacementKind,
                    densityPerTile = item.DensityPerTile,
                    minimumSpacingTiles = item.MinimumSpacingTiles,
                    channel = (int)item.Channel,
                    priority = item.Priority,
                    seedSalt = item.SeedSalt
                };
            }

            for (var i = 0; i < dto.densityOverrides.Length; i++)
            {
                var item = document.DensityOverrides[i];
                var points = new PointDto[item.Points != null ? item.Points.Count : 0];
                for (var p = 0; p < points.Length; p++)
                    points[p] = new PointDto { x = item.Points[p].X, y = item.Points[p].Y };
                dto.densityOverrides[i] = new OverrideDto
                {
                    id = item.Id,
                    densityMultiplier = item.DensityMultiplier,
                    targetEntryId = item.TargetEntryId,
                    shapeKind = (int)item.ShapeKind,
                    centerX = item.CenterX,
                    centerY = item.CenterY,
                    radiusTiles = item.RadiusTiles,
                    points = points
                };
            }
            return dto;
        }

        private static BiomeDressingProfileDocument FromDto(ProfileDto dto)
        {
            var document = new BiomeDressingProfileDocument
            {
                FormatVersion = dto.formatVersion,
                Id = dto.id ?? string.Empty,
                BiomeAreaId = dto.biomeAreaId ?? string.Empty,
                Seed = dto.seed,
                Entries = new List<BiomeDressingEntryDocument>(),
                DensityOverrides = new List<BiomeDressingOverrideDocument>()
            };

            if (dto.entries != null)
            {
                for (var i = 0; i < dto.entries.Length; i++)
                {
                    var item = dto.entries[i];
                    document.Entries.Add(new BiomeDressingEntryDocument
                    {
                        Id = item.id ?? string.Empty,
                        DefinitionId = item.definitionId ?? string.Empty,
                        PlacementKind = (WorldPlacementKind)item.placementKind,
                        DensityPerTile = item.densityPerTile,
                        MinimumSpacingTiles = item.minimumSpacingTiles,
                        Channel = (DressingOccupancyChannel)item.channel,
                        Priority = item.priority,
                        SeedSalt = item.seedSalt
                    });
                }
            }

            if (dto.densityOverrides != null)
            {
                for (var i = 0; i < dto.densityOverrides.Length; i++)
                {
                    var item = dto.densityOverrides[i];
                    var documentOverride = new BiomeDressingOverrideDocument
                    {
                        Id = item.id ?? string.Empty,
                        DensityMultiplier = item.densityMultiplier,
                        TargetEntryId = item.targetEntryId ?? string.Empty,
                        ShapeKind = (WorldAreaShapeKind)item.shapeKind,
                        CenterX = item.centerX,
                        CenterY = item.centerY,
                        RadiusTiles = item.radiusTiles,
                        Points = new List<RoadPointDocument>()
                    };
                    if (item.points != null)
                        for (var p = 0; p < item.points.Length; p++)
                            documentOverride.Points.Add(new RoadPointDocument { X = item.points[p].x, Y = item.points[p].y });
                    document.DensityOverrides.Add(documentOverride);
                }
            }
            return document;
        }

        private static ExceptionDto ToDto(BiomeDressingExceptionDocument document)
        {
            var dto = new ExceptionDto
            {
                formatVersion = document.FormatVersion,
                profileId = document.ProfileId,
                suppressed = new SuppressionDto[document.Suppressed != null ? document.Suppressed.Count : 0]
            };
            for (var i = 0; i < dto.suppressed.Length; i++)
            {
                var item = document.Suppressed[i];
                dto.suppressed[i] = new SuppressionDto
                {
                    entryId = item.EntryId,
                    x = item.X,
                    y = item.Y,
                    plane = item.Plane,
                    storey = item.Storey
                };
            }
            return dto;
        }

        private static BiomeDressingExceptionDocument FromDto(ExceptionDto dto)
        {
            var document = new BiomeDressingExceptionDocument
            {
                FormatVersion = dto.formatVersion,
                ProfileId = dto.profileId ?? string.Empty,
                Suppressed = new List<BiomeDressingSuppressionDocument>()
            };
            if (dto.suppressed != null)
            {
                for (var i = 0; i < dto.suppressed.Length; i++)
                {
                    var item = dto.suppressed[i];
                    document.Suppressed.Add(new BiomeDressingSuppressionDocument
                    {
                        EntryId = item.entryId ?? string.Empty,
                        X = item.x,
                        Y = item.y,
                        Plane = item.plane,
                        Storey = item.storey
                    });
                }
            }
            return document;
        }

        private static string SafeFileName(string id)
            => id.Replace("/", "_slash_").Replace("\\", "_backslash_");

        [Serializable]
        private sealed class ProfileDto
        {
            public int formatVersion;
            public string id;
            public string biomeAreaId;
            public uint seed;
            public EntryDto[] entries;
            public OverrideDto[] densityOverrides;
        }

        [Serializable]
        private sealed class EntryDto
        {
            public string id;
            public string definitionId;
            public int placementKind;
            public double densityPerTile;
            public int minimumSpacingTiles;
            public int channel;
            public int priority;
            public uint seedSalt;
        }

        [Serializable]
        private sealed class OverrideDto
        {
            public string id;
            public double densityMultiplier;
            public string targetEntryId;
            public int shapeKind;
            public int centerX;
            public int centerY;
            public int radiusTiles;
            public PointDto[] points;
        }

        [Serializable]
        private sealed class PointDto
        {
            public int x;
            public int y;
        }

        [Serializable]
        private sealed class ExceptionDto
        {
            public int formatVersion;
            public string profileId;
            public SuppressionDto[] suppressed;
        }

        [Serializable]
        private sealed class SuppressionDto
        {
            public string entryId;
            public int x;
            public int y;
            public int plane;
            public int storey;
        }
    }
}
