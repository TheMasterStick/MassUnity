using System;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Core.Resources;
using MassRPG.Core.World;

namespace MassRPG.Server.Resources
{
    public sealed class GatheringResult
    {
        private GatheringResult(bool success, string code, string message, ContentId itemId, int quantity)
        {
            Success = success;
            Code = code;
            Message = message;
            ItemId = itemId;
            Quantity = quantity;
        }

        public bool Success { get; }
        public string Code { get; }
        public string Message { get; }
        public ContentId ItemId { get; }
        public int Quantity { get; }

        public static GatheringResult Ok(ContentId itemId, int quantity)
            => new GatheringResult(true, "ok", string.Empty, itemId, quantity);
        public static GatheringResult Fail(string code, string message)
            => new GatheringResult(false, code, message, default, 0);
    }

    /// <summary>
    /// First authoritative one-cycle gathering service. Click-to-resource pathing/repeat timing will
    /// sit above this operation; successful inventory/XP/depletion mutation already belongs here.
    /// </summary>
    public sealed class GatheringService
    {
        private readonly IResourceNodeSource _nodes;
        private readonly IResourceRuleSource _resources;
        private readonly IGatheringToolSource _tools;
        private readonly IItemRuleSource _items;
        private readonly ResourceDepletionLedger _depletion;
        private readonly Random _random;

        public GatheringService(
            IResourceNodeSource nodes,
            IResourceRuleSource resources,
            IGatheringToolSource tools,
            IItemRuleSource items,
            ResourceDepletionLedger depletion,
            Random random = null)
        {
            _nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
            _tools = tools ?? throw new ArgumentNullException(nameof(tools));
            _items = items ?? throw new ArgumentNullException(nameof(items));
            _depletion = depletion ?? throw new ArgumentNullException(nameof(depletion));
            _random = random ?? new Random();
        }

        public GatheringResult TryGather(PlayerState player, ResourceNodeKey node, long nowUnixMilliseconds)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (!_nodes.Exists(node)) return GatheringResult.Fail("unknown_resource_node", "That resource node does not exist.");
            if (!_resources.TryGetRule(node.ResourceId, out var rule)) return GatheringResult.Fail("unknown_resource", "That resource type is not published.");
            if (!player.Location.SameLayer(node.Location)) return GatheringResult.Fail("wrong_layer", "That resource is on another plane or floor.");
            if (GridMath.RangeDistance(player.Tile, node.Location.Tile) > 1) return GatheringResult.Fail("out_of_range", "Move next to the resource first.");
            if (player.Skills.GetLevel(rule.Skill) < rule.RequiredLevel)
                return GatheringResult.Fail("level_requirement", $"You need {rule.Skill} level {rule.RequiredLevel} to gather this resource.");
            if (!HasRequiredTool(player, rule))
                return GatheringResult.Fail("tool_requirement", "You do not have a suitable gathering tool.");
            if (!_depletion.IsAvailable(player.CharacterId, node, rule.AvailabilityMode, nowUnixMilliseconds))
                return GatheringResult.Fail("resource_depleted", "That resource is still respawning.");

            var quantity = rule.MinimumYield == rule.MaximumYield
                ? rule.MinimumYield
                : _random.Next(rule.MinimumYield, rule.MaximumYield + 1);
            var added = InventoryRules.AddItem(player.Inventory, _items, rule.YieldItemId, quantity);
            if (added != quantity)
            {
                if (added > 0) InventoryRules.RemoveItem(player.Inventory, rule.YieldItemId, added);
                return GatheringResult.Fail("inventory_full", "Your inventory is too full.");
            }

            player.Skills.AddXp(rule.Skill, rule.Experience);
            _depletion.Deplete(player.CharacterId, node, rule.AvailabilityMode, rule.RespawnSeconds, nowUnixMilliseconds);
            return GatheringResult.Ok(rule.YieldItemId, quantity);
        }

        private bool HasRequiredTool(PlayerState player, ResourceGatheringRule rule)
        {
            if (rule.RequiredToolKind == GatheringToolKind.None) return true;

            if (player.Equipment.TryGet(EquipmentSlot.Weapon, out var equipped)
                && ToolMatches(equipped, rule)) return true;

            for (var i = 0; i < player.Inventory.Capacity; i++)
            {
                var stack = player.Inventory.GetSlot(i);
                if (stack != null && ToolMatches(stack.ItemId, rule)) return true;
            }

            return false;
        }

        private bool ToolMatches(ContentId itemId, ResourceGatheringRule rule)
        {
            return _tools.TryGetGatheringTool(itemId, out var tool)
                && tool.Kind == rule.RequiredToolKind
                && tool.Tier >= rule.MinimumToolTier;
        }
    }
}
