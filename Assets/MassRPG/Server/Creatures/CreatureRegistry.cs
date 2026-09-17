using System;
using System.Collections.Generic;

namespace MassRPG.Server.Creatures
{
    public sealed class CreatureRegistry
    {
        private readonly Dictionary<Guid, CreatureState> _instances = new Dictionary<Guid, CreatureState>();

        public IEnumerable<CreatureState> All => _instances.Values;
        public int Count => _instances.Count;

        public void Register(CreatureState creature)
        {
            if (creature == null) throw new ArgumentNullException(nameof(creature));
            if (_instances.ContainsKey(creature.InstanceId))
                throw new InvalidOperationException($"Creature instance '{creature.InstanceId}' is already registered.");
            _instances.Add(creature.InstanceId, creature);
        }

        public bool TryGet(Guid instanceId, out CreatureState creature) => _instances.TryGetValue(instanceId, out creature);
        public bool Remove(Guid instanceId) => _instances.Remove(instanceId);
    }
}
