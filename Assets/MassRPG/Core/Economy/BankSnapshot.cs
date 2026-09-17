using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using MassRPG.Core.Content;

namespace MassRPG.Core.Economy
{
    /// <summary>
    /// Immutable quantity entry used by the bank read boundary. Item display data remains in the
    /// published content catalog; this snapshot carries only stable machine-facing ids and counts.
    /// </summary>
    public readonly struct BankEntrySnapshot
    {
        public BankEntrySnapshot(ContentId itemId, int quantity)
        {
            if (itemId.IsEmpty) throw new ArgumentException("Bank entry item id cannot be empty.", nameof(itemId));
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            ItemId = itemId;
            Quantity = quantity;
        }

        public ContentId ItemId { get; }
        public int Quantity { get; }
    }

    /// <summary>
    /// Stable read-side bank state for client presentation or later network serialization. Entries
    /// are copied and sorted by permanent ContentId so UI ordering does not depend on dictionary
    /// enumeration order and later server mutations cannot alter an already-issued snapshot.
    /// </summary>
    public sealed class BankSnapshot
    {
        private readonly IReadOnlyDictionary<ContentId, int> _quantityByItem;

        public BankSnapshot(IEnumerable<BankEntrySnapshot> entries)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));

            var ordered = new List<BankEntrySnapshot>();
            var byItem = new Dictionary<ContentId, int>();
            foreach (var entry in entries)
            {
                if (entry.ItemId.IsEmpty)
                    throw new ArgumentException("Bank snapshot cannot contain an empty item id.", nameof(entries));
                if (entry.Quantity <= 0)
                    throw new ArgumentException("Bank snapshot quantities must be positive.", nameof(entries));
                if (byItem.ContainsKey(entry.ItemId))
                    throw new ArgumentException($"Bank snapshot contains duplicate item id '{entry.ItemId}'.", nameof(entries));

                ordered.Add(entry);
                byItem.Add(entry.ItemId, entry.Quantity);
            }

            ordered.Sort((left, right) => left.ItemId.CompareTo(right.ItemId));
            Entries = new ReadOnlyCollection<BankEntrySnapshot>(ordered);
            _quantityByItem = new ReadOnlyDictionary<ContentId, int>(byItem);
        }

        public IReadOnlyList<BankEntrySnapshot> Entries { get; }

        public int Count(ContentId itemId)
            => _quantityByItem.TryGetValue(itemId, out var quantity) ? quantity : 0;
    }
}
