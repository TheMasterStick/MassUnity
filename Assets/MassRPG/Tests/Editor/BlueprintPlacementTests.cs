using System;
using System.Collections.Generic;
using System.Linq;
using MassRPG.Core.Characters;
using MassRPG.Core.Construction;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Core.Skills;
using MassRPG.Core.World;
using MassRPG.Data.Construction;
using MassRPG.Data.Items;
using MassRPG.Server.Construction;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class BlueprintPlacementTests
    {
        [Test]
        public void WholeBlueprintRotation_RotatesOffsetsEdgesAndPieceFacingClockwise()
        {
            var piece = new BlueprintPiece(
                new ContentId("build.wall_wood"),
                2,
                -1,
                1,
                CardinalEdgeMask.North,
                3);
            var origin = Loc(100, 200);

            Assert.AreEqual(Loc(101, 202, 1), piece.Resolve(origin, 1));
            Assert.AreEqual(CardinalEdgeMask.East, piece.ResolveEdge(1));
            Assert.AreEqual(0, piece.ResolveRotationQuarterTurns(1));
        }

        [Test]
        public void Preview_IsSideEffectFree_AndAggregatesMaterials()
        {
            var setup = CreateSetup();
            Give(setup.Player, setup.Items, "plank", 6);
            Give(setup.Player, setup.Items, "nails", 4);
            var beforeXp = setup.Player.Skills.GetXp(SkillId.Construction);
            var blueprint = SupportedUpperFloorBlueprint(setup.Player.CharacterId);

            var preview = setup.Construction.PreviewBlueprint(setup.Player, setup.Plot.PlotId, blueprint, Loc(10, 10));

            Assert.IsTrue(preview.Success);
            Assert.AreEqual(3, preview.Pieces.Count);
            Assert.AreEqual(24, preview.ConstructionXp);
            Assert.AreEqual(6, preview.Materials.Single(x => x.ItemId == new ContentId("plank")).Quantity);
            Assert.AreEqual(4, preview.Materials.Single(x => x.ItemId == new ContentId("nails")).Quantity);
            Assert.IsFalse(setup.Construction.TryGetState(setup.Plot.PlotId, out _), "Preview must not create persistent structure state.");
            Assert.AreEqual(6, setup.Player.Inventory.CountItem(new ContentId("plank")));
            Assert.AreEqual(4, setup.Player.Inventory.CountItem(new ContentId("nails")));
            Assert.AreEqual(beforeXp, setup.Player.Skills.GetXp(SkillId.Construction));
        }

        [Test]
        public void BlueprintPlacement_AllowsSupportDependenciesInAnySourceOrder_AndCommitsAllAtOnce()
        {
            var setup = CreateSetup();
            Give(setup.Player, setup.Items, "plank", 6);
            Give(setup.Player, setup.Items, "nails", 4);
            var beforeXp = setup.Player.Skills.GetXp(SkillId.Construction);
            var blueprint = SupportedUpperFloorBlueprint(setup.Player.CharacterId);

            // The upper floor deliberately appears first in the blueprint; the preview planner must
            // discover that the two ground-floor support walls can be validated before it.
            var result = setup.Construction.TryPlaceBlueprint(setup.Player, setup.Plot.PlotId, blueprint, Loc(10, 10));

            Assert.IsTrue(result.Success);
            Assert.AreEqual(3, result.Pieces.Count);
            Assert.IsTrue(setup.Construction.TryGetState(setup.Plot.PlotId, out var state));
            Assert.AreEqual(3, state.Count);
            Assert.AreEqual(0, setup.Player.Inventory.CountItem(new ContentId("plank")));
            Assert.AreEqual(0, setup.Player.Inventory.CountItem(new ContentId("nails")));
            Assert.AreEqual(beforeXp + 24, setup.Player.Skills.GetXp(SkillId.Construction));
            Assert.IsTrue(result.Pieces.Any(piece => piece.DefinitionId == new ContentId("build.floor_wood") && piece.Anchor.Storey == 1));
        }

        [Test]
        public void BlueprintPlacement_MissingAggregateMaterialLeavesEverythingUntouched()
        {
            var setup = CreateSetup();
            Give(setup.Player, setup.Items, "plank", 5);
            Give(setup.Player, setup.Items, "nails", 4);
            var beforeXp = setup.Player.Skills.GetXp(SkillId.Construction);
            var blueprint = SupportedUpperFloorBlueprint(setup.Player.CharacterId);

            var result = setup.Construction.TryPlaceBlueprint(setup.Player, setup.Plot.PlotId, blueprint, Loc(10, 10));

            Assert.IsFalse(result.Success);
            Assert.AreEqual("missing_materials", result.Code);
            Assert.IsFalse(setup.Construction.TryGetState(setup.Plot.PlotId, out _));
            Assert.AreEqual(5, setup.Player.Inventory.CountItem(new ContentId("plank")));
            Assert.AreEqual(4, setup.Player.Inventory.CountItem(new ContentId("nails")));
            Assert.AreEqual(beforeXp, setup.Player.Skills.GetXp(SkillId.Construction));
        }

        [Test]
        public void BlueprintPlacement_InternalCollisionFailsBeforeAnyMutation()
        {
            var setup = CreateSetup();
            Give(setup.Player, setup.Items, "plank", 10);
            Give(setup.Player, setup.Items, "nails", 10);
            var blueprint = new BuildingBlueprint(
                Guid.NewGuid(),
                setup.Player.CharacterId,
                "Duplicate wall",
                new[]
                {
                    new BlueprintPiece(new ContentId("build.wall_wood"), 0, 0, 0, CardinalEdgeMask.East),
                    new BlueprintPiece(new ContentId("build.wall_wood"), 1, 0, 0, CardinalEdgeMask.West)
                });

            var result = setup.Construction.TryPlaceBlueprint(setup.Player, setup.Plot.PlotId, blueprint, Loc(10, 10));

            Assert.IsFalse(result.Success);
            Assert.AreEqual("occupied_build_slot", result.Code);
            Assert.IsFalse(setup.Construction.TryGetState(setup.Plot.PlotId, out _));
            Assert.AreEqual(10, setup.Player.Inventory.CountItem(new ContentId("plank")));
            Assert.AreEqual(10, setup.Player.Inventory.CountItem(new ContentId("nails")));
        }

        private static BuildingBlueprint SupportedUpperFloorBlueprint(Guid author)
            => new BuildingBlueprint(
                Guid.NewGuid(),
                author,
                "Supported upper floor",
                new[]
                {
                    new BlueprintPiece(new ContentId("build.floor_wood"), 0, 0, 1),
                    new BlueprintPiece(new ContentId("build.wall_wood"), 0, 0, 0, CardinalEdgeMask.North),
                    new BlueprintPiece(new ContentId("build.wall_wood"), 0, 0, 0, CardinalEdgeMask.West)
                });

        private static Setup CreateSetup()
        {
            var items = MigrationSeedItemCatalog.Create();
            var registry = new PlayerPlotRegistry(new OpenPlacementMap());
            var owner = Guid.NewGuid();
            var placement = registry.TryPlace(
                Guid.NewGuid(), owner, WorldConstants.SurfacePlane,
                Rect(8, 8, 8, 8), Rect(6, 6, 12, 12));
            Assert.IsTrue(placement.Success);
            var player = new PlayerState(owner, "Blueprint Builder") { Location = Loc(10, 10) };
            var construction = new PlotConstructionService(registry, MigrationSeedBuildPieceCatalog.Create(), items);
            return new Setup(items, registry, placement.Plot, player, construction);
        }

        private static void Give(PlayerState player, ItemCatalog items, string itemId, int quantity)
        {
            Assert.AreEqual(quantity, InventoryRules.AddItem(player.Inventory, items, new ContentId(itemId), quantity));
        }

        private static IEnumerable<GridCoord> Rect(int x, int y, int width, int height)
        {
            for (var yy = 0; yy < height; yy++)
                for (var xx = 0; xx < width; xx++)
                    yield return new GridCoord(x + xx, y + yy);
        }

        private static GridLocation Loc(int x, int y, int storey = 0)
            => new GridLocation(new GridCoord(x, y), WorldConstants.SurfacePlane, storey);

        private sealed class OpenPlacementMap : IPlotPlacementMap
        {
            public bool CanReserveForPlayerPlot(GridLocation location) => true;
        }

        private sealed class Setup
        {
            public Setup(ItemCatalog items, PlayerPlotRegistry registry, PlayerPlotState plot, PlayerState player, PlotConstructionService construction)
            {
                Items = items;
                Registry = registry;
                Plot = plot;
                Player = player;
                Construction = construction;
            }

            public ItemCatalog Items { get; }
            public PlayerPlotRegistry Registry { get; }
            public PlayerPlotState Plot { get; }
            public PlayerState Player { get; }
            public PlotConstructionService Construction { get; }
        }
    }
}
