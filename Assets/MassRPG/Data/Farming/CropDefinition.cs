using System;
using System.Collections.Generic;
using MassRPG.Core.Content;

namespace MassRPG.Data.Farming
{
    public enum FarmPatchKind
    {
        Crop,
        Herb
    }

    /// <summary>Published crop/herb growth data; mutable planted state lives on the server.</summary>
    public sealed class CropDefinition
    {
        public CropDefinition(
            ContentId id,
            string displayName,
            FarmPatchKind patchKind,
            int requiredFarmingLevel,
            int plantingExperience,
            int harvestExperience,
            int growthMilliseconds,
            ContentId seedItemId,
            ContentId yieldItemId,
            int minimumYield = 2,
            int maximumYield = 4)
        {
            if (id.IsEmpty) throw new ArgumentException("Crop id cannot be empty.", nameof(id));
            if (requiredFarmingLevel < 1 || requiredFarmingLevel > 300) throw new ArgumentOutOfRangeException(nameof(requiredFarmingLevel));
            if (plantingExperience < 0) throw new ArgumentOutOfRangeException(nameof(plantingExperience));
            if (harvestExperience < 0) throw new ArgumentOutOfRangeException(nameof(harvestExperience));
            if (growthMilliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(growthMilliseconds));
            if (seedItemId.IsEmpty) throw new ArgumentException("Seed id cannot be empty.", nameof(seedItemId));
            if (yieldItemId.IsEmpty) throw new ArgumentException("Yield id cannot be empty.", nameof(yieldItemId));
            if (minimumYield <= 0 || maximumYield < minimumYield) throw new ArgumentOutOfRangeException(nameof(minimumYield));

            Id = id;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? id.Value : displayName;
            PatchKind = patchKind;
            RequiredFarmingLevel = requiredFarmingLevel;
            PlantingExperience = plantingExperience;
            HarvestExperience = harvestExperience;
            GrowthMilliseconds = growthMilliseconds;
            SeedItemId = seedItemId;
            YieldItemId = yieldItemId;
            MinimumYield = minimumYield;
            MaximumYield = maximumYield;
        }

        public ContentId Id { get; }
        public string DisplayName { get; set; }
        public FarmPatchKind PatchKind { get; set; }
        public int RequiredFarmingLevel { get; set; }
        public int PlantingExperience { get; set; }
        public int HarvestExperience { get; set; }
        public int GrowthMilliseconds { get; set; }
        public ContentId SeedItemId { get; set; }
        public ContentId YieldItemId { get; set; }
        public int MinimumYield { get; set; }
        public int MaximumYield { get; set; }
    }

    public interface ICropDefinitionSource
    {
        bool TryGet(ContentId cropId, out CropDefinition definition);
    }

    public sealed class CropCatalog : ICropDefinitionSource
    {
        private readonly Dictionary<ContentId, CropDefinition> _definitions = new Dictionary<ContentId, CropDefinition>();

        public IEnumerable<CropDefinition> All => _definitions.Values;

        public void Register(CropDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (_definitions.ContainsKey(definition.Id)) throw new InvalidOperationException($"Duplicate crop id '{definition.Id}'.");
            _definitions.Add(definition.Id, definition);
        }

        public bool TryGet(ContentId cropId, out CropDefinition definition)
            => _definitions.TryGetValue(cropId, out definition);
    }
}
