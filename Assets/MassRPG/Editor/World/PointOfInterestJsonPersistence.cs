using System;
using System.Collections.Generic;
using System.IO;
using MassRPG.Core.Content;
using MassRPG.Data.World.Semantics;
using UnityEngine;

namespace MassRPG.Editor.World
{
    public static class PointOfInterestJsonPersistence
    {
        private static string _root;

        public static string Root
        {
            get
            {
                if (string.IsNullOrEmpty(_root))
                    _root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "WorldData", "Semantics", "PointsOfInterest"));
                return _root;
            }
        }

        public static string FilePath(ContentId id)
            => Path.Combine(Root, SafeFileName(id.Value) + ".json").Replace('\\', '/');

        public static void Save(PointOfInterestDefinition poi)
        {
            if (poi == null) throw new ArgumentNullException(nameof(poi));
            Directory.CreateDirectory(Root);
            File.WriteAllText(FilePath(poi.Id), JsonUtility.ToJson(ToDto(PointOfInterestDocumentCodec.Encode(poi)), true));
        }

        public static bool TryLoad(string path, out PointOfInterestDefinition poi)
        {
            poi = null;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
            try
            {
                var dto = JsonUtility.FromJson<PoiDto>(File.ReadAllText(path));
                if (dto == null) return false;
                poi = PointOfInterestDocumentCodec.Decode(FromDto(dto));
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load MassRPG POI '{path}': {ex}");
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

        private static PoiDto ToDto(PointOfInterestDocument document)
        {
            var dto = new PoiDto
            {
                formatVersion = document.FormatVersion,
                id = document.Id,
                displayName = document.DisplayName,
                kind = (int)document.Kind,
                markerCategory = (int)document.MarkerCategory,
                mapVisibility = (int)document.MapVisibility,
                x = document.X,
                y = document.Y,
                plane = document.Plane,
                storey = document.Storey,
                visibleFootprint = ShapeDto.FromDocument(document.VisibleFootprint),
                protectionFootprint = ShapeDto.FromDocument(document.ProtectionFootprint),
                serviceTags = document.ServiceTags != null ? document.ServiceTags.ToArray() : Array.Empty<string>()
            };
            return dto;
        }

        private static PointOfInterestDocument FromDto(PoiDto dto)
            => new PointOfInterestDocument
            {
                FormatVersion = dto.formatVersion,
                Id = dto.id ?? string.Empty,
                DisplayName = dto.displayName ?? string.Empty,
                Kind = (PointOfInterestKind)dto.kind,
                MarkerCategory = (MapMarkerCategory)dto.markerCategory,
                MapVisibility = (PlayerMapVisibility)dto.mapVisibility,
                X = dto.x,
                Y = dto.y,
                Plane = dto.plane,
                Storey = dto.storey,
                VisibleFootprint = dto.visibleFootprint != null ? dto.visibleFootprint.ToDocument() : null,
                ProtectionFootprint = dto.protectionFootprint != null ? dto.protectionFootprint.ToDocument() : null,
                ServiceTags = new List<string>(dto.serviceTags ?? Array.Empty<string>())
            };

        [Serializable]
        private sealed class PoiDto
        {
            public int formatVersion;
            public string id;
            public string displayName;
            public int kind;
            public int markerCategory;
            public int mapVisibility;
            public int x;
            public int y;
            public int plane;
            public int storey;
            public ShapeDto visibleFootprint;
            public ShapeDto protectionFootprint;
            public string[] serviceTags;
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
