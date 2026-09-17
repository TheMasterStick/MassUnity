using System;
using System.Collections.Generic;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Core.World;
using MassRPG.Server.Death;
using MassRPG.Server.Items;

namespace MassRPG.Server.Pvp
{
    public sealed class PvpDeathPenaltyPolicy
    {
        public PvpDeathPenaltyPolicy(
            int defenderCurrencyLossBasisPoints,
            int defenderDurabilityLossBasisPoints,
            bool skulledDropAllInventory,
            bool skulledDropAllEquipment,
            long lootProtectionMilliseconds,
            long lootLifetimeMilliseconds)
        {
            if (defenderCurrencyLossBasisPoints < 0 || defenderCurrencyLossBasisPoints > 10000) throw new ArgumentOutOfRangeException(nameof(defenderCurrencyLossBasisPoints));
            if (defenderDurabilityLossBasisPoints < 0 || defenderDurabilityLossBasisPoints > 10000) throw new ArgumentOutOfRangeException(nameof(defenderDurabilityLossBasisPoints));
            if (lootProtectionMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(lootProtectionMilliseconds));
            if (lootLifetimeMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(lootLifetimeMilliseconds));
            DefenderCurrencyLossBasisPoints = defenderCurrencyLossBasisPoints;
            DefenderDurabilityLossBasisPoints = defenderDurabilityLossBasisPoints;
            SkulledDropAllInventory = skulledDropAllInventory;
            SkulledDropAllEquipment = skulledDropAllEquipment;
            LootProtectionMilliseconds = lootProtectionMilliseconds;
            LootLifetimeMilliseconds = lootLifetimeMilliseconds;
        }

        public int DefenderCurrencyLossBasisPoints { get; }
        public int DefenderDurabilityLossBasisPoints { get; }
        public bool SkulledDropAllInventory { get; }
        public bool SkulledDropAllEquipment { get; }
        public long LootProtectionMilliseconds { get; }
        public long LootLifetimeMilliseconds { get; }
    }

    public readonly struct PvpDeathResult
    {
        private PvpDeathResult(bool success, string code, GridLocation respawnLocation, int stacksDropped, int equipmentDropped, int currencyDestroyed)
        {
            Success = success;
            Code = code;
            RespawnLocation = respawnLocation;
            StacksDropped = stacksDropped;
            EquipmentDropped = equipmentDropped;
            CurrencyDestroyed = currencyDestroyed;
        }

        public bool Success { get; }
        public string Code { get; }
        public GridLocation RespawnLocation { get; }
        public int StacksDropped { get; }
        public int EquipmentDropped { get; }
        public int CurrencyDestroyed { get; }

        public static PvpDeathResult Ok(GridLocation respawnLocation, int stacksDropped, int equipmentDropped, int currencyDestroyed)
            => new PvpDeathResult(true, "respawned", respawnLocation, stacksDropped, equipmentDropped, currencyDestroyed);

        public static PvpDeathResult Fail(string code)
            => new PvpDeathResult(false, code, default, 0, 0, 0);
    }

    /// <summary>
    /// PvP death settlement kept separate from ordinary PvE death so live balance can treat a
    /// skulled aggressor and a normal defender differently. Exact percentages remain policy data.
    /// Full-loot mode can include both inventory and equipped items. Loot may be temporarily
    /// protected to the killer without trusting the client to decide ownership.
    /// </summary>
    public sealed class PvpDeathService
    {
        private readonly PlayerRespawnRegistry _respawns;
        private readonly ISettlementRespawnSource _settlements;
        private readonly IDeathInventoryDropSelector _defenderDrops;
        private readonly PvpDeathPenaltyPolicy _policy;
        private readonly GroundItemService _groundItems;
        private readonly IItemRuleSource _itemRules;
        private readonly IEquipmentDurabilityLossSink _durability;

        public PvpDeathService(
            PlayerRespawnRegistry respawns,
            ISettlementRespawnSource settlements,
            IDeathInventoryDropSelector defenderDrops,
            PvpDeathPenaltyPolicy policy,
            GroundItemService groundItems,
            IItemRuleSource itemRules,
            IEquipmentDurabilityLossSink durability = null)
        {
            _respawns = respawns ?? throw new ArgumentNullException(nameof(respawns));
            _settlements = settlements ?? throw new ArgumentNullException(nameof(settlements));
            _defenderDrops = defenderDrops ?? throw new ArgumentNullException(nameof(defenderDrops));
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            _groundItems = groundItems ?? throw new ArgumentNullException(nameof(groundItems));
            _itemRules = itemRules ?? throw new ArgumentNullException(nameof(itemRules));
            _durability = durability;
        }

