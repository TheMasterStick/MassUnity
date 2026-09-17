using System;
using MassRPG.Core.Characters;
using MassRPG.Core.Combat;
using MassRPG.Core.Creatures;
using MassRPG.Core.World;
using MassRPG.Data.Creatures;

namespace MassRPG.Server.Creatures
{
    /// <summary>
    /// Plans creature approach paths without teaching mobs to tactically circle a target merely
    /// because the side they came from is crowded. Final attack positions stay on the creature's
    /// approach sector; if those positions are occupied, trailing creatures naturally wait/queue.
    /// </summary>
    public sealed class CreatureCombatMovementPlanner
    {
        private readonly IGridTraversalMap _world;
        private readonly IRangedLineOfSightMap _lineOfSight;
        private readonly CreatureOccupancyIndex _occupancy;

        public CreatureCombatMovementPlanner(
            IGridTraversalMap world,
            IRangedLineOfSightMap lineOfSight,
            CreatureOccupancyIndex occupancy)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _lineOfSight = lineOfSight ?? throw new ArgumentNullException(nameof(lineOfSight));
            _occupancy = occupancy ?? throw new ArgumentNullException(nameof(occupancy));
        }

        public bool IsInAttackPosition(CreatureState creature, CreatureDefinition definition, PlayerState target)
        {
            if (creature == null) throw new ArgumentNullException(nameof(creature));
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (!creature.Anchor.SameLayer(target.Location)) return false;

            foreach (var occupiedTile in definition.Footprint.OccupiedTiles(creature.Anchor))
            {
                if (definition.CombatStyle == CombatStyle.Melee)
                {
                    if (CombatGeometry.CanMelee(_world, occupiedTile, target.Location)) return true;
                }
                else if (CombatGeometry.CanRangedOrMagic(
                    _lineOfSight,
                    occupiedTile,
                    target.Location,
                    definition.AttackRangeTiles))
                {
                    return true;
                }
            }

            return false;
        }

        public GridPathResult FindApproach(
            CreatureState creature,
            CreatureDefinition definition,
            PlayerState target,
            int maxVisitedNodes = 10000)
        {
            if (creature == null) throw new ArgumentNullException(nameof(creature));
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (!creature.Anchor.SameLayer(target.Location)) return GridPathResult.Failed("different_layer", 0);

            var sector = ApproachSector.From(creature.Anchor.Tile, target.Location.Tile);
            var map = new CreatureAnchorTraversalMap(_world, _occupancy, creature.InstanceId, definition.Footprint);

            return GridPathfinder.FindNearestMatching(
                map,
                creature.Anchor,
                candidate => sector.Contains(candidate.Tile, target.Location.Tile)
                    && IsAttackPositionAtAnchor(candidate, definition, target),
                maxVisitedNodes);
        }

        public bool TryAdvanceOneStep(CreatureState creature, CreatureDefinition definition, GridLocation nextAnchor)
        {
            if (creature == null) throw new ArgumentNullException(nameof(creature));
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            var map = new CreatureAnchorTraversalMap(_world, _occupancy, creature.InstanceId, definition.Footprint);
            if (!GridTraversal.CanStep(map, creature.Anchor, nextAnchor)) return false;
            if (!_occupancy.TryPlace(creature.InstanceId, nextAnchor, definition.Footprint)) return false;
            creature.Anchor = nextAnchor;
            return true;
        }

        private bool IsAttackPositionAtAnchor(
            GridLocation anchor,
            CreatureDefinition definition,
            PlayerState target)
        {
            foreach (var occupiedTile in definition.Footprint.OccupiedTiles(anchor))
            {
                if (definition.CombatStyle == CombatStyle.Melee)
                {
                    if (CombatGeometry.CanMelee(_world, occupiedTile, target.Location)) return true;
                }
                else if (CombatGeometry.CanRangedOrMagic(
                    _lineOfSight,
                    occupiedTile,
                    target.Location,
                    definition.AttackRangeTiles))
                {
                    return true;
                }
            }
            return false;
        }

        private sealed class CreatureAnchorTraversalMap : IGridTraversalMap
        {
            private readonly IGridTraversalMap _inner;
            private readonly CreatureOccupancyIndex _occupancy;
            private readonly Guid _creatureId;
            private readonly CreatureFootprint _footprint;

            public CreatureAnchorTraversalMap(
                IGridTraversalMap inner,
                CreatureOccupancyIndex occupancy,
                Guid creatureId,
                CreatureFootprint footprint)
            {
                _inner = inner;
                _occupancy = occupancy;
                _creatureId = creatureId;
                _footprint = footprint;
            }

            public bool IsWalkable(GridLocation anchor)
            {
                if (!_occupancy.CanOccupy(_creatureId, anchor, _footprint)) return false;
                int? elevation = null;
                foreach (var tile in _footprint.OccupiedTiles(anchor))
                {
                    if (!_inner.IsWalkable(tile)) return false;
                    var tileElevation = _inner.GetLogicalElevation(tile);
                    if (!elevation.HasValue) elevation = tileElevation;
                    else if (elevation.Value != tileElevation) return false;
                }
                return true;
            }

            public int GetLogicalElevation(GridLocation location) => _inner.GetLogicalElevation(location);

            public bool CanTraverseCardinalEdge(GridLocation from, GridLocation to)
            {
                if (!IsWalkable(to)) return false;
                for (var y = 0; y < _footprint.Height; y++)
                {
                    for (var x = 0; x < _footprint.Width; x++)
                    {
                        var fromTile = new GridLocation(
                            new GridCoord(from.Tile.X + x, from.Tile.Y + y), from.Plane, from.Storey);
                        var toTile = new GridLocation(
                            new GridCoord(to.Tile.X + x, to.Tile.Y + y), to.Plane, to.Storey);
                        if (!_inner.CanTraverseCardinalEdge(fromTile, toTile)) return false;
                    }
                }
                return true;
            }
        }

        private readonly struct ApproachSector
        {
            private ApproachSector(int xSign, int ySign)
            {
                XSign = xSign;
                YSign = ySign;
            }

            private int XSign { get; }
            private int YSign { get; }

            public static ApproachSector From(GridCoord creature, GridCoord target)
                => new ApproachSector(Math.Sign(creature.X - target.X), Math.Sign(creature.Y - target.Y));

            public bool Contains(GridCoord candidate, GridCoord target)
            {
                var dx = candidate.X - target.X;
                var dy = candidate.Y - target.Y;

                if (XSign != 0 && YSign == 0)
                    return XSign > 0 ? dx > 0 : dx < 0;
                if (YSign != 0 && XSign == 0)
                    return YSign > 0 ? dy > 0 : dy < 0;

                if (XSign > 0 && dx < 0) return false;
                if (XSign < 0 && dx > 0) return false;
                if (YSign > 0 && dy < 0) return false;
                if (YSign < 0 && dy > 0) return false;

                // A diagonal approach may use the diagonal itself or either neighboring cardinal
                // side (for example NE can settle at N, NE or E), but not the far side of target.
                return dx != 0 || dy != 0;
            }
        }
    }
}
