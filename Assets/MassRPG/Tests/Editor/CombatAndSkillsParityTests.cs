using System;
using MassRPG.Core.Characters;
using MassRPG.Core.Combat;
using MassRPG.Core.Skills;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class CombatAndSkillsParityTests
    {
        [Test]
        public void BrowserXpCurve_RemainsExactThroughLevel99()
        {
            Assert.AreEqual(0L, SkillProgression.XpForLevel(1));
            Assert.AreEqual(83L, SkillProgression.XpForLevel(2));
            Assert.AreEqual(13034431L, SkillProgression.XpForLevel(99));
            Assert.AreEqual(99, SkillProgression.LevelForXp(13034431L));
        }

        [Test]
        public void CanonicalSkillCeiling_Is300()
        {
            Assert.AreEqual(300, SkillProgression.MaxLevel);
            Assert.Greater(SkillProgression.XpForLevel(300), SkillProgression.XpForLevel(99));
            Assert.AreEqual(300, SkillProgression.LevelForXp(SkillProgression.XpForLevel(300)));
        }

        [Test]
        public void FreshPlayer_CombatLevelMatchesBrowserReference()
        {
            var player = new PlayerState(Guid.NewGuid(), "Test");
            Assert.AreEqual(3, player.CombatLevel);
            Assert.AreEqual(10, player.MaxHitpoints);
        }

        [Test]
        public void CombatMath_PortsBrowserFormula()
        {
            Assert.AreEqual(1, CombatMath.MaxHitMelee(1, 0));
            Assert.AreEqual(27, CombatMath.MaxHitMelee(99, 100));
            Assert.AreEqual(61, CombatMath.MaxHitMagic(99));
            Assert.AreEqual(576, CombatMath.AttackRoll(1, 0));
            Assert.AreEqual(576, CombatMath.DefenceRoll(1, 0));
            Assert.AreEqual(576.0 / (2.0 * 577.0), CombatMath.HitChance(576, 576), 0.0000001);
        }
    }
}
