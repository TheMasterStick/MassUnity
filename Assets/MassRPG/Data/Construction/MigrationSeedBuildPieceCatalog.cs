using MassRPG.Core.Content;

namespace MassRPG.Data.Construction
{
    /// <summary>
    /// Development construction content combining the useful browser structure costs with the
    /// modular piece vocabulary settled for persistent player housing.
    /// </summary>
    public static class MigrationSeedBuildPieceCatalog
    {
        public static BuildPieceCatalog Create()
        {
            var catalog = new BuildPieceCatalog();

            catalog.Register(Piece("build.floor_wood", "Wooden floor", BuildPieceKind.Floor,
                BuildPlacementMode.Tile, BuildOccupancyLayer.Surface, 1, 8,
                BuildSupportRequirement.FloorBelow, null, Cost("plank", 2)));
            catalog.Register(Piece("build.wall_wood", "Wooden wall", BuildPieceKind.Wall,
                BuildPlacementMode.CardinalEdge, BuildOccupancyLayer.Structure, 1, 8,
                BuildSupportRequirement.GroundOrFloor, null, Cost("plank", 2), Cost("nails", 2)));
            catalog.Register(Piece("build.doorway_wood", "Wooden doorway", BuildPieceKind.Doorway,
                BuildPlacementMode.CardinalEdge, BuildOccupancyLayer.Structure, 1, 10,
                BuildSupportRequirement.GroundOrFloor, null, Cost("plank", 2), Cost("nails", 2)));
            catalog.Register(Piece("build.fence_wood", "Wooden fence", BuildPieceKind.Fence,
                BuildPlacementMode.CardinalEdge, BuildOccupancyLayer.Structure, 1, 8,
                BuildSupportRequirement.GroundOnly, null, Cost("plank", 2)));
            catalog.Register(Piece("build.gate_wood", "Wooden gate", BuildPieceKind.Gate,
                BuildPlacementMode.CardinalEdge, BuildOccupancyLayer.Structure, 3, 12,
                BuildSupportRequirement.GroundOnly, null, Cost("plank", 3), Cost("nails", 2)));
            catalog.Register(Piece("build.stairs_wood", "Wooden stairs", BuildPieceKind.Stairs,
                BuildPlacementMode.Tile, BuildOccupancyLayer.Structure, 5, 20,
                BuildSupportRequirement.GroundOrFloor, null, Cost("plank", 4), Cost("nails", 4)));
            catalog.Register(Piece("build.roof_wood", "Wooden roof", BuildPieceKind.Roof,
                BuildPlacementMode.Tile, BuildOccupancyLayer.Roof, 5, 12,
                BuildSupportRequirement.AdjacentWall, null, Cost("plank", 2), Cost("nails", 2)));
            catalog.Register(Piece("build.wall_stone", "Stone wall", BuildPieceKind.Wall,
                BuildPlacementMode.CardinalEdge, BuildOccupancyLayer.Structure, 10, 25,
                BuildSupportRequirement.GroundOrFloor, null, Cost("stone", 6)));

            catalog.Register(Piece("build.workbench", "Workbench", BuildPieceKind.Workstation,
                BuildPlacementMode.Tile, BuildOccupancyLayer.Fixture, 1, 20,
                BuildSupportRequirement.GroundOrFloor, new ContentId("station.workbench"), Cost("plank", 4)));
            catalog.Register(Piece("build.bed", "Bed", BuildPieceKind.Bed,
                BuildPlacementMode.Tile, BuildOccupancyLayer.Fixture, 5, 30,
                BuildSupportRequirement.GroundOrFloor, null, Cost("plank", 3)));
            catalog.Register(Piece("build.storage_chest", "Storage chest", BuildPieceKind.Container,
                BuildPlacementMode.Tile, BuildOccupancyLayer.Fixture, 15, 60,
                BuildSupportRequirement.GroundOrFloor, null, Cost("plank", 6), Cost("nails", 6)));
            catalog.Register(Piece("build.cooking_range", "Cooking range", BuildPieceKind.Workstation,
                BuildPlacementMode.Tile, BuildOccupancyLayer.Fixture, 10, 50,
                BuildSupportRequirement.GroundOrFloor, new ContentId("station.fire"), Cost("stone", 15)));
            catalog.Register(Piece("build.furnace", "Furnace", BuildPieceKind.Workstation,
                BuildPlacementMode.Tile, BuildOccupancyLayer.Fixture, 16, 80,
                BuildSupportRequirement.GroundOnly, new ContentId("station.furnace"), Cost("stone", 30)));
            catalog.Register(Piece("build.anvil", "Anvil", BuildPieceKind.Workstation,
                BuildPlacementMode.Tile, BuildOccupancyLayer.Fixture, 20, 90,
                BuildSupportRequirement.GroundOrFloor, new ContentId("station.anvil"), Cost("iron_bar", 5)));
            catalog.Register(Piece("build.tannery", "Tannery", BuildPieceKind.Workstation,
                BuildPlacementMode.Tile, BuildOccupancyLayer.Fixture, 25, 100,
                BuildSupportRequirement.GroundOnly, new ContentId("station.tannery"), Cost("stone", 20), Cost("plank", 5)));
            catalog.Register(Piece("build.loom", "Loom", BuildPieceKind.Workstation,
                BuildPlacementMode.Tile, BuildOccupancyLayer.Fixture, 8, 40,
                BuildSupportRequirement.GroundOrFloor, new ContentId("station.loom"), Cost("plank", 6)));

            return catalog;
        }

        private static BuildPieceDefinition Piece(
            string id,
            string name,
            BuildPieceKind kind,
            BuildPlacementMode placementMode,
            BuildOccupancyLayer occupancyLayer,
            int level,
            long xp,
            BuildSupportRequirement support,
            ContentId? stationId,
            params BuildMaterialCost[] costs)
            => new BuildPieceDefinition(
                new ContentId(id), name, kind, placementMode, occupancyLayer, level, xp, costs, support, stationId);

        private static BuildMaterialCost Cost(string itemId, int quantity)
            => new BuildMaterialCost(new ContentId(itemId), quantity);
    }
}
