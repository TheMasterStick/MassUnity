using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Data.Items;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class InventoryRulesTests
    {
        [Test]
        public void StackableItems_MergeIntoExistingStack()
        {
            var catalog = new ItemCatalog();
            var coins = new ContentId("coins");
            catalog.Register(new ItemDefinition(coins, "Coins", ItemType.Currency, true));
            var inventory = new InventoryState();

            Assert.AreEqual(50, InventoryRules.AddItem(inventory, catalog, coins, 50));
            Assert.AreEqual(25, InventoryRules.AddItem(inventory, catalog, coins, 25));
            Assert.AreEqual(75, inventory.CountItem(coins));
            Assert.AreEqual(27, inventory.EmptySlotCount);
        }

        [Test]
        public void TwoHandedWeapon_DisplacesWeaponAndShield()
        {
            var catalog = BuildEquipmentCatalog();
            var inventory = new InventoryState();
            var equipment = new EquipmentState();
            var sword = new ContentId("test.sword");
            var shield = new ContentId("test.shield");
            var greatsword = new ContentId("test.greatsword");

            InventoryRules.AddItem(inventory, catalog, sword, 1);
            InventoryRules.AddItem(inventory, catalog, shield, 1);
            InventoryRules.AddItem(inventory, catalog, greatsword, 1);

            Assert.IsTrue(InventoryRules.EquipFromInventory(inventory, equipment, catalog, 0).Success);
            Assert.IsTrue(InventoryRules.EquipFromInventory(inventory, equipment, catalog, 1).Success);
            Assert.IsTrue(InventoryRules.EquipFromInventory(inventory, equipment, catalog, 2).Success);

            Assert.IsTrue(equipment.TryGet(EquipmentSlot.Weapon, out var equippedWeapon));
            Assert.AreEqual(greatsword, equippedWeapon);
            Assert.IsFalse(equipment.IsOccupied(EquipmentSlot.Shield));
            Assert.AreEqual(1, inventory.CountItem(sword));
            Assert.AreEqual(1, inventory.CountItem(shield));
        }

        [Test]
        public void RingItem_UsesBothSettledRingSlots()
        {
            var catalog = new ItemCatalog();
            var ringA = new ContentId("test.ring_a");
            var ringB = new ContentId("test.ring_b");
            var ringSlots = new[] { EquipmentSlot.Ring1, EquipmentSlot.Ring2 };
            catalog.Register(new ItemDefinition(ringA, "Ring A", ItemType.Armor, false, allowedEquipmentSlots: ringSlots));
            catalog.Register(new ItemDefinition(ringB, "Ring B", ItemType.Armor, false, allowedEquipmentSlots: ringSlots));
            var inventory = new InventoryState();
            var equipment = new EquipmentState();
            InventoryRules.AddItem(inventory, catalog, ringA, 1);
            InventoryRules.AddItem(inventory, catalog, ringB, 1);

            Assert.IsTrue(InventoryRules.EquipFromInventory(inventory, equipment, catalog, 0).Success);
            Assert.IsTrue(InventoryRules.EquipFromInventory(inventory, equipment, catalog, 1).Success);
            Assert.IsTrue(equipment.IsOccupied(EquipmentSlot.Ring1));
            Assert.IsTrue(equipment.IsOccupied(EquipmentSlot.Ring2));
        }

        private static ItemCatalog BuildEquipmentCatalog()
        {
            var catalog = new ItemCatalog();
            catalog.Register(new ItemDefinition(new ContentId("test.sword"), "Sword", ItemType.Weapon, false,
                allowedEquipmentSlots: new[] { EquipmentSlot.Weapon }));
            catalog.Register(new ItemDefinition(new ContentId("test.shield"), "Shield", ItemType.Armor, false,
                allowedEquipmentSlots: new[] { EquipmentSlot.Shield }));
            catalog.Register(new ItemDefinition(new ContentId("test.greatsword"), "Greatsword", ItemType.Weapon, false,
                allowedEquipmentSlots: new[] { EquipmentSlot.Weapon }, twoHanded: true));
            return catalog;
        }
    }
}
