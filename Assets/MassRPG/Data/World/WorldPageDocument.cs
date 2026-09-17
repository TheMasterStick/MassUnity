using System;
using System.Collections.Generic;
using MassRPG.Core.Content;
using MassRPG.Core.World;

namespace MassRPG.Data.World
{
    /// <summary>
    /// Compact persistence DTO. Repeated neighboring tiles are run-length encoded instead of
    /// writing a record for every one of the world's 32.4 billion logical cells.
    /// </summary>
    public sealed class WorldPageDocument
    {
        public const int CurrentFormatVersion = 1;

        public int FormatVersion { get; set; } = CurrentFormatVersion;
        public int PageX { get; set; }
        public int PageY { get; set; }
        public int Plane { get; set; }
        public int Storey { get; set; }
        public int PageSize { get; set; }
        public List<string> GroundPalette { get; set; } = new List<string>();
        public List<WorldPageRun> Runs { get; set; } = new List<WorldPageRun>();
    }

    public sealed class WorldPageRun
    {
        public int Length { get; set; }
        public ushort GroundIndex { get; set; }
        public short Elevation { get; set; }
        public byte Flags { get; set; }
        public byte MovementEdges { get; set; }
        public byte LineOfSightEdges { get; set; }
        public byte ElevationTransitionEdges { get; set; }
    }

    public static class WorldPageCodec
    {
        public static WorldPageDocument Encode(AuthoredWorldPage page)
        {
            if (page == null) throw new ArgumentNullException(nameof(page));
            var document = new WorldPageDocument
            {
                PageX = page.Key.Page.X,
                PageY = page.Key.Page.Y,
                Plane = page.Key.Plane,
                Storey = page.Key.Storey,
                PageSize = page.PageSize
            };

            var palette = new Dictionary<ContentId, ushort>();
            EncodedCell? current = null;
            WorldPageRun currentRun = null;

            for (var i = 0; i < page.CellCount; i++)
            {
                var cell = page.GetCellByIndex(i);
                if (!palette.TryGetValue(cell.GroundId, out var groundIndex))
                {
                    if (document.GroundPalette.Count >= ushort.MaxValue)
                        throw new InvalidOperationException("Encoded page exceeded the 16-bit ground palette limit.");
                    groundIndex = (ushort)document.GroundPalette.Count;
                    palette.Add(cell.GroundId, groundIndex);
                    document.GroundPalette.Add(cell.GroundId.Value);
                }

                var encoded = new EncodedCell(cell, groundIndex);
                if (current.HasValue && current.Value.Equals(encoded))
                {
                    currentRun.Length++;
                    continue;
                }

                current = encoded;
                currentRun = encoded.ToRun();
                document.Runs.Add(currentRun);
            }

            return document;
        }

        public static AuthoredWorldPage Decode(WorldPageDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (document.FormatVersion != WorldPageDocument.CurrentFormatVersion)
                throw new InvalidOperationException($"Unsupported world page format version {document.FormatVersion}.");
            if (document.PageSize <= 0) throw new InvalidOperationException("World page has an invalid page size.");
            if (document.GroundPalette == null || document.GroundPalette.Count == 0)
                throw new InvalidOperationException("World page has no ground palette.");

            var ids = new ContentId[document.GroundPalette.Count];
            for (var i = 0; i < ids.Length; i++) ids[i] = new ContentId(document.GroundPalette[i]);
            var key = new WorldPageKey(new WorldPageCoord(document.PageX, document.PageY), document.Plane, document.Storey);
            var page = new AuthoredWorldPage(key, document.PageSize, ids[0]);
            var cursor = 0;

            if (document.Runs == null) throw new InvalidOperationException("World page has no run collection.");
            foreach (var run in document.Runs)
            {
                if (run.Length <= 0) throw new InvalidOperationException("World page contains an invalid run length.");
                if (run.GroundIndex >= ids.Length) throw new InvalidOperationException("World page references an invalid ground palette index.");
                var cell = new AuthoredTileCell(
                    ids[run.GroundIndex],
                    run.Elevation,
                    (TileFlags)run.Flags,
                    (CardinalEdgeMask)run.MovementEdges,
                    (CardinalEdgeMask)run.LineOfSightEdges,
                    (CardinalEdgeMask)run.ElevationTransitionEdges);

                for (var n = 0; n < run.Length; n++)
                {
                    if (cursor >= page.CellCount) throw new InvalidOperationException("World page runs overflow the page dimensions.");
                    page.SetCellByIndex(cursor++, cell);
                }
            }

            if (cursor != page.CellCount)
                throw new InvalidOperationException("World page runs do not fill the page dimensions.");
            return page;
        }

        private readonly struct EncodedCell : IEquatable<EncodedCell>
        {
            public EncodedCell(AuthoredTileCell cell, ushort groundIndex)
            {
                GroundIndex = groundIndex;
                Elevation = cell.Elevation;
                Flags = (byte)cell.Flags;
                MovementEdges = (byte)cell.MovementBlockedEdges;
                LineOfSightEdges = (byte)cell.LineOfSightBlockedEdges;
                ElevationTransitionEdges = (byte)cell.ElevationTransitionEdges;
            }

            public ushort GroundIndex { get; }
            public short Elevation { get; }
            public byte Flags { get; }
            public byte MovementEdges { get; }
            public byte LineOfSightEdges { get; }
            public byte ElevationTransitionEdges { get; }

            public bool Equals(EncodedCell other)
                => GroundIndex == other.GroundIndex
                && Elevation == other.Elevation
                && Flags == other.Flags
                && MovementEdges == other.MovementEdges
                && LineOfSightEdges == other.LineOfSightEdges
                && ElevationTransitionEdges == other.ElevationTransitionEdges;

            public WorldPageRun ToRun() => new WorldPageRun
            {
                Length = 1,
                GroundIndex = GroundIndex,
                Elevation = Elevation,
                Flags = Flags,
                MovementEdges = MovementEdges,
                LineOfSightEdges = LineOfSightEdges,
                ElevationTransitionEdges = ElevationTransitionEdges
            };
        }
    }
}
