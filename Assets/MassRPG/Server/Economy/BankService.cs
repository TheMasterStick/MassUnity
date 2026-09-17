using System;
using System.Collections.Generic;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;

namespace MassRPG.Server.Economy
{
    /// <summary>
    /// Authoritative character bank state. Current migrated items are quantity-based; when unique
    /// per-instance item state (durability/rolls/etc.) is introduced, those items can move to an
    /// instance store without changing the transaction boundary used by the client.
    /// </summary>
    public sealed class CharacterBankState
    {
        private readonly Dictionary<ContentId, int> _quantities = new Dictionary<ContentId, int>();

        public int Count(ContentId itemId)
            => _quantities.TryGetValue(itemId, out var quantity) ? quantity : 0;

        public IEnumerable<KeyValuePair<ContentId, int>> Entries => _quantities;

        internal void Add(ContentId itemId, int quantity)
        {
            if (quantity <= 0) return;
            _quantities[itemId] = checked(Count(itemId) + quantity);
        }

        internal bool Remove(ContentId itemId, int quantity)
        {
            if (quantity <= 0 || Count(itemId) < quantity) return false;
            var remaining = Count(itemId) - quantity;
            if (remaining == 0) _quantities.Remove(itemId);
            else _quantities[itemId] = remaining;
            return true;
        }
    }

    public sealed class CharacterBankRegistry
    {
        private readonly Dictionary<Guid, CharacterBankState> _banks = new Dictionary<Guid, CharacterBankState>();

        public CharacterBankState GetOrCreate(Guid characterId)
        {
            if (characterId == Guid.Empty) throw new ArgumentException("Character id cannot be empty.", nameof(characterId));
            if (!_banks.TryGetValue(characterId, out var bank))
            {
                bank = new CharacterBankState();
                _banks.Add(characterId, bank);
            }
            return bank;
        }

        public bool TryGet(Guid characterId, out CharacterBankState bank) => _banks.TryGetValue(characterId, out bank);
    }

    public readonly struct BankTransactionResult
    {
        private BankTransactionResult(bool success, string code, ContentId itemId, int quantity)
        {
            Success = success;
            Code = code ?? string.Empty;
            ItemId = itemId;
            Quantity = quantity;
        }

        public bool Success { get; }
        public string Code { get; }
        public ContentId ItemId { get; }
        public int Quantity { get; }

        public static BankTransactionResult Ok(ContentId itemId, int quantity)
            => new BankTransactionResult(true, "ok", itemId, quantity);

        public static BankTransactionResult Fail(string code)
            => new BankTransactionResult(false, code, default, 0);
    }

    /// <summary>
    /// Server-side bank transfer rules migrated from the browser prototype. The service is deliberately
    /// presentation-free and moves only authoritative inventory/bank quantities.
    /// </summary>
    public sealed class BankService
    {
        private readonly IItemRuleSource _itemRules;

        public BankService(IItemRuleSource itemRules)
        {
            _itemRules = itemRules ?? throw new ArgumentNullException(nameof(itemRules));
        }

        public BankTransactionResult Deposit(PlayerState player, CharacterBankState bank, int inventorySlot, int requestedQuantity)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (bank == null) throw new ArgumentNullException(nameof(bank));
            if (requestedQuantity <= 0) return BankTransactionResult.Fail("invalid_quantity");
            if (inventorySlot < 0 || inventorySlot >= player.Inventory.Capacity)
                return BankTransactionResult.Fail("invalid_slot");

            var stack = player.Inventory.GetSlot(inventorySlot);
            if (stack == null) return BankTransactionResult.Fail("empty_slot");
            if (!_itemRules.TryGetRule(stack.ItemId, out _)) return BankTransactionResult.Fail("unknown_item");

            var quantity = Math.Min(requestedQuantity, stack.Quantity);
            if (!InventoryRules.RemoveItem(player.Inventory, stack.ItemId, quantity))
                return BankTransactionResult.Fail("inventory_changed");

            bank.Add(stack.ItemId, quantity);
            return BankTransactionResult.Ok(stack.ItemId, quantity);
        }

        public BankTransactionResult Withdraw(PlayerState player, CharacterBankState bank, ContentId itemId, int requestedQuantity)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (bank == null) throw new ArgumentNullException(nameof(bank));
            if (itemId.IsEmpty) return BankTransactionResult.Fail("invalid_item");
            if (requestedQuantity <= 0) return BankTransactionResult.Fail("invalid_quantity");
            if (!_itemRules.TryGetRule(itemId, out var rule)) return BankTransactionResult.Fail("unknown_item");

            var available = bank.Count(itemId);
            if (available <= 0) return BankTransactionResult.Fail("not_in_bank");

            var capacity = InventoryAddCapacity(player.Inventory, itemId, rule.Stackable);
            if (capacity <= 0) return BankTransactionResult.Fail("inventory_full");

            var quantity = Math.Min(requestedQuantity, Math.Min(available, capacity));
            var added = InventoryRules.AddItem(player.Inventory, _itemRules, itemId, quantity);
            if (added <= 0) return BankTransactionResult.Fail("inventory_full");
            if (!bank.Remove(itemId, added))
                throw new InvalidOperationException("Bank quantity changed during authoritative withdrawal.");

            return BankTransactionResult.Ok(itemId, added);
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
