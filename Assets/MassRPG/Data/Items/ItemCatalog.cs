using System;
using System.Collections.Generic;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Core.Resources;

namespace MassRPG.Data.Items
{
    public interface IItemDefinitionSource
    {
        bool TryGetDefinition(ContentId id, out ItemDefinition definition);
    }

    /// <summary>
    /// Runtime-facing catalog contract. The Unity editor will eventually publish versioned data
    /// into this shape instead of hard-coding content in gameplay assemblies.
    /// </summary>
    public sealed class ItemCatalog : IItemRuleSource, IGatheringToolSource, IItemDefinitionSource
    {
        private readonly Dictionary<ContentId, ItemDefinition> _items = new Dictionary<ContentId, ItemDefinition>();

        public IEnumerable<ItemDefinition> All => _items.Values;
        public int Count => _items.Count;

        public void Register(ItemDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (_items.ContainsKey(definition.Id))
                throw new InvalidOperationException($"Duplicate item id '{definition.Id}'. Stable content ids must be unique.");
            _items.Add(definition.Id, definition);
        }

        public bool TryGetDefinition(ContentId id, out ItemDefinition definition) => _items.TryGetValue(id, out definition);

        public bool TryGetRule(ContentId itemId, out ItemRule rule)
        {
            if (_items.TryGetValue(itemId, out var definition))
            {
                rule = definition.ToRule();
                return true;
            }

            rule = null;
            return false;
        }

        public bool TryGetGatheringTool(ContentId itemId, out GatheringToolRule tool)
        {
            if (_items.TryGetValue(itemId, out var definition) && definition.GatheringToolKind != GatheringToolKind.None)
            {
                tool = new GatheringToolRule(itemId, definition.GatheringToolKind, definition.ToolTier);
                return true;
            }

            tool = default;
            return false;
        }
    }
}
