using System;
using MassRPG.Core.Characters;
using MassRPG.Core.Combat;
using MassRPG.Core.Creatures;
using MassRPG.Core.World;
using MassRPG.Data.Creatures;
using MassRPG.Server.Creatures;

namespace MassRPG.Server.Combat
{
    public sealed class CombatApproachPlanner
    {
        private readonly IGridTraversalMap _movementMap;
        private readonly IRangedLineOfSightMap _lineOfSightMap;

        public CombatApproachPlanner(IGridTraversalMap movementMap, IRangedLineOfSightMap lineOfSightMap)
        {
            _movementMap = movementMap ?? throw new ArgumentNullException(nameof(movementMap));
            _lineOfSightMap = lineOfSightMap ?? throw new ArgumentNullException(nameof(lineOfSightMap));
        }

        public GridPathResult FindApproach(
            PlayerState player,
            CreatureState target,
            CreatureDefinition definition,
            PlayerAttackProfile profile,
            int maxVisitedNodes = 25000)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (!player.Location.SameLayer(target.Anchor)) return GridPathResult.Failed("different_layer", 0);

            var map = new FootprintBlockingMap(_movementMap, target.Anchor, definition.Footprint);
            Func<GridLocation, bool> inAttackPosition = location => IsInAttackPosition(location, target, definition.Footprint, profile);
            return GridPathfinder.FindNearestMatching(map, player.Location, inAttackPosition, maxVisitedNodes);
        }

        public bool IsInAttackPosition(
            GridLocation attacker,
            CreatureState target,
            CreatureFootprint footprint,
            PlayerAttackProfile profile)
        {
            if (profile.Style == CombatStyle.Melee)
                return CombatGeometry.CanMelee(_movementMap, attacker, target.Anchor, footprint);
            return CombatGeometry.CanRangedOrMagic(_lineOfSightMap, attacker, target.Anchor, footprint, profile.RangeTiles);
        }

        private sealed class FootprintBlockingMap : IGridTraversalMap
        {
            private readonly IGridTraversalMap _inner;
            private readonly GridLocation _anchor;
            private readonly CreatureFootprint _footprint;

            public FootprintBlockingMap(IGridTraversalMap inner, GridLocation anchor, CreatureFootprint footprint)
            {
                _inner = inner;
                _anchor = anchor;
                _footprint = footprint;
            }

            public bool IsWalkable(GridLocation location)
                => !_footprint.Contains(_anchor, location) && _inner.IsWalkable(location);

            public int GetLogicalElevation(GridLocation location) => _inner.GetLogicalElevation(location);

            public bool CanTraverseCardinalEdge(GridLocation from, GridLocation to)
                => IsWalkable(to) && _inner.CanTraverseCardinalEdge(from, to);
        }
    }
}
