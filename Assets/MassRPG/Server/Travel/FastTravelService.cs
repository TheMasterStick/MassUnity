using System;
using System.Collections.Generic;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Data.Travel;

namespace MassRPG.Server.Travel
{
    public interface IFastTravelCostPolicy
    {
        int CostCoins(FastTravelNodeDefinition origin, FastTravelNodeDefinition destination);
    }

    /// <summary>
    /// Optional distance-based policy. Values are explicit data inputs rather than hidden live balance.
    /// Distance uses logical tile distance and can later be replaced by authored route/region costs.
    /// </summary>
    public sealed class DistanceFastTravelCostPolicy : IFastTravelCostPolicy
    {
        public DistanceFastTravelCostPolicy(int baseCoins, int coinsPerDistanceUnit, int distanceUnitTiles)
        {
            if (baseCoins < 0) throw new ArgumentOutOfRangeException(nameof(baseCoins));
            if (coinsPerDistanceUnit < 0) throw new ArgumentOutOfRangeException(nameof(coinsPerDistanceUnit));
            if (distanceUnitTiles <= 0) throw new ArgumentOutOfRangeException(nameof(distanceUnitTiles));
            BaseCoins = baseCoins;
            CoinsPerDistanceUnit = coinsPerDistanceUnit;
            DistanceUnitTiles = distanceUnitTiles;
        }

        public int BaseCoins { get; }
        public int CoinsPerDistanceUnit { get; }
        public int DistanceUnitTiles { get; }

        public int CostCoins(FastTravelNodeDefinition origin, FastTravelNodeDefinition destination)
        {
            if (origin == null) throw new ArgumentNullException(nameof(origin));
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            var dx = (long)destination.Location.Tile.X - origin.Location.Tile.X;
            var dy = (long)destination.Location.Tile.Y - origin.Location.Tile.Y;
            var distance = Math.Sqrt(dx * dx + dy * dy);
            var units = (long)Math.Ceiling(distance / DistanceUnitTiles);
            var total = checked((long)BaseCoins + units * CoinsPerDistanceUnit);
            if (total > int.MaxValue) throw new OverflowException("Fast-travel cost exceeds supported currency range.");
            return (int)total;
        }
    }

    public interface IFastTravelCombatGate
    {
        bool IsTravelBlocked(Guid characterId, long nowUnixMilliseconds, out long blockedUntilUnixMilliseconds);
    }

    /// <summary>
    /// Tracks the settled post-combat travel lock without coupling the travel system to one combat implementation.
    /// Combat/PvP code marks activity whenever a character attacks or is attacked.
    /// </summary>
    public sealed class FastTravelCombatGate : IFastTravelCombatGate
    {
        private readonly Dictionary<Guid, long> _blockedUntil = new Dictionary<Guid, long>();
        private readonly long _postCombatDelayMilliseconds;

        public FastTravelCombatGate(long postCombatDelayMilliseconds)
        {
            if (postCombatDelayMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(postCombatDelayMilliseconds));
            _postCombatDelayMilliseconds = postCombatDelayMilliseconds;
        }

        public void MarkCombatActivity(Guid characterId, long nowUnixMilliseconds)
        {
            if (characterId == Guid.Empty) return;
            _blockedUntil[characterId] = checked(nowUnixMilliseconds + _postCombatDelayMilliseconds);
        }

        public bool IsTravelBlocked(Guid characterId, long nowUnixMilliseconds, out long blockedUntilUnixMilliseconds)
        {
            if (_blockedUntil.TryGetValue(characterId, out blockedUntilUnixMilliseconds)
                && nowUnixMilliseconds < blockedUntilUnixMilliseconds)
                return true;
            blockedUntilUnixMilliseconds = 0;
            return false;
        }
    }

    public sealed class CharacterFastTravelState
    {
        private readonly HashSet<ContentId> _activated = new HashSet<ContentId>();

        public CharacterFastTravelState(Guid characterId)
        {
            if (characterId == Guid.Empty) throw new ArgumentException("Character id cannot be empty.", nameof(characterId));
            CharacterId = characterId;
        }

        public Guid CharacterId { get; }
        public ContentId? OpenOriginNodeId { get; internal set; }
        public long ArrivalProtectionUntilUnixMilliseconds { get; internal set; }
        public IEnumerable<ContentId> ActivatedNodeIds => _activated;

        public bool IsActivated(ContentId nodeId) => _activated.Contains(nodeId);
        internal bool Activate(ContentId nodeId) => _activated.Add(nodeId);

        public bool HasArrivalProtection(long nowUnixMilliseconds)
            => nowUnixMilliseconds < ArrivalProtectionUntilUnixMilliseconds;

        public void ClearArrivalProtection() => ArrivalProtectionUntilUnixMilliseconds = 0;
        public void CloseMap() => OpenOriginNodeId = null;
    }

    public sealed class FastTravelStateRegistry
    {
        private readonly Dictionary<Guid, CharacterFastTravelState> _states = new Dictionary<Guid, CharacterFastTravelState>();

        public CharacterFastTravelState GetOrCreate(Guid characterId)
        {
            if (!_states.TryGetValue(characterId, out var state))
            {
                state = new CharacterFastTravelState(characterId);
                _states.Add(characterId, state);
            }
            return state;
        }
    }

