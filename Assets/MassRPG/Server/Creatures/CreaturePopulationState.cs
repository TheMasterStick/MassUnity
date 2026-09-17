using System;
using System.Collections.Generic;
using MassRPG.Core.Content;

namespace MassRPG.Server.Creatures
{
    /// <summary>
    /// Compact persistent state for one ordinary spawn population. Exact ordinary creature
    /// positions/identities are intentionally discarded while sleeping; population and respawn
    /// timing survive chunk/interest unloads.
    /// </summary>
    public sealed class CreaturePopulationState
    {
        private readonly HashSet<Guid> _materializedInstances = new HashSet<Guid>();

        public CreaturePopulationState(ContentId spawnRegionId, int initialPopulation)
        {
            if (spawnRegionId.IsEmpty) throw new ArgumentException("Spawn region id cannot be empty.", nameof(spawnRegionId));
            if (initialPopulation < 0) throw new ArgumentOutOfRangeException(nameof(initialPopulation));
            SpawnRegionId = spawnRegionId;
            Population = initialPopulation;
        }

        public ContentId SpawnRegionId { get; }
        public int Population { get; internal set; }
        public long? NextRespawnAtUnixMilliseconds { get; internal set; }
        public bool IsActive { get; internal set; }
        public IReadOnlyCollection<Guid> MaterializedInstances => _materializedInstances;

        internal bool Track(Guid instanceId) => _materializedInstances.Add(instanceId);
        internal bool Untrack(Guid instanceId) => _materializedInstances.Remove(instanceId);
        internal void ClearMaterialized() => _materializedInstances.Clear();
    }
}
