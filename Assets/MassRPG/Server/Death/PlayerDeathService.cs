using System;
using System.Collections.Generic;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Core.World;
using MassRPG.Server.Items;

namespace MassRPG.Server.Death
{
    public enum RespawnPreference
    {
        Home,
        NearestSettlement
    }

    public sealed class PlayerRespawnProfile
    {
        public PlayerRespawnProfile(Guid characterId)
        {
            if (characterId == Guid.Empty) throw new ArgumentException("Character id cannot be empty.", nameof(characterId));
            CharacterId = characterId;
            Preference = RespawnPreference.NearestSettlement;
        }

        public Guid CharacterId { get; }
        public RespawnPreference Preference { get; set; }
        public GridLocation? HomeLocation { get; set; }
    }

    public sealed class PlayerRespawnRegistry
    {
        private readonly Dictionary<Guid, PlayerRespawnProfile> _profiles = new Dictionary<Guid, PlayerRespawnProfile>();

        public PlayerRespawnProfile GetOrCreate(Guid characterId)
        {
            if (!_profiles.TryGetValue(characterId, out var profile))
            {
                profile = new PlayerRespawnProfile(characterId);
                _profiles.Add(characterId, profile);
            }
            return profile;
        }
    }

    public interface ISettlementRespawnSource
    {
        bool TryFindNearest(GridLocation deathLocation, out GridLocation respawnLocation);
    }

    public interface IDeathInventoryDropSelector
    {
        IReadOnlyList<int> SelectInventorySlots(PlayerState player);
    }

    /// <summary>
    /// Deterministic configurable carried-item selector. The live number is deliberately not set
    /// here: balance data chooses how many non-currency inventory slots PvE death should drop.
    /// </summary>
    public sealed class ConfiguredInventoryDropSelector : IDeathInventoryDropSelector
    {
        private readonly int _slotCount;
        private readonly ContentId _currencyId;

        public ConfiguredInventoryDropSelector(int slotCount, ContentId currencyId)
        {
            if (slotCount < 0) throw new ArgumentOutOfRangeException(nameof(slotCount));
            _slotCount = slotCount;
            _currencyId = currencyId;
        }

