using System;
using System.Collections.Generic;
using MassRPG.Core.Content;

namespace MassRPG.Data.Construction
{
    public enum BuildPieceKind
    {
        Floor,
        Wall,
        Doorway,
        Fence,
        Gate,
        Roof,
        Stairs,
        Container,
        Workstation,
        Bed,
        Decoration
    }

    public enum BuildPlacementMode
    {
        Tile,
        CardinalEdge
    }

    public enum BuildOccupancyLayer
    {
        Surface,
        Structure,
        Fixture,
        Roof
    }

    [Flags]
    public enum BuildSupportRequirement
    {
        None = 0,
        GroundOrFloor = 1 << 0,
        FloorBelow = 1 << 1,
        AdjacentWall = 1 << 2,
        GroundOnly = 1 << 3
    }

    public readonly struct BuildMaterialCost
    {
        public BuildMaterialCost(ContentId itemId, int quantity)
        {
            if (itemId.IsEmpty) throw new ArgumentException("Material item id cannot be empty.", nameof(itemId));
            if (quantity < 1) throw new ArgumentOutOfRangeException(nameof(quantity));
            ItemId = itemId;
            Quantity = quantity;
        }

        public ContentId ItemId { get; }
        public int Quantity { get; }
    }

    /// <summary>
    /// Published modular building piece. Geometry/prefab references stay client-side; this data is
    /// the authoritative gameplay description used by plots, persistence and the Data Editor.
    /// </summary>
    public sealed class BuildPieceDefinition
    {
        private readonly List<BuildMaterialCost> _costs;

        public BuildPieceDefinition(
            ContentId id,
            string displayName,
            BuildPieceKind kind,
            BuildPlacementMode placementMode,
            BuildOccupancyLayer occupancyLayer,
            int constructionLevel,
            long constructionXp,
            IEnumerable<BuildMaterialCost> costs,
            BuildSupportRequirement supportRequirement = BuildSupportRequirement.GroundOrFloor,
            ContentId? stationId = null,
            bool allowsRotation = true,
            int footprintWidth = 1,
            int footprintHeight = 1)
        {
            if (id.IsEmpty) throw new ArgumentException("Build piece id cannot be empty.", nameof(id));
            if (constructionLevel < 1 || constructionLevel > 300) throw new ArgumentOutOfRangeException(nameof(constructionLevel));
            if (constructionXp < 0) throw new ArgumentOutOfRangeException(nameof(constructionXp));
            if (costs == null) throw new ArgumentNullException(nameof(costs));
            if (footprintWidth < 1 || footprintHeight < 1) throw new ArgumentOutOfRangeException(nameof(footprintWidth));

            Id = id;
            DisplayName = displayName ?? string.Empty;
            Kind = kind;
            PlacementMode = placementMode;
            OccupancyLayer = occupancyLayer;
            ConstructionLevel = constructionLevel;
            ConstructionXp = constructionXp;
            _costs = new List<BuildMaterialCost>(costs);
            SupportRequirement = supportRequirement;
            StationId = stationId;
            AllowsRotation = allowsRotation;
            FootprintWidth = footprintWidth;
            FootprintHeight = footprintHeight;
        }

        public ContentId Id { get; }
        public string DisplayName { get; set; }
        public BuildPieceKind Kind { get; set; }
        public BuildPlacementMode PlacementMode { get; set; }
        public BuildOccupancyLayer OccupancyLayer { get; set; }
        public int ConstructionLevel { get; set; }
        public long ConstructionXp { get; set; }
        public IReadOnlyList<BuildMaterialCost> Costs => _costs;
        public BuildSupportRequirement SupportRequirement { get; set; }
        public ContentId? StationId { get; set; }
        public bool AllowsRotation { get; set; }
        public int FootprintWidth { get; set; }
        public int FootprintHeight { get; set; }
    }

    public interface IBuildPieceDefinitionSource
    {
        bool TryGet(ContentId id, out BuildPieceDefinition definition);
    }

    public sealed class BuildPieceCatalog : IBuildPieceDefinitionSource
    {
        private readonly Dictionary<ContentId, BuildPieceDefinition> _definitions = new Dictionary<ContentId, BuildPieceDefinition>();

        public IEnumerable<BuildPieceDefinition> All => _definitions.Values;
        public int Count => _definitions.Count;

        public void Register(BuildPieceDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (_definitions.ContainsKey(definition.Id)) throw new InvalidOperationException("Duplicate build piece id '" + definition.Id + "'.");
            _definitions.Add(definition.Id, definition);
        }

        public bool TryGet(ContentId id, out BuildPieceDefinition definition) => _definitions.TryGetValue(id, out definition);
    }
}
