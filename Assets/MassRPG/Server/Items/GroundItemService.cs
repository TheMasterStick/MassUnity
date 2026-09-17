using System;
using System.Collections.Generic;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Core.World;

namespace MassRPG.Server.Items
{
    public sealed class GroundItemState
    {
        private readonly HashSet<Guid> _protectedRecipients;

        public GroundItemState(
            Guid instanceId,
            ContentId itemId,
            int quantity,
            GridLocation location,
            long spawnedAtUnixMilliseconds,
            IEnumerable<Guid> protectedRecipients = null,
            long publicAtUnixMilliseconds = 0,
            long expiresAtUnixMilliseconds = 0)
        {
            if (instanceId == Guid.Empty) throw new ArgumentException("Ground item instance id cannot be empty.", nameof(instanceId));
            if (itemId.IsEmpty) throw new ArgumentException("Item id cannot be empty.", nameof(itemId));
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            InstanceId = instanceId;
            ItemId = itemId;
            Quantity = quantity;
            Location = location;
            SpawnedAtUnixMilliseconds = spawnedAtUnixMilliseconds;
            PublicAtUnixMilliseconds = publicAtUnixMilliseconds;
            ExpiresAtUnixMilliseconds = expiresAtUnixMilliseconds;
            _protectedRecipients = protectedRecipients == null
                ? new HashSet<Guid>()
                : new HashSet<Guid>(protectedRecipients);
        }

        public Guid InstanceId { get; }
        public ContentId ItemId { get; }
        public int Quantity { get; internal set; }
        public GridLocation Location { get; }
        public long SpawnedAtUnixMilliseconds { get; }
        public long PublicAtUnixMilliseconds { get; }
        public long ExpiresAtUnixMilliseconds { get; }
        public IEnumerable<Guid> ProtectedRecipients => _protectedRecipients;

        public bool IsExpired(long nowUnixMilliseconds)
            => ExpiresAtUnixMilliseconds > 0 && nowUnixMilliseconds >= ExpiresAtUnixMilliseconds;

        public bool CanBeTakenBy(Guid characterId, long nowUnixMilliseconds)
        {
            if (IsExpired(nowUnixMilliseconds)) return false;
            if (_protectedRecipients.Count == 0) return true;
            if (_protectedRecipients.Contains(characterId)) return true;
            return PublicAtUnixMilliseconds > 0 && nowUnixMilliseconds >= PublicAtUnixMilliseconds;
        }
    }

    public sealed class GroundItemRegistry
    {
        private readonly Dictionary<Guid, GroundItemState> _items = new Dictionary<Guid, GroundItemState>();

        public IEnumerable<GroundItemState> All => _items.Values;
        public bool TryGet(Guid instanceId, out GroundItemState item) => _items.TryGetValue(instanceId, out item);

        internal void Add(GroundItemState item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (_items.ContainsKey(item.InstanceId)) throw new InvalidOperationException("Duplicate ground item instance id.");
            _items.Add(item.InstanceId, item);
        }

        internal bool Remove(Guid instanceId) => _items.Remove(instanceId);

        public int RemoveExpired(long nowUnixMilliseconds)
        {
            var expired = new List<Guid>();
            foreach (var pair in _items)
                if (pair.Value.IsExpired(nowUnixMilliseconds)) expired.Add(pair.Key);
            for (var i = 0; i < expired.Count; i++) _items.Remove(expired[i]);
            return expired.Count;
        }
    }

    public readonly struct GroundItemResult
    {
        private GroundItemResult(bool success, string code, Guid groundItemId, ContentId itemId, int quantity)
        {
            Success = success;
            Code = code ?? string.Empty;
            GroundItemId = groundItemId;
            ItemId = itemId;
            Quantity = quantity;
        }

        public bool Success { get; }
        public string Code { get; }
        public Guid GroundItemId { get; }
        public ContentId ItemId { get; }
        public int Quantity { get; }

        public static GroundItemResult Ok(Guid groundItemId, ContentId itemId, int quantity)
            => new GroundItemResult(true, "ok", groundItemId, itemId, quantity);

        public static GroundItemResult Fail(string code)
            => new GroundItemResult(false, code, Guid.Empty, default, 0);
    }

