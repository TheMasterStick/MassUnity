using System;
using System.Collections.Generic;
using MassRPG.Core.Content;
using MassRPG.Core.Skills;

namespace MassRPG.Core.Inventory
{
    public sealed class InventoryOperationResult
    {
        private InventoryOperationResult(bool success, string code, string message)
        {
            Success = success;
            Code = code;
            Message = message;
        }

        public bool Success { get; }
        public string Code { get; }
        public string Message { get; }

        public static InventoryOperationResult Ok() => new InventoryOperationResult(true, "ok", string.Empty);
        public static InventoryOperationResult Fail(string code, string message) => new InventoryOperationResult(false, code, message);
    }

    public static class InventoryRules
    {
        public static int AddItem(InventoryState inventory, IItemRuleSource rules, ContentId itemId, int quantity)
        {
            if (quantity <= 0) return 0;
            if (!rules.TryGetRule(itemId, out var rule)) return 0;

            var remaining = quantity;
            if (rule.Stackable)
            {
                for (var i = 0; i < inventory.Capacity; i++)
                {
                    var existing = inventory.GetSlot(i);
                    if (existing == null || existing.ItemId != itemId) continue;
                    existing.Quantity = checked(existing.Quantity + remaining);
                    return quantity;
                }
            }

            while (remaining > 0)
            {
                var empty = inventory.FindEmptySlot();
                if (empty < 0) break;
                var amount = rule.Stackable ? remaining : 1;
                inventory.SetSlot(empty, new InventoryStack(itemId, amount));
                remaining -= amount;
            }

            return quantity - remaining;
        }

        public static bool RemoveItem(InventoryState inventory, ContentId itemId, int quantity)
        {
            if (quantity <= 0 || inventory.CountItem(itemId) < quantity) return false;

            var remaining = quantity;
            for (var i = 0; i < inventory.Capacity && remaining > 0; i++)
            {
                var stack = inventory.GetSlot(i);
                if (stack == null || stack.ItemId != itemId) continue;

                var take = Math.Min(stack.Quantity, remaining);
                stack.Quantity -= take;
                remaining -= take;
                if (stack.Quantity == 0) inventory.SetSlot(i, null);
            }

            return true;
        }

        public static InventoryOperationResult MoveSlot(InventoryState inventory, IItemRuleSource rules, int fromIndex, int toIndex)
        {
            if (!InBounds(inventory, fromIndex) || !InBounds(inventory, toIndex))
                return InventoryOperationResult.Fail("invalid_slot", "Inventory slot is out of range.");
            if (fromIndex == toIndex) return InventoryOperationResult.Ok();

            var from = inventory.GetSlot(fromIndex);
            if (from == null) return InventoryOperationResult.Fail("empty_slot", "There is no item in that slot.");

            var to = inventory.GetSlot(toIndex);
            if (to == null)
            {
                inventory.SetSlot(toIndex, from);
                inventory.SetSlot(fromIndex, null);
                return InventoryOperationResult.Ok();
            }

            if (to.ItemId == from.ItemId && rules.TryGetRule(from.ItemId, out var rule) && rule.Stackable)
            {
                to.Quantity = checked(to.Quantity + from.Quantity);
                inventory.SetSlot(fromIndex, null);
                return InventoryOperationResult.Ok();
            }

            inventory.SetSlot(toIndex, from);
            inventory.SetSlot(fromIndex, to);
            return InventoryOperationResult.Ok();
        }

        public static InventoryOperationResult EquipFromInventory(
            InventoryState inventory,
            EquipmentState equipment,
            IItemRuleSource rules,
            int inventoryIndex,
            EquipmentSlot? requestedSlot = null)
            => EquipFromInventory(inventory, equipment, rules, null, inventoryIndex, requestedSlot);

