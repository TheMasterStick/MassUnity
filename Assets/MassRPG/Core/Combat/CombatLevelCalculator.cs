using System;
using MassRPG.Core.Skills;

namespace MassRPG.Core.Combat
{
    public static class CombatLevelCalculator
    {
        /// <summary>
        /// Parity implementation of the browser prototype's current combat-level formula.
        /// Combat skills, rather than a generic character level, determine combat level.
        /// </summary>
        public static int Calculate(SkillSet skills)
        {
            if (skills == null) throw new ArgumentNullException(nameof(skills));

            var attack = skills.GetLevel(SkillId.Attack);
            var strength = skills.GetLevel(SkillId.Strength);
            var defence = skills.GetLevel(SkillId.Defence);
            var hitpoints = skills.GetLevel(SkillId.Hitpoints);
            var ranged = skills.GetLevel(SkillId.Ranged);
            var magic = skills.GetLevel(SkillId.Magic);

            var baseLevel = 0.25 * (defence + hitpoints);
            var melee = 0.325 * (attack + strength);
            var range = 0.325 * Math.Floor(ranged * 1.5);
            var mage = 0.325 * Math.Floor(magic * 1.5);

            return (int)Math.Floor(baseLevel + Math.Max(melee, Math.Max(range, mage)));
        }
    }
}
