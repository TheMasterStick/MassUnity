using System;
using System.Collections.Generic;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Skills;
using MassRPG.Data.Effects;

namespace MassRPG.Server.Effects
{
    public readonly struct ActiveStatusEffectView
    {
        public ActiveStatusEffectView(ContentId effectId, long expiresAtUnixMilliseconds, StatusEffectFlags flags)
        {
            EffectId = effectId;
            ExpiresAtUnixMilliseconds = expiresAtUnixMilliseconds;
            Flags = flags;
        }

        public ContentId EffectId { get; }
        public long ExpiresAtUnixMilliseconds { get; }
        public StatusEffectFlags Flags { get; }
    }

    /// <summary>
    /// Server-owned temporary effect state. Permanent skills remain XP-driven and respect their
    /// trainable ceilings; this service supplies effective levels while effects are active.
    /// Effective levels may temporarily exceed the trainable ceiling (for example Attack > 100 or
    /// Mining > 300), while debuffs can never reduce an effective level below 1.
    /// Reapplying the same effect id refreshes its duration instead of stacking duplicate copies.
    /// Distinct effect ids may combine, which keeps stacking policy explicit in content identity
    /// rather than hidden in combat code.
    /// </summary>
    public sealed class StatusEffectService : IEffectiveSkillLevelSource
    {
        private sealed class ActiveEffect
        {
            public ActiveEffect(PotionEffectDefinition definition, long expiresAtUnixMilliseconds)
            {
                Definition = definition;
                ExpiresAtUnixMilliseconds = expiresAtUnixMilliseconds;
            }

            public PotionEffectDefinition Definition { get; }
            public long ExpiresAtUnixMilliseconds { get; }
        }

        private readonly Dictionary<Guid, Dictionary<ContentId, ActiveEffect>> _active =
            new Dictionary<Guid, Dictionary<ContentId, ActiveEffect>>();

        public long Apply(PlayerState player, PotionEffectDefinition definition, long nowUnixMilliseconds)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (!player.IsAlive) throw new InvalidOperationException("Cannot apply a potion effect to a dead character.");

            var expiresAt = checked(nowUnixMilliseconds + definition.DurationMilliseconds);
            var state = GetOrCreate(player.CharacterId);
            state[definition.EffectId] = new ActiveEffect(definition, expiresAt);
            return expiresAt;
        }

        /// <summary>
        /// Restores a previously-authoritative absolute expiry time without restarting the effect's
        /// full duration. Already-expired entries are ignored. Persistence code should validate all
        /// effect ids first and clear old runtime state before calling this for a complete snapshot.
        /// </summary>
        public bool RestoreAbsolute(
            PlayerState player,
            PotionEffectDefinition definition,
            long expiresAtUnixMilliseconds,
            long nowUnixMilliseconds)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (expiresAtUnixMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(expiresAtUnixMilliseconds));
            if (expiresAtUnixMilliseconds <= nowUnixMilliseconds) return false;

            GetOrCreate(player.CharacterId)[definition.EffectId] =
                new ActiveEffect(definition, expiresAtUnixMilliseconds);
            return true;
        }

        public int GetEffectiveLevel(PlayerState player, SkillId skill, long nowUnixMilliseconds)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            var level = player.Skills.GetLevel(skill);
            if (!_active.TryGetValue(player.CharacterId, out var state)) return level;

            RemoveExpired(state, nowUnixMilliseconds);
            foreach (var effect in state.Values)
            {
                var modifiers = effect.Definition.SkillModifiers;
                for (var i = 0; i < modifiers.Count; i++)
                {
                    var modifier = modifiers[i];
                    if (modifier.Skill == skill) level = checked(level + modifier.FlatLevels);
                }
            }

            // Trainable ceilings belong to SkillSet/SkillProgression, not temporary effects.
            // Deliberately do not clamp upward: potions/buffs can exceed 100/300 temporarily.
            return Math.Max(1, level);
        }

        public bool HasFlag(PlayerState player, StatusEffectFlags flag, long nowUnixMilliseconds)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (flag == StatusEffectFlags.None) return false;
            if (!_active.TryGetValue(player.CharacterId, out var state)) return false;

            RemoveExpired(state, nowUnixMilliseconds);
            foreach (var effect in state.Values)
                if ((effect.Definition.Flags & flag) == flag) return true;
            return false;
        }

        public IReadOnlyList<ActiveStatusEffectView> GetActiveEffects(PlayerState player, long nowUnixMilliseconds)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (!_active.TryGetValue(player.CharacterId, out var state))
                return Array.Empty<ActiveStatusEffectView>();

            RemoveExpired(state, nowUnixMilliseconds);
            var result = new List<ActiveStatusEffectView>(state.Count);
            foreach (var effect in state.Values)
            {
                result.Add(new ActiveStatusEffectView(
                    effect.Definition.EffectId,
                    effect.ExpiresAtUnixMilliseconds,
                    effect.Definition.Flags));
            }
            result.Sort((left, right) => string.CompareOrdinal(left.EffectId.Value, right.EffectId.Value));
            return result;
        }

        public bool Remove(PlayerState player, ContentId effectId)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            return _active.TryGetValue(player.CharacterId, out var state) && state.Remove(effectId);
        }

        public void Clear(PlayerState player)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            _active.Remove(player.CharacterId);
        }

        private Dictionary<ContentId, ActiveEffect> GetOrCreate(Guid characterId)
        {
            if (!_active.TryGetValue(characterId, out var state))
            {
                state = new Dictionary<ContentId, ActiveEffect>();
                _active.Add(characterId, state);
            }
            return state;
        }

        private static void RemoveExpired(Dictionary<ContentId, ActiveEffect> state, long nowUnixMilliseconds)
        {
            if (state.Count == 0) return;
            var expired = new List<ContentId>();
            foreach (var pair in state)
                if (nowUnixMilliseconds >= pair.Value.ExpiresAtUnixMilliseconds) expired.Add(pair.Key);
            for (var i = 0; i < expired.Count; i++) state.Remove(expired[i]);
        }
    }
}
