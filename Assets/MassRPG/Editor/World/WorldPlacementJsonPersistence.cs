using System;
using System.Collections.Generic;
using System.IO;
using MassRPG.Core.World;
using MassRPG.Data.World.Placements;
using UnityEngine;

namespace MassRPG.Editor.World
{
    public static class WorldPlacementJsonPersistence
    {
        private static string _root;

        public static string Root
        {
            get
            {
                if (string.IsNullOrEmpty(_root))
                    _root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "WorldData", "Placements"));
                return _root;
            }
        }

        public static string FilePath(WorldPageKey key)
        {
            var folder = Path.Combine(Root, $"plane_{key.Plane}", $"storey_{key.Storey}");
            return Path.Combine(folder, $"page_{key.Page.X}_{key.Page.Y}.json").Replace('\\', '/');
        }

        public static void Save(WorldPlacementPageDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            var key = new WorldPageKey(new WorldPageCoord(document.PageX, document.PageY), document.Plane, document.Storey);
            var path = FilePath(key);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            if (document.Placements == null || document.Placements.Count == 0)
            {
                if (File.Exists(path)) File.Delete(path);
                return;
            }
            File.WriteAllText(path, JsonUtility.ToJson(ToDto(document), true));
        }

        public static bool TryLoad(WorldPageKey key, out WorldPlacementPageDocument document)
        {
            var path = FilePath(key);
            if (!File.Exists(path)) { document = null; return false; }
            try
            {
                var dto = JsonUtility.FromJson<PageDto>(File.ReadAllText(path));
                if (dto == null) { document = null; return false; }
                document = FromDto(dto);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load MassRPG placement page {key}: {ex}");
                document = null;
                return false;
            }
        }

        private static PageDto ToDto(WorldPlacementPageDocument document)
        {
            var dto = new PageDto
            {
                formatVersion = document.FormatVersion,
                pageX = document.PageX,
                pageY = document.PageY,
                plane = document.Plane,
                storey = document.Storey,
                placements = new PlacementDto[document.Placements != null ? document.Placements.Count : 0]
            };
            for (var i = 0; i < dto.placements.Length; i++)
            {
                var p = document.Placements[i];
                dto.placements[i] = new PlacementDto
                {
                    instanceId = p.InstanceId,
                    definitionId = p.DefinitionId,
                    kind = (int)p.Kind,
                    x = p.X,
                    y = p.Y,
                    offsetX = p.OffsetX,
                    offsetY = p.OffsetY,
                    visualHeightOffset = p.VisualHeightOffset,
                    yawDegrees = p.YawDegrees,
                    enabled = p.Enabled
                };
            }
            return dto;
        }

        private static WorldPlacementPageDocument FromDto(PageDto dto)
        {
            var document = new WorldPlacementPageDocument
            {
                FormatVersion = dto.formatVersion,
                PageX = dto.pageX,
                PageY = dto.pageY,
                Plane = dto.plane,
                Storey = dto.storey,
                Placements = new List<WorldPlacementDocument>()
            };
            if (dto.placements != null)
            {
                for (var i = 0; i < dto.placements.Length; i++)
                {
                    var p = dto.placements[i];
                    document.Placements.Add(new WorldPlacementDocument
                    {
                        InstanceId = p.instanceId ?? string.Empty,
                        DefinitionId = p.definitionId ?? string.Empty,
                        Kind = (WorldPlacementKind)p.kind,
                        X = p.x,
                        Y = p.y,
                        OffsetX = p.offsetX,
                        OffsetY = p.offsetY,
                        VisualHeightOffset = p.visualHeightOffset,
                        YawDegrees = p.yawDegrees,
                        Enabled = p.enabled
                    });
                }
            }
            return document;
        }

        [Serializable]
        private sealed class PageDto
        {
            public int formatVersion;
            public int pageX;
            public int pageY;
            public int plane;
            public int storey;
            public PlacementDto[] placements;
        }

        [Serializable]
        private sealed class PlacementDto
        {
            public string instanceId;
            public string definitionId;
            public int kind;
            public int x;
            public int y;
            public float offsetX;
            public float offsetY;
            public float visualHeightOffset;
            public float yawDegrees;
            public bool enabled;
        }
    }
}
