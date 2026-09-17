using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using MassRPG.Core.Combat;

namespace MassRPG.Core.Skills
{
    /// <summary>
    /// Immutable read-side snapshot for the skills overview. Presentation code can render combat
    /// level and per-skill XP progress without duplicating authoritative progression rules or
    /// retaining a mutable SkillSet reference.
    /// </summary>
    public sealed class SkillOverviewSnapshot
    {
        private readonly IReadOnlyDictionary<SkillId, SkillProgressSnapshot> _bySkill;

        private SkillOverviewSnapshot(
            int combatLevel,
            IReadOnlyList<SkillProgressSnapshot> skills,
            IReadOnlyDictionary<SkillId, SkillProgressSnapshot> bySkill)
        {
            CombatLevel = combatLevel;
            Skills = skills;
            _bySkill = bySkill;
        }

        public int CombatLevel { get; }
        public IReadOnlyList<SkillProgressSnapshot> Skills { get; }

        public SkillProgressSnapshot Get(SkillId skill)
        {
            if (!_bySkill.TryGetValue(skill, out var snapshot))
                throw new ArgumentOutOfRangeException(nameof(skill), skill, "Unknown skill.");
            return snapshot;
        }

        public static SkillOverviewSnapshot Capture(SkillSet skills)
        {
            if (skills == null) throw new ArgumentNullException(nameof(skills));

            var ordered = new List<SkillProgressSnapshot>();
            var bySkill = new Dictionary<SkillId, SkillProgressSnapshot>();
            foreach (SkillId skill in Enum.GetValues(typeof(SkillId)))
            {
                var progress = SkillProgressSnapshot.FromXp(skill, skills.GetXp(skill));
                ordered.Add(progress);
                bySkill.Add(skill, progress);
            }

            return new SkillOverviewSnapshot(
                CombatLevelCalculator.Calculate(skills),
                new ReadOnlyCollection<SkillProgressSnapshot>(ordered),
                new ReadOnlyDictionary<SkillId, SkillProgressSnapshot>(bySkill));
        }
    }
}
