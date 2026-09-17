using System;
using System.Collections.Generic;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Core.Skills;
using MassRPG.Core.World;
using MassRPG.Data.Spells;
using MassRPG.Server.Creatures;
using MassRPG.Server.Pvp;

namespace MassRPG.Server.Spells
{
    public readonly struct SpellCastResult
    {
        public SpellCastResult(bool success, string code, int magnitude = 0, bool targetKilled = false)
        {
            Success = success;
            Code = code;
            Magnitude = magnitude;
            TargetKilled = targetKilled;
        }

        public bool Success { get; }
        public string Code { get; }
        public int Magnitude { get; }
        public bool TargetKilled { get; }
    }

    /// <summary>
    /// Authoritative spell validation/execution. Reagents, skill requirements, cooldown, range and
    /// logical LOS are server-owned. Player-target damage additionally passes through the same PvP
    /// eligibility/skull policy as ordinary weapon attacks.
    /// </summary>
    public sealed class SpellCastingService
    {
        private readonly ISpellDefinitionSource _spells;
        private readonly IItemRuleSource _items;
        private readonly IRangedLineOfSightMap _lineOfSight;
        private readonly PvpService _pvp;
        private readonly Dictionary<Guid, long> _nextCastAt = new Dictionary<Guid, long>();

        public SpellCastingService(
            ISpellDefinitionSource spells,
            IItemRuleSource items,
            IRangedLineOfSightMap lineOfSight,
            PvpService pvp = null)
        {
            _spells = spells ?? throw new ArgumentNullException(nameof(spells));
            _items = items ?? throw new ArgumentNullException(nameof(items));
            _lineOfSight = lineOfSight ?? throw new ArgumentNullException(nameof(lineOfSight));
            _pvp = pvp;
        }

        public SpellCastResult TryCastOnCreature(
            PlayerState caster,
            ContentId spellId,
            CreatureState target,
            long nowUnixMilliseconds)
        {
            if (caster == null) return new SpellCastResult(false, "caster_missing");
            if (target == null || !target.IsAlive) return new SpellCastResult(false, "target_missing");
            if (!_spells.TryGet(spellId, out var spell)) return new SpellCastResult(false, "spell_missing");
            if (spell.TargetKind != SpellTargetKind.Creature) return new SpellCastResult(false, "wrong_target_kind");

            var validation = ValidateCommon(caster, spell, target.Anchor, nowUnixMilliseconds);
            if (!validation.Success) return validation;
            if (spell.EffectKind != SpellEffectKind.Damage) return new SpellCastResult(false, "unsupported_effect");

            ConsumeReagents(caster, spell);
            StartCooldown(caster.CharacterId, spell, nowUnixMilliseconds);
            caster.Skills.AddXp(SkillId.Magic, spell.MagicXp);

            var damage = Math.Min(spell.EffectMagnitude, target.CurrentHitpoints);
            target.CurrentHitpoints -= damage;
            return new SpellCastResult(true, "ok", damage, !target.IsAlive);
        }

        public SpellCastResult TryCastOnPlayer(
            PlayerState caster,
            ContentId spellId,
            PlayerState target,
            long nowUnixMilliseconds)
        {
            if (caster == null) return new SpellCastResult(false, "caster_missing");
            if (target == null || !target.IsAlive) return new SpellCastResult(false, "target_missing");
            if (_pvp == null) return new SpellCastResult(false, "pvp_unavailable");
            if (!_spells.TryGet(spellId, out var spell)) return new SpellCastResult(false, "spell_missing");
            if (spell.TargetKind != SpellTargetKind.Player) return new SpellCastResult(false, "wrong_target_kind");
            if (spell.EffectKind != SpellEffectKind.Damage) return new SpellCastResult(false, "unsupported_effect");

            var pvpDecision = _pvp.EvaluateAttack(caster, target);
            if (!pvpDecision.Allowed) return new SpellCastResult(false, pvpDecision.Code);
            var validation = ValidateCommon(caster, spell, target.Location, nowUnixMilliseconds);
            if (!validation.Success) return validation;

            ConsumeReagents(caster, spell);
            StartCooldown(caster.CharacterId, spell, nowUnixMilliseconds);
            caster.Skills.AddXp(SkillId.Magic, spell.MagicXp);
            _pvp.MarkAggressor(caster, target, nowUnixMilliseconds);
            caster.Combat.Begin(target.CharacterId);
            target.Combat.Begin(caster.CharacterId);

            var damage = Math.Min(spell.EffectMagnitude, target.CurrentHitpoints);
            target.CurrentHitpoints -= damage;
            if (!target.IsAlive)
            {
                target.Combat.End();
                target.Movement.Clear();
                target.Production.Clear();
            }
            return new SpellCastResult(true, "ok", damage, !target.IsAlive);
        }