        public IReadOnlyList<int> SelectInventorySlots(PlayerState player)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            var slots = new List<int>(_slotCount);
            for (var i = 0; i < player.Inventory.Capacity && slots.Count < _slotCount; i++)
            {
                var stack = player.Inventory.GetSlot(i);
                if (stack == null || stack.ItemId == _currencyId) continue;
                slots.Add(i);
            }
            return slots;
        }
    }

    public interface IEquipmentDurabilityLossSink
    {
        void ApplyDeathDurabilityLoss(PlayerState player, int lossBasisPoints);
    }

    /// <summary>
    /// Explicit balance inputs for PvE death. No percentages are hidden in the simulation. Values
    /// are basis points (10000 = 100%) so the eventual live balance can change without code changes.
    /// </summary>
    public sealed class PveDeathPenaltyPolicy
    {
        public PveDeathPenaltyPolicy(
            int currencyLossBasisPoints,
            int durabilityLossBasisPoints,
            long dropProtectionMilliseconds,
            long dropLifetimeMilliseconds)
        {
            if (currencyLossBasisPoints < 0 || currencyLossBasisPoints > 10000) throw new ArgumentOutOfRangeException(nameof(currencyLossBasisPoints));
            if (durabilityLossBasisPoints < 0 || durabilityLossBasisPoints > 10000) throw new ArgumentOutOfRangeException(nameof(durabilityLossBasisPoints));
            if (dropProtectionMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(dropProtectionMilliseconds));
            if (dropLifetimeMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(dropLifetimeMilliseconds));
            CurrencyLossBasisPoints = currencyLossBasisPoints;
            DurabilityLossBasisPoints = durabilityLossBasisPoints;
            DropProtectionMilliseconds = dropProtectionMilliseconds;
            DropLifetimeMilliseconds = dropLifetimeMilliseconds;
        }

        public int CurrencyLossBasisPoints { get; }
        public int DurabilityLossBasisPoints { get; }
        public long DropProtectionMilliseconds { get; }
        public long DropLifetimeMilliseconds { get; }
    }

    public readonly struct PlayerDeathResult
    {
        private PlayerDeathResult(bool success, string code, GridLocation respawnLocation, int currencyLost, int itemStacksDropped)
        {
            Success = success;
            Code = code ?? string.Empty;
            RespawnLocation = respawnLocation;
            CurrencyLost = currencyLost;
            ItemStacksDropped = itemStacksDropped;
        }

        public bool Success { get; }
        public string Code { get; }
        public GridLocation RespawnLocation { get; }
        public int CurrencyLost { get; }
        public int ItemStacksDropped { get; }

        public static PlayerDeathResult Ok(GridLocation location, int currencyLost, int itemStacksDropped)
            => new PlayerDeathResult(true, "respawned", location, currencyLost, itemStacksDropped);
        public static PlayerDeathResult Fail(string code)
            => new PlayerDeathResult(false, code, default, 0, 0);
    }

    /// <summary>
    /// Server-owned PvE death boundary. Carried-item selection, money loss and durability loss are
    /// injected/configured instead of guessing final live percentages during migration.
    /// </summary>
    public sealed class PlayerDeathService
    {
        public static readonly ContentId CoinId = new ContentId("coins");

        private readonly PlayerRespawnRegistry _respawns;
        private readonly ISettlementRespawnSource _settlements;
        private readonly IDeathInventoryDropSelector _drops;
        private readonly PveDeathPenaltyPolicy _policy;
        private readonly GroundItemService _groundItems;
        private readonly IItemRuleSource _itemRules;
        private readonly IEquipmentDurabilityLossSink _durability;

        public PlayerDeathService(
            PlayerRespawnRegistry respawns,
            ISettlementRespawnSource settlements,
            IDeathInventoryDropSelector drops,
            PveDeathPenaltyPolicy policy,
            GroundItemService groundItems,
            IItemRuleSource itemRules,
            IEquipmentDurabilityLossSink durability = null)
        {
            _respawns = respawns ?? throw new ArgumentNullException(nameof(respawns));
            _settlements = settlements ?? throw new ArgumentNullException(nameof(settlements));
            _drops = drops ?? throw new ArgumentNullException(nameof(drops));
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            _groundItems = groundItems ?? throw new ArgumentNullException(nameof(groundItems));
            _itemRules = itemRules ?? throw new ArgumentNullException(nameof(itemRules));
            _durability = durability;
        }

        public PlayerDeathResult ResolvePveDeath(PlayerState player, long nowUnixMilliseconds)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (player.IsAlive) return PlayerDeathResult.Fail("character_not_dead");

            var profile = _respawns.GetOrCreate(player.CharacterId);
            if (!TryResolveRespawn(profile, player.Location, out var respawnLocation))
                return PlayerDeathResult.Fail("no_respawn_location");

            var deathLocation = player.Location;
            var currencyLost = RemoveCurrencyLoss(player);
            var dropped = DropSelectedItems(player, deathLocation, nowUnixMilliseconds);
            if (_policy.DurabilityLossBasisPoints > 0)
                _durability?.ApplyDeathDurabilityLoss(player, _policy.DurabilityLossBasisPoints);

            player.Combat.End();
            player.Movement.Clear();
            player.Production.Clear();
            player.Location = respawnLocation;
            player.CurrentHitpoints = player.MaxHitpoints;
            return PlayerDeathResult.Ok(respawnLocation, currencyLost, dropped);
        }

        private bool TryResolveRespawn(PlayerRespawnProfile profile, GridLocation deathLocation, out GridLocation location)
        {
            if (profile.Preference == RespawnPreference.Home && profile.HomeLocation.HasValue)
            {
                location = profile.HomeLocation.Value;
                return true;
            }

            if (_settlements.TryFindNearest(deathLocation, out location)) return true;
            if (profile.HomeLocation.HasValue)
            {
                location = profile.HomeLocation.Value;
                return true;
            }
            return false;
        }

        private int RemoveCurrencyLoss(PlayerState player)
        {
            var coins = player.Inventory.CountItem(CoinId);
            if (coins <= 0 || _policy.CurrencyLossBasisPoints <= 0) return 0;
            var loss = (int)Math.Floor(coins * (_policy.CurrencyLossBasisPoints / 10000.0));
            if (loss <= 0) return 0;
            return InventoryRules.RemoveItem(player.Inventory, CoinId, loss) ? loss : 0;
        }

        private int DropSelectedItems(PlayerState player, GridLocation deathLocation, long nowUnixMilliseconds)
        {
            var selected = _drops.SelectInventorySlots(player);
            if (selected == null || selected.Count == 0) return 0;

            var unique = new HashSet<int>();
            var snapshots = new List<InventoryStack>();
            for (var i = 0; i < selected.Count; i++)
            {
                var slot = selected[i];
                if (slot < 0 || slot >= player.Inventory.Capacity || !unique.Add(slot)) continue;
                var stack = player.Inventory.GetSlot(slot);
                if (stack == null || stack.ItemId == CoinId) continue;
                snapshots.Add(new InventoryStack(stack.ItemId, stack.Quantity));
            }

            var dropped = 0;
            for (var i = 0; i < snapshots.Count; i++)
            {
                var stack = snapshots[i];
                if (!_itemRules.TryGetRule(stack.ItemId, out _)) continue;
                if (!InventoryRules.RemoveItem(player.Inventory, stack.ItemId, stack.Quantity)) continue;

                var publicAt = _policy.DropProtectionMilliseconds <= 0
                    ? 0
                    : checked(nowUnixMilliseconds + _policy.DropProtectionMilliseconds);
                var expiresAt = _policy.DropLifetimeMilliseconds <= 0
                    ? 0
                    : checked(nowUnixMilliseconds + _policy.DropLifetimeMilliseconds);
                _groundItems.Spawn(
                    stack.ItemId,
                    stack.Quantity,
                    deathLocation,
                    nowUnixMilliseconds,
                    new[] { player.CharacterId },
                    publicAt,
                    expiresAt);
                dropped++;
            }
            return dropped;
        }
    }
}
