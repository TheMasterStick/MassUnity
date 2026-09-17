using System;
using MassRPG.Core.Authority;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Data.Items;
using MassRPG.Server.Authority;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class CombatEquipmentLockTests
    {
        [Test]
        public void ArmorIsLockedDuringCombatButWeaponSwapRemainsAllowed()
        {
            var items = new ItemCatalog();
            var chest = new ContentId("test_chest");
            var weapon = new ContentId("test_weapon");
            items.Register(new ItemDefinition(chest, "Test chest", ItemType.Armor, false, 1, "", new[] { EquipmentSlot.Chest }));
            items.Register(new ItemDefinition(weapon, "Test weapon", ItemType.Weapon, false, 1, "", new[] { EquipmentSlot.Weapon }));
            var player = new PlayerState(Guid.NewGuid(), "Fighter");
            InventoryRules.AddItem(player.Inventory, items, chest, 1);
            InventoryRules.AddItem(player.Inventory, items, weapon, 1);
            player.Combat.Begin(Guid.NewGuid());
            var authority = new LocalGameAuthority(items);
            authority.RegisterPlayer(player);

            var armorDecision = authority.Submit(new EquipInventoryItemRequest(Guid.NewGuid(), player.CharacterId, 0));
            var weaponDecision = authority.Submit(new EquipInventoryItemRequest(Guid.NewGuid(), player.CharacterId, 1));

            Assert.IsFalse(armorDecision.Accepted);
            Assert.AreEqual("equipment_locked_in_combat", armorDecision.Code);
            Assert.IsTrue(weaponDecision.Accepted);
            Assert.AreEqual(weapon, player.Equipment.GetOrNull(EquipmentSlot.Weapon).Value);
        }
    }
}
