using System;
using System.Collections.Generic;
using MassRPG.Core.Content;
using MassRPG.Core.Resources;

namespace MassRPG.Data.Resources
{
    public sealed class ResourceCatalog : IResourceRuleSource
    {
        private readonly Dictionary<ContentId, ResourceDefinition> _definitions = new Dictionary<ContentId, ResourceDefinition>();

        public IEnumerable<ResourceDefinition> All => _definitions.Values;
        public int Count => _definitions.Count;

        public void Register(ResourceDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (_definitions.ContainsKey(definition.Id))
                throw new InvalidOperationException($"Duplicate resource id '{definition.Id}'.");
            _definitions.Add(definition.Id, definition);
        }

        public bool TryGet(ContentId id, out ResourceDefinition definition) => _definitions.TryGetValue(id, out definition);

        public bool TryGetRule(ContentId resourceId, out ResourceGatheringRule rule)
        {
            if (_definitions.TryGetValue(resourceId, out var definition))
            {
                rule = definition.ToRule();
                return true;
            }

            rule = null;
            return false;
        }
    }
}
