using System;
using System.Collections.Generic;
using MassRPG.Core.Content;
using MassRPG.Core.World;
using MassRPG.Data.World.Semantics;

namespace MassRPG.Data.Creatures
{
    public enum CreatureRoamingMode
    {
        FreeRoam,
        LocalPatrolArea,
        FixedSpawnPoint,
        PatrolRoute
    }

    /// <summary>
    /// Authored population rule for ordinary creatures. This is simulation/editor data and is not
    /// automatically exposed on the public player map. Population cap is fixed authored population,
    /// not scaled simply because more players stand nearby.
    /// </summary>
    public sealed class CreatureSpawnRegionDefinition
    {
        private readonly List<GridCoord> _patrolRoute = new List<GridCoord>();

        public CreatureSpawnRegionDefinition(
            ContentId id,
            ContentId creatureDefinitionId,
            WorldAreaShape area,
            int plane,
            int storey,
            int populationCap,
            long respawnIntervalMilliseconds,
            CreatureRoamingMode roamingMode = CreatureRoamingMode.FreeRoam,
            IEnumerable<GridCoord> patrolRoute = null)
        {
            if (id.IsEmpty) throw new ArgumentException("Spawn region id cannot be empty.", nameof(id));
            if (creatureDefinitionId.IsEmpty) throw new ArgumentException("Creature definition id cannot be empty.", nameof(creatureDefinitionId));
            if (area == null) throw new ArgumentNullException(nameof(area));
            if (populationCap < 1) throw new ArgumentOutOfRangeException(nameof(populationCap));
            if (respawnIntervalMilliseconds < 1) throw new ArgumentOutOfRangeException(nameof(respawnIntervalMilliseconds));

            Id = id;
            CreatureDefinitionId = creatureDefinitionId;
            Area = area;
            Plane = plane;
            Storey = storey;
            PopulationCap = populationCap;
            RespawnIntervalMilliseconds = respawnIntervalMilliseconds;
            RoamingMode = roamingMode;
            if (patrolRoute != null)
            {
                foreach (var point in patrolRoute)
                {
                    if (!WorldConstants.IsInsideWorld(point)) throw new ArgumentOutOfRangeException(nameof(patrolRoute));
                    _patrolRoute.Add(point);
                }
            }
            if (roamingMode == CreatureRoamingMode.PatrolRoute && _patrolRoute.Count < 2)
                throw new ArgumentException("Patrol-route spawn regions require at least two patrol points.", nameof(patrolRoute));
        }

        public ContentId Id { get; }
        public ContentId CreatureDefinitionId { get; }
        public WorldAreaShape Area { get; }
        public int Plane { get; }
        public int Storey { get; }
        public int PopulationCap { get; set; }
        public long RespawnIntervalMilliseconds { get; set; }
        public CreatureRoamingMode RoamingMode { get; set; }
        public IReadOnlyList<GridCoord> PatrolRoute => _patrolRoute;
    }
}