        public SpellCastResult TryCastSelf(PlayerState caster, ContentId spellId, long nowUnixMilliseconds)
        {
            if (caster == null) return new SpellCastResult(false, "caster_missing");
            if (!_spells.TryGet(spellId, out var spell)) return new SpellCastResult(false, "spell_missing");
            if (spell.TargetKind != SpellTargetKind.Self) return new SpellCastResult(false, "wrong_target_kind");

            var validation = ValidateCommon(caster, spell, caster.Location, nowUnixMilliseconds);
            if (!validation.Success) return validation;
            if (spell.EffectKind != SpellEffectKind.Heal) return new SpellCastResult(false, "unsupported_effect");

            ConsumeReagents(caster, spell);
            StartCooldown(caster.CharacterId, spell, nowUnixMilliseconds);
            caster.Skills.AddXp(SkillId.Magic, spell.MagicXp);

            var missing = Math.Max(0, caster.MaxHitpoints - caster.CurrentHitpoints);
            var healed = Math.Min(spell.EffectMagnitude, missing);
            caster.CurrentHitpoints += healed;
            return new SpellCastResult(true, "ok", healed, false);
        }

        public long NextCastAt(Guid characterId)
            => _nextCastAt.TryGetValue(characterId, out var value) ? value : 0L;

        private SpellCastResult ValidateCommon(PlayerState caster, SpellDefinition spell, GridLocation targetLocation, long nowUnixMilliseconds)
        {
            if (!caster.IsAlive) return new SpellCastResult(false, "caster_dead");
            if (caster.Skills.GetLevel(SkillId.Magic) < spell.RequiredMagicLevel)
                return new SpellCastResult(false, "magic_level_too_low");
            if (nowUnixMilliseconds < NextCastAt(caster.CharacterId))
                return new SpellCastResult(false, "cast_cooldown");
            if (!caster.Location.SameLayer(targetLocation))
                return new SpellCastResult(false, "different_layer");
            if (GridMath.RangeDistance(caster.Location.Tile, targetLocation.Tile) > spell.RangeTiles)
                return new SpellCastResult(false, "out_of_range");
            if (spell.RequiresLineOfSight && !GridLineOfSight.HasLineOfSight(_lineOfSight, caster.Location, targetLocation))
                return new SpellCastResult(false, "line_of_sight_blocked");

            for (var i = 0; i < spell.Reagents.Count; i++)
            {
                var reagent = spell.Reagents[i];
                if (caster.Inventory.CountItem(reagent.ItemId) < reagent.Quantity)
                    return new SpellCastResult(false, "missing_reagent");
            }
            return new SpellCastResult(true, "ok");
        }

        private void ConsumeReagents(PlayerState caster, SpellDefinition spell)
        {
            for (var i = 0; i < spell.Reagents.Count; i++)
            {
                var reagent = spell.Reagents[i];
                if (!InventoryRules.RemoveItem(caster.Inventory, reagent.ItemId, reagent.Quantity))
                    throw new InvalidOperationException("Spell reagent validation/consumption became inconsistent.");
            }
        }

        private void StartCooldown(Guid characterId, SpellDefinition spell, long nowUnixMilliseconds)
        {
            _nextCastAt[characterId] = checked(nowUnixMilliseconds + spell.CastCooldownMilliseconds);
        }
    }
}
