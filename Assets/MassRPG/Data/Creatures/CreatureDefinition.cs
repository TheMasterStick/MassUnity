using System;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Creatures;

namespace MassRPG.Data.Creatures
{
    /// <summary>
    /// Published creature template. Runtime instances keep only changing state and reference this
    /// permanent ID so balancing a creature definition updates all instances consistently.
    /// </summary>
    public sealed class CreatureDefinition
    {
        public CreatureDefinition(
            ContentId id,
            string displayName,
            int combatLevel,
            int maxHitpoints,
            int attackLevel,
            int strengthLevel,
            int defenceLevel,
            int attackBonus,
            int strengthBonus,
            int defenceBonus,
            CombatStyle combatStyle,
            int attackIntervalMilliseconds,
            int attackRangeTiles,
            CreatureDisposition disposition,
            CreatureFootprint footprint,
            int aggroRadiusTiles = 4,
            int leashRadiusTiles = 8,
            bool persistentNamedInstance = false,
            ContentId? lootTableId = null)
        {
            if (id.IsEmpty) throw new ArgumentException("Creature id cannot be empty.", nameof(id));
            if (combatLevel < 1) throw new ArgumentOutOfRangeException(nameof(combatLevel));
            if (maxHitpoints < 1) throw new ArgumentOutOfRangeException(nameof(maxHitpoints));
            if (attackLevel < 1) throw new ArgumentOutOfRangeException(nameof(attackLevel));
            if (strengthLevel < 1) throw new ArgumentOutOfRangeException(nameof(strengthLevel));
            if (defenceLevel < 1) throw new ArgumentOutOfRangeException(nameof(defenceLevel));
            if (attackIntervalMilliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(attackIntervalMilliseconds));
            if (attackRangeTiles < 1) throw new ArgumentOutOfRangeException(nameof(attackRangeTiles));
            if (aggroRadiusTiles < 0) throw new ArgumentOutOfRangeException(nameof(aggroRadiusTiles));
            if (leashRadiusTiles < 0) throw new ArgumentOutOfRangeException(nameof(leashRadiusTiles));
            if (lootTableId.HasValue && lootTableId.Value.IsEmpty)
                throw new ArgumentException("Loot table id cannot be empty when supplied.", nameof(lootTableId));

            Id = id;
            DisplayName = displayName ?? string.Empty;
            CombatLevel = combatLevel;
            MaxHitpoints = maxHitpoints;
            AttackLevel = attackLevel;
            StrengthLevel = strengthLevel;
            DefenceLevel = defenceLevel;
            AttackBonus = attackBonus;
            StrengthBonus = strengthBonus;
            DefenceBonus = defenceBonus;
            CombatStyle = combatStyle;
            AttackIntervalMilliseconds = attackIntervalMilliseconds;
            AttackRangeTiles = attackRangeTiles;
            Disposition = disposition;
            Footprint = footprint;
            AggroRadiusTiles = aggroRadiusTiles;
            LeashRadiusTiles = leashRadiusTiles;
            PersistentNamedInstance = persistentNamedInstance;
            LootTableId = lootTableId;
        }

        public ContentId Id { get; }
        public string DisplayName { get; set; }
        public int CombatLevel { get; set; }
        public int MaxHitpoints { get; set; }
        public int AttackLevel { get; set; }
        public int StrengthLevel { get; set; }
        public int DefenceLevel { get; set; }
        public int AttackBonus { get; set; }
        public int StrengthBonus { get; set; }
        public int DefenceBonus { get; set; }
        public CombatStyle CombatStyle { get; set; }
        public int AttackIntervalMilliseconds { get; set; }
        public int AttackRangeTiles { get; set; }
        public CreatureDisposition Disposition { get; set; }
        public CreatureFootprint Footprint { get; set; }
        public int AggroRadiusTiles { get; set; }
        public int LeashRadiusTiles { get; set; }
        public bool PersistentNamedInstance { get; set; }
        public ContentId? LootTableId { get; set; }
    }
}
