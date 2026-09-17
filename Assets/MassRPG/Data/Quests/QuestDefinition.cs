using System;
using System.Collections.Generic;
using MassRPG.Core.Content;
using MassRPG.Core.Skills;
using MassRPG.Core.World;

namespace MassRPG.Data.Quests
{
    public enum QuestObjectiveKind
    {
        KillCreature,
        GatherItem,
        TalkToNpc,
        ReachLocation,
        UseObject,
        Custom
    }

    public sealed class QuestObjectiveDefinition
    {
        public QuestObjectiveDefinition(
            string id,
            QuestObjectiveKind kind,
            int requiredCount = 1,
            ContentId? targetContentId = null,
            GridLocation? targetLocation = null,
            int locationRadiusTiles = 0)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Quest objective id cannot be empty.", nameof(id));
            if (requiredCount < 1) throw new ArgumentOutOfRangeException(nameof(requiredCount));
            if (locationRadiusTiles < 0) throw new ArgumentOutOfRangeException(nameof(locationRadiusTiles));
            if (kind == QuestObjectiveKind.ReachLocation && !targetLocation.HasValue)
                throw new ArgumentException("Reach-location objectives require a target location.", nameof(targetLocation));
            if (kind != QuestObjectiveKind.ReachLocation && !targetContentId.HasValue)
                throw new ArgumentException("Non-location quest objectives require a target content id.", nameof(targetContentId));
            Id = id;
            Kind = kind;
            RequiredCount = requiredCount;
            TargetContentId = targetContentId;
            TargetLocation = targetLocation;
            LocationRadiusTiles = locationRadiusTiles;
        }

        public string Id { get; }
        public QuestObjectiveKind Kind { get; }
        public int RequiredCount { get; }
        public ContentId? TargetContentId { get; }
        public GridLocation? TargetLocation { get; }
        public int LocationRadiusTiles { get; }
    }

    public readonly struct QuestSkillRequirement
    {
        public QuestSkillRequirement(SkillId skill, int level)
        {
            var maximumLevel = SkillProgression.MaxLevelFor(skill);
            if (level < 1 || level > maximumLevel)
                throw new ArgumentOutOfRangeException(
                    nameof(level),
                    $"{skill} quest requirements must be between 1 and {maximumLevel}.");
            Skill = skill;
            Level = level;
        }

        public SkillId Skill { get; }
        public int Level { get; }
    }

    public readonly struct QuestItemReward
    {
        public QuestItemReward(ContentId itemId, int quantity)
        {
            if (itemId.IsEmpty) throw new ArgumentException("Quest reward item id cannot be empty.", nameof(itemId));
            if (quantity < 1) throw new ArgumentOutOfRangeException(nameof(quantity));
            ItemId = itemId;
            Quantity = quantity;
        }

        public ContentId ItemId { get; }
        public int Quantity { get; }
    }

    public readonly struct QuestXpReward
    {
        public QuestXpReward(SkillId skill, long xp)
        {
            if (xp < 1) throw new ArgumentOutOfRangeException(nameof(xp));
            Skill = skill;
            Xp = xp;
        }

        public SkillId Skill { get; }
        public long Xp { get; }
    }

    public sealed class QuestDefinition
    {
        private readonly List<QuestObjectiveDefinition> _objectives;
        private readonly List<ContentId> _prerequisiteQuestIds;
        private readonly List<QuestSkillRequirement> _skillRequirements;
        private readonly List<QuestItemReward> _itemRewards;
        private readonly List<QuestXpReward> _xpRewards;

        public QuestDefinition(
            ContentId id,
            string displayName,
            IEnumerable<QuestObjectiveDefinition> objectives,
            IEnumerable<ContentId> prerequisiteQuestIds = null,
            IEnumerable<QuestSkillRequirement> skillRequirements = null,
            IEnumerable<QuestItemReward> itemRewards = null,
            IEnumerable<QuestXpReward> xpRewards = null,
            bool repeatable = false)
        {
            if (id.IsEmpty) throw new ArgumentException("Quest id cannot be empty.", nameof(id));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Quest display name cannot be empty.", nameof(displayName));
            if (objectives == null) throw new ArgumentNullException(nameof(objectives));
            Id = id;
            DisplayName = displayName;
            Repeatable = repeatable;
            _objectives = new List<QuestObjectiveDefinition>(objectives);
            if (_objectives.Count == 0) throw new ArgumentException("Quest requires at least one objective.", nameof(objectives));
            _prerequisiteQuestIds = prerequisiteQuestIds == null ? new List<ContentId>() : new List<ContentId>(prerequisiteQuestIds);
            _skillRequirements = skillRequirements == null ? new List<QuestSkillRequirement>() : new List<QuestSkillRequirement>(skillRequirements);
            _itemRewards = itemRewards == null ? new List<QuestItemReward>() : new List<QuestItemReward>(itemRewards);
            _xpRewards = xpRewards == null ? new List<QuestXpReward>() : new List<QuestXpReward>(xpRewards);
            ValidateObjectiveIds();
        }

        public ContentId Id { get; }
        public string DisplayName { get; }
        public bool Repeatable { get; }
        public IReadOnlyList<QuestObjectiveDefinition> Objectives => _objectives;
        public IReadOnlyList<ContentId> PrerequisiteQuestIds => _prerequisiteQuestIds;
        public IReadOnlyList<QuestSkillRequirement> SkillRequirements => _skillRequirements;
        public IReadOnlyList<QuestItemReward> ItemRewards => _itemRewards;
        public IReadOnlyList<QuestXpReward> XpRewards => _xpRewards;

        private void ValidateObjectiveIds()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < _objectives.Count; i++)
            {
                var objective = _objectives[i] ?? throw new InvalidOperationException("Quest cannot contain a null objective.");
                if (!ids.Add(objective.Id)) throw new InvalidOperationException("Duplicate quest objective id '" + objective.Id + "'.");
            }
        }
    }

    public interface IQuestDefinitionSource
    {
        bool TryGet(ContentId id, out QuestDefinition definition);
    }

    public sealed class QuestCatalog : IQuestDefinitionSource
    {
        private readonly Dictionary<ContentId, QuestDefinition> _definitions = new Dictionary<ContentId, QuestDefinition>();

        public IEnumerable<QuestDefinition> All => _definitions.Values;

        public void Register(QuestDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (_definitions.ContainsKey(definition.Id)) throw new InvalidOperationException("Duplicate quest id '" + definition.Id + "'.");
            _definitions.Add(definition.Id, definition);
        }

        public bool TryGet(ContentId id, out QuestDefinition definition) => _definitions.TryGetValue(id, out definition);
    }
}
