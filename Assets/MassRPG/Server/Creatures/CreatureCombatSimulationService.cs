using System;
using System.Collections.Generic;
using MassRPG.Core.Characters;
using MassRPG.Core.Combat;
using MassRPG.Core.Creatures;
using MassRPG.Core.Skills;
using MassRPG.Core.World;
using MassRPG.Data.Creatures;

namespace MassRPG.Server.Creatures
{
    public enum CreatureAdvanceKind
    {
        Idle,
        AcquiredTarget,
        Moved,
        WaitingForSpace,
        WaitingForCooldown,
        Attacked,
        TargetKilled,
        GaveUp,
        Failed
    }

    public readonly struct CreatureAdvanceResult
    {
        public CreatureAdvanceResult(CreatureAdvanceKind kind, string code, int damage = 0, double hitChance = 0.0, bool hit = false)
        {
            Kind = kind;
            Code = code ?? string.Empty;
            Damage = damage;
            HitChance = hitChance;
            Hit = hit;
        }

        public CreatureAdvanceKind Kind { get; }
        public string Code { get; }
        public int Damage { get; }
        public double HitChance { get; }
        public bool Hit { get; }
    }

    /// <summary>
    /// First server-side creature combat brain. Aggressive creatures acquire nearby players;
    /// neutral creatures enter through retaliation state; passive creatures never attack. Chasing
    /// uses the side-preserving soft-occupancy planner and gives up outside the authored leash.
    /// </summary>
    public sealed class CreatureCombatSimulationService
    {
        private readonly ICreatureDefinitionSource _definitions;
        private readonly IPlayerAttackProfileSource _playerProfiles;
        private readonly CreatureCombatMovementPlanner _movement;
        private readonly IGridTraversalMap _world;

