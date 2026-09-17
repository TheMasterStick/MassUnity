using System;
using System.Collections.Generic;
using System.IO;
using MassRPG.Core.Content;
using MassRPG.Data.World.Semantics;
using UnityEngine;

namespace MassRPG.Editor.World
{
    /// <summary>
    /// Editor adapter for versioned road documents. Like terrain pages, semantic world data lives
    /// outside Unity Assets so the authored world does not become an AssetDatabase hierarchy.
    /// </summary>
    public static class RoadJsonPersistence
    {
        private static string _root;

        public static string Root
        {
            get
            {
                if (string.IsNullOrEmpty(_root))
                    _root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "WorldData", "Semantics", "Roads"));
                return _root;
            }
        }

        public static string FilePath(ContentId id)
            => Path.Combine(Root, SafeFileName(id.Value) + ".json").Replace('\\', '/');

        public static void Save(RoadDefinition road)
        {
            if (road == null) throw new ArgumentNullException(nameof(road));
            Directory.CreateDirectory(Root);
            var document = RoadDocumentCodec.Encode(road);
            File.WriteAllText(FilePath(road.Id), JsonUtility.ToJson(ToDto(document), true));
        }

        public static bool TryLoad(string path, out RoadDefinition road)
        {
            road = null;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
            try
            {
                var dto = JsonUtility.FromJson<RoadDto>(File.ReadAllText(path));
                if (dto == null) return false;
                road = RoadDocumentCodec.Decode(FromDto(dto));
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load MassRPG road '{path}': {ex}");
                return false;
            }
        }

        public static bool TryLoad(ContentId id, out RoadDefinition road) => TryLoad(FilePath(id), out road);

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

        private static RoadDto ToDto(RoadDocument document)
        {
            var dto = new RoadDto
            {
                formatVersion = document.FormatVersion,
                id = document.Id,
                displayName = document.DisplayName,
                plane = document.Plane,
                widthTiles = document.WidthTiles,
                surfaceGroundId = document.SurfaceGroundId,
                visibleOnPlayerMap = document.VisibleOnPlayerMap,
                movementSpeedMultiplier = document.MovementSpeedMultiplier,
                routePreferenceWeight = document.RoutePreferenceWeight,
                reduceAggressiveSpawns = document.ReduceAggressiveSpawns,
                points = new PointDto[document.Points != null ? document.Points.Count : 0]
            };
            for (var i = 0; i < dto.points.Length; i++)
                dto.points[i] = new PointDto { x = document.Points[i].X, y = document.Points[i].Y };
            return dto;
        }

        private static RoadDocument FromDto(RoadDto dto)
        {
            var document = new RoadDocument
            {
                FormatVersion = dto.formatVersion,
                Id = dto.id ?? string.Empty,
                DisplayName = dto.displayName ?? string.Empty,
                Plane = dto.plane,
                WidthTiles = dto.widthTiles,
                SurfaceGroundId = dto.surfaceGroundId ?? string.Empty,
                VisibleOnPlayerMap = dto.visibleOnPlayerMap,
                MovementSpeedMultiplier = dto.movementSpeedMultiplier,
                RoutePreferenceWeight = dto.routePreferenceWeight,
                ReduceAggressiveSpawns = dto.reduceAggressiveSpawns,
                Points = new List<RoadPointDocument>()
            };
            if (dto.points != null)
                for (var i = 0; i < dto.points.Length; i++)
                    document.Points.Add(new RoadPointDocument { X = dto.points[i].x, Y = dto.points[i].y });
            return document;
        }

        [Serializable]
        private sealed class RoadDto
        {
            public int formatVersion;
            public string id;
            public string displayName;
            public int plane;
            public int widthTiles;
            public string surfaceGroundId;
            public bool visibleOnPlayerMap;
            public double movementSpeedMultiplier;
            public double routePreferenceWeight;
            public bool reduceAggressiveSpawns;
            public PointDto[] points;
        }

        [Serializable]
        private sealed class PointDto
        {
            public int x;
            public int y;
        }
    }
}
