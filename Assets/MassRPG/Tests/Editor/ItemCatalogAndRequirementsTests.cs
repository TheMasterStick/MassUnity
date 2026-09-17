using System;
using MassRPG.Core.Authority;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Core.Skills;
using MassRPG.Data.Items;
using MassRPG.Server.Authority;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class ItemCatalogAndRequirementsTests
    {
        [Test]
        public void MigrationCatalog_ContainsRepresentativeRealContent()
        {
            var catalog = MigrationSeedItemCatalog.Create();

            Assert.IsTrue(catalog.TryGetDefinition(new ContentId("coins"), out var coins));
            Assert.IsTrue(coins.Stackable);
            Assert.IsTrue(catalog.TryGetDefinition(new ContentId("dragonite_ore"), out _));
            Assert.IsTrue(catalog.TryGetDefinition(new ContentId("oak_logs"), out _));
            Assert.IsTrue(catalog.TryGetDefinition(new ContentId("iron_sword"), out var sword));
            Assert.AreEqual(EquipmentSlot.Weapon, sword.AllowedEquipmentSlots[0]);
        }

        [Test]
        public void Authority_RejectsEquipmentWhenSkillRequirementIsNotMet()
        {
            var catalog = new ItemCatalog();
            var blade = new ContentId("test_blade");
            catalog.Register(new ItemDefinition(
                blade, "Test blade", ItemType.Weapon, false, 1, "",
                new[] { EquipmentSlot.Weapon }, false, null, SkillId.Attack, 20));
            var player = new PlayerState(Guid.NewGuid(), "Requirements");
            InventoryRules.AddItem(player.Inventory, catalog, blade, 1);
            var authority = new LocalGameAuthority(catalog);
            authority.RegisterPlayer(player);

            var decision = authority.Submit(new EquipInventoryItemRequest(Guid.NewGuid(), player.CharacterId, 0));

            Assert.IsFalse(decision.Accepted);
            Assert.AreEqual("requirement_not_met", decision.Code);
            Assert.IsNull(player.Equipment.GetOrNull(EquipmentSlot.Weapon));
        }
    }
}
