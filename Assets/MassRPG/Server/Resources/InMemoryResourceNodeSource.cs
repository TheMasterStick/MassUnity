using System.Collections.Generic;
using MassRPG.Core.Resources;

namespace MassRPG.Server.Resources
{
    /// <summary>
    /// Development/vertical-slice node source. The production resolver will combine authored node
    /// placement with deterministic biome-generated resources without requiring all nodes in RAM.
    /// </summary>
    public sealed class InMemoryResourceNodeSource : IResourceNodeSource
    {
        private readonly HashSet<ResourceNodeKey> _nodes = new HashSet<ResourceNodeKey>();

        public int Count => _nodes.Count;
        public bool Register(ResourceNodeKey node) => _nodes.Add(node);
        public bool Remove(ResourceNodeKey node) => _nodes.Remove(node);
        public bool Exists(ResourceNodeKey node) => _nodes.Contains(node);
    }
}
