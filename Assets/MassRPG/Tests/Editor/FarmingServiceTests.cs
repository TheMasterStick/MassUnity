using System;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Core.Resources;
using MassRPG.Core.World;
using MassRPG.Data.Farming;
using MassRPG.Data.Items;
using MassRPG.Server.Farming;
using MassRPG.Server.Resources;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class FarmingServiceTests
    {
        [Test]
        public void CropUsesTimestampGrowthAndHarvestClearsPatch()
        {
            var items = CreateItems();
            var crops = new CropCatalog();
            crops.Register(new CropDefinition(
                new ContentId("potato"), "Potato", FarmPatchKind.Crop, 1, 12, 8, 24_000,
                new ContentId("potato_seed"), new ContentId("potato"), 2, 2));

            var nodes = new InMemoryResourceNodeSource();
            var player = new PlayerState(Guid.NewGuid(), "Farmer");
            var patch = new ResourceNodeKey(
                FarmingService.CropPatchId,
                new GridLocation(new GridCoord(player.Tile.X + 1, player.Tile.Y), player.Plane, player.Storey));
            nodes.Register(patch);
            InventoryRules.AddItem(player.Inventory, items, FarmingService.SeedDibberId, 1);
            InventoryRules.AddItem(player.Inventory, items, new ContentId("potato_seed"), 1);

            var ledger = new FarmPatchLedger();
            var service = new FarmingService(nodes, crops, items, ledger, new Random(1));
            var planted = service.Plant(player, patch, new ContentId("potato"), 1_000);

            Assert.IsTrue(planted.Success);
            Assert.AreEqual(25_000, planted.ReadyAtUnixMilliseconds);
            Assert.AreEqual(0, player.Inventory.CountItem(new ContentId("potato_seed")));
            Assert.AreEqual("still_growing", service.Harvest(player, patch, 24_999).Code);

            var harvested = service.Harvest(player, patch, 25_000);
            Assert.IsTrue(harvested.Success);
            Assert.AreEqual(2, harvested.Quantity);
            Assert.AreEqual(2, player.Inventory.CountItem(new ContentId("potato")));
            Assert.IsFalse(ledger.TryGet(patch, out _));
        }

        [Test]
        public void HerbCannotBePlantedInCropPatch()
        {
            var items = CreateItems();
            var crops = new CropCatalog();
            crops.Register(new CropDefinition(
                new ContentId("guam"), "Guam leaf", FarmPatchKind.Herb, 1, 12, 12, 10_000,
                new ContentId("guam_seed"), new ContentId("grimy_guam")));

            var nodes = new InMemoryResourceNodeSource();
            var player = new PlayerState(Guid.NewGuid(), "Farmer");
            var patch = new ResourceNodeKey(
                FarmingService.CropPatchId,
                new GridLocation(new GridCoord(player.Tile.X + 1, player.Tile.Y), player.Plane, player.Storey));
            nodes.Register(patch);
            InventoryRules.AddItem(player.Inventory, items, FarmingService.SeedDibberId, 1);
            InventoryRules.AddItem(player.Inventory, items, new ContentId("guam_seed"), 1);

            var service = new FarmingService(nodes, crops, items, new FarmPatchLedger(), new Random(1));
            var result = service.Plant(player, patch, new ContentId("guam"), 0);

            Assert.IsFalse(result.Success);
            Assert.AreEqual("wrong_patch_type", result.Code);
            Assert.AreEqual(1, player.Inventory.CountItem(new ContentId("guam_seed")));
        }

        private static ItemCatalog CreateItems()
        {
            var catalog = new ItemCatalog();
            catalog.Register(new ItemDefinition(FarmingService.SeedDibberId, "Seed dibber", ItemType.Tool, false, 4));
            catalog.Register(new ItemDefinition(new ContentId("potato_seed"), "Potato seed", ItemType.Seed, true, 2));
            catalog.Register(new ItemDefinition(new ContentId("potato"), "Potato", ItemType.Food, true, 24));
            catalog.Register(new ItemDefinition(new ContentId("guam_seed"), "Guam seed", ItemType.Seed, true, 4));
            catalog.Register(new ItemDefinition(new ContentId("grimy_guam"), "Grimy guam leaf", ItemType.Material, true, 48));
            return catalog;
        }
    }
}
