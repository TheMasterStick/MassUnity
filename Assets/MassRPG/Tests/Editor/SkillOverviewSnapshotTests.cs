using System;
using MassRPG.Core.Combat;
using MassRPG.Core.Skills;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class SkillOverviewSnapshotTests
    {
        [Test]
        public void CaptureIncludesEverySkillExactlyOnceInEnumOrder()
        {
            var skills = new SkillSet();
            var snapshot = SkillOverviewSnapshot.Capture(skills);
            var values = (SkillId[])Enum.GetValues(typeof(SkillId));

            Assert.AreEqual(values.Length, snapshot.Skills.Count);
            for (var i = 0; i < values.Length; i++)
            {
                Assert.AreEqual(values[i], snapshot.Skills[i].Skill);
                Assert.AreEqual(values[i], snapshot.Get(values[i]).Skill);
            }
        }

        [Test]
        public void CaptureUsesAuthoritativeCombatLevelAndPerSkillCaps()
        {
            var skills = new SkillSet();
            skills.SetXp(SkillId.Attack, SkillProgression.XpForLevel(SkillId.Attack, 75));
            skills.SetXp(SkillId.Strength, SkillProgression.XpForLevel(SkillId.Strength, 80));
            skills.SetXp(SkillId.Mining, SkillProgression.XpForLevel(SkillId.Mining, 120));

            var snapshot = SkillOverviewSnapshot.Capture(skills);

            Assert.AreEqual(CombatLevelCalculator.Calculate(skills), snapshot.CombatLevel);
            Assert.AreEqual(75, snapshot.Get(SkillId.Attack).Level);
            Assert.AreEqual(100, snapshot.Get(SkillId.Attack).MaximumLevel);
            Assert.AreEqual(120, snapshot.Get(SkillId.Mining).Level);
            Assert.AreEqual(300, snapshot.Get(SkillId.Mining).MaximumLevel);
        }

        [Test]
        public void CaptureDoesNotChangeWhenLiveSkillsChangeLater()
        {
            var skills = new SkillSet();
            var before = SkillOverviewSnapshot.Capture(skills);
            var originalMiningXp = before.Get(SkillId.Mining).TotalXp;

            skills.SetXp(SkillId.Mining, SkillProgression.XpForLevel(SkillId.Mining, 50));
            var after = SkillOverviewSnapshot.Capture(skills);

            Assert.AreEqual(originalMiningXp, before.Get(SkillId.Mining).TotalXp);
            Assert.AreEqual(1, before.Get(SkillId.Mining).Level);
            Assert.AreEqual(50, after.Get(SkillId.Mining).Level);
            Assert.Greater(after.Get(SkillId.Mining).TotalXp, before.Get(SkillId.Mining).TotalXp);
        }

        [Test]
        public void CaptureRejectsNullSkillSet()
        {
            Assert.Throws<ArgumentNullException>(() => SkillOverviewSnapshot.Capture(null));
        }
    }
}