    public readonly struct FastTravelResult
    {
        private FastTravelResult(bool success, string code, int costCoins, long protectionUntilUnixMilliseconds)
        {
            Success = success;
            Code = code ?? string.Empty;
            CostCoins = costCoins;
            ProtectionUntilUnixMilliseconds = protectionUntilUnixMilliseconds;
        }

        public bool Success { get; }
        public string Code { get; }
        public int CostCoins { get; }
        public long ProtectionUntilUnixMilliseconds { get; }

        public static FastTravelResult Ok(string code, int costCoins = 0, long protectionUntilUnixMilliseconds = 0)
            => new FastTravelResult(true, code, costCoins, protectionUntilUnixMilliseconds);
        public static FastTravelResult Fail(string code)
            => new FastTravelResult(false, code, 0, 0);
    }

    /// <summary>
    /// Server-authoritative node activation and travel. Opening the destination map commits no money;
    /// destination selection revalidates combat/location and only then charges and moves the character.
    /// This means being attacked while the map is open cancels safely without a fee.
    /// </summary>
    public sealed class FastTravelService
    {
        public static readonly ContentId CoinId = new ContentId("coins");

        private readonly IFastTravelNodeSource _nodes;
        private readonly IFastTravelCostPolicy _costs;
        private readonly IFastTravelCombatGate _combatGate;
        private readonly FastTravelStateRegistry _states;
        private readonly IItemRuleSource _items;
        private readonly long _arrivalProtectionMilliseconds;

        public FastTravelService(
            IFastTravelNodeSource nodes,
            IFastTravelCostPolicy costs,
            IFastTravelCombatGate combatGate,
            FastTravelStateRegistry states,
            IItemRuleSource items,
            long arrivalProtectionMilliseconds)
        {
            _nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
            _costs = costs ?? throw new ArgumentNullException(nameof(costs));
            _combatGate = combatGate ?? throw new ArgumentNullException(nameof(combatGate));
            _states = states ?? throw new ArgumentNullException(nameof(states));
            _items = items ?? throw new ArgumentNullException(nameof(items));
            if (arrivalProtectionMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(arrivalProtectionMilliseconds));
            _arrivalProtectionMilliseconds = arrivalProtectionMilliseconds;
        }

        public FastTravelResult ActivateCurrentNode(PlayerState player, ContentId nodeId)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (!_nodes.TryGet(nodeId, out var node)) return FastTravelResult.Fail("unknown_node");
            if (player.Location != node.Location) return FastTravelResult.Fail("must_stand_on_node");
            var state = _states.GetOrCreate(player.CharacterId);
            state.Activate(nodeId);
            return FastTravelResult.Ok("activated");
        }

        public FastTravelResult OpenDestinationMap(PlayerState player, ContentId originNodeId, long nowUnixMilliseconds)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            var state = _states.GetOrCreate(player.CharacterId);
            state.CloseMap();
            if (!_nodes.TryGet(originNodeId, out var origin)) return FastTravelResult.Fail("unknown_origin");
            if (!state.IsActivated(originNodeId)) return FastTravelResult.Fail("origin_not_activated");
            if (player.Location != origin.Location) return FastTravelResult.Fail("must_stand_on_origin");
            if (player.Combat.IsActive) return FastTravelResult.Fail("in_combat");
            if (_combatGate.IsTravelBlocked(player.CharacterId, nowUnixMilliseconds, out _))
                return FastTravelResult.Fail("post_combat_delay");

            state.OpenOriginNodeId = originNodeId;
            return FastTravelResult.Ok("map_open");
        }

        public FastTravelResult CommitTravel(PlayerState player, ContentId destinationNodeId, long nowUnixMilliseconds)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            var state = _states.GetOrCreate(player.CharacterId);
            if (!state.OpenOriginNodeId.HasValue) return FastTravelResult.Fail("travel_map_not_open");
            var originId = state.OpenOriginNodeId.Value;

            if (!_nodes.TryGet(originId, out var origin)) { state.CloseMap(); return FastTravelResult.Fail("unknown_origin"); }
            if (!_nodes.TryGet(destinationNodeId, out var destination)) return FastTravelResult.Fail("unknown_destination");
            if (destinationNodeId == originId) return FastTravelResult.Fail("same_node");
            if (!state.IsActivated(destinationNodeId)) return FastTravelResult.Fail("destination_not_activated");
            if (player.Location != origin.Location) { state.CloseMap(); return FastTravelResult.Fail("left_origin"); }
            if (player.Combat.IsActive) { state.CloseMap(); return FastTravelResult.Fail("in_combat"); }
            if (_combatGate.IsTravelBlocked(player.CharacterId, nowUnixMilliseconds, out _))
            {
                state.CloseMap();
                return FastTravelResult.Fail("post_combat_delay");
            }

            int cost;
            try { cost = _costs.CostCoins(origin, destination); }
            catch (OverflowException) { return FastTravelResult.Fail("travel_cost_overflow"); }
            if (cost < 0) return FastTravelResult.Fail("invalid_travel_cost");
            if (player.Inventory.CountItem(CoinId) < cost) return FastTravelResult.Fail("insufficient_coins");
            if (cost > 0 && !InventoryRules.RemoveItem(player.Inventory, CoinId, cost))
                return FastTravelResult.Fail("coins_changed");

            player.Movement.Clear();
            player.Combat.End();
            player.Production.Clear();
            player.Location = destination.Location;
            state.CloseMap();
            state.ArrivalProtectionUntilUnixMilliseconds = checked(nowUnixMilliseconds + _arrivalProtectionMilliseconds);
            return FastTravelResult.Ok("travelled", cost, state.ArrivalProtectionUntilUnixMilliseconds);
        }
    }
}