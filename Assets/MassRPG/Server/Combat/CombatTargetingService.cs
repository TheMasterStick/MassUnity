using System;
using MassRPG.Core.Characters;
using MassRPG.Core.Combat;
using MassRPG.Data.Creatures;
using MassRPG.Server.Creatures;

namespace MassRPG.Server.Combat
{
    public sealed class CombatTargetingResult
    {
        private CombatTargetingResult(bool success, string code, string message)
        {
            Success = success;
            Code = code;
            Message = message;
        }

        public bool Success { get; }
        public string Code { get; }
        public string Message { get; }
        public static CombatTargetingResult Ok() => new CombatTargetingResult(true, "ok", string.Empty);
        public static CombatTargetingResult Fail(string code, string message) => new CombatTargetingResult(false, code, message);
    }

    /// <summary>
    /// Authoritative click-to-fight target acquisition and approach path planning. The client names
    /// the target only; server rules determine range, LOS and the tile where the player stops.
    /// </summary>
    public sealed class CombatTargetingService
    {
        private readonly CreatureRegistry _creatures;
        private readonly ICreatureDefinitionSource _definitions;
        private readonly IPlayerAttackProfileSource _profiles;
        private readonly CombatApproachPlanner _approach;

        public CombatTargetingService(
            CreatureRegistry creatures,
            ICreatureDefinitionSource definitions,
            IPlayerAttackProfileSource profiles,
            CombatApproachPlanner approach)
        {
            _creatures = creatures ?? throw new ArgumentNullException(nameof(creatures));
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
            _approach = approach ?? throw new ArgumentNullException(nameof(approach));
        }

        public CombatTargetingResult BeginAttack(PlayerState player, Guid creatureInstanceId)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (!_creatures.TryGet(creatureInstanceId, out var creature))
                return CombatTargetingResult.Fail("unknown_creature", "That creature no longer exists.");
            if (!creature.IsAlive)
                return CombatTargetingResult.Fail("creature_dead", "That creature is already dead.");
            if (!_definitions.TryGet(creature.DefinitionId, out var definition))
                return CombatTargetingResult.Fail("unknown_creature_definition", "That creature definition is not published.");
            if (!player.Location.SameLayer(creature.Anchor))
                return CombatTargetingResult.Fail("different_layer", "The creature is on another plane or floor.");

            var profile = _profiles.Resolve(player);
            var path = _approach.FindApproach(player, creature, definition, profile);
            if (!path.Success)
                return CombatTargetingResult.Fail(path.Code, "No valid path into attack range could be found.");

            player.Combat.Begin(creature.InstanceId);
            player.Movement.ReplacePath(path.Steps);
            return CombatTargetingResult.Ok();
        }

        public void Stop(PlayerState player, bool clearCombatApproach = true)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            player.Combat.End();
            if (clearCombatApproach) player.Movement.Clear();
        }
    }
}