    /// <summary>
    /// Authoritative drop/spawn/take boundary. Loot, manual drops and death-loss can all create the
    /// same ground-item state without trusting client-side GameObjects or collider ownership.
    /// </summary>
    public sealed class GroundItemService
    {
        private readonly IItemRuleSource _itemRules;
        private readonly GroundItemRegistry _registry;

        public GroundItemService(IItemRuleSource itemRules, GroundItemRegistry registry)
        {
            _itemRules = itemRules ?? throw new ArgumentNullException(nameof(itemRules));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public GroundItemState Spawn(
            ContentId itemId,
            int quantity,
            GridLocation location,
            long nowUnixMilliseconds,
            IEnumerable<Guid> protectedRecipients = null,
            long publicAtUnixMilliseconds = 0,
            long expiresAtUnixMilliseconds = 0,
            Guid? instanceId = null)
        {
            if (!_itemRules.TryGetRule(itemId, out _)) throw new ArgumentException("Unknown item id.", nameof(itemId));
            var id = instanceId ?? Guid.NewGuid();
            var state = new GroundItemState(id, itemId, quantity, location, nowUnixMilliseconds,
                protectedRecipients, publicAtUnixMilliseconds, expiresAtUnixMilliseconds);
            _registry.Add(state);
            return state;
        }

        public GroundItemResult DropFromInventory(PlayerState player, int inventorySlot, int requestedQuantity, long nowUnixMilliseconds)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (inventorySlot < 0 || inventorySlot >= player.Inventory.Capacity) return GroundItemResult.Fail("invalid_slot");
            if (requestedQuantity <= 0) return GroundItemResult.Fail("invalid_quantity");
            var stack = player.Inventory.GetSlot(inventorySlot);
            if (stack == null) return GroundItemResult.Fail("empty_slot");
            if (!_itemRules.TryGetRule(stack.ItemId, out _)) return GroundItemResult.Fail("unknown_item");

            var quantity = Math.Min(requestedQuantity, stack.Quantity);
            if (!InventoryRules.RemoveItem(player.Inventory, stack.ItemId, quantity)) return GroundItemResult.Fail("inventory_changed");
            var ground = Spawn(stack.ItemId, quantity, player.Location, nowUnixMilliseconds);
            return GroundItemResult.Ok(ground.InstanceId, ground.ItemId, quantity);
        }

        public GroundItemResult Take(PlayerState player, Guid groundItemId, long nowUnixMilliseconds)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (!_registry.TryGet(groundItemId, out var item)) return GroundItemResult.Fail("ground_item_missing");
            if (item.IsExpired(nowUnixMilliseconds))
            {
                _registry.Remove(groundItemId);
                return GroundItemResult.Fail("ground_item_expired");
            }
            if (!item.CanBeTakenBy(player.CharacterId, nowUnixMilliseconds)) return GroundItemResult.Fail("ground_item_protected");
            if (!player.Location.SameLayer(item.Location)) return GroundItemResult.Fail("wrong_layer");
            if (GridMath.RangeDistance(player.Tile, item.Location.Tile) > 1) return GroundItemResult.Fail("out_of_range");
            if (!_itemRules.TryGetRule(item.ItemId, out var rule)) return GroundItemResult.Fail("unknown_item");

            var capacity = InventoryAddCapacity(player.Inventory, item.ItemId, rule.Stackable);
            if (capacity <= 0) return GroundItemResult.Fail("inventory_full");
            var quantity = Math.Min(item.Quantity, capacity);
            var added = InventoryRules.AddItem(player.Inventory, _itemRules, item.ItemId, quantity);
            if (added <= 0) return GroundItemResult.Fail("inventory_full");

            item.Quantity -= added;
            if (item.Quantity <= 0) _registry.Remove(item.InstanceId);
            return GroundItemResult.Ok(item.InstanceId, item.ItemId, added);
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
