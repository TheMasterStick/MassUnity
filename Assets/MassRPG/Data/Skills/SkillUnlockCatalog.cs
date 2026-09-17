using System;
using System.Collections.Generic;
using MassRPG.Core.Content;
using MassRPG.Core.Skills;

namespace MassRPG.Data.Skills
{
    public enum SkillUnlockKind
    {
        Recipe,
        ItemRequirement,
        Gathering,
        Construction,
        Other
    }

    /// <summary>
    /// Data-facing entry for a skillbook/unlock ledger. It describes an unlock but does not decide
    /// whether the player currently meets it; presentation can compare LevelRequired with the
    /// authoritative skill level.
    /// </summary>
    public sealed class SkillUnlockDefinition
    {
        public SkillUnlockDefinition(
            SkillId skill,
            int levelRequired,
            SkillUnlockKind kind,
            string category,
            string displayName,
            string detail,
            ContentId? sourceContentId = null)
        {
            var maximumLevel = SkillProgression.MaxLevelFor(skill);
            if (levelRequired < 1 || levelRequired > maximumLevel)
                throw new ArgumentOutOfRangeException(
                    nameof(levelRequired),
                    $"{skill} unlock levels must be between 1 and {maximumLevel}.");

            Skill = skill;
            LevelRequired = levelRequired;
            Kind = kind;
            Category = category ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Detail = detail ?? string.Empty;
            SourceContentId = sourceContentId;
        }

        public SkillId Skill { get; }
        public int LevelRequired { get; }
        public SkillUnlockKind Kind { get; }
        public string Category { get; }
        public string DisplayName { get; }
        public string Detail { get; }
        public ContentId? SourceContentId { get; }
    }

    public sealed class SkillUnlockCatalog
    {
        private readonly List<SkillUnlockDefinition> _entries = new List<SkillUnlockDefinition>();

        public IReadOnlyList<SkillUnlockDefinition> All => _entries;
        public int Count => _entries.Count;

        public void Register(SkillUnlockDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            _entries.Add(definition);
        }

        public IReadOnlyList<SkillUnlockDefinition> ForSkill(SkillId skill)
        {
            var result = new List<SkillUnlockDefinition>();
            for (var i = 0; i < _entries.Count; i++)
                if (_entries[i].Skill == skill) result.Add(_entries[i]);

            result.Sort((left, right) =>
            {
                var level = left.LevelRequired.CompareTo(right.LevelRequired);
                if (level != 0) return level;
                var category = string.Compare(left.Category, right.Category, StringComparison.OrdinalIgnoreCase);
                if (category != 0) return category;
                return string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
            });
            return result.ToArray();
        }
    }
}
