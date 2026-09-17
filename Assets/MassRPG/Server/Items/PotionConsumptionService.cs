using System;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Data.Effects;
using MassRPG.Data.Items;
using MassRPG.Server.Effects;

namespace MassRPG.Server.Items
{
    public readonly struct PotionConsumptionResult
    {
        private PotionConsumptionResult(bool success, string code, ContentId potionItemId, ContentId effectId, long expiresAtUnixMilliseconds)
        {
            Success = success;
            Code = code ?? string.Empty;
            PotionItemId = potionItemId;
            EffectId = effectId;
            ExpiresAtUnixMilliseconds = expiresAtUnixMilliseconds;
        }

        public bool Success { get; }
        public string Code { get; }
        public ContentId PotionItemId { get; }
        public ContentId EffectId { get; }
        public long ExpiresAtUnixMilliseconds { get; }

        public static PotionConsumptionResult Ok(ContentId potionItemId, ContentId effectId, long expiresAtUnixMilliseconds)
            => new PotionConsumptionResult(true, "ok", potionItemId, effectId, expiresAtUnixMilliseconds);

        public static PotionConsumptionResult Fail(string code)
            => new PotionConsumptionResult(false, code, default, default, 0);
    }

    /// <summary>
    /// Authoritative potion consumption. A potion is not consumed unless both the item definition
    /// and a data-driven effect definition exist. The resulting temporary state is delegated to
    /// StatusEffectService so permanent SkillSet XP remains untouched.
    /// </summary>
    public sealed class PotionConsumptionService
    {
        private readonly IItemDefinitionSource _items;
        private readonly IPotionEffectSource _effects;
        private readonly StatusEffectService _statusEffects;

        public PotionConsumptionService(
            IItemDefinitionSource items,
            IPotionEffectSource effects,
            StatusEffectService statusEffects)
        {
            _items = items ?? throw new ArgumentNullException(nameof(items));
            _effects = effects ?? throw new ArgumentNullException(nameof(effects));
            _statusEffects = statusEffects ?? throw new ArgumentNullException(nameof(statusEffects));
        }

        public PotionConsumptionResult Drink(PlayerState player, int inventorySlot, long nowUnixMilliseconds)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (!player.IsAlive) return PotionConsumptionResult.Fail("character_dead");
            if (inventorySlot < 0 || inventorySlot >= player.Inventory.Capacity)
                return PotionConsumptionResult.Fail("invalid_slot");

            var stack = player.Inventory.GetSlot(inventorySlot);
            if (stack == null) return PotionConsumptionResult.Fail("empty_slot");
            if (!_items.TryGetDefinition(stack.ItemId, out var item))
                return PotionConsumptionResult.Fail("unknown_item");
            if (item.Type != ItemType.Potion)
                return PotionConsumptionResult.Fail("not_potion");
            if (!_effects.TryGetByPotion(item.Id, out var effect))
                return PotionConsumptionResult.Fail("potion_effect_missing");

            if (!InventoryRules.RemoveItem(player.Inventory, item.Id, 1))
                return PotionConsumptionResult.Fail("inventory_changed");

            var expiresAt = _statusEffects.Apply(player, effect, nowUnixMilliseconds);
            return PotionConsumptionResult.Ok(item.Id, effect.EffectId, expiresAt);
        }
    }
}
