using System;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Data.Economy;
using MassRPG.Data.Items;

namespace MassRPG.Server.Economy
{
    public readonly struct ShopTransactionResult
    {
        private ShopTransactionResult(bool success, string code, ContentId itemId, int quantity, int coinAmount)
        {
            Success = success;
            Code = code ?? string.Empty;
            ItemId = itemId;
            Quantity = quantity;
            CoinAmount = coinAmount;
        }

        public bool Success { get; }
        public string Code { get; }
        public ContentId ItemId { get; }
        public int Quantity { get; }
        public int CoinAmount { get; }

        public static ShopTransactionResult Ok(ContentId itemId, int quantity, int coinAmount)
            => new ShopTransactionResult(true, "ok", itemId, quantity, coinAmount);

        public static ShopTransactionResult Fail(string code)
            => new ShopTransactionResult(false, code, default, 0, 0);
    }

    /// <summary>
    /// Authoritative general/specialist shop transactions. The browser prototype used infinite
    /// published stock; that remains the migration baseline while finite player/NPC market stock can
    /// later layer on top without putting pricing or inventory mutation in the Unity client.
    /// </summary>
    public sealed class ShopService
    {
        public static readonly ContentId CoinId = new ContentId("coins");

        private readonly IShopDefinitionSource _shops;
        private readonly IItemDefinitionSource _items;
        private readonly IItemRuleSource _itemRules;

        public ShopService(IShopDefinitionSource shops, IItemDefinitionSource items, IItemRuleSource itemRules)
        {
            _shops = shops ?? throw new ArgumentNullException(nameof(shops));
            _items = items ?? throw new ArgumentNullException(nameof(items));
            _itemRules = itemRules ?? throw new ArgumentNullException(nameof(itemRules));
        }

        public ShopTransactionResult Buy(PlayerState player, ContentId shopId, ContentId itemId, int quantity)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (quantity <= 0) return ShopTransactionResult.Fail("invalid_quantity");
            if (!_shops.TryGet(shopId, out var shop)) return ShopTransactionResult.Fail("unknown_shop");
            if (!shop.Sells(itemId)) return ShopTransactionResult.Fail("not_sold_here");
            if (!_items.TryGetDefinition(itemId, out var item)) return ShopTransactionResult.Fail("unknown_item");
            if (!_itemRules.TryGetRule(itemId, out var rule)) return ShopTransactionResult.Fail("unknown_item");

            int unitPrice;
            int totalPrice;
            try
            {
                unitPrice = shop.BuyUnitPrice(item.Value);
                totalPrice = checked(unitPrice * quantity);
            }
            catch (OverflowException)
            {
                return ShopTransactionResult.Fail("price_overflow");
            }

            if (player.Inventory.CountItem(CoinId) < totalPrice)
                return ShopTransactionResult.Fail("insufficient_coins");
            if (InventoryAddCapacity(player.Inventory, itemId, rule.Stackable) < quantity)
                return ShopTransactionResult.Fail("inventory_full");

            if (!InventoryRules.RemoveItem(player.Inventory, CoinId, totalPrice))
                return ShopTransactionResult.Fail("coins_changed");

            var added = InventoryRules.AddItem(player.Inventory, _itemRules, itemId, quantity);
            if (added != quantity)
            {
                InventoryRules.AddItem(player.Inventory, _itemRules, CoinId, totalPrice);
                throw new InvalidOperationException("Shop inventory capacity check did not match item insertion.");
            }

            return ShopTransactionResult.Ok(itemId, quantity, totalPrice);
        }

        public ShopTransactionResult Sell(PlayerState player, ContentId shopId, int inventorySlot, int requestedQuantity)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (requestedQuantity <= 0) return ShopTransactionResult.Fail("invalid_quantity");
            if (inventorySlot < 0 || inventorySlot >= player.Inventory.Capacity)
                return ShopTransactionResult.Fail("invalid_slot");
            if (!_shops.TryGet(shopId, out var shop)) return ShopTransactionResult.Fail("unknown_shop");

            var stack = player.Inventory.GetSlot(inventorySlot);
            if (stack == null) return ShopTransactionResult.Fail("empty_slot");
            if (stack.ItemId == CoinId) return ShopTransactionResult.Fail("cannot_sell_currency");
            if (!shop.Buys(stack.ItemId)) return ShopTransactionResult.Fail("not_bought_here");
            if (!_items.TryGetDefinition(stack.ItemId, out var item)) return ShopTransactionResult.Fail("unknown_item");

            var quantity = Math.Min(requestedQuantity, stack.Quantity);
            int totalPrice;
            try
            {
                totalPrice = checked(shop.SellUnitPrice(item.Value) * quantity);
            }
            catch (OverflowException)
            {
                return ShopTransactionResult.Fail("price_overflow");
            }

            if (!InventoryRules.RemoveItem(player.Inventory, stack.ItemId, quantity))
                return ShopTransactionResult.Fail("inventory_changed");

            var coinsAdded = InventoryRules.AddItem(player.Inventory, _itemRules, CoinId, totalPrice);
            if (coinsAdded != totalPrice)
                throw new InvalidOperationException("Selling freed inventory space but the coin payout could not be inserted.");

            return ShopTransactionResult.Ok(stack.ItemId, quantity, totalPrice);
        }

        private static int InventoryAddCapacity(InventoryState inventory, ContentId itemId, bool stackable)
        {
            if (stackable)
            {
                for (var i = 0; i < inventory.Capacity; i++)
                {
                    var stack = inventory.GetSlot(i);
                    if (stack != null && stack.ItemId == itemId) return int.MaxValue;
                }
                return inventory.FindEmptySlot() >= 0 ? int.MaxValue : 0;
            }

            return inventory.EmptySlotCount;
        }
    }
}
