using System;
using System.Collections.Generic;
using MassRPG.Core.Resources;
using MassRPG.Core.World;

namespace MassRPG.Core.Interactions
{
    public enum InteractionTargetKind
    {
        CombatCreature,
        Npc,
        GameplayObject,
        Resource,
        GroundItem,
        OtherPlayer,
        LocalPlayer,
        MovementTile
    }

    public enum InteractionActionKind
    {
        Attack,
        Talk,
        Trade,
        Use,
        Gather,
        Take,
        Follow,
        WalkHere,
        Examine,
        Custom
    }

    /// <summary>
    /// Logical interaction target. This deliberately contains no collider/GameObject reference:
    /// Unity physics can help discover what the cursor points at, but authoritative target identity
    /// and ordering stay in the logical interaction layer.
    /// </summary>
    public sealed class InteractionTarget
    {
        public InteractionTarget(
            string targetId,
            InteractionTargetKind kind,
            GridLocation location,
            string displayName,
            bool hostile = false,
            bool inCombat = false,
            bool currentCombatTarget = false,
            Guid? instanceId = null,
            ResourceNodeKey? resourceNode = null)
        {
            if (string.IsNullOrWhiteSpace(targetId)) throw new ArgumentException("Interaction target id cannot be empty.", nameof(targetId));
            if (instanceId.HasValue && instanceId.Value == Guid.Empty)
                throw new ArgumentException("Interaction instance id cannot be empty when supplied.", nameof(instanceId));
            if (resourceNode.HasValue && resourceNode.Value.Location != location)
                throw new ArgumentException("Resource-node identity must match the interaction target location.", nameof(resourceNode));

            TargetId = targetId;
            Kind = kind;
            Location = location;
            DisplayName = displayName ?? string.Empty;
            Hostile = hostile;
            InCombat = inCombat;
            CurrentCombatTarget = currentCombatTarget;
            InstanceId = instanceId;
            ResourceNode = resourceNode;
        }

        public string TargetId { get; }
        public InteractionTargetKind Kind { get; }
        public GridLocation Location { get; }
        public string DisplayName { get; }
        public bool Hostile { get; }
        public bool InCombat { get; }
        public bool CurrentCombatTarget { get; }
        public Guid? InstanceId { get; }
        public ResourceNodeKey? ResourceNode { get; }
    }

    public readonly struct InteractionOption
    {
        public InteractionOption(
            InteractionTarget target,
            InteractionActionKind action,
            string label,
            bool canBeDefaultAction = true)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
            Action = action;
            Label = label ?? string.Empty;
            CanBeDefaultAction = canBeDefaultAction;
        }

