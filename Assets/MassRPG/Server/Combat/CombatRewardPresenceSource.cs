using System;
using System.Collections.Generic;
using MassRPG.Core.Characters;
using MassRPG.Core.Combat;
using MassRPG.Core.World;
using MassRPG.Data.Creatures;
using MassRPG.Server.Creatures;

namespace MassRPG.Server.Combat
{
    /// <summary>
    /// Computes which connected characters are currently eligible for distance-based kill rewards.
    /// Contribution thresholds, party sharing and first-claim policy remain separate in
    /// CombatRewardPlanner; this source answers only the spatial presence question.
    /// </summary>
    public interface ICombatRewardPresenceSource
    {
        IReadOnlyList<CombatRewardPresence> BuildPresences(
            CreatureState creature,
            IEnumerable<PlayerState> players);
    }

    /// <summary>
    /// Reward-range implementation using logical grid distance to the nearest occupied footprint tile.
    /// The range is mandatory configuration so migration does not silently invent a live MMO value.
    /// </summary>
    public sealed class DistanceCombatRewardPresenceSource : ICombatRewardPresenceSource
    {
        private readonly ICreatureDefinitionSource _definitions;
        private readonly int _rewardRangeTiles;

        public DistanceCombatRewardPresenceSource(
            ICreatureDefinitionSource definitions,
            int rewardRangeTiles)
        {
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            if (rewardRangeTiles < 0) throw new ArgumentOutOfRangeException(nameof(rewardRangeTiles));
            _rewardRangeTiles = rewardRangeTiles;
        }

        public int RewardRangeTiles => _rewardRangeTiles;

        public IReadOnlyList<CombatRewardPresence> BuildPresences(
            CreatureState creature,
            IEnumerable<PlayerState> players)
        {
            if (creature == null) throw new ArgumentNullException(nameof(creature));
            if (players == null) throw new ArgumentNullException(nameof(players));
            if (!_definitions.TryGet(creature.DefinitionId, out var definition))
                throw new InvalidOperationException("Cannot resolve reward presence for unknown creature definition '" + creature.DefinitionId + "'.");

            var result = new List<CombatRewardPresence>();
            foreach (var player in players)
            {
                if (player == null) continue;
                var closest = CombatGeometry.ClosestTargetTile(player.Location, creature.Anchor, definition.Footprint);
                var inRange = closest.HasValue
                    && GridMath.RangeDistance(player.Location.Tile, closest.Value.Tile) <= _rewardRangeTiles;
                result.Add(new CombatRewardPresence(player.CharacterId, inRange));
            }
            return result;
        }
    }
}
