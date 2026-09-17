using System;
using System.Collections.Generic;
using MassRPG.Core.Content;

namespace MassRPG.Data.Spells
{
    public enum SpellTargetKind
    {
        Self,
        Creature,
        Player,
        Ground
    }

    public enum SpellEffectKind
    {
        Damage,
        Heal,
        Teleport,
        Buff,
        Debuff,
        Utility,
        Custom
    }

    public readonly struct SpellReagentCost
    {
        public SpellReagentCost(ContentId itemId, int quantity)
        {
            if (itemId.IsEmpty) throw new ArgumentException("Spell reagent item id cannot be empty.", nameof(itemId));
            if (quantity < 1) throw new ArgumentOutOfRangeException(nameof(quantity));
            ItemId = itemId;
            Quantity = quantity;
        }

        public ContentId ItemId { get; }
        public int Quantity { get; }
    }

    public sealed class SpellDefinition
    {
        private readonly List<SpellReagentCost> _reagents;

        public SpellDefinition(
            ContentId id,
            string displayName,
            int requiredMagicLevel,
            SpellTargetKind targetKind,
            SpellEffectKind effectKind,
            int rangeTiles,
            long castCooldownMilliseconds,
            int effectMagnitude,
            long magicXp,
            IEnumerable<SpellReagentCost> reagents = null,
            bool requiresLineOfSight = true)
        {
            if (id.IsEmpty) throw new ArgumentException("Spell id cannot be empty.", nameof(id));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Spell display name cannot be empty.", nameof(displayName));
            if (requiredMagicLevel < 1) throw new ArgumentOutOfRangeException(nameof(requiredMagicLevel));
            if (rangeTiles < 0) throw new ArgumentOutOfRangeException(nameof(rangeTiles));
            if (castCooldownMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(castCooldownMilliseconds));
            if (effectMagnitude < 0) throw new ArgumentOutOfRangeException(nameof(effectMagnitude));
            if (magicXp < 0) throw new ArgumentOutOfRangeException(nameof(magicXp));
            Id = id;
            DisplayName = displayName;
            RequiredMagicLevel = requiredMagicLevel;
            TargetKind = targetKind;
            EffectKind = effectKind;
            RangeTiles = rangeTiles;
            CastCooldownMilliseconds = castCooldownMilliseconds;
            EffectMagnitude = effectMagnitude;
            MagicXp = magicXp;
            RequiresLineOfSight = requiresLineOfSight;
            _reagents = reagents == null ? new List<SpellReagentCost>() : new List<SpellReagentCost>(reagents);
        }

        public ContentId Id { get; }
        public string DisplayName { get; }
        public int RequiredMagicLevel { get; }
        public SpellTargetKind TargetKind { get; }
        public SpellEffectKind EffectKind { get; }
        public int RangeTiles { get; }
        public long CastCooldownMilliseconds { get; }
        public int EffectMagnitude { get; }
        public long MagicXp { get; }
        public bool RequiresLineOfSight { get; }
        public IReadOnlyList<SpellReagentCost> Reagents => _reagents;
    }

    public interface ISpellDefinitionSource
    {
        bool TryGet(ContentId id, out SpellDefinition definition);
    }

    public sealed class SpellCatalog : ISpellDefinitionSource
    {
        private readonly Dictionary<ContentId, SpellDefinition> _definitions = new Dictionary<ContentId, SpellDefinition>();

        public IEnumerable<SpellDefinition> All => _definitions.Values;

        public void Register(SpellDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (_definitions.ContainsKey(definition.Id)) throw new InvalidOperationException("Duplicate spell id '" + definition.Id + "'.");
            _definitions.Add(definition.Id, definition);
        }

        public bool TryGet(ContentId id, out SpellDefinition definition) => _definitions.TryGetValue(id, out definition);
    }
}
