using System;
using System.Collections.Generic;
using MassRPG.Core.Content;
using MassRPG.Core.World;
using MassRPG.Data.Creatures;

namespace MassRPG.Server.Creatures
{
    public interface ICreatureSpawnPlacementSource
    {
        bool TryChooseSpawnLocation(
            CreatureSpawnRegionDefinition region,
            int materializationOrdinal,
            out GridLocation location);
    }

    /// <summary>
    /// Server-side population manager for ordinary authored spawn regions. Regions may sleep when no
    /// players are nearby without resetting. Population refills from timestamps and ordinary actors
    /// are rematerialized only when the region becomes active again.
    /// </summary>
    public sealed class CreaturePopulationService
    {
        private readonly CreatureRegistry _creatures;
        private readonly ICreatureDefinitionSource _definitions;
        private readonly ICreatureSpawnPlacementSource _placement;
        private readonly CreatureOccupancyIndex _occupancy;
        private readonly Dictionary<ContentId, CreatureSpawnRegionDefinition> _regions = new Dictionary<ContentId, CreatureSpawnRegionDefinition>();
        private readonly Dictionary<ContentId, CreaturePopulationState> _states = new Dictionary<ContentId, CreaturePopulationState>();
        private readonly Dictionary<Guid, ContentId> _instanceRegions = new Dictionary<Guid, ContentId>();

        public CreaturePopulationService(
            CreatureRegistry creatures,
            ICreatureDefinitionSource definitions,
            ICreatureSpawnPlacementSource placement,
            CreatureOccupancyIndex occupancy = null)
        {
            _creatures = creatures ?? throw new ArgumentNullException(nameof(creatures));
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            _placement = placement ?? throw new ArgumentNullException(nameof(placement));
            _occupancy = occupancy;
        }

        public IEnumerable<CreaturePopulationState> States => _states.Values;

        public void RegisterRegion(CreatureSpawnRegionDefinition region)
        {
            if (region == null) throw new ArgumentNullException(nameof(region));
            if (_regions.ContainsKey(region.Id))
                throw new InvalidOperationException($"Duplicate spawn region id '{region.Id}'.");
            if (!_definitions.TryGet(region.CreatureDefinitionId, out var creatureDefinition))
                throw new InvalidOperationException($"Spawn region '{region.Id}' references unknown creature '{region.CreatureDefinitionId}'.");
            if (creatureDefinition.PersistentNamedInstance)
                throw new InvalidOperationException("Persistent named creatures require explicit persistent spawn state, not an ordinary population region.");
            _regions.Add(region.Id, region);
        }

        public bool TryGetState(ContentId regionId, out CreaturePopulationState state)
            => _states.TryGetValue(regionId, out state);

        public bool TryGetSpawnRegion(Guid creatureInstanceId, out ContentId regionId)
            => _instanceRegions.TryGetValue(creatureInstanceId, out regionId);

        public CreaturePopulationState Activate(ContentId regionId, long nowUnixMilliseconds)
        {
            var region = RequireRegion(regionId);
            var state = GetOrCreateState(region);
            ApplyElapsedRespawns(region, state, nowUnixMilliseconds);
            state.IsActive = true;
            MaterializeToPopulation(region, state);
            return state;
        }

        public void Deactivate(ContentId regionId)
        {
            if (!_states.TryGetValue(regionId, out var state)) return;
            foreach (var instanceId in CopyIds(state.MaterializedInstances))
                RemoveMaterialized(instanceId);
            state.ClearMaterialized();
            state.IsActive = false;
        }

        public bool RecordKill(ContentId regionId, Guid creatureInstanceId, long nowUnixMilliseconds)
        {
            var region = RequireRegion(regionId);
            var state = GetOrCreateState(region);
            if (!state.Untrack(creatureInstanceId)) return false;

            RemoveMaterialized(creatureInstanceId);
            if (state.Population > 0) state.Population--;
            if (state.Population < region.PopulationCap && !state.NextRespawnAtUnixMilliseconds.HasValue)
                state.NextRespawnAtUnixMilliseconds = checked(nowUnixMilliseconds + region.RespawnIntervalMilliseconds);
            return true;
        }

