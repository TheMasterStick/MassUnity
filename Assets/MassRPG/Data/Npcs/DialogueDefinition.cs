using System;
using System.Collections.Generic;
using MassRPG.Core.Content;

namespace MassRPG.Data.Npcs
{
    public sealed class DialogueOptionDefinition
    {
        public DialogueOptionDefinition(string id, string text, string nextNodeId = null, ContentId? actionId = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Dialogue option id cannot be empty.", nameof(id));
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Dialogue option text cannot be empty.", nameof(text));
            Id = id;
            Text = text;
            NextNodeId = string.IsNullOrWhiteSpace(nextNodeId) ? null : nextNodeId;
            ActionId = actionId;
        }

        public string Id { get; }
        public string Text { get; }
        public string NextNodeId { get; }
        public ContentId? ActionId { get; }
        public bool EndsConversation => NextNodeId == null;
    }

    public sealed class DialogueNodeDefinition
    {
        private readonly List<DialogueOptionDefinition> _options;

        public DialogueNodeDefinition(string id, string speakerText, IEnumerable<DialogueOptionDefinition> options)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Dialogue node id cannot be empty.", nameof(id));
            if (speakerText == null) throw new ArgumentNullException(nameof(speakerText));
            if (options == null) throw new ArgumentNullException(nameof(options));
            Id = id;
            SpeakerText = speakerText;
            _options = new List<DialogueOptionDefinition>(options);
        }

        public string Id { get; }
        public string SpeakerText { get; }
        public IReadOnlyList<DialogueOptionDefinition> Options => _options;
    }

    public sealed class DialogueDefinition
    {
        private readonly Dictionary<string, DialogueNodeDefinition> _nodes = new Dictionary<string, DialogueNodeDefinition>(StringComparer.Ordinal);

        public DialogueDefinition(ContentId id, string entryNodeId, IEnumerable<DialogueNodeDefinition> nodes)
        {
            if (id.IsEmpty) throw new ArgumentException("Dialogue id cannot be empty.", nameof(id));
            if (string.IsNullOrWhiteSpace(entryNodeId)) throw new ArgumentException("Dialogue entry node cannot be empty.", nameof(entryNodeId));
            if (nodes == null) throw new ArgumentNullException(nameof(nodes));
            Id = id;
            EntryNodeId = entryNodeId;
            foreach (var node in nodes)
            {
                if (node == null) throw new ArgumentException("Dialogue cannot contain a null node.", nameof(nodes));
                if (_nodes.ContainsKey(node.Id)) throw new InvalidOperationException("Duplicate dialogue node id '" + node.Id + "'.");
                _nodes.Add(node.Id, node);
            }
            if (!_nodes.ContainsKey(entryNodeId))
                throw new InvalidOperationException("Dialogue entry node '" + entryNodeId + "' does not exist.");
            ValidateLinks();
        }

        public ContentId Id { get; }
        public string EntryNodeId { get; }
        public IEnumerable<DialogueNodeDefinition> Nodes => _nodes.Values;

        public bool TryGetNode(string nodeId, out DialogueNodeDefinition node)
        {
            if (nodeId == null)
            {
                node = null;
                return false;
            }
            return _nodes.TryGetValue(nodeId, out node);
        }

        private void ValidateLinks()
        {
            foreach (var node in _nodes.Values)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (var i = 0; i < node.Options.Count; i++)
                {
                    var option = node.Options[i];
                    if (!seen.Add(option.Id))
                        throw new InvalidOperationException("Duplicate dialogue option id '" + option.Id + "' in node '" + node.Id + "'.");
                    if (option.NextNodeId != null && !_nodes.ContainsKey(option.NextNodeId))
                        throw new InvalidOperationException("Dialogue option '" + option.Id + "' points to missing node '" + option.NextNodeId + "'.");
                }
            }
        }
    }

    public interface IDialogueDefinitionSource
    {
        bool TryGet(ContentId id, out DialogueDefinition definition);
    }

    public sealed class DialogueCatalog : IDialogueDefinitionSource
    {
        private readonly Dictionary<ContentId, DialogueDefinition> _definitions = new Dictionary<ContentId, DialogueDefinition>();

        public void Register(DialogueDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (_definitions.ContainsKey(definition.Id))
                throw new InvalidOperationException("Duplicate dialogue id '" + definition.Id + "'.");
            _definitions.Add(definition.Id, definition);
        }

        public bool TryGet(ContentId id, out DialogueDefinition definition) => _definitions.TryGetValue(id, out definition);
    }
}
