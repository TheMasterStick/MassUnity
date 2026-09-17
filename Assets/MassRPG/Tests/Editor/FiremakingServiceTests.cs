using System;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Core.Skills;
using MassRPG.Core.World;
using MassRPG.Data.Firemaking;
using MassRPG.Data.Items;
using MassRPG.Server.Firemaking;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class FiremakingServiceTests
    {
        [Test]
        public void LightingConsumesLogAwardsXpAndExpiresByTimestamp()
        {
            var items = CreateItems();
            var definitions = new FiremakingCatalog();
            definitions.Register(new FiremakingDefinition(new ContentId("normal_logs"), 1, 40, 90_000));
            var fires = new TemporaryCampfireLedger();
            var service = new FiremakingService(definitions, items, new FixedPlacement(true), fires);
            var player = new PlayerState(Guid.NewGuid(), "Firemaker");
            InventoryRules.AddItem(player.Inventory, items, FiremakingService.TinderboxId, 1);
            InventoryRules.AddItem(player.Inventory, items, new ContentId("normal_logs"), 2);
            var xpBefore = player.Skills.GetXp(SkillId.Firemaking);

            var result = service.Light(player, new ContentId("normal_logs"), 1_000, Guid.NewGuid());

            Assert.IsTrue(result.Success);
            Assert.AreEqual(91_000, result.ExpiresAtUnixMilliseconds);
            Assert.AreEqual(1, player.Inventory.CountItem(new ContentId("normal_logs")));
            Assert.AreEqual(xpBefore + 40, player.Skills.GetXp(SkillId.Firemaking));
            Assert.IsTrue(fires.TryGet(player.Location, 90_999, out _));
            Assert.IsFalse(fires.TryGet(player.Location, 91_000, out _));
        }

        [Test]
        public void BlockedLocationDoesNotConsumeLogs()
        {
            var items = CreateItems();
            var definitions = new FiremakingCatalog();
            definitions.Register(new FiremakingDefinition(new ContentId("normal_logs"), 1, 40, 90_000));
            var service = new FiremakingService(definitions, items, new FixedPlacement(false), new TemporaryCampfireLedger());
            var player = new PlayerState(Guid.NewGuid(), "Firemaker");
            InventoryRules.AddItem(player.Inventory, items, FiremakingService.TinderboxId, 1);
            InventoryRules.AddItem(player.Inventory, items, new ContentId("normal_logs"), 1);

            var result = service.Light(player, new ContentId("normal_logs"), 0);

            Assert.IsFalse(result.Success);
            Assert.AreEqual("blocked_location", result.Code);
            Assert.AreEqual(1, player.Inventory.CountItem(new ContentId("normal_logs")));
        }

        private static ItemCatalog CreateItems()
        {
            var catalog = new ItemCatalog();
            catalog.Register(new ItemDefinition(FiremakingService.TinderboxId, "Tinderbox", ItemType.Tool, false, 5));
            catalog.Register(new ItemDefinition(new ContentId("normal_logs"), "Logs", ItemType.Resource, true, 2));
            return catalog;
        }

        private sealed class FixedPlacement : IFirePlacementValidator
        {
            private readonly bool _allowed;
            public FixedPlacement(bool allowed) => _allowed = allowed;
            public bool CanPlaceCampfire(GridLocation location) => _allowed;
        }
    }
}
