using System;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Core.Skills;
using MassRPG.Core.World;
using MassRPG.Data.Items;
using MassRPG.Data.Recipes;
using MassRPG.Server.Production;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class MigrationContentCoverageTests
    {
        [Test]
        public void ItemCatalog_ContainsBroaderBrowserProgressionAndEquipmentTiming()
        {
            var items = MigrationSeedItemCatalog.Create();

            Assert.Greater(items.Count, 100);
            Assert.IsTrue(items.TryGetDefinition(new ContentId("dragonite_2h_sword"), out var twoHanded));
            Assert.IsTrue(twoHanded.TwoHanded);
            Assert.AreEqual(SkillId.Attack, twoHanded.EquipRequirementSkill.Value);
            Assert.AreEqual(60, twoHanded.EquipRequirementLevel);
            Assert.AreEqual(4200, twoHanded.AttackIntervalMilliseconds);

            Assert.IsTrue(items.TryGetDefinition(new ContentId("magic_shortbow"), out var bow));
            Assert.IsTrue(bow.TwoHanded);
            Assert.AreEqual(SkillId.Ranged, bow.EquipRequirementSkill.Value);
            Assert.AreEqual(50, bow.EquipRequirementLevel);
            Assert.AreEqual(6, bow.AttackRangeTiles);
            Assert.AreEqual(3000, bow.AttackIntervalMilliseconds);

            Assert.IsTrue(items.TryGetDefinition(new ContentId("rune_arrow"), out var arrow));
            Assert.AreEqual(ItemType.Ammunition, arrow.Type);
            Assert.AreEqual(0, arrow.AllowedEquipmentSlots.Length);
        }

        [Test]
        public void RecipeCatalog_ContainsSmithingCookingFletchingCraftingHerbloreAndConstruction()
        {
            var recipes = MigrationSeedRecipeCatalog.Create();

            Assert.Greater(recipes.Count, 90);
            Assert.IsTrue(recipes.TryGet(new ContentId("smith_rune_battleaxe"), out _));
            Assert.IsTrue(recipes.TryGet(new ContentId("cook_shark"), out var cookShark));
            Assert.IsTrue(cookShark.CanBurn);
            Assert.AreEqual(new ContentId("burnt_shark"), cookShark.FailureOutputItemId.Value);
            Assert.IsTrue(recipes.TryGet(new ContentId("fletch_magic_string"), out _));
            Assert.IsTrue(recipes.TryGet(new ContentId("craft_ruby_amulet"), out _));
            Assert.IsTrue(recipes.TryGet(new ContentId("brew_prayer_potion"), out _));
            Assert.IsTrue(recipes.TryGet(new ContentId("cut_plank"), out _));
        }

        [Test]
        public void CookingBurn_UsesExplicitFailureOutputAndReducedXp()
        {
            var items = MigrationSeedItemCatalog.Create();
            var recipes = MigrationSeedRecipeCatalog.Create();
            var station = Loc(11, 10);
            var production = new ProductionService(
                recipes,
                items,
                new FixedStationSource(new ContentId("station.fire"), station),
                () => 0.0);
            var player = new PlayerState(Guid.NewGuid(), "Cook") { Location = Loc(10, 10) };
            Assert.AreEqual(1, InventoryRules.AddItem(player.Inventory, items, new ContentId("raw_meat"), 1));
            var beforeXp = player.Skills.GetXp(SkillId.Cooking);

            var start = production.TryStart(player, new ContentId("cook_meat"), 1, station, 1000);
            Assert.IsTrue(start.Success);
            var result = production.Advance(player, 2200);

            Assert.AreEqual(ProductionAdvanceKind.CompletedAll, result.Kind);
            Assert.AreEqual(0, player.Inventory.CountItem(new ContentId("raw_meat")));
            Assert.AreEqual(0, player.Inventory.CountItem(new ContentId("cooked_meat")));
            Assert.AreEqual(1, player.Inventory.CountItem(new ContentId("burnt_meat")));
            Assert.AreEqual(beforeXp + 1, player.Skills.GetXp(SkillId.Cooking));
        }

        [Test]
        public void CookingBurnChance_PreservesBrowserFloorAndScaling()
        {
            Assert.AreEqual(0.03, ProductionService.CalculateBurnChance(99, 1), 0.0001);
            Assert.Greater(ProductionService.CalculateBurnChance(1, 1), 0.03);
            Assert.LessOrEqual(ProductionService.CalculateBurnChance(1, 76), 0.75);
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
    }
}
