using System;
using MassRPG.Core.Characters;
using MassRPG.Core.Combat;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Core.Skills;
using MassRPG.Core.World;
using MassRPG.Data.Effects;
using MassRPG.Data.Items;
using MassRPG.Data.World;
using MassRPG.Data.World.Semantics;
using MassRPG.Server.Combat;
using MassRPG.Server.Effects;
using MassRPG.Server.Items;
using MassRPG.Server.Pvp;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class PotionStatusEffectTests
    {
        private static readonly ContentId PotionId = new ContentId("test_attack_potion");
        private static readonly ContentId EffectId = new ContentId("effect.test_attack_boost");
        private static readonly ContentId Grass = new ContentId("terrain.grass");

        [Test]
        public void DrinkingPotionConsumesOneRefreshesSameEffectAndNeverChangesPermanentXp()
        {
            var items = CreateItems();
            var effects = new PotionEffectCatalog();
            effects.Register(CreateAttackEffect(1000));
            var status = new StatusEffectService();
            var potions = new PotionConsumptionService(items, effects, status);
            var player = new PlayerState(Guid.NewGuid(), "Alchemist");
            InventoryRules.AddItem(player.Inventory, items, PotionId, 2);
            var slot = FindSlot(player.Inventory, PotionId);
            var baseAttack = player.Skills.GetLevel(SkillId.Attack);
            var baseXp = player.Skills.GetXp(SkillId.Attack);

            var first = potions.Drink(player, slot, 1000);

            Assert.IsTrue(first.Success);
            Assert.AreEqual(2000, first.ExpiresAtUnixMilliseconds);
            Assert.AreEqual(1, player.Inventory.CountItem(PotionId));
            Assert.AreEqual(baseAttack + 3, status.GetEffectiveLevel(player, SkillId.Attack, 1500));
            Assert.AreEqual(baseAttack + 4, status.GetEffectiveLevel(player, SkillId.Strength, 1500));
            Assert.AreEqual(baseXp, player.Skills.GetXp(SkillId.Attack));
            Assert.IsTrue(status.HasFlag(player, StatusEffectFlags.PoisonImmunity, 1500));

            var second = potions.Drink(player, FindSlot(player.Inventory, PotionId), 1500);

            Assert.IsTrue(second.Success);
            Assert.AreEqual(2500, second.ExpiresAtUnixMilliseconds);
            Assert.AreEqual(0, player.Inventory.CountItem(PotionId));
            Assert.AreEqual(1, status.GetActiveEffects(player, 1600).Count);
            Assert.AreEqual(baseAttack + 3, status.GetEffectiveLevel(player, SkillId.Attack, 2400));
            Assert.AreEqual(baseAttack, status.GetEffectiveLevel(player, SkillId.Attack, 2500));
            Assert.IsFalse(status.HasFlag(player, StatusEffectFlags.PoisonImmunity, 2500));
        }

        [Test]
        public void PotionWithoutAuthoredEffectIsRejectedWithoutConsumption()
        {
            var items = CreateItems();
            var status = new StatusEffectService();
            var service = new PotionConsumptionService(items, new PotionEffectCatalog(), status);
            var player = new PlayerState(Guid.NewGuid(), "Tester");
            InventoryRules.AddItem(player.Inventory, items, PotionId, 1);

            var result = service.Drink(player, FindSlot(player.Inventory, PotionId), 1000);

            Assert.IsFalse(result.Success);
            Assert.AreEqual("potion_effect_missing", result.Code);
            Assert.AreEqual(1, player.Inventory.CountItem(PotionId));
        }

        [Test]
        public void PvpCombatUsesTemporaryEffectiveAttackLevelWithoutMutatingBaseSkill()
        {
            var world = new AuthoredWorldPageStore(Grass, 32);
            world.GetOrCreatePage(Loc(0, 0));
            var items = MigrationSeedItemCatalog.Create();
            var status = new StatusEffectService();
            var pvp = new PvpService(new WorldSemanticCatalog());
            var attacker = new PlayerState(Guid.NewGuid(), "Boosted") { Location = Loc(10, 10) };
            var defender = new PlayerState(Guid.NewGuid(), "Defender") { Location = Loc(11, 10) };
            pvp.SetOptIn(attacker.CharacterId, true);
            pvp.SetOptIn(defender.CharacterId, true);

            var effect = new PotionEffectDefinition(
                new ContentId("virtual_potion"),
                new ContentId("effect.large_attack_test"),
                5000,
                new[] { new SkillLevelModifier(SkillId.Attack, 100) });
            status.Apply(attacker, effect, 1000);

            var profiles = new DataDrivenPlayerAttackProfileSource(items);
            var combat = new PvpCombatService(pvp, profiles, world, world, skillLevels: status);
            var result = combat.TryAttack(attacker, defender, 1001, Sequence(1.0, 0.0));

            var effectiveAttack = attacker.Skills.GetLevel(SkillId.Attack) + 100;
            var expected = CombatMath.HitChance(
                CombatMath.AttackRoll(effectiveAttack, 0),
                CombatMath.DefenceRoll(defender.Skills.GetLevel(SkillId.Defence), 0));

            Assert.AreEqual(expected, result.HitChance, 0.0000001);
            Assert.AreEqual(1, attacker.Skills.GetLevel(SkillId.Attack));
        }

        private static PotionEffectDefinition CreateAttackEffect(long durationMilliseconds)
            => new PotionEffectDefinition(
                PotionId,
                EffectId,
                durationMilliseconds,
                new[]
                {
                    new SkillLevelModifier(SkillId.Attack, 3),
                    new SkillLevelModifier(SkillId.Strength, 4)
                },
                StatusEffectFlags.PoisonImmunity);

        private static ItemCatalog CreateItems()
        {
            var catalog = new ItemCatalog();
            catalog.Register(new ItemDefinition(PotionId, "Test attack potion", ItemType.Potion, true, 10));
            catalog.Register(new ItemDefinition(new ContentId("bread"), "Bread", ItemType.Food, true, 5, healAmount: 5));
            return catalog;
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

        private static Func<double> Sequence(params double[] values)
        {
            var index = 0;
            return () => values[Math.Min(index++, values.Length - 1)];
        }

        private static GridLocation Loc(int x, int y)
            => new GridLocation(new GridCoord(x, y), WorldConstants.SurfacePlane, 0);
    }
}
