using System;
using MassRPG.Core.Characters;

namespace MassRPG.Core.Skills
{
    /// <summary>
    /// Resolves the level gameplay should use at a specific server time. Permanent progression stays
    /// in SkillSet XP; temporary boosts/debuffs live behind this interface so drinking a potion never
    /// mutates earned XP or permanently rewrites a skill level.
    /// </summary>
    public interface IEffectiveSkillLevelSource
    {
        int GetEffectiveLevel(PlayerState player, SkillId skill, long nowUnixMilliseconds);
    }

    public sealed class BaseEffectiveSkillLevelSource : IEffectiveSkillLevelSource
    {
        public static readonly BaseEffectiveSkillLevelSource Instance = new BaseEffectiveSkillLevelSource();

        private BaseEffectiveSkillLevelSource()
        {
        }

        public int GetEffectiveLevel(PlayerState player, SkillId skill, long nowUnixMilliseconds)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            return player.Skills.GetLevel(skill);
        }
    }
}