        public PvpDeathResult ResolveDeath(
            PlayerState defeated,
            bool wasSkulledAggressor,
            Guid? killerCharacterId,
            long nowUnixMilliseconds)
        {
            if (defeated == null) throw new ArgumentNullException(nameof(defeated));
            if (defeated.IsAlive) return PvpDeathResult.Fail("character_not_dead");
            if (!TryResolveRespawn(defeated, out var respawnLocation)) return PvpDeathResult.Fail("no_respawn_location");

            var deathLocation = defeated.Location;
            var protectedRecipients = killerCharacterId.HasValue && killerCharacterId.Value != Guid.Empty
                ? new[] { killerCharacterId.Value }
                : Array.Empty<Guid>();
            var publicAt = _policy.LootProtectionMilliseconds <= 0
                ? 0
                : checked(nowUnixMilliseconds + _policy.LootProtectionMilliseconds);
            var expiresAt = _policy.LootLifetimeMilliseconds <= 0
                ? 0
                : checked(nowUnixMilliseconds + _policy.LootLifetimeMilliseconds);

            var stacksDropped = 0;
            var equipmentDropped = 0;
            var currencyDestroyed = 0;

            if (wasSkulledAggressor)
            {
                if (_policy.SkulledDropAllInventory)
                    stacksDropped += DropEntireInventory(defeated, deathLocation, protectedRecipients, publicAt, expiresAt, nowUnixMilliseconds);
                if (_policy.SkulledDropAllEquipment)
                    equipmentDropped += DropEntireEquipment(defeated, deathLocation, protectedRecipients, publicAt, expiresAt, nowUnixMilliseconds);
            }
            else
            {
                currencyDestroyed = RemoveDefenderCurrencyLoss(defeated);
                stacksDropped += DropDefenderSelectedInventory(defeated, deathLocation, protectedRecipients, publicAt, expiresAt, nowUnixMilliseconds);
                if (_policy.DefenderDurabilityLossBasisPoints > 0)
                    _durability?.ApplyDeathDurabilityLoss(defeated, _policy.DefenderDurabilityLossBasisPoints);
            }

            defeated.Combat.End();
            defeated.Movement.Clear();
            defeated.Production.Clear();
            defeated.Location = respawnLocation;
            defeated.CurrentHitpoints = defeated.MaxHitpoints;
            return PvpDeathResult.Ok(respawnLocation, stacksDropped, equipmentDropped, currencyDestroyed);
        }

        private bool TryResolveRespawn(PlayerState player, out GridLocation location)
        {
            var profile = _respawns.GetOrCreate(player.CharacterId);
            if (profile.Preference == RespawnPreference.Home && profile.HomeLocation.HasValue)
            {
                location = profile.HomeLocation.Value;
                return true;
            }
            if (_settlements.TryFindNearest(player.Location, out location)) return true;
            if (profile.HomeLocation.HasValue)
            {
                location = profile.HomeLocation.Value;
                return true;
            }
            return false;
        }

        private int DropEntireInventory(
            PlayerState player,
            GridLocation location,
            IReadOnlyList<Guid> recipients,
            long publicAt,
            long expiresAt,
            long now)
        {
            var stacks = new List<InventoryStack>();
            for (var i = 0; i < player.Inventory.Capacity; i++)
            {
                var stack = player.Inventory.GetSlot(i);
                if (stack != null) stacks.Add(new InventoryStack(stack.ItemId, stack.Quantity));
            }

            var dropped = 0;
            for (var i = 0; i < stacks.Count; i++)
            {
                var stack = stacks[i];
                if (!InventoryRules.RemoveItem(player.Inventory, stack.ItemId, stack.Quantity)) continue;
                SpawnLoot(stack.ItemId, stack.Quantity, location, recipients, publicAt, expiresAt, now);
                dropped++;
            }
            return dropped;
        }

        private int DropEntireEquipment(
            PlayerState player,
            GridLocation location,
            IReadOnlyList<Guid> recipients,
            long publicAt,
            long expiresAt,
            long now)
        {
            var equipped = new List<KeyValuePair<EquipmentSlot, ContentId>>();
            foreach (var pair in player.Equipment.EquippedItems) equipped.Add(pair);
            var dropped = 0;
            for (var i = 0; i < equipped.Count; i++)
            {
                var pair = equipped[i];
                if (!InventoryRules.TryExtractEquippedItem(player.Equipment, pair.Key, out var itemId)) continue;
                SpawnLoot(itemId, 1, location, recipients, publicAt, expiresAt, now);
                dropped++;
            }
            return dropped;
        }

        private int DropDefenderSelectedInventory(
            PlayerState player,
            GridLocation location,
            IReadOnlyList<Guid> recipients,
            long publicAt,
            long expiresAt,
            long now)
        {
            var selected = _defenderDrops.SelectInventorySlots(player);
            if (selected == null || selected.Count == 0) return 0;
            var seen = new HashSet<int>();
            var stacks = new List<InventoryStack>();
            for (var i = 0; i < selected.Count; i++)
            {
                var slot = selected[i];
                if (slot < 0 || slot >= player.Inventory.Capacity || !seen.Add(slot)) continue;
                var stack = player.Inventory.GetSlot(slot);
                if (stack == null || stack.ItemId == PlayerDeathService.CoinId) continue;
                stacks.Add(new InventoryStack(stack.ItemId, stack.Quantity));
            }

            var dropped = 0;
            for (var i = 0; i < stacks.Count; i++)
            {
                var stack = stacks[i];
                if (!InventoryRules.RemoveItem(player.Inventory, stack.ItemId, stack.Quantity)) continue;
                SpawnLoot(stack.ItemId, stack.Quantity, location, recipients, publicAt, expiresAt, now);
                dropped++;
            }
            return dropped;
        }

        private int RemoveDefenderCurrencyLoss(PlayerState player)
        {
            var coins = player.Inventory.CountItem(PlayerDeathService.CoinId);
            if (coins <= 0 || _policy.DefenderCurrencyLossBasisPoints <= 0) return 0;
            var loss = (int)Math.Floor(coins * (_policy.DefenderCurrencyLossBasisPoints / 10000.0));
            if (loss <= 0) return 0;
            return InventoryRules.RemoveItem(player.Inventory, PlayerDeathService.CoinId, loss) ? loss : 0;
        }

        private void SpawnLoot(
            ContentId itemId,
            int quantity,
            GridLocation location,
            IReadOnlyList<Guid> recipients,
            long publicAt,
            long expiresAt,
            long now)
        {
            if (!_itemRules.TryGetRule(itemId, out _))
                throw new InvalidOperationException("PvP death attempted to drop unknown item '" + itemId + "'.");
            _groundItems.Spawn(itemId, quantity, location, now, recipients, publicAt, expiresAt);
        }
    }
}
