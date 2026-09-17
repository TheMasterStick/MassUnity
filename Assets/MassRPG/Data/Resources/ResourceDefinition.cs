using System;
using MassRPG.Core.Content;
using MassRPG.Core.Resources;
using MassRPG.Core.Skills;

namespace MassRPG.Data.Resources
{
    /// <summary>
    /// Published gameplay definition for a harvestable resource type. Placement belongs to world
    /// data; temporary depletion belongs to authoritative server state.
    /// </summary>
    public sealed class ResourceDefinition
    {
        public ResourceDefinition(
            ContentId id,
            string displayName,
            SkillId gatheringSkill,
            int requiredLevel,
            int experience,
            ContentId yieldItemId,
            int respawnSeconds,
            ResourceAvailabilityMode availabilityMode = ResourceAvailabilityMode.Personal,
            int minimumYield = 1,
            int maximumYield = 1,
            GatheringToolKind requiredToolKind = GatheringToolKind.None,
            int minimumToolTier = 0)
        {
            if (id.IsEmpty) throw new ArgumentException("Resource id cannot be empty.", nameof(id));
            if (yieldItemId.IsEmpty) throw new ArgumentException("Yield item id cannot be empty.", nameof(yieldItemId));
            var maximumLevel = SkillProgression.MaxLevelFor(gatheringSkill);
            if (requiredLevel < 1 || requiredLevel > maximumLevel)
                throw new ArgumentOutOfRangeException(nameof(requiredLevel), $"{gatheringSkill} resource requirements cannot exceed level {maximumLevel}.");
            if (experience < 0) throw new ArgumentOutOfRangeException(nameof(experience));
            if (respawnSeconds < 0) throw new ArgumentOutOfRangeException(nameof(respawnSeconds));
            if (minimumYield <= 0 || maximumYield < minimumYield) throw new ArgumentOutOfRangeException(nameof(maximumYield));
            if (minimumToolTier < 0) throw new ArgumentOutOfRangeException(nameof(minimumToolTier));

            Id = id;
            DisplayName = displayName ?? string.Empty;
            GatheringSkill = gatheringSkill;
            RequiredLevel = requiredLevel;
            Experience = experience;
            YieldItemId = yieldItemId;
            RespawnSeconds = respawnSeconds;
            AvailabilityMode = availabilityMode;
            MinimumYield = minimumYield;
            MaximumYield = maximumYield;
            RequiredToolKind = requiredToolKind;
            MinimumToolTier = minimumToolTier;
        }

        public ContentId Id { get; }
        public string DisplayName { get; set; }
        public SkillId GatheringSkill { get; set; }
        public int RequiredLevel { get; set; }
        public int Experience { get; set; }
        public ContentId YieldItemId { get; set; }
        public int RespawnSeconds { get; set; }
        public ResourceAvailabilityMode AvailabilityMode { get; set; }
        public int MinimumYield { get; set; }
        public int MaximumYield { get; set; }
        public GatheringToolKind RequiredToolKind { get; set; }
        public int MinimumToolTier { get; set; }

        public ResourceGatheringRule ToRule() => new ResourceGatheringRule(
            Id,
            GatheringSkill,
            RequiredLevel,
            Experience,
            YieldItemId,
            RespawnSeconds,
            AvailabilityMode,
            RequiredToolKind,
            MinimumToolTier,
            MinimumYield,
            MaximumYield);
    }
}