        public bool RecordKillForInstance(Guid creatureInstanceId, long nowUnixMilliseconds)
        {
            if (!_instanceRegions.TryGetValue(creatureInstanceId, out var regionId)) return false;
            return RecordKill(regionId, creatureInstanceId, nowUnixMilliseconds);
        }

        public void Advance(long nowUnixMilliseconds)
        {
            foreach (var pair in _states)
            {
                var region = RequireRegion(pair.Key);
                var state = pair.Value;
                ApplyElapsedRespawns(region, state, nowUnixMilliseconds);
                if (state.IsActive) MaterializeToPopulation(region, state);
            }
        }

        private CreaturePopulationState GetOrCreateState(CreatureSpawnRegionDefinition region)
        {
            if (_states.TryGetValue(region.Id, out var state)) return state;
            state = new CreaturePopulationState(region.Id, region.PopulationCap);
            _states.Add(region.Id, state);
            return state;
        }

        private void ApplyElapsedRespawns(
            CreatureSpawnRegionDefinition region,
            CreaturePopulationState state,
            long nowUnixMilliseconds)
        {
            if (state.Population >= region.PopulationCap)
            {
                state.Population = region.PopulationCap;
                state.NextRespawnAtUnixMilliseconds = null;
                return;
            }

            if (!state.NextRespawnAtUnixMilliseconds.HasValue) return;
            var next = state.NextRespawnAtUnixMilliseconds.Value;
            if (nowUnixMilliseconds < next) return;

            var elapsedAfterFirst = nowUnixMilliseconds - next;
            var respawnCount = 1L + elapsedAfterFirst / region.RespawnIntervalMilliseconds;
            var missing = region.PopulationCap - state.Population;
            var applied = (int)Math.Min(respawnCount, missing);
            state.Population += applied;

            if (state.Population >= region.PopulationCap)
            {
                state.Population = region.PopulationCap;
                state.NextRespawnAtUnixMilliseconds = null;
            }
            else
            {
                state.NextRespawnAtUnixMilliseconds = checked(next + applied * region.RespawnIntervalMilliseconds);
            }
        }

        private void MaterializeToPopulation(CreatureSpawnRegionDefinition region, CreaturePopulationState state)
        {
            if (!_definitions.TryGet(region.CreatureDefinitionId, out var definition))
                throw new InvalidOperationException($"Creature definition '{region.CreatureDefinitionId}' disappeared after region registration.");

            var needed = state.Population - state.MaterializedInstances.Count;
            var attempts = 0;
            var maxAttempts = Math.Max(needed * 8, 16);
            while (needed > 0 && attempts < maxAttempts)
            {
                var ordinal = state.MaterializedInstances.Count + attempts;
                attempts++;
                if (!_placement.TryChooseSpawnLocation(region, ordinal, out var location)) break;
                if (location.Plane != region.Plane || location.Storey != region.Storey) continue;
                if (!region.Area.Contains(location.Tile)) continue;

                var instanceId = Guid.NewGuid();
                if (_occupancy != null && !_occupancy.TryPlace(instanceId, location, definition.Footprint)) continue;

                var creature = CreatureState.Spawn(instanceId, definition, location);
                _creatures.Register(creature);
                state.Track(creature.InstanceId);
                _instanceRegions[creature.InstanceId] = region.Id;
                needed--;
            }
        }

        private void RemoveMaterialized(Guid instanceId)
        {
            _occupancy?.Remove(instanceId);
            _creatures.Remove(instanceId);
            _instanceRegions.Remove(instanceId);
        }

        private CreatureSpawnRegionDefinition RequireRegion(ContentId regionId)
        {
            if (!_regions.TryGetValue(regionId, out var region))
                throw new KeyNotFoundException($"Unknown creature spawn region '{regionId}'.");
            return region;
        }

        private static List<Guid> CopyIds(IReadOnlyCollection<Guid> ids)
        {
            var copy = new List<Guid>(ids.Count);
            foreach (var id in ids) copy.Add(id);
            return copy;
        }
    }
}
