using System;
using System.Collections.Generic;
using MassRPG.Core.Creatures;
using MassRPG.Core.World;

namespace MassRPG.Server.Creatures
{
    /// <summary>
    /// Soft creature-to-creature occupancy. Players/NPCs are deliberately not represented here.
    /// Creatures prefer separate logical tiles and wait when a narrow route is occupied instead of
    /// permanently turning other creatures into world/path blockers.
    /// </summary>
    public sealed class CreatureOccupancyIndex
    {
        private readonly Dictionary<GridLocation, Guid> _occupied = new Dictionary<GridLocation, Guid>();
        private readonly Dictionary<Guid, List<GridLocation>> _byCreature = new Dictionary<Guid, List<GridLocation>>();

        public bool IsOccupied(GridLocation tile, Guid? ignoreCreature = null)
        {
            if (!_occupied.TryGetValue(tile, out var owner)) return false;
            return !ignoreCreature.HasValue || owner != ignoreCreature.Value;
        }

        public bool CanOccupy(Guid creatureId, GridLocation anchor, CreatureFootprint footprint)
        {
            foreach (var tile in footprint.OccupiedTiles(anchor))
            {
                if (!WorldConstants.IsInsideWorld(tile.Tile)) return false;
                if (IsOccupied(tile, creatureId)) return false;
            }
            return true;
        }

        public bool TryPlace(Guid creatureId, GridLocation anchor, CreatureFootprint footprint)
        {
            if (creatureId == Guid.Empty) throw new ArgumentException("Creature id cannot be empty.", nameof(creatureId));
            if (!CanOccupy(creatureId, anchor, footprint)) return false;

            Remove(creatureId);
            var tiles = new List<GridLocation>(footprint.TileCount);
            foreach (var tile in footprint.OccupiedTiles(anchor))
            {
                _occupied[tile] = creatureId;
                tiles.Add(tile);
            }
            _byCreature[creatureId] = tiles;
            return true;
        }

        public void Remove(Guid creatureId)
        {
            if (!_byCreature.TryGetValue(creatureId, out var tiles)) return;
            for (var i = 0; i < tiles.Count; i++)
            {
                if (_occupied.TryGetValue(tiles[i], out var owner) && owner == creatureId)
                    _occupied.Remove(tiles[i]);
            }
            _byCreature.Remove(creatureId);
        }
    }
}
