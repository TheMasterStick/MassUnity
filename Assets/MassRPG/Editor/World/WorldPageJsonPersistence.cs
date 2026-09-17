using System;
using System.Collections.Generic;
using System.IO;
using MassRPG.Core.World;
using MassRPG.Data.World;
using UnityEngine;

namespace MassRPG.Editor.World
{
    /// <summary>
    /// Editor-side disk persistence for authored world pages. Runtime/editor gameplay data remains
    /// engine-independent; this class only adapts WorldPageDocument to Unity's JSON utility.
    ///
    /// IMPORTANT: production world pages deliberately live outside the Unity Assets tree. A complete
    /// 180k world can contain more than one hundred thousand storage pages; importing every page as a
    /// Unity asset would make AssetDatabase itself part of the world-streaming bottleneck. The files
    /// still live in the repository and remain versionable, but Unity treats them as external authored
    /// data rather than project assets.
    /// </summary>
    public static class WorldPageJsonPersistence
    {
        private static string _productionRoot;
        private static string _recoveryRoot;

        /// <summary>Repository-level canonical authored pages, e.g. &lt;repo&gt;/WorldData/Pages.</summary>
        public static string ProductionRoot
        {
            get
            {
                if (string.IsNullOrEmpty(_productionRoot))
                    _productionRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "WorldData", "Pages"));
                return _productionRoot;
            }
        }

        /// <summary>Local non-versioned crash recovery beneath the Unity project's Library folder.</summary>
        public static string RecoveryRoot
        {
            get
            {
                if (string.IsNullOrEmpty(_recoveryRoot))
                    _recoveryRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "MassRPG", "WorldEditorRecovery"));
                return _recoveryRoot;
            }
        }

        public static string FilePath(WorldPageKey key) => FilePath(key, ProductionRoot);

        public static string FilePath(WorldPageKey key, string root)
        {
            if (string.IsNullOrWhiteSpace(root)) throw new ArgumentException("World page root cannot be empty.", nameof(root));
            var folder = Path.Combine(root, $"plane_{key.Plane}", $"storey_{key.Storey}");
            return Path.Combine(folder, $"page_{key.Page.X}_{key.Page.Y}.json").Replace('\\', '/');
        }

        public static void Save(WorldPageDocument document) => Save(document, ProductionRoot);

        public static void Save(WorldPageDocument document, string root)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            var key = new WorldPageKey(new WorldPageCoord(document.PageX, document.PageY), document.Plane, document.Storey);
            var path = FilePath(key, root);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(path, JsonUtility.ToJson(ToDto(document), true));
        }

        public static bool TryLoad(WorldPageKey key, out WorldPageDocument document)
            => TryLoad(key, out document, ProductionRoot);

        public static bool TryLoad(WorldPageKey key, out WorldPageDocument document, string root)
        {
            var path = FilePath(key, root);
            if (!File.Exists(path))
            {
                document = null;
                return false;
            }

            var json = File.ReadAllText(path);
            var dto = JsonUtility.FromJson<PageDto>(json);
            if (dto == null)
            {
                document = null;
                return false;
            }

            document = FromDto(dto);
            return true;
        }

        public static IEnumerable<string> EnumeratePageFiles(int plane, int storey, string root = null)
        {
            var baseRoot = string.IsNullOrWhiteSpace(root) ? ProductionRoot : root;
            var folder = Path.Combine(baseRoot, $"plane_{plane}", $"storey_{storey}");
            return Directory.Exists(folder)
                ? Directory.EnumerateFiles(folder, "page_*.json", SearchOption.TopDirectoryOnly)
                : Array.Empty<string>();
        }

        public static bool TryParsePageKeyFromFile(string path, int plane, int storey, out WorldPageKey key)
        {
            key = default;
            if (string.IsNullOrWhiteSpace(path)) return false;
            var file = Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrEmpty(file) || !file.StartsWith("page_", StringComparison.Ordinal)) return false;
            var parts = file.Substring(5).Split('_');
            if (parts.Length != 2 || !int.TryParse(parts[0], out var x) || !int.TryParse(parts[1], out var y)) return false;
            key = new WorldPageKey(new WorldPageCoord(x, y), plane, storey);
            return true;
        }

        private static PageDto ToDto(WorldPageDocument document)
        {
            var dto = new PageDto
            {
                formatVersion = document.FormatVersion,
                pageX = document.PageX,
                pageY = document.PageY,
                plane = document.Plane,
                storey = document.Storey,
                pageSize = document.PageSize,
                groundPalette = document.GroundPalette != null ? document.GroundPalette.ToArray() : Array.Empty<string>(),
                runs = new RunDto[document.Runs != null ? document.Runs.Count : 0]
            };

            for (var i = 0; i < dto.runs.Length; i++)
            {
                var run = document.Runs[i];
                dto.runs[i] = new RunDto
                {
                    length = run.Length,
                    groundIndex = run.GroundIndex,
                    elevation = run.Elevation,
                    flags = run.Flags,
                    movementEdges = run.MovementEdges,
                    lineOfSightEdges = run.LineOfSightEdges,
                    elevationTransitionEdges = run.ElevationTransitionEdges
                };
            }
            return dto;
        }

        private static WorldPageDocument FromDto(PageDto dto)
        {
            var document = new WorldPageDocument
            {
                FormatVersion = dto.formatVersion,
                PageX = dto.pageX,
                PageY = dto.pageY,
                Plane = dto.plane,
                Storey = dto.storey,
                PageSize = dto.pageSize,
                GroundPalette = new List<string>(dto.groundPalette ?? Array.Empty<string>()),
                Runs = new List<WorldPageRun>()
            };

            if (dto.runs != null)
            {
                for (var i = 0; i < dto.runs.Length; i++)
                {
                    var run = dto.runs[i];
                    document.Runs.Add(new WorldPageRun
                    {
                        Length = run.length,
                        GroundIndex = run.groundIndex,
                        Elevation = run.elevation,
                        Flags = run.flags,
                        MovementEdges = run.movementEdges,
                        LineOfSightEdges = run.lineOfSightEdges,
                        ElevationTransitionEdges = run.elevationTransitionEdges
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
            public int pageSize;
            public string[] groundPalette;
            public RunDto[] runs;
        }

        [Serializable]
        private sealed class RunDto
        {
            public int length;
            public ushort groundIndex;
            public short elevation;
            public byte flags;
            public byte movementEdges;
            public byte lineOfSightEdges;
            public byte elevationTransitionEdges;
        }
    }
}
