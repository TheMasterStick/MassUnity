using System;

namespace MassRPG.Core.Skills
{
    /// <summary>
    /// RuneScape-style reference XP curve with MassRPG's skill-specific ceilings.
    /// Combat skills cap at 100; non-combat skills cap at 300. Levels 1-99 intentionally
    /// match the browser prototype exactly. Values above 99 are a migration baseline and can
    /// be rebalanced later without changing the state model.
    /// </summary>
    public static class SkillProgression
    {
        public const int CombatSkillMaxLevel = 100;
        public const int NonCombatSkillMaxLevel = 300;

        /// <summary>
        /// Absolute ceiling used to size the shared XP table. Prefer MaxLevelFor(skill) for
        /// gameplay validation and UI logic.
        /// </summary>
        public const int MaxLevel = NonCombatSkillMaxLevel;

        private static readonly long[] XpTable = BuildXpTable();

        public static bool IsCombatSkill(SkillId skill)
        {
            switch (skill)
            {
                case SkillId.Hitpoints:
                case SkillId.Attack:
                case SkillId.Strength:
                case SkillId.Defence:
                case SkillId.Ranged:
                case SkillId.Magic:
                    return true;
                default:
                    return false;
            }
        }

        public static int MaxLevelFor(SkillId skill)
            => IsCombatSkill(skill) ? CombatSkillMaxLevel : NonCombatSkillMaxLevel;

        private static long[] BuildXpTable()
        {
            var table = new long[MaxLevel + 1];
            long points = 0;
            table[1] = 0;

            for (var level = 1; level < MaxLevel; level++)
            {
                var term = (long)Math.Floor(level + 300.0 * Math.Pow(2.0, level / 7.0));
                points = checked(points + term);
                table[level + 1] = points / 4;
            }

            return table;
        }

        /// <summary>
        /// Returns the raw curve threshold up to the absolute level-300 ceiling. Use the
        /// skill-aware overload when resolving a particular skill.
        /// </summary>
        public static long XpForLevel(int level)
        {
            var clamped = Math.Max(1, Math.Min(MaxLevel, level));
            return XpTable[clamped];
        }

        public static long XpForLevel(SkillId skill, int level)
        {
            var clamped = Math.Max(1, Math.Min(MaxLevelFor(skill), level));
            return XpTable[clamped];
        }

        /// <summary>
        /// Resolves against the absolute curve. Prefer the skill-aware overload for gameplay state.
        /// </summary>
        public static int LevelForXp(long xp)
            => LevelForXpWithCap(xp, MaxLevel);

        public static int LevelForXp(SkillId skill, long xp)
            => LevelForXpWithCap(xp, MaxLevelFor(skill));

        public static double ProgressToNextLevel(long xp)
            => ProgressToNextLevelWithCap(xp, MaxLevel);

        public static double ProgressToNextLevel(SkillId skill, long xp)
            => ProgressToNextLevelWithCap(xp, MaxLevelFor(skill));

        private static int LevelForXpWithCap(long xp, int maxLevel)
        {
            if (xp <= 0) return 1;

            for (var level = maxLevel; level >= 1; level--)
            {
                if (xp >= XpTable[level]) return level;
            }

            return 1;
        }

        private static double ProgressToNextLevelWithCap(long xp, int maxLevel)
        {
            var level = LevelForXpWithCap(xp, maxLevel);
            if (level >= maxLevel) return 1.0;

            var current = XpTable[level];
            var next = XpTable[level + 1];
            return (double)(xp - current) / (next - current);
        }
    }
}
