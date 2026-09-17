using System;
using System.Collections.Generic;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Core.Resources;
using MassRPG.Core.Skills;
using MassRPG.Core.World;
using MassRPG.Data.Farming;

namespace MassRPG.Server.Farming
{
    public sealed class FarmPatchState
    {
        public FarmPatchState(ResourceNodeKey patch, ContentId cropId, Guid plantedByCharacterId, long plantedAtUnixMilliseconds, long readyAtUnixMilliseconds)
        {
            Patch = patch;
            CropId = cropId;
            PlantedByCharacterId = plantedByCharacterId;
            PlantedAtUnixMilliseconds = plantedAtUnixMilliseconds;
            ReadyAtUnixMilliseconds = readyAtUnixMilliseconds;
        }

        public ResourceNodeKey Patch { get; }
        public ContentId CropId { get; }
        public Guid PlantedByCharacterId { get; }
        public long PlantedAtUnixMilliseconds { get; }
        public long ReadyAtUnixMilliseconds { get; }
        public bool IsReady(long nowUnixMilliseconds) => nowUnixMilliseconds >= ReadyAtUnixMilliseconds;
    }

    public sealed class FarmPatchLedger
    {
        private readonly Dictionary<ResourceNodeKey, FarmPatchState> _patches = new Dictionary<ResourceNodeKey, FarmPatchState>();

        public IEnumerable<FarmPatchState> PlantedPatches => _patches.Values;
        public bool TryGet(ResourceNodeKey patch, out FarmPatchState state) => _patches.TryGetValue(patch, out state);
        internal void Set(FarmPatchState state) => _patches[state.Patch] = state;
        internal bool Clear(ResourceNodeKey patch) => _patches.Remove(patch);
    }

    public readonly struct FarmingResult
    {
        private FarmingResult(bool success, string code, ContentId itemId, int quantity, long readyAtUnixMilliseconds)
        {
            Success = success;
            Code = code ?? string.Empty;
            ItemId = itemId;
            Quantity = quantity;
            ReadyAtUnixMilliseconds = readyAtUnixMilliseconds;
        }

        public bool Success { get; }
        public string Code { get; }
        public ContentId ItemId { get; }
        public int Quantity { get; }
        public long ReadyAtUnixMilliseconds { get; }

        public static FarmingResult Planted(long readyAtUnixMilliseconds)
            => new FarmingResult(true, "planted", default, 0, readyAtUnixMilliseconds);

        public static FarmingResult Harvested(ContentId itemId, int quantity)
            => new FarmingResult(true, "harvested", itemId, quantity, 0);

        public static FarmingResult Fail(string code)
            => new FarmingResult(false, code, default, 0, 0);
    }

    /// <summary>
    /// Authoritative timestamp-driven farming migrated from the browser crop patches. Crops keep
    /// growing while areas are asleep/offline because readiness is derived from timestamps rather
    /// than simulation ticks.
    /// </summary>
    public sealed class FarmingService
    {
        public static readonly ContentId CropPatchId = new ContentId("farm_patch");
        public static readonly ContentId HerbPatchId = new ContentId("herb_patch");
        public static readonly ContentId SeedDibberId = new ContentId("seed_dibber");

        private readonly IResourceNodeSource _nodes;
        private readonly ICropDefinitionSource _crops;
        private readonly IItemRuleSource _items;
        private readonly FarmPatchLedger _patches;
        private readonly Random _random;

        public FarmingService(
            IResourceNodeSource nodes,
            ICropDefinitionSource crops,
            IItemRuleSource items,
            FarmPatchLedger patches,
            Random random = null)
        {
            _nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
            _crops = crops ?? throw new ArgumentNullException(nameof(crops));
            _items = items ?? throw new ArgumentNullException(nameof(items));
            _patches = patches ?? throw new ArgumentNullException(nameof(patches));
            _random = random ?? new Random();
        }

        public FarmingResult Plant(PlayerState player, ResourceNodeKey patch, ContentId cropId, long nowUnixMilliseconds)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (!_nodes.Exists(patch)) return FarmingResult.Fail("unknown_patch");
            if (!IsPatchResource(patch.ResourceId)) return FarmingResult.Fail("not_farming_patch");
            if (!player.Location.SameLayer(patch.Location)) return FarmingResult.Fail("wrong_layer");
            if (GridMath.RangeDistance(player.Tile, patch.Location.Tile) > 1) return FarmingResult.Fail("out_of_range");
            if (_patches.TryGet(patch, out _)) return FarmingResult.Fail("patch_occupied");
            if (!_crops.TryGet(cropId, out var crop)) return FarmingResult.Fail("unknown_crop");
            if (!PatchMatchesCrop(patch.ResourceId, crop.PatchKind)) return FarmingResult.Fail("wrong_patch_type");
            if (player.Skills.GetLevel(SkillId.Farming) < crop.RequiredFarmingLevel) return FarmingResult.Fail("level_requirement");
            if (player.Inventory.CountItem(SeedDibberId) <= 0) return FarmingResult.Fail("seed_dibber_required");
            if (player.Inventory.CountItem(crop.SeedItemId) <= 0) return FarmingResult.Fail("seed_required");

            if (!InventoryRules.RemoveItem(player.Inventory, crop.SeedItemId, 1))
                return FarmingResult.Fail("seed_changed");

            var readyAt = checked(nowUnixMilliseconds + crop.GrowthMilliseconds);
            _patches.Set(new FarmPatchState(patch, crop.Id, player.CharacterId, nowUnixMilliseconds, readyAt));
            player.Skills.AddXp(SkillId.Farming, crop.PlantingExperience);
            return FarmingResult.Planted(readyAt);
        }

        public FarmingResult Harvest(PlayerState player, ResourceNodeKey patch, long nowUnixMilliseconds)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (!_nodes.Exists(patch)) return FarmingResult.Fail("unknown_patch");
            if (!player.Location.SameLayer(patch.Location)) return FarmingResult.Fail("wrong_layer");
            if (GridMath.RangeDistance(player.Tile, patch.Location.Tile) > 1) return FarmingResult.Fail("out_of_range");
            if (!_patches.TryGet(patch, out var state)) return FarmingResult.Fail("patch_empty");
            if (!state.IsReady(nowUnixMilliseconds)) return FarmingResult.Fail("still_growing");
            if (!_crops.TryGet(state.CropId, out var crop)) return FarmingResult.Fail("unknown_crop");

            var quantity = crop.MinimumYield == crop.MaximumYield
                ? crop.MinimumYield
                : _random.Next(crop.MinimumYield, crop.MaximumYield + 1);

            var added = InventoryRules.AddItem(player.Inventory, _items, crop.YieldItemId, quantity);
            if (added != quantity)
            {
                if (added > 0) InventoryRules.RemoveItem(player.Inventory, crop.YieldItemId, added);
                return FarmingResult.Fail("inventory_full");
            }

            _patches.Clear(patch);
            player.Skills.AddXp(SkillId.Farming, crop.HarvestExperience);
            return FarmingResult.Harvested(crop.YieldItemId, quantity);
        }

        private static bool IsPatchResource(ContentId resourceId)
            => resourceId == CropPatchId || resourceId == HerbPatchId;

        private static bool PatchMatchesCrop(ContentId resourceId, FarmPatchKind patchKind)
            => patchKind == FarmPatchKind.Herb ? resourceId == HerbPatchId : resourceId == CropPatchId;
    }
}
