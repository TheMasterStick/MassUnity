using System;
using System.Collections.Generic;
using System.IO;
using MassRPG.Core.World;
using MassRPG.Data.World;
using UnityEngine;

namespace MassRPG.Client.World
{
    /// <summary>
    /// Local-development page source for the authored Twin Lands repository. It mirrors the editor's
    /// WorldData/Pages JSON format but lives in the client assembly so Play From Here and the first
    /// production scene can stream real authored pages without depending on UnityEditor code.
    ///
    /// Production MMO clients can replace this source with packaged/server-delivered page payloads;
    /// the AuthoredWorldPage/WorldPageCodec boundary remains the same.
    /// </summary>
    public static class RepositoryWorldPageSource
    {
        public const string WorldDataEnvironmentVariable = "MASSRPG_WORLD_DATA";

        public static string DefaultRoot
        {
            get
            {
                var configured = Environment.GetEnvironmentVariable(WorldDataEnvironmentVariable);
                if (!string.IsNullOrWhiteSpace(configured)) return Path.GetFullPath(configured);

                // In the Unity project Application.dataPath = <repo>/unity/Assets.
                var repositoryRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "WorldData", "Pages"));
                if (Directory.Exists(repositoryRoot)) return repositoryRoot;

                // A standalone development build may package/copy pages beneath StreamingAssets.
                return Path.Combine(Application.streamingAssetsPath, "WorldData", "Pages");
            }
        }

        public static string FilePath(WorldPageKey key, string root = null)
        {
            var baseRoot = string.IsNullOrWhiteSpace(root) ? DefaultRoot : root;
            return Path.Combine(
                baseRoot,
                "plane_" + key.Plane,
                "storey_" + key.Storey,
                "page_" + key.Page.X + "_" + key.Page.Y + ".json");
        }

        public static bool TryLoad(
            WorldPageKey key,
            int expectedPageSize,
            out AuthoredWorldPage page,
            out string error,
            string root = null)
        {
            page = null;
            error = string.Empty;
            try
            {
                var path = FilePath(key, root);
                if (!File.Exists(path)) return false;
                var dto = JsonUtility.FromJson<PageDto>(File.ReadAllText(path));
                if (dto == null) throw new InvalidDataException("World page JSON did not produce a document.");
                var document = FromDto(dto);
                if (document.PageX != key.Page.X || document.PageY != key.Page.Y || document.Plane != key.Plane || document.Storey != key.Storey)
                    throw new InvalidDataException("World page document identity does not match its requested repository key.");
                if (document.PageSize != expectedPageSize)
                    throw new InvalidDataException("World page size " + document.PageSize + " does not match runtime page size " + expectedPageSize + ".");
                page = WorldPageCodec.Decode(document);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                page = null;
                return false;
            }
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

            if (dto.runs == null) return document;
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
