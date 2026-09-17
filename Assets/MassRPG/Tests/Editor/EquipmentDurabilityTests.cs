using System;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Data.Items;
using MassRPG.Server.Items;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class EquipmentDurabilityTests
    {
        [Test]
        public void EquippedItemStartsPristineThenDeathLossAndRepairAreClamped()
        {
            var items = MigrationSeedItemCatalog.Create();
            var player = new PlayerState(Guid.NewGuid(), "Durable");
            InventoryRules.AddItem(player.Inventory, items, new ContentId("bronze_sword"), 1);
            Assert.IsTrue(InventoryRules.EquipFromInventory(
                player.Inventory,
                player.Equipment,
                items,
                player.Skills,
                FindSlot(player.Inventory, new ContentId("bronze_sword")),
                EquipmentSlot.MainHand).Success);

            var durability = new EquipmentDurabilityService();
            Assert.IsTrue(durability.TryGet(player, EquipmentSlot.MainHand, out var pristine));
            Assert.AreEqual(10000, pristine.DurabilityBasisPoints);

            durability.ApplyDeathDurabilityLoss(player, 2500);
            Assert.IsTrue(durability.TryGet(player, EquipmentSlot.MainHand, out var damaged));
            Assert.AreEqual(7500, damaged.DurabilityBasisPoints);

            Assert.IsTrue(durability.TryRepair(player, EquipmentSlot.MainHand, 5000));
            Assert.IsTrue(durability.TryGet(player, EquipmentSlot.MainHand, out var repaired));
            Assert.AreEqual(10000, repaired.DurabilityBasisPoints);

            Assert.IsTrue(durability.TryDamage(player, EquipmentSlot.MainHand, 50000));
            Assert.IsTrue(durability.TryGet(player, EquipmentSlot.MainHand, out var broken));
            Assert.AreEqual(0, broken.DurabilityBasisPoints);
            Assert.IsTrue(broken.IsBroken);
        }

        [Test]
        public void ReplacingItemDoesNotInheritOldItemsDurability()
        {
            var items = MigrationSeedItemCatalog.Create();
            var player = new PlayerState(Guid.NewGuid(), "Swap");
            InventoryRules.AddItem(player.Inventory, items, new ContentId("bronze_sword"), 1);
            InventoryRules.AddItem(player.Inventory, items, new ContentId("bronze_dagger"), 1);
            Assert.IsTrue(InventoryRules.EquipFromInventory(
                player.Inventory, player.Equipment, items, player.Skills,
                FindSlot(player.Inventory, new ContentId("bronze_sword")), EquipmentSlot.MainHand).Success);

            var durability = new EquipmentDurabilityService();
            Assert.IsTrue(durability.TryDamage(player, EquipmentSlot.MainHand, 4000));
            Assert.IsTrue(durability.TryGet(player, EquipmentSlot.MainHand, out var old));
            Assert.AreEqual(6000, old.DurabilityBasisPoints);

            Assert.IsTrue(InventoryRules.EquipFromInventory(
                player.Inventory, player.Equipment, items, player.Skills,
                FindSlot(player.Inventory, new ContentId("bronze_dagger")), EquipmentSlot.MainHand).Success);

            Assert.IsTrue(durability.TryGet(player, EquipmentSlot.MainHand, out var replacement));
            Assert.AreEqual(new ContentId("bronze_dagger"), replacement.ItemId);
            Assert.AreEqual(10000, replacement.DurabilityBasisPoints);
        }

        [Test]
        public void DurabilityRestoreRejectsItemSlotMismatch()
        {
            var items = MigrationSeedItemCatalog.Create();
            var player = new PlayerState(Guid.NewGuid(), "Restore");
            InventoryRules.AddItem(player.Inventory, items, new ContentId("bronze_sword"), 1);
            Assert.IsTrue(InventoryRules.EquipFromInventory(
                player.Inventory, player.Equipment, items, player.Skills,
                FindSlot(player.Inventory, new ContentId("bronze_sword")), EquipmentSlot.MainHand).Success);
            var durability = new EquipmentDurabilityService();

            Assert.Throws<InvalidOperationException>(() => durability.Restore(
                player,
                new[] { new EquipmentDurabilityEntry(EquipmentSlot.MainHand, new ContentId("bronze_dagger"), 9000) }));
        }

        private static int FindSlot(InventoryState inventory, ContentId itemId)
        {
            for (var i = 0; i < inventory.Capacity; i++)
            {
                var stack = inventory.GetSlot(i);
                if (stack != null && stack.ItemId == itemId) return i;
            }
            return -1;
        }
    }
}
