using System;
using System.Collections.Generic;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Core.Resources;
using MassRPG.Core.Skills;

namespace MassRPG.Data.Items
{
    public enum ItemType
    {
        Tool,
        Weapon,
        Armor,
        Resource,
        Food,
        Potion,
        Material,
        Currency,
        Seed,
        Ammunition,
        Miscellaneous
    }

    public sealed class CombatBonuses
    {
        public int Attack { get; set; }
        public int Strength { get; set; }
        public int Defence { get; set; }
        public int RangedAttack { get; set; }
        public int RangedStrength { get; set; }
        public int Magic { get; set; }
    }

    public sealed class ItemDefinition
    {
        public ItemDefinition(
            ContentId id,
            string displayName,
            ItemType type,
            bool stackable,
            int value = 0,
            string description = "",
            EquipmentSlot[] allowedEquipmentSlots = null,
            bool twoHanded = false,
            CombatBonuses bonuses = null,
            SkillId? equipRequirementSkill = null,
            int equipRequirementLevel = 1,
            int healAmount = 0,
            int toolTier = 0,
            GatheringToolKind gatheringToolKind = GatheringToolKind.None,
            bool? canDualWield = null,
            bool? canConsume = null)
        {
            if (id.IsEmpty) throw new ArgumentException("Item id cannot be empty.", nameof(id));
            var maximumRequirementLevel = equipRequirementSkill.HasValue
                ? SkillProgression.MaxLevelFor(equipRequirementSkill.Value)
                : SkillProgression.MaxLevel;
            if (equipRequirementLevel < 1 || equipRequirementLevel > maximumRequirementLevel)
                throw new ArgumentOutOfRangeException(
                    nameof(equipRequirementLevel),
                    equipRequirementSkill.HasValue
                        ? $"{equipRequirementSkill.Value} equipment requirements cannot exceed level {maximumRequirementLevel}."
                        : $"Equipment requirements cannot exceed level {maximumRequirementLevel}.");
            if (healAmount < 0) throw new ArgumentOutOfRangeException(nameof(healAmount));
            if (toolTier < 0) throw new ArgumentOutOfRangeException(nameof(toolTier));

            Id = id;
            DisplayName = displayName ?? string.Empty;
            Type = type;
            Stackable = stackable;
            Value = value;
            Description = description ?? string.Empty;
            AllowedEquipmentSlots = allowedEquipmentSlots ?? Array.Empty<EquipmentSlot>();
            TwoHanded = twoHanded;
            Bonuses = bonuses ?? new CombatBonuses();
            EquipRequirementSkill = equipRequirementSkill;
            EquipRequirementLevel = equipRequirementLevel;
            HealAmount = healAmount;
            ToolTier = toolTier;
            GatheringToolKind = gatheringToolKind;

            // MassRPG's default weapon rule: a normal one-handed weapon can be used in either hand.
            // Individual specialist weapons can opt out later without changing inventory rules.
            CanDualWield = canDualWield ?? (type == ItemType.Weapon && !twoHanded && ContainsMainHand(AllowedEquipmentSlots));
            CanConsume = canConsume ?? (type == ItemType.Food && healAmount > 0);
        }

        public ContentId Id { get; }
        public string DisplayName { get; set; }
        public ItemType Type { get; set; }
        public bool Stackable { get; set; }
        public int Value { get; set; }
        public string Description { get; set; }
        public EquipmentSlot[] AllowedEquipmentSlots { get; set; }
        public bool TwoHanded { get; set; }
        public bool CanDualWield { get; set; }
        public bool CanConsume { get; set; }
        public CombatBonuses Bonuses { get; set; }
        public SkillId? EquipRequirementSkill { get; set; }
        public int EquipRequirementLevel { get; set; }
        public int HealAmount { get; set; }
        public int ToolTier { get; set; }
        public GatheringToolKind GatheringToolKind { get; set; }

        /// <summary>Optional weapon timing override. Zero means use the combat-style fallback.</summary>
        public int AttackIntervalMilliseconds { get; set; }
        /// <summary>Optional weapon range override. Zero means use the combat-style fallback.</summary>
        public int AttackRangeTiles { get; set; }

        public ItemRule ToRule() => new ItemRule(
            Id,
            Stackable,
            ExpandedEquipmentSlots(),
            TwoHanded,
            EquipRequirementSkill,
            EquipRequirementLevel);

        private EquipmentSlot[] ExpandedEquipmentSlots()
        {
            if (!CanDualWield || TwoHanded || ContainsOffHand(AllowedEquipmentSlots))
                return AllowedEquipmentSlots;

            var slots = new List<EquipmentSlot>(AllowedEquipmentSlots.Length + 1);
            for (var i = 0; i < AllowedEquipmentSlots.Length; i++) slots.Add(AllowedEquipmentSlots[i]);
            slots.Add(EquipmentSlot.OffHand);
            return slots.ToArray();
        }

        private static bool ContainsMainHand(EquipmentSlot[] slots)
        {
            for (var i = 0; i < slots.Length; i++)
                if (slots[i] == EquipmentSlot.MainHand) return true;
            return false;
        }

        private static bool ContainsOffHand(EquipmentSlot[] slots)
        {
            for (var i = 0; i < slots.Length; i++)
                if (slots[i] == EquipmentSlot.OffHand) return true;
            return false;
        }
    }
}
