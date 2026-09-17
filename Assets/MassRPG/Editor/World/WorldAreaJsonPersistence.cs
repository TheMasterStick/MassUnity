using System;
using System.Collections.Generic;
using System.IO;
using MassRPG.Core.Content;
using MassRPG.Data.World.Semantics;
using UnityEngine;

namespace MassRPG.Editor.World
{
    /// <summary>Editor-only JSON adapter for overlapping semantic world areas.</summary>
    public static class WorldAreaJsonPersistence
    {
        private static string _root;

        public static string Root
        {
            get
            {
                if (string.IsNullOrEmpty(_root))
                    _root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "WorldData", "Semantics", "Areas"));
                return _root;
            }
        }

        public static string FilePath(ContentId id)
            => Path.Combine(Root, SafeFileName(id.Value) + ".json").Replace('\\', '/');

        public static void Save(WorldAreaDefinition area)
        {
            if (area == null) throw new ArgumentNullException(nameof(area));
            Directory.CreateDirectory(Root);
            var document = WorldAreaDocumentCodec.Encode(area);
            File.WriteAllText(FilePath(area.Id), JsonUtility.ToJson(ToDto(document), true));
        }

        public static bool TryLoad(string path, out WorldAreaDefinition area)
        {
            area = null;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
            try
            {
                var dto = JsonUtility.FromJson<AreaDto>(File.ReadAllText(path));
                if (dto == null) return false;
                area = WorldAreaDocumentCodec.Decode(FromDto(dto));
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load MassRPG world area '{path}': {ex}");
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

        private static AreaDto ToDto(WorldAreaDocument document)
        {
            var dto = new AreaDto
            {
                formatVersion = document.FormatVersion,
                id = document.Id,
                displayName = document.DisplayName,
                kind = (int)document.Kind,
                mapVisibility = (int)document.MapVisibility,
                plane = document.Plane,
                shapeKind = (int)document.ShapeKind,
                centerX = document.CenterX,
                centerY = document.CenterY,
                radiusTiles = document.RadiusTiles,
                points = new PointDto[document.Points != null ? document.Points.Count : 0]
            };
            for (var i = 0; i < dto.points.Length; i++)
                dto.points[i] = new PointDto { x = document.Points[i].X, y = document.Points[i].Y };
            return dto;
        }

        private static WorldAreaDocument FromDto(AreaDto dto)
        {
            var document = new WorldAreaDocument
            {
                FormatVersion = dto.formatVersion,
                Id = dto.id ?? string.Empty,
                DisplayName = dto.displayName ?? string.Empty,
                Kind = (WorldAreaKind)dto.kind,
                MapVisibility = (PlayerMapVisibility)dto.mapVisibility,
                Plane = dto.plane,
                ShapeKind = (WorldAreaShapeKind)dto.shapeKind,
                CenterX = dto.centerX,
                CenterY = dto.centerY,
                RadiusTiles = dto.radiusTiles,
                Points = new List<RoadPointDocument>()
            };
            if (dto.points != null)
                for (var i = 0; i < dto.points.Length; i++)
                    document.Points.Add(new RoadPointDocument { X = dto.points[i].x, Y = dto.points[i].y });
            return document;
        }

        [Serializable]
        private sealed class AreaDto
        {
            public int formatVersion;
            public string id;
            public string displayName;
            public int kind;
            public int mapVisibility;
            public int plane;
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
    }
}
