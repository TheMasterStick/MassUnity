using System;
using System.Collections.Generic;
using MassRPG.Core.Content;
using MassRPG.Core.World;

namespace MassRPG.Data.Travel
{
    /// <summary>Published sparse fast-travel node. All activated nodes are mutually reachable.</summary>
    public sealed class FastTravelNodeDefinition
    {
        public FastTravelNodeDefinition(ContentId id, string displayName, GridLocation location)
        {
            if (id.IsEmpty) throw new ArgumentException("Travel node id cannot be empty.", nameof(id));
            Id = id;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? id.Value : displayName;
            Location = location;
        }

        public ContentId Id { get; }
        public string DisplayName { get; set; }
        public GridLocation Location { get; set; }
    }

    public interface IFastTravelNodeSource
    {
        bool TryGet(ContentId nodeId, out FastTravelNodeDefinition node);
        IEnumerable<FastTravelNodeDefinition> All { get; }
    }

    public sealed class FastTravelNodeCatalog : IFastTravelNodeSource
    {
        private readonly Dictionary<ContentId, FastTravelNodeDefinition> _nodes = new Dictionary<ContentId, FastTravelNodeDefinition>();

        public IEnumerable<FastTravelNodeDefinition> All => _nodes.Values;

        public void Register(FastTravelNodeDefinition node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (_nodes.ContainsKey(node.Id)) throw new InvalidOperationException($"Duplicate fast travel node id '{node.Id}'.");
            _nodes.Add(node.Id, node);
        }

        public bool TryGet(ContentId nodeId, out FastTravelNodeDefinition node) => _nodes.TryGetValue(nodeId, out node);
    }
}