        public CreatureCombatSimulationService(
            ICreatureDefinitionSource definitions,
            IPlayerAttackProfileSource playerProfiles,
            CreatureCombatMovementPlanner movement,
            IGridTraversalMap world)
        {
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            _playerProfiles = playerProfiles ?? throw new ArgumentNullException(nameof(playerProfiles));
            _movement = movement ?? throw new ArgumentNullException(nameof(movement));
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        public CreatureAdvanceResult TryAcquireAggro(CreatureState creature, IEnumerable<PlayerState> players)
        {
            if (creature == null) throw new ArgumentNullException(nameof(creature));
            if (players == null) throw new ArgumentNullException(nameof(players));
            if (!creature.IsAlive || creature.TargetCharacterId.HasValue)
                return new CreatureAdvanceResult(CreatureAdvanceKind.Idle, "no_acquisition_needed");
            if (!_definitions.TryGet(creature.DefinitionId, out var definition))
                return new CreatureAdvanceResult(CreatureAdvanceKind.Failed, "unknown_creature_definition");
            if (definition.Disposition != CreatureDisposition.Aggressive)
                return new CreatureAdvanceResult(CreatureAdvanceKind.Idle, "not_aggressive");

            PlayerState best = null;
            var bestDistance = int.MaxValue;
            foreach (var player in players)
            {
                if (player == null || !player.IsAlive || !creature.Anchor.SameLayer(player.Location)) continue;
                var distance = definition.Footprint.RangeDistanceTo(creature.Anchor, player.Location);
                if (distance > definition.AggroRadiusTiles || distance >= bestDistance) continue;
                if (GridMath.RangeDistance(creature.HomeAnchor.Tile, player.Location.Tile) > definition.LeashRadiusTiles) continue;
                best = player;
                bestDistance = distance;
            }

            if (best == null) return new CreatureAdvanceResult(CreatureAdvanceKind.Idle, "no_target_in_aggro_range");
            creature.TargetCharacterId = best.CharacterId;
            return new CreatureAdvanceResult(CreatureAdvanceKind.AcquiredTarget, "target_acquired");
        }

        public CreatureAdvanceResult Advance(
            CreatureState creature,
            PlayerState target,
            long nowUnixMilliseconds,
            Func<double> random01)
        {
            if (creature == null) throw new ArgumentNullException(nameof(creature));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (random01 == null) throw new ArgumentNullException(nameof(random01));
            if (!creature.IsAlive) return new CreatureAdvanceResult(CreatureAdvanceKind.Idle, "creature_dead");
            if (!_definitions.TryGet(creature.DefinitionId, out var definition))
                return new CreatureAdvanceResult(CreatureAdvanceKind.Failed, "unknown_creature_definition");
            if (definition.Disposition == CreatureDisposition.Passive)
            {
                creature.TargetCharacterId = null;
                return new CreatureAdvanceResult(CreatureAdvanceKind.Idle, "passive_creature");
            }
            if (!target.IsAlive || !creature.Anchor.SameLayer(target.Location))
            {
                creature.TargetCharacterId = null;
                return new CreatureAdvanceResult(CreatureAdvanceKind.GaveUp, "target_unavailable");
            }

            if (GridMath.RangeDistance(creature.HomeAnchor.Tile, target.Location.Tile) > definition.LeashRadiusTiles)
            {
                creature.TargetCharacterId = null;
                return new CreatureAdvanceResult(CreatureAdvanceKind.GaveUp, "leash_exceeded");
            }

            if (!_movement.IsInAttackPosition(creature, definition, target))
            {
                var path = _movement.FindApproach(creature, definition, target);
                if (!path.Success || path.Steps.Count == 0)
                    return new CreatureAdvanceResult(CreatureAdvanceKind.WaitingForSpace, path.Code);
                if (!_movement.TryAdvanceOneStep(creature, definition, path.Steps[0]))
                    return new CreatureAdvanceResult(CreatureAdvanceKind.WaitingForSpace, "soft_blocked");
                return new CreatureAdvanceResult(CreatureAdvanceKind.Moved, "approaching_target");
            }

            if (nowUnixMilliseconds < creature.NextAttackAtUnixMilliseconds)
                return new CreatureAdvanceResult(CreatureAdvanceKind.WaitingForCooldown, "attack_cooldown");

            var defender = _playerProfiles.Resolve(target);
            var attackRoll = CombatMath.AttackRoll(definition.AttackLevel, definition.AttackBonus);
            var defenceRoll = CombatMath.DefenceRoll(target.Skills.GetLevel(SkillId.Defence), defender.DefenceBonus);
            var hitChance = CombatMath.HitChance(attackRoll, defenceRoll);
            var attackerElevation = _world.GetLogicalElevation(creature.Anchor);
            var targetElevation = _world.GetLogicalElevation(target.Location);
            hitChance = CombatGeometry.ApplyElevationAccuracyModifier(
                hitChance,
                definition.CombatStyle,
                attackerElevation,
                targetElevation);

            var hit = random01() <= hitChance;
            var maxHit = ResolveMaxHit(definition);
            var damage = hit ? (int)Math.Floor(random01() * (maxHit + 1)) : 0;
            if (damage < 0) damage = 0;
            if (damage > target.CurrentHitpoints) damage = target.CurrentHitpoints;
            target.CurrentHitpoints -= damage;
            creature.NextAttackAtUnixMilliseconds = checked(nowUnixMilliseconds + definition.AttackIntervalMilliseconds);

            if (!target.IsAlive)
            {
                target.Combat.End();
                target.Movement.Clear();
                target.Production.Clear();
                creature.TargetCharacterId = null;
                return new CreatureAdvanceResult(CreatureAdvanceKind.TargetKilled, "target_killed", damage, hitChance, hit);
            }

            return new CreatureAdvanceResult(CreatureAdvanceKind.Attacked, "attack_resolved", damage, hitChance, hit);
        }

        private static int ResolveMaxHit(CreatureDefinition definition)
        {
            switch (definition.CombatStyle)
            {
                case CombatStyle.Ranged:
                    return CombatMath.MaxHitRanged(definition.StrengthLevel, definition.StrengthBonus);
                case CombatStyle.Magic:
                    return CombatMath.MaxHitMagic(definition.StrengthLevel);
                default:
                    return CombatMath.MaxHitMelee(definition.StrengthLevel, definition.StrengthBonus);
            }
        }
    }
}
