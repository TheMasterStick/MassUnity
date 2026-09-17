using System;
using System.Collections.Generic;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.World;
using MassRPG.Data.Npcs;

namespace MassRPG.Server.Npcs
{
    public sealed class NpcState
    {
        public NpcState(Guid instanceId, ContentId definitionId, GridLocation location)
        {
            if (instanceId == Guid.Empty) throw new ArgumentException("NPC instance id cannot be empty.", nameof(instanceId));
            if (definitionId.IsEmpty) throw new ArgumentException("NPC definition id cannot be empty.", nameof(definitionId));
            InstanceId = instanceId;
            DefinitionId = definitionId;
            Location = location;
        }

        public Guid InstanceId { get; }
        public ContentId DefinitionId { get; }
        public GridLocation Location { get; set; }
    }

    public sealed class NpcRegistry
    {
        private readonly Dictionary<Guid, NpcState> _instances = new Dictionary<Guid, NpcState>();

        public void Register(NpcState npc)
        {
            if (npc == null) throw new ArgumentNullException(nameof(npc));
            if (_instances.ContainsKey(npc.InstanceId)) throw new InvalidOperationException("Duplicate NPC instance id.");
            _instances.Add(npc.InstanceId, npc);
        }

        public bool TryGet(Guid instanceId, out NpcState npc) => _instances.TryGetValue(instanceId, out npc);
    }

    public sealed class DialogueSession
    {
        internal DialogueSession(Guid characterId, Guid npcInstanceId, ContentId dialogueId, string nodeId)
        {
            CharacterId = characterId;
            NpcInstanceId = npcInstanceId;
            DialogueId = dialogueId;
            NodeId = nodeId;
        }

        public Guid CharacterId { get; }
        public Guid NpcInstanceId { get; }
        public ContentId DialogueId { get; }
        public string NodeId { get; internal set; }
        public bool IsClosed { get; internal set; }
    }

    public readonly struct DialogueAdvanceResult
    {
        public DialogueAdvanceResult(bool success, string code, DialogueNodeDefinition node, ContentId? actionId, bool ended)
        {
            Success = success;
            Code = code;
            Node = node;
            ActionId = actionId;
            Ended = ended;
        }

        public bool Success { get; }
        public string Code { get; }
        public DialogueNodeDefinition Node { get; }
        public ContentId? ActionId { get; }
        public bool Ended { get; }
    }

    /// <summary>
    /// Authoritative NPC interaction boundary. Range/layer validity is server-owned and dialogue
    /// choices advance a data graph rather than trusting client-provided next-node ids.
    /// </summary>
    public sealed class NpcInteractionService
    {
        private readonly INpcDefinitionSource _npcs;
        private readonly IDialogueDefinitionSource _dialogues;
        private readonly NpcRegistry _registry;

        public NpcInteractionService(INpcDefinitionSource npcs, IDialogueDefinitionSource dialogues, NpcRegistry registry)
        {
            _npcs = npcs ?? throw new ArgumentNullException(nameof(npcs));
            _dialogues = dialogues ?? throw new ArgumentNullException(nameof(dialogues));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public bool CanInteract(PlayerState player, NpcState npc)
        {
            if (player == null || npc == null) return false;
            if (!_npcs.TryGet(npc.DefinitionId, out var definition)) return false;
            if (!player.Location.SameLayer(npc.Location)) return false;
            return GridMath.RangeDistance(player.Location.Tile, npc.Location.Tile) <= definition.InteractionRangeTiles;
        }

        public IReadOnlyList<NpcServiceDefinition> GetAvailableServices(PlayerState player, Guid npcInstanceId)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (!_registry.TryGet(npcInstanceId, out var npc) || !CanInteract(player, npc))
                return Array.Empty<NpcServiceDefinition>();
            if (!_npcs.TryGet(npc.DefinitionId, out var definition))
                return Array.Empty<NpcServiceDefinition>();
            return definition.Services;
        }

        public DialogueAdvanceResult BeginDialogue(PlayerState player, Guid npcInstanceId, ContentId dialogueId, out DialogueSession session)
        {
            session = null;
            if (player == null) return new DialogueAdvanceResult(false, "player_missing", null, null, false);
            if (!_registry.TryGet(npcInstanceId, out var npc))
                return new DialogueAdvanceResult(false, "npc_missing", null, null, false);
            if (!CanInteract(player, npc))
                return new DialogueAdvanceResult(false, "out_of_range", null, null, false);
            if (!_npcs.TryGet(npc.DefinitionId, out var npcDefinition))
                return new DialogueAdvanceResult(false, "npc_definition_missing", null, null, false);

            var exposesDialogue = false;
            for (var i = 0; i < npcDefinition.Services.Count; i++)
            {
                var service = npcDefinition.Services[i];
                if (service.Kind == NpcServiceKind.Dialogue && service.TargetId == dialogueId)
                {
                    exposesDialogue = true;
                    break;
                }
            }
            if (!exposesDialogue)
                return new DialogueAdvanceResult(false, "dialogue_not_offered", null, null, false);
            if (!_dialogues.TryGet(dialogueId, out var dialogue))
                return new DialogueAdvanceResult(false, "dialogue_missing", null, null, false);
            if (!dialogue.TryGetNode(dialogue.EntryNodeId, out var entry))
                return new DialogueAdvanceResult(false, "entry_missing", null, null, false);

            session = new DialogueSession(player.CharacterId, npcInstanceId, dialogue.Id, entry.Id);
            return new DialogueAdvanceResult(true, "ok", entry, null, false);
        }

        public DialogueAdvanceResult Choose(PlayerState player, DialogueSession session, string optionId)
        {
            if (player == null) return new DialogueAdvanceResult(false, "player_missing", null, null, false);
            if (session == null || session.IsClosed) return new DialogueAdvanceResult(false, "session_closed", null, null, true);
            if (session.CharacterId != player.CharacterId) return new DialogueAdvanceResult(false, "wrong_character", null, null, false);
            if (!_registry.TryGet(session.NpcInstanceId, out var npc) || !CanInteract(player, npc))
                return new DialogueAdvanceResult(false, "out_of_range", null, null, false);
            if (!_dialogues.TryGet(session.DialogueId, out var dialogue) || !dialogue.TryGetNode(session.NodeId, out var current))
                return new DialogueAdvanceResult(false, "dialogue_state_invalid", null, null, false);

            DialogueOptionDefinition chosen = null;
            for (var i = 0; i < current.Options.Count; i++)
            {
                if (string.Equals(current.Options[i].Id, optionId, StringComparison.Ordinal))
                {
                    chosen = current.Options[i];
                    break;
                }
            }
            if (chosen == null)
                return new DialogueAdvanceResult(false, "option_invalid", current, null, false);

            if (chosen.EndsConversation)
            {
                session.IsClosed = true;
                return new DialogueAdvanceResult(true, "ok", null, chosen.ActionId, true);
            }

            if (!dialogue.TryGetNode(chosen.NextNodeId, out var next))
                return new DialogueAdvanceResult(false, "next_node_missing", current, null, false);
            session.NodeId = next.Id;
            return new DialogueAdvanceResult(true, "ok", next, chosen.ActionId, false);
        }
    }
}
