using System;
using MassRPG.Core.Authority;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Core.Skills;
using MassRPG.Core.World;
using MassRPG.Data.Items;
using MassRPG.Data.Recipes;
using MassRPG.Server.Authority;
using MassRPG.Server.Production;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class ProductionServiceTests
    {
        [Test]
        public void Smelting_IsTimedAndAuthorityConsumesInputsThenAwardsOutputAndXp()
        {
            var items = MigrationSeedItemCatalog.Create();
            var recipes = MigrationSeedRecipeCatalog.Create();
            var station = Loc(11, 10);
            var production = new ProductionService(recipes, items, new FixedStationSource(new ContentId("station.furnace"), station));
            var authority = new LocalGameAuthority(items, production: production);
            var player = new PlayerState(Guid.NewGuid(), "Smith") { Location = Loc(10, 10) };
            player.Skills.SetXp(SkillId.Smithing, SkillProgression.XpForLevel(15));
            Assert.AreEqual(1, InventoryRules.AddItem(player.Inventory, items, new ContentId("iron_ore"), 1));
            authority.RegisterPlayer(player);
            var beforeXp = player.Skills.GetXp(SkillId.Smithing);

            var start = authority.Submit(new StartProductionRequest(
                Guid.NewGuid(), player.CharacterId, new ContentId("smelt_iron_bar"), 1, station), 1000);

            Assert.IsTrue(start.Accepted);
            Assert.IsTrue(player.Production.IsActive);
            Assert.AreEqual(0, player.Inventory.CountItem(new ContentId("iron_bar")));

            var early = authority.AdvanceProduction(player.CharacterId, 2199);
            Assert.AreEqual(ProductionAdvanceKind.Waiting, early.Kind);
            Assert.AreEqual(1, player.Inventory.CountItem(new ContentId("iron_ore")));

            var done = authority.AdvanceProduction(player.CharacterId, 2200);
            Assert.AreEqual(ProductionAdvanceKind.CompletedAll, done.Kind);
            Assert.AreEqual(0, player.Inventory.CountItem(new ContentId("iron_ore")));
            Assert.AreEqual(1, player.Inventory.CountItem(new ContentId("iron_bar")));
            Assert.AreEqual(beforeXp + 13, player.Skills.GetXp(SkillId.Smithing));
            Assert.IsFalse(player.Production.IsActive);
        }

        [Test]
        public void DragoniteRecipe_PreservesSettledTwoOreTwoCoalRule()
        {
            var recipes = MigrationSeedRecipeCatalog.Create();
            Assert.IsTrue(recipes.TryGet(new ContentId("smelt_dragonite_bar"), out var recipe));
            Assert.AreEqual(92, recipe.LevelRequired);
            Assert.AreEqual(2, QuantityOf(recipe, "dragonite_ore"));
            Assert.AreEqual(2, QuantityOf(recipe, "coal"));
            Assert.AreEqual(new ContentId("dragonite_bar"), recipe.OutputItemId);
        }

        [Test]
        public void LeavingStationByMovement_CancelsProduction()
        {
            var items = MigrationSeedItemCatalog.Create();
            var recipes = MigrationSeedRecipeCatalog.Create();
            var station = Loc(11, 10);
            var production = new ProductionService(recipes, items, new FixedStationSource(new ContentId("station.furnace"), station));
            var player = new PlayerState(Guid.NewGuid(), "Smith") { Location = Loc(10, 10) };
            player.Skills.SetXp(SkillId.Smithing, SkillProgression.XpForLevel(15));
            InventoryRules.AddItem(player.Inventory, items, new ContentId("iron_ore"), 1);

            var map = new OpenMap();
            var authority = new LocalGameAuthority(items, map, production: production);
            authority.RegisterPlayer(player);
            Assert.IsTrue(authority.Submit(new StartProductionRequest(
                Guid.NewGuid(), player.CharacterId, new ContentId("smelt_iron_bar"), 1, station), 1000).Accepted);

            Assert.IsTrue(authority.Submit(new MoveToRequest(Guid.NewGuid(), player.CharacterId, Loc(8, 10)), 1100).Accepted);
            Assert.IsFalse(player.Production.IsActive);
        }

        private static int QuantityOf(RecipeDefinition recipe, string itemId)
        {
            var id = new ContentId(itemId);
            for (var i = 0; i < recipe.Inputs.Count; i++)
                if (recipe.Inputs[i].ItemId == id) return recipe.Inputs[i].Quantity;
            return 0;
        }

        private static GridLocation Loc(int x, int y)
            => new GridLocation(new GridCoord(x, y), WorldConstants.SurfacePlane, 0);

        private sealed class FixedStationSource : IProductionStationSource
        {
            private readonly ContentId _station;
            private readonly GridLocation _location;
            public FixedStationSource(ContentId station, GridLocation location)
            {
                _station = station;
                _location = location;
            }
            public bool IsStationAt(ContentId stationId, GridLocation location)
                => stationId == _station && location == _location;
        }

        private sealed class OpenMap : IGridTraversalMap
        {
            public bool IsWalkable(GridLocation location) => WorldConstants.IsInsideWorld(location.Tile);
            public int GetLogicalElevation(GridLocation location) => 0;
            public bool CanTraverseCardinalEdge(GridLocation from, GridLocation to) => IsWalkable(to);
        }
    }
}
