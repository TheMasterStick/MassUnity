using System;
using System.Collections.Generic;

namespace MassRPG.Core.Skills
{
    public sealed class SkillSet
    {
        private readonly Dictionary<SkillId, long> _xp = new Dictionary<SkillId, long>();

        public SkillSet()
        {
            foreach (SkillId skill in Enum.GetValues(typeof(SkillId)))
            {
                var startingLevel = skill == SkillId.Hitpoints ? 10 : 1;
                _xp[skill] = SkillProgression.XpForLevel(skill, startingLevel);
            }
        }

        public long GetXp(SkillId skill) => _xp[skill];
        public int GetLevel(SkillId skill) => SkillProgression.LevelForXp(skill, GetXp(skill));
        public int GetMaxLevel(SkillId skill) => SkillProgression.MaxLevelFor(skill);

        public void SetXp(SkillId skill, long xp)
        {
            _xp[skill] = Math.Max(0, xp);
        }

        public long AddXp(SkillId skill, long amount)
        {
            if (amount <= 0) return _xp[skill];
            _xp[skill] = checked(_xp[skill] + amount);
            return _xp[skill];
        }
    }
}
