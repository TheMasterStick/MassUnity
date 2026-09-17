using System;
using MassRPG.Core.Characters;
using MassRPG.Core.Inventory;
using MassRPG.Core.Skills;
using MassRPG.Data.Effects;
using MassRPG.Data.Items;
using MassRPG.Server.Effects;
using MassRPG.Server.Items;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class MigrationSeedPotionEffectCatalogTests
    {
        [Test]
        public void AttackAndStrengthPotionsPreserveBrowserPlusThreeWithoutHardCodingDuration()
        {
            var effects = MigrationSeedPotionEffectCatalog.Create(12_345, 54_321);

            Assert.IsTrue(effects.TryGetByPotion(MigrationSeedPotionEffectCatalog.AttackPotionId, out var attack));
            Assert.AreEqual(12_345, attack.DurationMilliseconds);
            Assert.AreEqual(1, attack.SkillModifiers.Count);
            Assert.AreEqual(SkillId.Attack, attack.SkillModifiers[0].Skill);
            Assert.AreEqual(3, attack.SkillModifiers[0].FlatLevels);

            Assert.IsTrue(effects.TryGetByPotion(MigrationSeedPotionEffectCatalog.StrengthPotionId, out var strength));
            Assert.AreEqual(12_345, strength.DurationMilliseconds);
            Assert.AreEqual(SkillId.Strength, strength.SkillModifiers[0].Skill);
            Assert.AreEqual(3, strength.SkillModifiers[0].FlatLevels);
        }

        [Test]
        public void AntipoisonProvidesImmunityFlagWithCallerSelectedDuration()
        {
            var effects = MigrationSeedPotionEffectCatalog.Create(1_000, 9_000);

            Assert.IsTrue(effects.TryGetByPotion(MigrationSeedPotionEffectCatalog.AntipoisonId, out var antipoison));
            Assert.AreEqual(9_000, antipoison.DurationMilliseconds);
            Assert.AreEqual(StatusEffectFlags.PoisonImmunity, antipoison.Flags);
            Assert.AreEqual(0, antipoison.SkillModifiers.Count);
        }

        [Test]
        public void MigrationAttackPotionCanBeConsumedThroughRuntimeEffectService()
        {
            var items = MigrationSeedItemCatalog.Create();
            var effects = MigrationSeedPotionEffectCatalog.Create(5_000, 5_000);
            var status = new StatusEffectService();
            var potions = new PotionConsumptionService(items, effects, status);
            var player = new PlayerState(Guid.NewGuid(), "Legacy Potion");
            InventoryRules.AddItem(player.Inventory, items, MigrationSeedPotionEffectCatalog.AttackPotionId, 1);
            var slot = FindSlot(player.Inventory, MigrationSeedPotionEffectCatalog.AttackPotionId);
            var baseAttack = player.Skills.GetLevel(SkillId.Attack);

            var result = potions.Drink(player, slot, 1_000);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(0, player.Inventory.CountItem(MigrationSeedPotionEffectCatalog.AttackPotionId));
            Assert.AreEqual(baseAttack + 3, status.GetEffectiveLevel(player, SkillId.Attack, 5_999));
            Assert.AreEqual(baseAttack, status.GetEffectiveLevel(player, SkillId.Attack, 6_000));
        }

        [Test]
        public void InvalidDurationsAreRejectedInsteadOfInventingBalance()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MigrationSeedPotionEffectCatalog.Create(0, 5_000));
            Assert.Throws<ArgumentOutOfRangeException>(() => MigrationSeedPotionEffectCatalog.Create(5_000, 0));
        }

        private static int FindSlot(InventoryState inventory, MassRPG.Core.Content.ContentId itemId)
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
