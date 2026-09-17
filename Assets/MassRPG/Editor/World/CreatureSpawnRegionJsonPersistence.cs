using System;
using System.Collections.Generic;
using System.IO;
using MassRPG.Core.Content;
using MassRPG.Data.Creatures;
using MassRPG.Data.World.Semantics;
using UnityEngine;

namespace MassRPG.Editor.World
{
    /// <summary>
    /// Editor-only JSON adapter for authored creature population regions. These are simulation data
    /// and deliberately live outside the public-map POI/area presentation.
    /// </summary>
    public static class CreatureSpawnRegionJsonPersistence
    {
        private static string _root;

        public static string Root
        {
            get
            {
                if (string.IsNullOrEmpty(_root))
                    _root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "WorldData", "Creatures", "SpawnRegions"));
                return _root;
            }
        }

        public static string FilePath(ContentId id)
            => Path.Combine(Root, SafeFileName(id.Value) + ".json").Replace('\\', '/');

        public static void Save(CreatureSpawnRegionDefinition region)
        {
            if (region == null) throw new ArgumentNullException(nameof(region));
            Directory.CreateDirectory(Root);
            var document = CreatureSpawnRegionDocumentCodec.Encode(region);
            File.WriteAllText(FilePath(region.Id), JsonUtility.ToJson(ToDto(document), true));
        }

        public static bool TryLoad(string path, out CreatureSpawnRegionDefinition region)
        {
            region = null;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
            try
            {
                var dto = JsonUtility.FromJson<SpawnDto>(File.ReadAllText(path));
                if (dto == null) return false;
                region = CreatureSpawnRegionDocumentCodec.Decode(FromDto(dto));
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load MassRPG creature spawn region '{path}': {ex}");
                return false;
            }
        }

        public static IEnumerable<string> EnumerateFiles()
            => Directory.Exists(Root)
                ? Directory.EnumerateFiles(Root, "*.json", SearchOption.TopDirectoryOnly)
                : Array.Empty<string>();

        public static bool Delete(ContentId id)
        {
            var path = FilePath(id);
            if (!File.Exists(path)) return false;
            File.Delete(path);
            return true;
        }

        private static string SafeFileName(string id)
            => id.Replace("/", "_slash_").Replace("\\", "_backslash_");

        private static SpawnDto ToDto(CreatureSpawnRegionDocument document)
        {
            var dto = new SpawnDto
            {
                formatVersion = document.FormatVersion,
                id = document.Id,
                creatureDefinitionId = document.CreatureDefinitionId,
                plane = document.Plane,
                storey = document.Storey,
                populationCap = document.PopulationCap,
                respawnIntervalMilliseconds = document.RespawnIntervalMilliseconds,
                roamingMode = (int)document.RoamingMode,
                area = ShapeDto.FromDocument(document.Area),
                patrolRoute = new PointDto[document.PatrolRoute != null ? document.PatrolRoute.Count : 0]
            };
            for (var i = 0; i < dto.patrolRoute.Length; i++)
                dto.patrolRoute[i] = new PointDto { x = document.PatrolRoute[i].X, y = document.PatrolRoute[i].Y };
            return dto;
        }

        private static CreatureSpawnRegionDocument FromDto(SpawnDto dto)
        {
            var document = new CreatureSpawnRegionDocument
            {
                FormatVersion = dto.formatVersion,
                Id = dto.id ?? string.Empty,
                CreatureDefinitionId = dto.creatureDefinitionId ?? string.Empty,
                Plane = dto.plane,
                Storey = dto.storey,
                PopulationCap = dto.populationCap,
                RespawnIntervalMilliseconds = dto.respawnIntervalMilliseconds,
                RoamingMode = (CreatureRoamingMode)dto.roamingMode,
                Area = dto.area != null ? dto.area.ToDocument() : null,
                PatrolRoute = new List<RoadPointDocument>()
            };
            if (dto.patrolRoute != null)
                for (var i = 0; i < dto.patrolRoute.Length; i++)
                    document.PatrolRoute.Add(new RoadPointDocument { X = dto.patrolRoute[i].x, Y = dto.patrolRoute[i].y });
            return document;
        }

        [Serializable]
        private sealed class SpawnDto
        {
            public int formatVersion;
            public string id;
            public string creatureDefinitionId;
            public int plane;
            public int storey;
            public int populationCap;
            public long respawnIntervalMilliseconds;
            public int roamingMode;
            public ShapeDto area;
            public PointDto[] patrolRoute;
        }

        [Serializable]
        private sealed class ShapeDto
        {
            public int kind;
            public int centerX;
            public int centerY;
            public int radiusTiles;
            public PointDto[] points;

            public static ShapeDto FromDocument(ShapeDocument document)
            {
                if (document == null) return null;
                var dto = new ShapeDto
                {
                    kind = (int)document.Kind,
                    centerX = document.CenterX,
                    centerY = document.CenterY,
                    radiusTiles = document.RadiusTiles,
                    points = new PointDto[document.Points != null ? document.Points.Count : 0]
                };
                for (var i = 0; i < dto.points.Length; i++)
                    dto.points[i] = new PointDto { x = document.Points[i].X, y = document.Points[i].Y };
                return dto;
            }

            public ShapeDocument ToDocument()
            {
                var document = new ShapeDocument
                {
                    Kind = (WorldAreaShapeKind)kind,
                    CenterX = centerX,
                    CenterY = centerY,
                    RadiusTiles = radiusTiles,
                    Points = new List<RoadPointDocument>()
                };
                if (points != null)
                    for (var i = 0; i < points.Length; i++)
                        document.Points.Add(new RoadPointDocument { X = points[i].x, Y = points[i].y });
                return document;
            }
        }

        [Serializable]
        private sealed class PointDto
        {
            public int x;
            public int y;
        }
    }
}
