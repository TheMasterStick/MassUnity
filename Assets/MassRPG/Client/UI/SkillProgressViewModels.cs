using System;
using System.Collections.Generic;
using MassRPG.Core.Characters;
using MassRPG.Core.Skills;
using MassRPG.Data.Skills;

namespace MassRPG.Client.UI
{
    public readonly struct SkillCardViewData
    {
        public SkillCardViewData(SkillId skill, SkillProgressSnapshot progress)
        {
            Skill = skill;
            Progress = progress;
        }

        public SkillId Skill { get; }
        public SkillProgressSnapshot Progress { get; }
    }

    public sealed class SkillsPanelViewData
    {
        public SkillsPanelViewData(int combatLevel, IReadOnlyList<SkillCardViewData> skills)
        {
            CombatLevel = combatLevel;
            Skills = skills ?? Array.Empty<SkillCardViewData>();
        }

        public int CombatLevel { get; }
        public IReadOnlyList<SkillCardViewData> Skills { get; }
    }

    public readonly struct SkillUnlockViewData
    {
        public SkillUnlockViewData(SkillUnlockDefinition definition, bool unlocked)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Unlocked = unlocked;
        }

        public SkillUnlockDefinition Definition { get; }
        public bool Unlocked { get; }
    }

    public sealed class SkillBookViewData
    {
        public SkillBookViewData(
            SkillId skill,
            SkillProgressSnapshot progress,
            IReadOnlyList<string> categories,
            string activeCategory,
            IReadOnlyList<SkillUnlockViewData> unlocks)
        {
            Skill = skill;
            Progress = progress;
            Categories = categories ?? Array.Empty<string>();
            ActiveCategory = activeCategory ?? "All";
            Unlocks = unlocks ?? Array.Empty<SkillUnlockViewData>();
        }

        public SkillId Skill { get; }
        public SkillProgressSnapshot Progress { get; }
        public IReadOnlyList<string> Categories { get; }
        public string ActiveCategory { get; }
        public IReadOnlyList<SkillUnlockViewData> Unlocks { get; }
    }

    /// <summary>
    /// Read-only bridge from authoritative player/data state into Unity skill UI. It deliberately
    /// owns no XP curve, level-cap or unlock rules of its own.
    /// </summary>
    public static class SkillProgressViewModels
    {
        public static SkillsPanelViewData BuildSkillsPanel(PlayerState player)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));

            var cards = new List<SkillCardViewData>();
            foreach (SkillId skill in Enum.GetValues(typeof(SkillId)))
                cards.Add(new SkillCardViewData(skill, SkillProgressSnapshot.FromXp(skill, player.Skills.GetXp(skill))));

            return new SkillsPanelViewData(player.CombatLevel, cards.ToArray());
        }

        public static SkillBookViewData BuildSkillBook(
            PlayerState player,
            SkillUnlockCatalog unlockCatalog,
            SkillId skill,
            string activeCategory = "All")
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (unlockCatalog == null) throw new ArgumentNullException(nameof(unlockCatalog));

            var progress = SkillProgressSnapshot.FromXp(skill, player.Skills.GetXp(skill));
            var definitions = unlockCatalog.ForSkill(skill);
            var categories = new List<string> { "All" };
            var seenCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "All" };
            for (var i = 0; i < definitions.Count; i++)
            {
                var category = definitions[i].Category;
                if (!string.IsNullOrWhiteSpace(category) && seenCategories.Add(category))
                    categories.Add(category);
            }

            var selectedCategory = string.IsNullOrWhiteSpace(activeCategory) ? "All" : activeCategory;
            if (!seenCategories.Contains(selectedCategory)) selectedCategory = "All";

            var entries = new List<SkillUnlockViewData>();
            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (!string.Equals(selectedCategory, "All", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(definition.Category, selectedCategory, StringComparison.OrdinalIgnoreCase))
                    continue;

                entries.Add(new SkillUnlockViewData(
                    definition,
                    progress.Level >= definition.LevelRequired));
            }

            return new SkillBookViewData(
                skill,
                progress,
                categories.ToArray(),
                selectedCategory,
                entries.ToArray());
        }
    }
}
