using System;
using MassRPG.Core.Content;
using MassRPG.Core.World;

namespace MassRPG.Data.World.Semantics
{
    public enum WorldAreaKind
    {
        NamedRegion,
        Biome,
        LevelBand,
        FactionTerritory,
        CreatureSpawnZone,
        ResourceDistributionZone,
        NoBuild,
        ForcedPvP,
        PvpProtected,
        DungeonArea,
        Custom
    }

    public enum PlayerMapVisibility
    {
        Public,
        HiddenUntilRevealed,
        NotPlayerMapData
    }

    /// <summary>
    /// One semantic area layer. Areas are deliberately independent and may overlap freely: the
    /// same tile can belong to a named region, biome, level band, kingdom, spawn zone and no-build
    /// zone at the same time. Plane is explicit so underground semantic areas do not leak onto the
    /// surface merely because their X/Y polygon overlaps.
    /// </summary>
    public sealed class WorldAreaDefinition
    {
        public WorldAreaDefinition(
            ContentId id,
            string displayName,
            WorldAreaKind kind,
            WorldAreaShape shape,
            PlayerMapVisibility mapVisibility = PlayerMapVisibility.NotPlayerMapData,
            int plane = WorldConstants.SurfacePlane)
        {
            if (id.IsEmpty) throw new ArgumentException("Area id cannot be empty.", nameof(id));
            Id = id;
            DisplayName = displayName ?? string.Empty;
            Kind = kind;
            Shape = shape ?? throw new ArgumentNullException(nameof(shape));
            MapVisibility = mapVisibility;
            Plane = plane;
        }

        public ContentId Id { get; }
        public string DisplayName { get; set; }
        public WorldAreaKind Kind { get; set; }
        public WorldAreaShape Shape { get; set; }
        public PlayerMapVisibility MapVisibility { get; set; }
        public int Plane { get; set; }
    }
}
