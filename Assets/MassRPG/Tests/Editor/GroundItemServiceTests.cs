using System;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Data.Items;
using MassRPG.Server.Items;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class GroundItemServiceTests
    {
        [Test]
        public void ManualDropCreatesTakeableAuthoritativeGroundItem()
        {
            var items = CreateItems();
            var registry = new GroundItemRegistry();
            var service = new GroundItemService(items, registry);
            var player = new PlayerState(Guid.NewGuid(), "Dropper");
            var bread = new ContentId("bread");
            InventoryRules.AddItem(player.Inventory, items, bread, 5);

            var dropped = service.DropFromInventory(player, 0, 3, 100);
            Assert.IsTrue(dropped.Success);
            Assert.AreEqual(2, player.Inventory.CountItem(bread));
            Assert.IsTrue(registry.TryGet(dropped.GroundItemId, out var ground));
            Assert.AreEqual(3, ground.Quantity);

            var taken = service.Take(player, dropped.GroundItemId, 101);
            Assert.IsTrue(taken.Success);
            Assert.AreEqual(3, taken.Quantity);
            Assert.AreEqual(5, player.Inventory.CountItem(bread));
            Assert.IsFalse(registry.TryGet(dropped.GroundItemId, out _));
        }

        [Test]
        public void ProtectedLootBecomesPublicAtConfiguredTimestamp()
        {
            var items = CreateItems();
            var registry = new GroundItemRegistry();
            var service = new GroundItemService(items, registry);
            var owner = new PlayerState(Guid.NewGuid(), "Owner");
            var stranger = new PlayerState(Guid.NewGuid(), "Stranger") { Location = owner.Location };
            var bread = new ContentId("bread");
            var ground = service.Spawn(bread, 1, owner.Location, 0, new[] { owner.CharacterId }, 5_000, 10_000, Guid.NewGuid());

            Assert.AreEqual("ground_item_protected", service.Take(stranger, ground.InstanceId, 4_999).Code);
            Assert.IsTrue(service.Take(stranger, ground.InstanceId, 5_000).Success);
        }

        [Test]
        public void ExpiredGroundItemCannotBePickedUp()
        {
            var items = CreateItems();
            var registry = new GroundItemRegistry();
            var service = new GroundItemService(items, registry);
            var player = new PlayerState(Guid.NewGuid(), "Late");
            var ground = service.Spawn(new ContentId("bread"), 1, player.Location, 0, null, 0, 1_000, Guid.NewGuid());

            var result = service.Take(player, ground.InstanceId, 1_000);
            Assert.IsFalse(result.Success);
            Assert.AreEqual("ground_item_expired", result.Code);
            Assert.IsFalse(registry.TryGet(ground.InstanceId, out _));
        }

        private static ItemCatalog CreateItems()
        {
            var catalog = new ItemCatalog();
            catalog.Register(new ItemDefinition(new ContentId("bread"), "Bread", ItemType.Food, true, 6));
            return catalog;
        }
    }
}
