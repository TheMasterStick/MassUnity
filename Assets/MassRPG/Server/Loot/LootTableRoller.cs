using System;
using System.Collections.Generic;
using MassRPG.Core.Content;
using MassRPG.Data.Loot;

namespace MassRPG.Server.Loot
{
    public sealed class LootRollResult
    {
        private readonly List<LootStack> _stacks;

        internal LootRollResult(List<LootStack> stacks)
        {
            _stacks = stacks ?? throw new ArgumentNullException(nameof(stacks));
        }

        public IReadOnlyList<LootStack> Stacks => _stacks;
        public bool IsEmpty => _stacks.Count == 0;
    }

    /// <summary>
    /// Server-owned loot roller. The caller supplies randomness so authoritative simulation/tests can
    /// use a server RNG, deterministic replay stream or seeded test source without client influence.
    /// </summary>
    public static class LootTableRoller
    {
        public static LootRollResult Roll(LootTableDefinition table, Func<double> random01)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (random01 == null) throw new ArgumentNullException(nameof(random01));

            var quantities = new Dictionary<ContentId, int>();
            var order = new List<ContentId>();

            for (var i = 0; i < table.GuaranteedDrops.Count; i++)
            {
                var drop = table.GuaranteedDrops[i];
                Add(quantities, order, drop.ItemId, RollQuantity(drop, random01));
            }

            if (table.WeightedDrops.Count > 0 && NextUnit(random01) >= table.NoDropChance)
            {
                long totalWeight = 0;
                for (var i = 0; i < table.WeightedDrops.Count; i++)
                    totalWeight = checked(totalWeight + table.WeightedDrops[i].Weight);

                if (totalWeight > 0)
                {
                    var roll = NextUnit(random01) * totalWeight;
                    LootEntryDefinition? chosen = null;
                    for (var i = 0; i < table.WeightedDrops.Count; i++)
                    {
                        roll -= table.WeightedDrops[i].Weight;
                        if (roll <= 0.0)
                        {
                            chosen = table.WeightedDrops[i];
                            break;
                        }
                    }

                    // Floating-point edge protection for a random source that returns a value very
                    // close to one. Browser Math.random never returns 1, but a custom server RNG
                    // adapter should not make a valid loot table silently fail.
                    if (!chosen.HasValue)
                        chosen = table.WeightedDrops[table.WeightedDrops.Count - 1];

                    var selected = chosen.Value;
                    Add(quantities, order, selected.ItemId, RollQuantity(selected, random01));
                }
            }

            var stacks = new List<LootStack>(order.Count);
            for (var i = 0; i < order.Count; i++)
                stacks.Add(new LootStack(order[i], quantities[order[i]]));
            return new LootRollResult(stacks);
        }

        private static int RollQuantity(LootEntryDefinition entry, Func<double> random01)
        {
            var span = entry.MaximumQuantity - entry.MinimumQuantity + 1;
            var offset = (int)Math.Floor(NextUnit(random01) * span);
            if (offset >= span) offset = span - 1;
            return entry.MinimumQuantity + offset;
        }

        private static double NextUnit(Func<double> random01)
        {
            var value = random01();
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new InvalidOperationException("Loot random source returned a non-finite value.");
            if (value <= 0.0) return 0.0;
            if (value >= 1.0) return BitDecrementOne();
            return value;
        }

        // Avoid depending on Math.BitDecrement, which is not available in every Unity profile.
        private static double BitDecrementOne() => 0.9999999999999999d;

        private static void Add(
            Dictionary<ContentId, int> quantities,
            List<ContentId> order,
            ContentId itemId,
            int quantity)
        {
            if (!quantities.TryGetValue(itemId, out var current))
            {
                quantities.Add(itemId, quantity);
                order.Add(itemId);
                return;
            }
            quantities[itemId] = checked(current + quantity);
        }
    }
}
