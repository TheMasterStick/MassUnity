using System;
using System.Collections.Generic;
using MassRPG.Core.Characters;
using MassRPG.Server.Creatures;

namespace MassRPG.Server.Combat
{
    /// <summary>
    /// World-simulation orchestration boundary for a dead ordinary creature. Settlement must read the
    /// dead creature's authoritative state before CreaturePopulationService removes that materialized
    /// instance from the registry. This coordinator makes that order explicit and reusable by the
    /// local Unity simulation now and a headless server tick later.
    /// </summary>
    public sealed class CombatKillSettlementCoordinator
    {
        private readonly CreatureRegistry _creatures;
        private readonly CreaturePopulationService _populations;
        private readonly CombatKillSettlementService _settlement;
        private readonly ICombatRewardPresenceSource _presence;

        public CombatKillSettlementCoordinator(
            CreatureRegistry creatures,
            CombatKillSettlementService settlement,
            ICombatRewardPresenceSource presence,
            CreaturePopulationService populations = null)
        {
            _creatures = creatures ?? throw new ArgumentNullException(nameof(creatures));
            _settlement = settlement ?? throw new ArgumentNullException(nameof(settlement));
            _presence = presence ?? throw new ArgumentNullException(nameof(presence));
            _populations = populations;
        }

        public CombatKillSettlementResult SettleKilledCreature(
            Guid creatureInstanceId,
            IEnumerable<PlayerState> players,
            long nowUnixMilliseconds,
            Func<double> random01)
        {
            if (creatureInstanceId == Guid.Empty) throw new ArgumentException("Creature instance id cannot be empty.", nameof(creatureInstanceId));
            if (players == null) throw new ArgumentNullException(nameof(players));
            if (random01 == null) throw new ArgumentNullException(nameof(random01));
            if (!_creatures.TryGet(creatureInstanceId, out var creature))
                return CombatKillSettlementResult.Fail("creature_missing_before_settlement");

            // Snapshot the currently connected character objects once so presence calculation and
            // reward delivery resolve against the same authoritative set during this settlement.
            var playerList = new List<PlayerState>();
            var byId = new Dictionary<Guid, PlayerState>();
            foreach (var player in players)
            {
                if (player == null) continue;
                playerList.Add(player);
                byId[player.CharacterId] = player;
            }

            var presences = _presence.BuildPresences(creature, playerList);
            var result = _settlement.Settle(
                creature,
                presences,
                nowUnixMilliseconds,
                random01,
                id => byId.TryGetValue(id, out var player) ? player : null);

            // Ordinary population bookkeeping/removal always happens after the settlement attempt.
            // A failed settlement must not leave a dead ordinary creature occupying its spawn slot.
            _populations?.RecordKillForInstance(creatureInstanceId, nowUnixMilliseconds);
            return result;
        }
    }
}
