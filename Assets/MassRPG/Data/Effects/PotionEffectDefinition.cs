using System;
using System.Collections.Generic;
using MassRPG.Core.Content;
using MassRPG.Core.Skills;

namespace MassRPG.Data.Effects
{
    [Flags]
    public enum StatusEffectFlags
    {
        None = 0,
        PoisonImmunity = 1 << 0
    }

    public readonly struct SkillLevelModifier
    {
        public SkillLevelModifier(SkillId skill, int flatLevels)
        {
            if (flatLevels == 0) throw new ArgumentOutOfRangeException(nameof(flatLevels), "A zero skill modifier has no effect.");
            Skill = skill;
            FlatLevels = flatLevels;
        }

        public SkillId Skill { get; }
        public int FlatLevels { get; }
    }

    /// <summary>
    /// Server-readable effect attached to a potion item. The effect has its own stable id so later
    /// content revisions can change item names/icons without changing active-effect identity.
    /// Duration and magnitudes are data, not hard-coded combat rules.
    /// </summary>
    public sealed class PotionEffectDefinition
    {
        private readonly List<SkillLevelModifier> _skillModifiers;

        public PotionEffectDefinition(
            ContentId potionItemId,
            ContentId effectId,
            long durationMilliseconds,
            IEnumerable<SkillLevelModifier> skillModifiers = null,
            StatusEffectFlags flags = StatusEffectFlags.None)
        {
            if (potionItemId.IsEmpty) throw new ArgumentException("Potion item id cannot be empty.", nameof(potionItemId));
            if (effectId.IsEmpty) throw new ArgumentException("Status effect id cannot be empty.", nameof(effectId));
            if (durationMilliseconds < 1) throw new ArgumentOutOfRangeException(nameof(durationMilliseconds));
            PotionItemId = potionItemId;
            EffectId = effectId;
            DurationMilliseconds = durationMilliseconds;
            Flags = flags;
            _skillModifiers = skillModifiers == null
                ? new List<SkillLevelModifier>()
                : new List<SkillLevelModifier>(skillModifiers);
        }

        public ContentId PotionItemId { get; }
        public ContentId EffectId { get; }
        public long DurationMilliseconds { get; }
        public IReadOnlyList<SkillLevelModifier> SkillModifiers => _skillModifiers;
        public StatusEffectFlags Flags { get; }
    }

    public interface IPotionEffectSource
    {
        bool TryGetByPotion(ContentId potionItemId, out PotionEffectDefinition definition);
        bool TryGetByEffect(ContentId effectId, out PotionEffectDefinition definition);
    }

    public sealed class PotionEffectCatalog : IPotionEffectSource
    {
        private readonly Dictionary<ContentId, PotionEffectDefinition> _byPotion = new Dictionary<ContentId, PotionEffectDefinition>();
        private readonly Dictionary<ContentId, PotionEffectDefinition> _byEffect = new Dictionary<ContentId, PotionEffectDefinition>();

        public IEnumerable<PotionEffectDefinition> All => _byPotion.Values;

        public void Register(PotionEffectDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (_byPotion.ContainsKey(definition.PotionItemId))
                throw new InvalidOperationException("Duplicate potion effect item id '" + definition.PotionItemId + "'.");
            if (_byEffect.ContainsKey(definition.EffectId))
                throw new InvalidOperationException("Duplicate status effect id '" + definition.EffectId + "'.");
            _byPotion.Add(definition.PotionItemId, definition);
            _byEffect.Add(definition.EffectId, definition);
        }

        public bool TryGetByPotion(ContentId potionItemId, out PotionEffectDefinition definition)
            => _byPotion.TryGetValue(potionItemId, out definition);

        public bool TryGetByEffect(ContentId effectId, out PotionEffectDefinition definition)
            => _byEffect.TryGetValue(effectId, out definition);
    }
}
