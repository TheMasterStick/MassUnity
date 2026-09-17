using System;
using System.Collections.Generic;
using MassRPG.Core.Content;

namespace MassRPG.Data.Creatures
{
    public interface ICreatureDefinitionSource
    {
        bool TryGet(ContentId creatureId, out CreatureDefinition definition);
    }

    public sealed class CreatureCatalog : ICreatureDefinitionSource
    {
        private readonly Dictionary<ContentId, CreatureDefinition> _definitions = new Dictionary<ContentId, CreatureDefinition>();

        public IEnumerable<CreatureDefinition> All => _definitions.Values;
        public int Count => _definitions.Count;

        public void Register(CreatureDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (_definitions.ContainsKey(definition.Id))
                throw new InvalidOperationException($"Duplicate creature id '{definition.Id}'.");
            _definitions.Add(definition.Id, definition);
        }

        public bool TryGet(ContentId creatureId, out CreatureDefinition definition)
            => _definitions.TryGetValue(creatureId, out definition);
    }
}
