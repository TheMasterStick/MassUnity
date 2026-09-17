using System;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Skills;
using MassRPG.Data.Effects;
using MassRPG.Server.Effects;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class EffectiveSkillLevelTests
    {
        [Test]
        public void CombatSkillCanTemporarilyExceedTrainableLevelOneHundred()
        {
            var player = new PlayerState(Guid.NewGuid(), "Boosted Fighter");
            player.Skills.SetXp(SkillId.Attack, SkillProgression.XpForLevel(SkillId.Attack, 100));
            var beforeXp = player.Skills.GetXp(SkillId.Attack);
            var effects = new StatusEffectService();
            effects.Apply(
                player,
                Effect("effect.attack_boost", SkillId.Attack, 8),
                1000);

            Assert.AreEqual(100, player.Skills.GetLevel(SkillId.Attack));
            Assert.AreEqual(108, effects.GetEffectiveLevel(player, SkillId.Attack, 1001));
            Assert.AreEqual(beforeXp, player.Skills.GetXp(SkillId.Attack));
        }

        [Test]
        public void NonCombatSkillCanTemporarilyExceedTrainableLevelThreeHundred()
        {
            var player = new PlayerState(Guid.NewGuid(), "Boosted Miner");
            player.Skills.SetXp(SkillId.Mining, SkillProgression.XpForLevel(SkillId.Mining, 300));
            var effects = new StatusEffectService();
            effects.Apply(
                player,
                Effect("effect.mining_boost", SkillId.Mining, 12),
                1000);

            Assert.AreEqual(300, player.Skills.GetLevel(SkillId.Mining));
            Assert.AreEqual(312, effects.GetEffectiveLevel(player, SkillId.Mining, 1001));
        }

        [Test]
        public void TemporaryDebuffCannotReduceEffectiveLevelBelowOne()
        {
            var player = new PlayerState(Guid.NewGuid(), "Debuffed Mage");
            var effects = new StatusEffectService();
            effects.Apply(
                player,
                Effect("effect.magic_debuff", SkillId.Magic, -50),
                1000);

            Assert.AreEqual(1, effects.GetEffectiveLevel(player, SkillId.Magic, 1001));
        }

        [Test]
        public void ExpiredBoostFallsBackToTrainableLevel()
        {
            var player = new PlayerState(Guid.NewGuid(), "Expired Boost");
            player.Skills.SetXp(SkillId.Ranged, SkillProgression.XpForLevel(SkillId.Ranged, 100));
            var effects = new StatusEffectService();
            effects.Apply(
                player,
                new PotionEffectDefinition(
                    new ContentId("potion.ranged_boost"),
                    new ContentId("effect.ranged_boost"),
                    1000,
                    new[] { new SkillLevelModifier(SkillId.Ranged, 5) }),
                1000);

            Assert.AreEqual(105, effects.GetEffectiveLevel(player, SkillId.Ranged, 1500));
            Assert.AreEqual(100, effects.GetEffectiveLevel(player, SkillId.Ranged, 2000));
        }

        private static PotionEffectDefinition Effect(string effectId, SkillId skill, int flatLevels)
            => new PotionEffectDefinition(
                new ContentId("potion." + effectId.Replace("effect.", string.Empty)),
                new ContentId(effectId),
                5000,
                new[] { new SkillLevelModifier(skill, flatLevels) });
    }
}
