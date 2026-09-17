using System;
using System.Collections.Generic;
using MassRPG.Core.Content;

namespace MassRPG.Data.Npcs
{
    public enum NpcServiceKind
    {
        Dialogue,
        Bank,
        Shop,
        Quest,
        FastTravel,
        Production,
        Custom
    }

    public readonly struct NpcServiceDefinition
    {
        public NpcServiceDefinition(NpcServiceKind kind, ContentId targetId)
        {
            if (targetId.IsEmpty) throw new ArgumentException("NPC service target id cannot be empty.", nameof(targetId));
            Kind = kind;
            TargetId = targetId;
        }

        public NpcServiceKind Kind { get; }
        public ContentId TargetId { get; }
    }

    /// <summary>
    /// Engine-independent NPC gameplay definition. Presentation assets are intentionally not stored
    /// here; the Unity client resolves those through the existing presentation-asset bridge.
    /// </summary>
    public sealed class NpcDefinition
    {
        private readonly List<NpcServiceDefinition> _services;

        public NpcDefinition(
            ContentId id,
            string displayName,
            IEnumerable<NpcServiceDefinition> services,
            int interactionRangeTiles = 1)
        {
            if (id.IsEmpty) throw new ArgumentException("NPC id cannot be empty.", nameof(id));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("NPC display name cannot be empty.", nameof(displayName));
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (interactionRangeTiles < 1) throw new ArgumentOutOfRangeException(nameof(interactionRangeTiles));
            Id = id;
            DisplayName = displayName;
            InteractionRangeTiles = interactionRangeTiles;
            _services = new List<NpcServiceDefinition>(services);
        }

        public ContentId Id { get; }
        public string DisplayName { get; }
        public int InteractionRangeTiles { get; }
        public IReadOnlyList<NpcServiceDefinition> Services => _services;
    }

    public interface INpcDefinitionSource
    {
        bool TryGet(ContentId id, out NpcDefinition definition);
    }

    public sealed class NpcCatalog : INpcDefinitionSource
    {
        private readonly Dictionary<ContentId, NpcDefinition> _definitions = new Dictionary<ContentId, NpcDefinition>();

        public IEnumerable<NpcDefinition> All => _definitions.Values;
        public int Count => _definitions.Count;

        public void Register(NpcDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (_definitions.ContainsKey(definition.Id))
                throw new InvalidOperationException("Duplicate NPC id '" + definition.Id + "'.");
            _definitions.Add(definition.Id, definition);
        }

        public bool TryGet(ContentId id, out NpcDefinition definition) => _definitions.TryGetValue(id, out definition);
    }
}
