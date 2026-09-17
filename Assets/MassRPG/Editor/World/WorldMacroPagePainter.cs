using System;
using System.Collections.Generic;
using MassRPG.Core.Content;
using MassRPG.Core.World;
using MassRPG.Data.World;

namespace MassRPG.Editor.World
{
    public enum MacroPagePaintKind
    {
        Land,
        Water,
        DeepWater,
        Unpainted
    }

    /// <summary>
    /// Coarse whole-world authoring writes canonical storage pages directly as compact single-run
    /// documents. This makes a 180k world practical to block out without allocating 512x512 tile
    /// arrays for every page touched. Fine editing later loads the same page into WorldEditSession.
    /// Macro painting is deliberately page-granular and destructive for the selected pages; it is
    /// intended for the initial rough geography pass, not detail work.
    /// </summary>
    public static class WorldMacroPagePainter
    {
        public static int PageColumns => DivideRoundUp(WorldConstants.WorldWidthTiles, WorldConstants.DefaultStoragePageSize);
        public static int PageRows => DivideRoundUp(WorldConstants.WorldHeightTiles, WorldConstants.DefaultStoragePageSize);

        public static WorldPageDocument CreateUniformPage(
            int pageX,
            int pageY,
            int plane,
            int storey,
            ContentId groundId,
            short elevation,
            MacroPagePaintKind kind)
        {
            if (pageX < 0 || pageX >= PageColumns) throw new ArgumentOutOfRangeException(nameof(pageX));
            if (pageY < 0 || pageY >= PageRows) throw new ArgumentOutOfRangeException(nameof(pageY));
            if (groundId.IsEmpty) throw new ArgumentException("Ground id cannot be empty.", nameof(groundId));

            var flags = TileFlags.None;
            switch (kind)
            {
                case MacroPagePaintKind.Water:
                    flags = TileFlags.Water;
                    break;
                case MacroPagePaintKind.DeepWater:
                    flags = TileFlags.DeepWater;
                    break;
                case MacroPagePaintKind.Unpainted:
                    groundId = new ContentId("ground.unpainted");
                    elevation = 0;
                    break;
            }

            var pageSize = WorldConstants.DefaultStoragePageSize;
            return new WorldPageDocument
            {
                PageX = pageX,
                PageY = pageY,
                Plane = plane,
                Storey = storey,
                PageSize = pageSize,
                GroundPalette = new List<string> { groundId.Value },
                Runs = new List<WorldPageRun>
                {
                    new WorldPageRun
                    {
                        Length = checked(pageSize * pageSize),
                        GroundIndex = 0,
                        Elevation = elevation,
                        Flags = (byte)flags,
                        MovementEdges = 0,
                        LineOfSightEdges = 0,
                        ElevationTransitionEdges = 0
                    }
                }
            };
        }

        public static void SaveUniformPage(
            int pageX,
            int pageY,
            int plane,
            int storey,
            ContentId groundId,
            short elevation,
            MacroPagePaintKind kind)
            => WorldPageJsonPersistence.Save(CreateUniformPage(pageX, pageY, plane, storey, groundId, elevation, kind));

        private static int DivideRoundUp(int value, int divisor) => (value + divisor - 1) / divisor;
    }

    public readonly struct WorldPageOverviewSummary
    {
        public WorldPageOverviewSummary(
            int pageX,
            int pageY,
            string dominantGround,
            int authoredCellCount,
            int waterCellCount,
            int deepWaterCellCount,
            short minimumElevation,
            short maximumElevation)
        {
            PageX = pageX;
            PageY = pageY;
            DominantGround = dominantGround ?? "ground.unpainted";
            AuthoredCellCount = authoredCellCount;
            WaterCellCount = waterCellCount;
            DeepWaterCellCount = deepWaterCellCount;
            MinimumElevation = minimumElevation;
            MaximumElevation = maximumElevation;
        }

        public int PageX { get; }
        public int PageY { get; }
        public string DominantGround { get; }
        public int AuthoredCellCount { get; }
        public int WaterCellCount { get; }
        public int DeepWaterCellCount { get; }
        public short MinimumElevation { get; }
        public short MaximumElevation { get; }
        public int WetCellCount => WaterCellCount + DeepWaterCellCount;
        public bool IsMostlyDeepWater => AuthoredCellCount > 0 && DeepWaterCellCount * 2 >= AuthoredCellCount;
        public bool IsMostlyWater => AuthoredCellCount > 0 && WetCellCount * 2 >= AuthoredCellCount;

        public static WorldPageOverviewSummary FromDocument(WorldPageDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            var groundCounts = new Dictionary<ushort, int>();
            var authored = 0;
            var water = 0;
            var deepWater = 0;
            var minElevation = short.MaxValue;
            var maxElevation = short.MinValue;

            if (document.Runs != null)
            {
                for (var i = 0; i < document.Runs.Count; i++)
                {
                    var run = document.Runs[i];
                    if (run.Length <= 0) continue;
                    authored += run.Length;
                    if (!groundCounts.TryGetValue(run.GroundIndex, out var count)) count = 0;
                    groundCounts[run.GroundIndex] = checked(count + run.Length);
                    var flags = (TileFlags)run.Flags;
                    if ((flags & TileFlags.DeepWater) != 0) deepWater += run.Length;
                    else if ((flags & TileFlags.Water) != 0) water += run.Length;
                    if (run.Elevation < minElevation) minElevation = run.Elevation;
                    if (run.Elevation > maxElevation) maxElevation = run.Elevation;
                }
            }

            ushort dominant = 0;
            var dominantCount = -1;
            foreach (var pair in groundCounts)
            {
                if (pair.Value <= dominantCount) continue;
                dominant = pair.Key;
                dominantCount = pair.Value;
            }

            var ground = document.GroundPalette != null && dominant < document.GroundPalette.Count
                ? document.GroundPalette[dominant]
                : "ground.unpainted";
            if (authored == 0) { minElevation = 0; maxElevation = 0; }

            return new WorldPageOverviewSummary(
                document.PageX,
                document.PageY,
                ground,
                authored,
                water,
                deepWater,
                minElevation,
                maxElevation);
        }
    }
}