        public InteractionTarget Target { get; }
        public InteractionActionKind Action { get; }
        public string Label { get; }
        public bool CanBeDefaultAction { get; }
    }

    /// <summary>
    /// Shared ordering rules for left-click defaults, right-click stacks and actor visibility.
    /// This encodes the settled MassRPG crowd behavior rather than relying on whichever collider
    /// happened to be hit first by Unity.
    /// </summary>
    public static class InteractionPriority
    {
        /// <summary>
        /// Picks the sensible left-click action. Current/hostile combat targets come first, then
        /// NPCs, ordinary creatures, objects/resources, ground items, other players and movement.
        /// Examine-only entries are never selected as the default action.
        /// </summary>
        public static bool TryResolveDefault(IReadOnlyList<InteractionOption> options, out InteractionOption resolved)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            var found = false;
            var bestRank = int.MaxValue;
            var bestActionRank = int.MaxValue;
            var bestIndex = int.MaxValue;
            resolved = default;

            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                if (!option.CanBeDefaultAction || option.Action == InteractionActionKind.Examine) continue;

                var targetRank = DefaultTargetRank(option.Target);
                var actionRank = PrimaryActionRank(option.Action);
                if (!found
                    || targetRank < bestRank
                    || (targetRank == bestRank && actionRank < bestActionRank)
                    || (targetRank == bestRank && actionRank == bestActionRank && i < bestIndex))
                {
                    found = true;
                    bestRank = targetRank;
                    bestActionRank = actionRank;
                    bestIndex = i;
                    resolved = option;
                }
            }

            return found;
        }

        /// <summary>
        /// Produces the complete right-click menu ordering without throwing any overlapping target
        /// away. Primary actions are ordered by target importance, then Walk Here, then Examine.
        /// The presentation layer may append Cancel after this list.
        /// </summary>
        public static IReadOnlyList<InteractionOption> BuildContextStack(IReadOnlyList<InteractionOption> options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            var indexed = new List<IndexedOption>(options.Count);
            for (var i = 0; i < options.Count; i++)
                indexed.Add(new IndexedOption(options[i], i));

            indexed.Sort((left, right) =>
            {
                var compare = ContextRank(left.Option).CompareTo(ContextRank(right.Option));
                return compare != 0 ? compare : left.SourceIndex.CompareTo(right.SourceIndex);
            });

            var result = new InteractionOption[indexed.Count];
            for (var i = 0; i < indexed.Count; i++) result[i] = indexed[i].Option;
            return result;
        }

        /// <summary>
        /// Actor presentation priority for crowded tiles. Lower values should remain visually on top:
        /// hostile/in-combat creature -> NPC -> local player -> passive/neutral creature -> other player.
        /// </summary>
        public static int ActorVisualRank(InteractionTarget target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            switch (target.Kind)
            {
                case InteractionTargetKind.CombatCreature:
                    return target.Hostile || target.InCombat || target.CurrentCombatTarget ? 0 : 30;
                case InteractionTargetKind.Npc:
                    return 10;
                case InteractionTargetKind.LocalPlayer:
                    return 20;
                case InteractionTargetKind.OtherPlayer:
                    return 40;
                default:
                    return 100;
            }
        }

        private static int DefaultTargetRank(InteractionTarget target)
        {
            switch (target.Kind)
            {
                case InteractionTargetKind.CombatCreature:
                    if (target.CurrentCombatTarget) return 0;
                    if (target.Hostile || target.InCombat) return 1;
                    // A neutral/passive creature is still attackable when directly clicked, but an
                    // overlapping NPC wins so crowds do not make NPCs inaccessible.
                    return 15;
                case InteractionTargetKind.Npc: return 10;
                case InteractionTargetKind.GameplayObject: return 20;
                case InteractionTargetKind.Resource: return 21;
                case InteractionTargetKind.GroundItem: return 30;
                case InteractionTargetKind.OtherPlayer: return 40;
                case InteractionTargetKind.LocalPlayer: return 50;
                case InteractionTargetKind.MovementTile: return 100;
                default: return 1000;
            }
        }

        private static int PrimaryActionRank(InteractionActionKind action)
        {
            switch (action)
            {
                case InteractionActionKind.Attack: return 0;
                case InteractionActionKind.Talk: return 1;
                case InteractionActionKind.Trade: return 2;
                case InteractionActionKind.Use: return 3;
                case InteractionActionKind.Gather: return 4;
                case InteractionActionKind.Take: return 5;
                case InteractionActionKind.Follow: return 6;
                case InteractionActionKind.Custom: return 20;
                case InteractionActionKind.WalkHere: return 90;
                case InteractionActionKind.Examine: return 100;
                default: return 1000;
            }
        }

        private static int ContextRank(InteractionOption option)
        {
            if (option.Action == InteractionActionKind.WalkHere) return 80000;
            if (option.Action == InteractionActionKind.Examine)
                return 90000 + DefaultTargetRank(option.Target) * 100 + PrimaryActionRank(option.Action);
            return DefaultTargetRank(option.Target) * 100 + PrimaryActionRank(option.Action);
        }

        private readonly struct IndexedOption
        {
            public IndexedOption(InteractionOption option, int sourceIndex)
            {
                Option = option;
                SourceIndex = sourceIndex;
            }

            public InteractionOption Option { get; }
            public int SourceIndex { get; }
        }
    }
}
