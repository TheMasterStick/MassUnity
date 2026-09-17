using System;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Core.Skills;
using MassRPG.Data.Items;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class DualWieldEquipmentTests
    {
        [Test]
        public void TwoOneHandedWeapons_CanOccupyBothHands()
        {
            var items = MigrationSeedItemCatalog.Create();
            var inventory = new InventoryState(8);
            var equipment = new EquipmentState();
            var skills = new SkillSet();
            var sword = new ContentId("iron_sword");
            var dagger = new ContentId("iron_dagger");
            InventoryRules.AddItem(inventory, items, sword, 1);
            InventoryRules.AddItem(inventory, items, dagger, 1);

            Assert.IsTrue(InventoryRules.EquipFromInventory(inventory, equipment, items, skills, 0, EquipmentSlot.MainHand).Success);
            Assert.IsTrue(InventoryRules.EquipFromInventory(inventory, equipment, items, skills, 1, EquipmentSlot.OffHand).Success);
            Assert.AreEqual(sword, equipment.GetOrNull(EquipmentSlot.MainHand));
            Assert.AreEqual(dagger, equipment.GetOrNull(EquipmentSlot.OffHand));
        }

        [Test]
        public void TwoHandedWeapon_DisplacesOffHandWeapon()
        {
            var items = MigrationSeedItemCatalog.Create();
            var inventory = new InventoryState(8);
            var equipment = new EquipmentState();
            var skills = new SkillSet();
            InventoryRules.AddItem(inventory, items, new ContentId("iron_sword"), 1);
            InventoryRules.AddItem(inventory, items, new ContentId("iron_dagger"), 1);
            InventoryRules.AddItem(inventory, items, new ContentId("iron_2h_sword"), 1);

            Assert.IsTrue(InventoryRules.EquipFromInventory(inventory, equipment, items, skills, 0, EquipmentSlot.MainHand).Success);
            Assert.IsTrue(InventoryRules.EquipFromInventory(inventory, equipment, items, skills, 1, EquipmentSlot.OffHand).Success);
            Assert.IsTrue(InventoryRules.EquipFromInventory(inventory, equipment, items, skills, 2, EquipmentSlot.MainHand).Success);

            Assert.AreEqual(new ContentId("iron_2h_sword"), equipment.GetOrNull(EquipmentSlot.MainHand));
            Assert.IsFalse(equipment.IsOccupied(EquipmentSlot.OffHand));
            Assert.AreEqual(1, inventory.CountItem(new ContentId("iron_dagger")));
        }

        [Test]
        public void ShieldAndOffHandWeapon_RemainMutuallyExclusiveBySharingOffHandSlot()
        {
            var items = MigrationSeedItemCatalog.Create();
            var inventory = new InventoryState(8);
            var equipment = new EquipmentState();
            var skills = new SkillSet();
            InventoryRules.AddItem(inventory, items, new ContentId("iron_dagger"), 1);
            InventoryRules.AddItem(inventory, items, new ContentId("iron_shield"), 1);

            Assert.IsTrue(InventoryRules.EquipFromInventory(inventory, equipment, items, skills, 0, EquipmentSlot.OffHand).Success);
            Assert.IsTrue(InventoryRules.EquipFromInventory(inventory, equipment, items, skills, 1, EquipmentSlot.OffHand).Success);

            Assert.AreEqual(new ContentId("iron_shield"), equipment.GetOrNull(EquipmentSlot.OffHand));
            Assert.AreEqual(1, inventory.CountItem(new ContentId("iron_dagger")));
        }
    }
}
