using System;
using MassRPG.Core.Content;
using MassRPG.Core.Skills;

namespace MassRPG.Core.Resources
{
    public enum GatheringToolKind
    {
        None,
        Hatchet,
        Pickaxe,
        FishingNet,
        FishingRod,
        LobsterPot,
        Harpoon
    }

    public readonly struct GatheringToolRule
    {
        public GatheringToolRule(ContentId itemId, GatheringToolKind kind, int tier)
        {
            if (itemId.IsEmpty) throw new ArgumentException("Tool item id cannot be empty.", nameof(itemId));
            if (tier < 0) throw new ArgumentOutOfRangeException(nameof(tier));
            ItemId = itemId;
            Kind = kind;
            Tier = tier;
        }

        public ContentId ItemId { get; }
        public GatheringToolKind Kind { get; }
        public int Tier { get; }
    }

    public interface IGatheringToolSource
    {
        bool TryGetGatheringTool(ContentId itemId, out GatheringToolRule tool);
    }

    public sealed class ResourceGatheringRule
    {
        public ResourceGatheringRule(
            ContentId resourceId,
            SkillId skill,
            int requiredLevel,
            int experience,
            ContentId yieldItemId,
            int respawnSeconds,
            ResourceAvailabilityMode availabilityMode,
            GatheringToolKind requiredToolKind = GatheringToolKind.None,
            int minimumToolTier = 0,
            int minimumYield = 1,
            int maximumYield = 1)
        {
            ResourceId = resourceId;
            Skill = skill;
            RequiredLevel = requiredLevel;
            Experience = experience;
            YieldItemId = yieldItemId;
            RespawnSeconds = respawnSeconds;
            AvailabilityMode = availabilityMode;
            RequiredToolKind = requiredToolKind;
            MinimumToolTier = minimumToolTier;
            MinimumYield = minimumYield;
            MaximumYield = maximumYield;
        }

        public ContentId ResourceId { get; }
        public SkillId Skill { get; }
        public int RequiredLevel { get; }
        public int Experience { get; }
        public ContentId YieldItemId { get; }
        public int RespawnSeconds { get; }
        public ResourceAvailabilityMode AvailabilityMode { get; }
        public GatheringToolKind RequiredToolKind { get; }
        public int MinimumToolTier { get; }
        public int MinimumYield { get; }
        public int MaximumYield { get; }
    }

    public interface IResourceRuleSource
    {
        bool TryGetRule(ContentId resourceId, out ResourceGatheringRule rule);
    }

    /// <summary>
    /// Resolves whether a logical resource node actually exists in the authored/deterministic
    /// world. The client cannot make a fake ResourceNodeKey harvestable merely by sending it.
    /// </summary>
    public interface IResourceNodeSource
    {
        bool Exists(ResourceNodeKey node);
    }
}
