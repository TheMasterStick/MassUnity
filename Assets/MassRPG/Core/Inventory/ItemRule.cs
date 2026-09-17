using System;
using MassRPG.Core.Content;
using MassRPG.Core.Skills;

namespace MassRPG.Core.Inventory
{
    /// <summary>
    /// Minimal gameplay-facing item metadata needed by pure Core rules.
    /// Rich authored item data lives in MassRPG.Data and is adapted to this contract.
    /// </summary>
    public sealed class ItemRule
    {
        public ItemRule(
            ContentId id,
            bool stackable,
            EquipmentSlot[] allowedEquipmentSlots,
            bool twoHanded,
            SkillId? equipRequirementSkill = null,
            int equipRequirementLevel = 1)
        {
            var maximumRequirementLevel = equipRequirementSkill.HasValue
                ? SkillProgression.MaxLevelFor(equipRequirementSkill.Value)
                : SkillProgression.MaxLevel;
            if (equipRequirementLevel < 1 || equipRequirementLevel > maximumRequirementLevel)
                throw new ArgumentOutOfRangeException(nameof(equipRequirementLevel));
            Id = id;
            Stackable = stackable;
            AllowedEquipmentSlots = allowedEquipmentSlots ?? Array.Empty<EquipmentSlot>();
            TwoHanded = twoHanded;
            EquipRequirementSkill = equipRequirementSkill;
            EquipRequirementLevel = equipRequirementLevel;
        }

        public ContentId Id { get; }
        public bool Stackable { get; }
        public EquipmentSlot[] AllowedEquipmentSlots { get; }
        public bool TwoHanded { get; }
        public SkillId? EquipRequirementSkill { get; }
        public int EquipRequirementLevel { get; }
    }

    public interface IItemRuleSource
    {
        bool TryGetRule(ContentId itemId, out ItemRule rule);
    }
}
