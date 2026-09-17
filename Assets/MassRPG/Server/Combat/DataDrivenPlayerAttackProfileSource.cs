using System;
using MassRPG.Core.Characters;
using MassRPG.Core.Combat;
using MassRPG.Core.Inventory;
using MassRPG.Data.Items;

namespace MassRPG.Server.Combat
{
    /// <summary>
    /// Development resolver for authoritative player combat range/timing/bonuses. Weapon data may
    /// override the migration fallbacks; exact final speeds/ranges remain content balance rather
    /// than being baked into combat geometry. Selected ranged ammunition contributes bonuses while
    /// remaining an inventory stack rather than an equipment slot.
    /// </summary>
    public sealed class DataDrivenPlayerAttackProfileSource : IPlayerAttackProfileSource
    {
        private readonly ItemCatalog _items;

        public DataDrivenPlayerAttackProfileSource(
            ItemCatalog items,
            int fallbackMeleeIntervalMilliseconds = 2400,
            int fallbackRangedIntervalMilliseconds = 3000,
            int fallbackMagicIntervalMilliseconds = 3000,
            int fallbackRangedRangeTiles = 6,
            int fallbackMagicRangeTiles = 6)
        {
            _items = items ?? throw new ArgumentNullException(nameof(items));
            FallbackMeleeIntervalMilliseconds = fallbackMeleeIntervalMilliseconds;
            FallbackRangedIntervalMilliseconds = fallbackRangedIntervalMilliseconds;
            FallbackMagicIntervalMilliseconds = fallbackMagicIntervalMilliseconds;
            FallbackRangedRangeTiles = fallbackRangedRangeTiles;
            FallbackMagicRangeTiles = fallbackMagicRangeTiles;
        }

        public int FallbackMeleeIntervalMilliseconds { get; }
        public int FallbackRangedIntervalMilliseconds { get; }
        public int FallbackMagicIntervalMilliseconds { get; }
        public int FallbackRangedRangeTiles { get; }
        public int FallbackMagicRangeTiles { get; }

        public PlayerAttackProfile Resolve(PlayerState player)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            var attack = 0;
            var strength = 0;
            var defence = 0;
            var rangedAttack = 0;
            var rangedStrength = 0;
            var magic = 0;

            foreach (var equipped in player.Equipment.EquippedItems)
            {
                if (!_items.TryGetDefinition(equipped.Value, out var definition)) continue;
                attack += definition.Bonuses.Attack;
                strength += definition.Bonuses.Strength;
                defence += definition.Bonuses.Defence;
                rangedAttack += definition.Bonuses.RangedAttack;
                rangedStrength += definition.Bonuses.RangedStrength;
                magic += definition.Bonuses.Magic;
            }

            if (player.CombatStyle == CombatStyle.Ranged
                && player.SelectedAmmunitionItemId.HasValue
                && player.Inventory.CountItem(player.SelectedAmmunitionItemId.Value) > 0
                && _items.TryGetDefinition(player.SelectedAmmunitionItemId.Value, out var ammunition)
                && ammunition.Type == ItemType.Ammunition)
            {
                rangedAttack += ammunition.Bonuses.RangedAttack;
                rangedStrength += ammunition.Bonuses.RangedStrength;
            }

            var range = player.CombatStyle == CombatStyle.Melee
                ? 1
                : player.CombatStyle == CombatStyle.Ranged ? FallbackRangedRangeTiles : FallbackMagicRangeTiles;
            var interval = player.CombatStyle == CombatStyle.Melee
                ? FallbackMeleeIntervalMilliseconds
                : player.CombatStyle == CombatStyle.Ranged ? FallbackRangedIntervalMilliseconds : FallbackMagicIntervalMilliseconds;

            if (player.Equipment.TryGet(EquipmentSlot.Weapon, out var weaponId)
                && _items.TryGetDefinition(weaponId, out var weapon))
            {
                if (weapon.AttackRangeTiles > 0) range = weapon.AttackRangeTiles;
                if (weapon.AttackIntervalMilliseconds > 0) interval = weapon.AttackIntervalMilliseconds;
            }

            return new PlayerAttackProfile(
                player.CombatStyle,
                Math.Max(1, range),
                Math.Max(1, interval),
                attack,
                strength,
                defence,
                rangedAttack,
                rangedStrength,
                magic);
        }
    }
}
