using System;
using System.Collections.Generic;
using MassRPG.Core.Content;

namespace MassRPG.Data.Loot
{
    public readonly struct LootEntryDefinition
    {
        public LootEntryDefinition(ContentId itemId, int minimumQuantity, int maximumQuantity, int weight = 1)
        {
            if (itemId.IsEmpty) throw new ArgumentException("Loot item id cannot be empty.", nameof(itemId));
            if (minimumQuantity < 1) throw new ArgumentOutOfRangeException(nameof(minimumQuantity));
            if (maximumQuantity < minimumQuantity) throw new ArgumentOutOfRangeException(nameof(maximumQuantity));
            if (weight < 1) throw new ArgumentOutOfRangeException(nameof(weight));
            ItemId = itemId;
            MinimumQuantity = minimumQuantity;
            MaximumQuantity = maximumQuantity;
            Weight = weight;
        }

        public ContentId ItemId { get; }
        public int MinimumQuantity { get; }
        public int MaximumQuantity { get; }
        public int Weight { get; }
    }

    /// <summary>
    /// Data-driven creature drop table matching the useful browser behavior: every guaranteed entry
    /// rolls once, then at most one weighted ordinary entry rolls unless NoDropChance wins. Live
    /// balancing can replace these values without changing combat/reward code.
    /// </summary>
    public sealed class LootTableDefinition
    {
        private readonly List<LootEntryDefinition> _guaranteed;
        private readonly List<LootEntryDefinition> _weighted;

        public LootTableDefinition(
            ContentId id,
            IEnumerable<LootEntryDefinition> guaranteedDrops,
            IEnumerable<LootEntryDefinition> weightedDrops,
            double noDropChance)
        {
            if (id.IsEmpty) throw new ArgumentException("Loot table id cannot be empty.", nameof(id));
            if (guaranteedDrops == null) throw new ArgumentNullException(nameof(guaranteedDrops));
            if (weightedDrops == null) throw new ArgumentNullException(nameof(weightedDrops));
            if (noDropChance < 0.0 || noDropChance > 1.0) throw new ArgumentOutOfRangeException(nameof(noDropChance));
            Id = id;
            _guaranteed = new List<LootEntryDefinition>(guaranteedDrops);
            _weighted = new List<LootEntryDefinition>(weightedDrops);
            NoDropChance = noDropChance;
        }

        public ContentId Id { get; }
        public IReadOnlyList<LootEntryDefinition> GuaranteedDrops => _guaranteed;
        public IReadOnlyList<LootEntryDefinition> WeightedDrops => _weighted;
        public double NoDropChance { get; set; }
    }

    public interface ILootTableSource
    {
        bool TryGet(ContentId id, out LootTableDefinition definition);
    }

    public sealed class LootTableCatalog : ILootTableSource
    {
        private readonly Dictionary<ContentId, LootTableDefinition> _definitions =
            new Dictionary<ContentId, LootTableDefinition>();

        public IEnumerable<LootTableDefinition> All => _definitions.Values;
        public int Count => _definitions.Count;

        public void Register(LootTableDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (_definitions.ContainsKey(definition.Id))
                throw new InvalidOperationException("Duplicate loot table id '" + definition.Id + "'.");
            _definitions.Add(definition.Id, definition);
        }

        public bool TryGet(ContentId id, out LootTableDefinition definition)
            => _definitions.TryGetValue(id, out definition);
    }
}
