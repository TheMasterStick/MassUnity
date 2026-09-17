using System;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Data.Items;

namespace MassRPG.Server.Items
{
    public readonly struct FoodConsumptionResult
    {
        private FoodConsumptionResult(bool success, string code, ContentId itemId, int healed)
        {
            Success = success;
            Code = code ?? string.Empty;
            ItemId = itemId;
            Healed = healed;
        }

        public bool Success { get; }
        public string Code { get; }
        public ContentId ItemId { get; }
        public int Healed { get; }

        public static FoodConsumptionResult Ok(ContentId itemId, int healed)
            => new FoodConsumptionResult(true, "ok", itemId, healed);

        public static FoodConsumptionResult Fail(string code)
            => new FoodConsumptionResult(false, code, default, 0);
    }

    /// <summary>
    /// Authoritative food consumption. Potions intentionally do not pass through this service:
    /// temporary stat/effect state will get its own model instead of pretending the browser's
    /// placeholder drink action is a finished MMO system.
    /// </summary>
    public sealed class FoodConsumptionService
    {
        private readonly IItemDefinitionSource _items;
        private readonly IItemRuleSource _itemRules;

        public FoodConsumptionService(IItemDefinitionSource items, IItemRuleSource itemRules)
        {
            _items = items ?? throw new ArgumentNullException(nameof(items));
            _itemRules = itemRules ?? throw new ArgumentNullException(nameof(itemRules));
        }

        public FoodConsumptionResult Eat(PlayerState player, int inventorySlot)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (!player.IsAlive) return FoodConsumptionResult.Fail("character_dead");
            if (inventorySlot < 0 || inventorySlot >= player.Inventory.Capacity)
                return FoodConsumptionResult.Fail("invalid_slot");

            var stack = player.Inventory.GetSlot(inventorySlot);
            if (stack == null) return FoodConsumptionResult.Fail("empty_slot");
            if (!_items.TryGetDefinition(stack.ItemId, out var definition))
                return FoodConsumptionResult.Fail("unknown_item");
            if (definition.Type != ItemType.Food || !definition.CanConsume || definition.HealAmount <= 0)
                return FoodConsumptionResult.Fail("not_edible");

            // Browser migration parity: raw/burnt cooking products are food-category crafting
            // materials but were deliberately excluded from the Eat action. Repository-authored
            // item data can set CanConsume=false directly as those definitions are normalized.
            if (definition.Id.Value.StartsWith("raw_", StringComparison.Ordinal)
                || definition.Id.Value.StartsWith("burnt_", StringComparison.Ordinal))
                return FoodConsumptionResult.Fail("not_edible");

            if (!InventoryRules.RemoveItem(player.Inventory, stack.ItemId, 1))
                return FoodConsumptionResult.Fail("inventory_changed");

            var before = Math.Max(0, Math.Min(player.CurrentHitpoints, player.MaxHitpoints));
            var after = Math.Min(player.MaxHitpoints, before + definition.HealAmount);
            player.CurrentHitpoints = after;
            return FoodConsumptionResult.Ok(stack.ItemId, after - before);
        }
    }
}