        public static InventoryOperationResult EquipFromInventory(
            InventoryState inventory,
            EquipmentState equipment,
            IItemRuleSource rules,
            SkillSet skills,
            int inventoryIndex,
            EquipmentSlot? requestedSlot = null)
        {
            if (!InBounds(inventory, inventoryIndex))
                return InventoryOperationResult.Fail("invalid_slot", "Inventory slot is out of range.");

            var stack = inventory.GetSlot(inventoryIndex);
            if (stack == null) return InventoryOperationResult.Fail("empty_slot", "There is no item in that slot.");
            if (stack.Quantity != 1) return InventoryOperationResult.Fail("invalid_equipment_stack", "Equippable items must be individual inventory entries.");
            if (!rules.TryGetRule(stack.ItemId, out var rule))
                return InventoryOperationResult.Fail("unknown_item", "Unknown item id.");
            if (rule.AllowedEquipmentSlots.Length == 0)
                return InventoryOperationResult.Fail("not_equippable", "That item cannot be equipped.");

            if (rule.EquipRequirementSkill.HasValue)
            {
                if (skills == null || skills.GetLevel(rule.EquipRequirementSkill.Value) < rule.EquipRequirementLevel)
                    return InventoryOperationResult.Fail("requirement_not_met", $"You need {rule.EquipRequirementSkill.Value} level {rule.EquipRequirementLevel} to equip that item.");
            }

            var target = ResolveEquipmentTargetSlot(equipment, rule, requestedSlot);
            if (!target.HasValue)
                return InventoryOperationResult.Fail("invalid_equipment_slot", "That item cannot be equipped in the requested slot.");

            var displaced = new HashSet<EquipmentSlot>();
            if (equipment.IsOccupied(target.Value)) displaced.Add(target.Value);

            if (target.Value == EquipmentSlot.Weapon && rule.TwoHanded && equipment.IsOccupied(EquipmentSlot.Shield))
                displaced.Add(EquipmentSlot.Shield);

            if (target.Value == EquipmentSlot.Shield && equipment.TryGet(EquipmentSlot.Weapon, out var currentWeapon))
            {
                if (rules.TryGetRule(currentWeapon, out var currentWeaponRule) && currentWeaponRule.TwoHanded)
                    displaced.Add(EquipmentSlot.Weapon);
            }

            if (displaced.Count > inventory.EmptySlotCount + 1)
                return InventoryOperationResult.Fail("inventory_full", "Your inventory is too full to unequip the replaced items.");

            inventory.SetSlot(inventoryIndex, null);
            foreach (var slot in displaced)
            {
                if (!equipment.Clear(slot, out var oldItem)) continue;
                if (AddItem(inventory, rules, oldItem, 1) != 1)
                    throw new InvalidOperationException("Equipment displacement capacity check failed.");
            }

            equipment.Set(target.Value, stack.ItemId);
            return InventoryOperationResult.Ok();
        }

        public static EquipmentSlot? ResolveEquipmentTargetSlot(EquipmentState equipment, ItemRule rule, EquipmentSlot? requested)
        {
            if (requested.HasValue)
            {
                for (var i = 0; i < rule.AllowedEquipmentSlots.Length; i++)
                    if (rule.AllowedEquipmentSlots[i] == requested.Value) return requested.Value;
                return null;
            }

            for (var i = 0; i < rule.AllowedEquipmentSlots.Length; i++)
            {
                var slot = rule.AllowedEquipmentSlots[i];
                if (!equipment.IsOccupied(slot)) return slot;
            }

            return rule.AllowedEquipmentSlots.Length > 0 ? rule.AllowedEquipmentSlots[0] : (EquipmentSlot?)null;
        }

        public static InventoryOperationResult Unequip(
            InventoryState inventory,
            EquipmentState equipment,
            IItemRuleSource rules,
            EquipmentSlot slot)
        {
            if (!equipment.TryGet(slot, out var itemId))
                return InventoryOperationResult.Fail("empty_equipment_slot", "There is no item equipped there.");
            if (inventory.FindEmptySlot() < 0)
                return InventoryOperationResult.Fail("inventory_full", "Your inventory is too full.");

            equipment.Clear(slot, out _);
            if (AddItem(inventory, rules, itemId, 1) != 1)
                throw new InvalidOperationException("Unequip capacity check failed.");

            return InventoryOperationResult.Ok();
        }

        /// <summary>
        /// Removes an equipped item without first routing it through inventory. Server systems use
        /// this for explicit sinks such as full-loot PvP death; ordinary player unequip still uses
        /// Unequip so inventory-capacity rules remain enforced.
        /// </summary>
        public static bool TryExtractEquippedItem(EquipmentState equipment, EquipmentSlot slot, out ContentId itemId)
        {
            if (equipment == null) throw new ArgumentNullException(nameof(equipment));
            return equipment.Clear(slot, out itemId);
        }

        private static bool InBounds(InventoryState inventory, int index) => index >= 0 && index < inventory.Capacity;
    }
}
