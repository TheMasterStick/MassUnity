using System;
using MassRPG.Core.Characters;
using MassRPG.Core.Creatures;
using MassRPG.Core.World;

namespace MassRPG.Core.Combat
{
    public static class CombatGeometry
    {
        public const double HighGroundAccuracyModifier = 0.05;

        /// <summary>
        /// All melee weapons use one logical tile of reach. Attacker and target must be on the
        /// same logical elevation and a real open adjacent connection must exist.
        /// </summary>
        public static bool CanMelee(IGridTraversalMap map, GridLocation attacker, GridLocation target)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (!attacker.SameLayer(target)) return false;
            if (!GridMath.IsAdjacent(attacker.Tile, target.Tile)) return false;
            if (map.GetLogicalElevation(attacker) != map.GetLogicalElevation(target)) return false;
            return GridTraversal.CanStep(map, attacker, target);
        }

        /// <summary>
        /// Multi-tile creature overload: adjacency to any occupied tile is valid.
        /// </summary>
        public static bool CanMelee(
            IGridTraversalMap map,
            GridLocation attacker,
            GridLocation targetAnchor,
            CreatureFootprint targetFootprint)
        {
            foreach (var targetTile in targetFootprint.OccupiedTiles(targetAnchor))
                if (CanMelee(map, attacker, targetTile)) return true;
            return false;
        }

        /// <summary>
        /// Ranged/magic range uses Chebyshev grid distance: range N reaches N tiles horizontally,
        /// vertically or diagonally. Elevation does not change LOS or range.
        /// </summary>
        public static bool CanRangedOrMagic(
            IRangedLineOfSightMap map,
            GridLocation attacker,
            GridLocation target,
            int rangeTiles)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (rangeTiles < 1) return false;
            if (!attacker.SameLayer(target)) return false;
            if (GridMath.RangeDistance(attacker.Tile, target.Tile) > rangeTiles) return false;
            return GridLineOfSight.HasLineOfSight(map, attacker, target);
        }

        /// <summary>
        /// Multi-tile creature overload: any visible occupied footprint tile can satisfy range/LOS.
        /// </summary>
        public static bool CanRangedOrMagic(
            IRangedLineOfSightMap map,
            GridLocation attacker,
            GridLocation targetAnchor,
            CreatureFootprint targetFootprint,
            int rangeTiles)
        {
            foreach (var targetTile in targetFootprint.OccupiedTiles(targetAnchor))
                if (CanRangedOrMagic(map, attacker, targetTile, rangeTiles)) return true;
            return false;
        }

        public static GridLocation? ClosestTargetTile(
            GridLocation attacker,
            GridLocation targetAnchor,
            CreatureFootprint targetFootprint)
        {
            GridLocation? best = null;
            var bestDistance = int.MaxValue;
            foreach (var tile in targetFootprint.OccupiedTiles(targetAnchor))
            {
                if (!attacker.SameLayer(tile)) continue;
                var distance = GridMath.RangeDistance(attacker.Tile, tile.Tile);
                if (distance >= bestDistance) continue;
                best = tile;
                bestDistance = distance;
            }
            return best;
        }

        /// <summary>
        /// Settled static high-ground rule: ranged/magic attacks get +5% accuracy from any higher
        /// logical elevation and -5% from any lower elevation. The amount does not stack by height.
        /// </summary>
        public static double ElevationAccuracyModifier(CombatStyle style, int attackerElevation, int targetElevation)
        {
            if (style == CombatStyle.Melee || attackerElevation == targetElevation) return 0.0;
            return attackerElevation > targetElevation ? HighGroundAccuracyModifier : -HighGroundAccuracyModifier;
        }

        public static double ApplyElevationAccuracyModifier(
            double baseHitChance,
            CombatStyle style,
            int attackerElevation,
            int targetElevation)
        {
            var adjusted = baseHitChance + ElevationAccuracyModifier(style, attackerElevation, targetElevation);
            return Math.Max(0.0, Math.Min(1.0, adjusted));
        }
    }
}
