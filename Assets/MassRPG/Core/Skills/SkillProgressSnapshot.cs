using System;

namespace MassRPG.Core.Skills
{
    /// <summary>
    /// Presentation-ready progression facts derived from authoritative XP rules. UI code should use
    /// this snapshot rather than duplicating level thresholds or assuming a universal skill cap.
    /// </summary>
    public readonly struct SkillProgressSnapshot
    {
        private SkillProgressSnapshot(
            SkillId skill,
            long totalXp,
            int level,
            int maximumLevel,
            long levelStartXp,
            long nextLevelXp,
            long xpRemaining,
            double progress,
            bool isMaximumLevel)
        {
            Skill = skill;
            TotalXp = totalXp;
            Level = level;
            MaximumLevel = maximumLevel;
            LevelStartXp = levelStartXp;
            NextLevelXp = nextLevelXp;
            XpRemaining = xpRemaining;
            Progress = progress;
            IsMaximumLevel = isMaximumLevel;
        }

        public SkillId Skill { get; }
        public long TotalXp { get; }
        public int Level { get; }
        public int MaximumLevel { get; }
        public long LevelStartXp { get; }
        public long NextLevelXp { get; }
        public long XpRemaining { get; }
        public double Progress { get; }
        public bool IsMaximumLevel { get; }

        public static SkillProgressSnapshot FromXp(SkillId skill, long xp)
        {
            var totalXp = Math.Max(0, xp);
            var maximumLevel = SkillProgression.MaxLevelFor(skill);
            var level = SkillProgression.LevelForXp(skill, totalXp);
            var levelStart = SkillProgression.XpForLevel(skill, level);
            if (level >= maximumLevel)
            {
                return new SkillProgressSnapshot(
                    skill,
                    totalXp,
                    level,
                    maximumLevel,
                    levelStart,
                    totalXp,
                    0,
                    1.0,
                    true);
            }

            var next = SkillProgression.XpForLevel(skill, level + 1);
            var remaining = Math.Max(0, next - totalXp);
            var progress = SkillProgression.ProgressToNextLevel(skill, totalXp);
            if (progress < 0.0) progress = 0.0;
            else if (progress > 1.0) progress = 1.0;

            return new SkillProgressSnapshot(
                skill,
                totalXp,
                level,
                maximumLevel,
                levelStart,
                next,
                remaining,
                progress,
                false);
        }
    }
}
