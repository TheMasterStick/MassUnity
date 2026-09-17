using System;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Data.Items;

namespace MassRPG.Server.Combat
{
    public readonly struct RangedAmmunitionResult
    {
        private RangedAmmunitionResult(bool success, string code, ContentId itemId, int remainingQuantity)
        {
            Success = success;
            Code = code ?? string.Empty;
            ItemId = itemId;
            RemainingQuantity = remainingQuantity;
        }

        public bool Success { get; }
        public string Code { get; }
        public ContentId ItemId { get; }
        public int RemainingQuantity { get; }

        public static RangedAmmunitionResult Ok(ContentId itemId, int remainingQuantity)
            => new RangedAmmunitionResult(true, "ok", itemId, remainingQuantity);

        public static RangedAmmunitionResult Fail(string code)
            => new RangedAmmunitionResult(false, code, default, 0);
    }

    /// <summary>
    /// Authoritative ranged-ammunition state without reintroducing the browser's removed ammo
    /// equipment slot. Arrows remain stackable inventory items; the player selects one ammunition
    /// id and each actual ranged attack attempt consumes one from inventory.
    /// </summary>
    public sealed class RangedAmmunitionService
    {
        private readonly ItemCatalog _items;

        public RangedAmmunitionService(ItemCatalog items)
        {
            _items = items ?? throw new ArgumentNullException(nameof(items));
        }

        public RangedAmmunitionResult Select(PlayerState player, ContentId ammunitionItemId)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            var validation = ValidateAmmunition(player, ammunitionItemId);
            if (!validation.Success) return validation;
            player.SelectedAmmunitionItemId = ammunitionItemId;
            return RangedAmmunitionResult.Ok(ammunitionItemId, player.Inventory.CountItem(ammunitionItemId));
        }

        public void ClearSelection(PlayerState player)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            player.SelectedAmmunitionItemId = null;
        }

        public RangedAmmunitionResult ValidateForAttack(PlayerState player)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (player.CombatStyle != CombatStyle.Ranged)
                return RangedAmmunitionResult.Ok(default, 0);

            if (!player.Equipment.TryGet(EquipmentSlot.Weapon, out var weaponId)
                || !_items.TryGetDefinition(weaponId, out var weapon)
                || weapon.Bonuses.RangedAttack <= 0)
                return RangedAmmunitionResult.Fail("ranged_weapon_missing");

            if (!player.SelectedAmmunitionItemId.HasValue)
                return RangedAmmunitionResult.Fail("ranged_ammunition_missing");

            return ValidateAmmunition(player, player.SelectedAmmunitionItemId.Value);
        }

        public RangedAmmunitionResult ConsumeForAttack(PlayerState player)
        {
            var validation = ValidateForAttack(player);
            if (!validation.Success || player.CombatStyle != CombatStyle.Ranged) return validation;

            var itemId = player.SelectedAmmunitionItemId.Value;
            if (!InventoryRules.RemoveItem(player.Inventory, itemId, 1))
            {
                player.SelectedAmmunitionItemId = null;
                return RangedAmmunitionResult.Fail("ranged_ammunition_changed");
            }

            var remaining = player.Inventory.CountItem(itemId);
            if (remaining <= 0) player.SelectedAmmunitionItemId = null;
            return RangedAmmunitionResult.Ok(itemId, remaining);
        }

        private RangedAmmunitionResult ValidateAmmunition(PlayerState player, ContentId itemId)
        {
            if (!_items.TryGetDefinition(itemId, out var definition)
                || definition.Type != ItemType.Ammunition
                || definition.Bonuses.RangedStrength <= 0)
                return RangedAmmunitionResult.Fail("invalid_ranged_ammunition");

            if (definition.EquipRequirementSkill.HasValue
                && player.Skills.GetLevel(definition.EquipRequirementSkill.Value) < definition.EquipRequirementLevel)
                return RangedAmmunitionResult.Fail("ranged_ammunition_requirement_not_met");

            var count = player.Inventory.CountItem(itemId);
            if (count <= 0) return RangedAmmunitionResult.Fail("ranged_ammunition_missing");
            return RangedAmmunitionResult.Ok(itemId, count);
        }
    }
}
