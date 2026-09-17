using System;
using System.Collections.Generic;
using MassRPG.Core.Content;
using MassRPG.Core.Skills;

namespace MassRPG.Data.Recipes
{
    public readonly struct RecipeIngredient
    {
        public RecipeIngredient(ContentId itemId, int quantity)
        {
            if (itemId.IsEmpty) throw new ArgumentException("Ingredient item id cannot be empty.", nameof(itemId));
            if (quantity < 1) throw new ArgumentOutOfRangeException(nameof(quantity));
            ItemId = itemId;
            Quantity = quantity;
        }

        public ContentId ItemId { get; }
        public int Quantity { get; }
    }

    /// <summary>
    /// Published production recipe. Stations and tools are content IDs rather than executable
    /// types so recipes can be edited/versioned by the MassRPG Data Editor.
    /// </summary>
    public sealed class RecipeDefinition
    {
        private readonly List<RecipeIngredient> _inputs;

        public RecipeDefinition(
            ContentId id,
            string displayName,
            SkillId skill,
            int levelRequired,
            IEnumerable<RecipeIngredient> inputs,
            ContentId outputItemId,
            int outputQuantity,
            long xp,
            int durationMilliseconds,
            string category,
            ContentId? stationId = null,
            ContentId? toolRequiredId = null,
            bool canBurn = false,
            ContentId? failureOutputItemId = null,
            double failureXpFraction = 0.10)
        {
            if (id.IsEmpty) throw new ArgumentException("Recipe id cannot be empty.", nameof(id));
            var maximumLevel = SkillProgression.MaxLevelFor(skill);
            if (levelRequired < 1 || levelRequired > maximumLevel)
                throw new ArgumentOutOfRangeException(nameof(levelRequired), $"{skill} recipe requirements cannot exceed level {maximumLevel}.");
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            if (outputItemId.IsEmpty) throw new ArgumentException("Output item id cannot be empty.", nameof(outputItemId));
            if (outputQuantity < 1) throw new ArgumentOutOfRangeException(nameof(outputQuantity));
            if (xp < 0) throw new ArgumentOutOfRangeException(nameof(xp));
            if (durationMilliseconds < 1) throw new ArgumentOutOfRangeException(nameof(durationMilliseconds));
            if (failureXpFraction < 0.0 || failureXpFraction > 1.0) throw new ArgumentOutOfRangeException(nameof(failureXpFraction));
            if (canBurn && !failureOutputItemId.HasValue)
                throw new ArgumentException("Burnable recipes require an explicit failure output item.", nameof(failureOutputItemId));

            Id = id;
            DisplayName = displayName ?? string.Empty;
            Skill = skill;
            LevelRequired = levelRequired;
            _inputs = new List<RecipeIngredient>(inputs);
            if (_inputs.Count == 0) throw new ArgumentException("A recipe must have at least one input.", nameof(inputs));
            OutputItemId = outputItemId;
            OutputQuantity = outputQuantity;
            Xp = xp;
            DurationMilliseconds = durationMilliseconds;
            Category = category ?? string.Empty;
            StationId = stationId;
            ToolRequiredId = toolRequiredId;
            CanBurn = canBurn;
            FailureOutputItemId = failureOutputItemId;
            FailureXpFraction = failureXpFraction;
        }

        public ContentId Id { get; }
        public string DisplayName { get; set; }
        public SkillId Skill { get; set; }
        public int LevelRequired { get; set; }
        public IReadOnlyList<RecipeIngredient> Inputs => _inputs;
        public ContentId OutputItemId { get; set; }
        public int OutputQuantity { get; set; }
        public long Xp { get; set; }
        public int DurationMilliseconds { get; set; }
        public string Category { get; set; }
        public ContentId? StationId { get; set; }
        public ContentId? ToolRequiredId { get; set; }
        public bool CanBurn { get; set; }
        public ContentId? FailureOutputItemId { get; set; }
        public double FailureXpFraction { get; set; }
    }
}
