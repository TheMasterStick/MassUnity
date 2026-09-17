using System;
using System.Collections.Generic;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Core.Skills;
using MassRPG.Core.World;
using MassRPG.Data.Firemaking;

namespace MassRPG.Server.Firemaking
{
    public interface IFirePlacementValidator
    {
        bool CanPlaceCampfire(GridLocation location);
    }

    public sealed class TemporaryCampfireState
    {
        public TemporaryCampfireState(Guid instanceId, Guid ownerCharacterId, GridLocation location, long litAtUnixMilliseconds, long expiresAtUnixMilliseconds)
        {
            InstanceId = instanceId;
            OwnerCharacterId = ownerCharacterId;
            Location = location;
            LitAtUnixMilliseconds = litAtUnixMilliseconds;
            ExpiresAtUnixMilliseconds = expiresAtUnixMilliseconds;
        }

        public Guid InstanceId { get; }
        public Guid OwnerCharacterId { get; }
        public GridLocation Location { get; }
        public long LitAtUnixMilliseconds { get; }
        public long ExpiresAtUnixMilliseconds { get; }
        public bool IsExpired(long nowUnixMilliseconds) => nowUnixMilliseconds >= ExpiresAtUnixMilliseconds;
    }

    public sealed class TemporaryCampfireLedger
    {
        private readonly Dictionary<GridLocation, TemporaryCampfireState> _byLocation = new Dictionary<GridLocation, TemporaryCampfireState>();

        public IEnumerable<TemporaryCampfireState> Active => _byLocation.Values;

        public bool TryGet(GridLocation location, long nowUnixMilliseconds, out TemporaryCampfireState fire)
        {
            if (_byLocation.TryGetValue(location, out fire))
            {
                if (!fire.IsExpired(nowUnixMilliseconds)) return true;
                _byLocation.Remove(location);
            }
            fire = null;
            return false;
        }

        internal void Add(TemporaryCampfireState fire) => _byLocation[fire.Location] = fire;

        public int RemoveExpired(long nowUnixMilliseconds)
        {
            var expired = new List<GridLocation>();
            foreach (var pair in _byLocation)
                if (pair.Value.IsExpired(nowUnixMilliseconds)) expired.Add(pair.Key);
            for (var i = 0; i < expired.Count; i++) _byLocation.Remove(expired[i]);
            return expired.Count;
        }
    }

    public readonly struct FiremakingResult
    {
        private FiremakingResult(bool success, string code, Guid instanceId, long expiresAtUnixMilliseconds)
        {
            Success = success;
            Code = code ?? string.Empty;
            InstanceId = instanceId;
            ExpiresAtUnixMilliseconds = expiresAtUnixMilliseconds;
        }

        public bool Success { get; }
        public string Code { get; }
        public Guid InstanceId { get; }
        public long ExpiresAtUnixMilliseconds { get; }

        public static FiremakingResult Lit(Guid instanceId, long expiresAtUnixMilliseconds)
            => new FiremakingResult(true, "lit", instanceId, expiresAtUnixMilliseconds);

        public static FiremakingResult Fail(string code)
            => new FiremakingResult(false, code, Guid.Empty, 0);
    }

    /// <summary>
    /// Authoritative player-lit campfires. Expiry is timestamp based so unloaded areas do not need
    /// to tick just to burn fires down; presentation can materialize active ledger entries nearby.
    /// </summary>
    public sealed class FiremakingService
    {
        public static readonly ContentId TinderboxId = new ContentId("tinderbox");

        private readonly IFiremakingDefinitionSource _definitions;
        private readonly IItemRuleSource _items;
        private readonly IFirePlacementValidator _placement;
        private readonly TemporaryCampfireLedger _fires;

        public FiremakingService(
            IFiremakingDefinitionSource definitions,
            IItemRuleSource items,
            IFirePlacementValidator placement,
            TemporaryCampfireLedger fires)
        {
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            _items = items ?? throw new ArgumentNullException(nameof(items));
            _placement = placement ?? throw new ArgumentNullException(nameof(placement));
            _fires = fires ?? throw new ArgumentNullException(nameof(fires));
        }

        public FiremakingResult Light(PlayerState player, ContentId logItemId, long nowUnixMilliseconds, Guid? instanceId = null)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (!_definitions.TryGet(logItemId, out var definition)) return FiremakingResult.Fail("invalid_logs");
            if (player.Inventory.CountItem(TinderboxId) <= 0) return FiremakingResult.Fail("tinderbox_required");
            if (player.Inventory.CountItem(logItemId) <= 0) return FiremakingResult.Fail("logs_required");
            if (player.Skills.GetLevel(SkillId.Firemaking) < definition.RequiredLevel) return FiremakingResult.Fail("level_requirement");
            if (!_placement.CanPlaceCampfire(player.Location)) return FiremakingResult.Fail("blocked_location");
            if (_fires.TryGet(player.Location, nowUnixMilliseconds, out _)) return FiremakingResult.Fail("fire_already_here");

            if (!InventoryRules.RemoveItem(player.Inventory, logItemId, 1)) return FiremakingResult.Fail("logs_changed");

            var id = instanceId ?? Guid.NewGuid();
            if (id == Guid.Empty) throw new ArgumentException("Campfire instance id cannot be empty.", nameof(instanceId));
            var expiresAt = checked(nowUnixMilliseconds + definition.LifetimeMilliseconds);
            _fires.Add(new TemporaryCampfireState(id, player.CharacterId, player.Location, nowUnixMilliseconds, expiresAt));
            player.Skills.AddXp(SkillId.Firemaking, definition.Experience);
            return FiremakingResult.Lit(id, expiresAt);
        }
    }
}
