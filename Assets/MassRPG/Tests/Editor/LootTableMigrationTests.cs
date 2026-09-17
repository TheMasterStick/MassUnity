using System;
using System.Collections.Generic;
using MassRPG.Core.Content;
using MassRPG.Data.Creatures;
using MassRPG.Data.Loot;
using MassRPG.Server.Loot;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class LootTableMigrationTests
    {
        [Test]
        public void MigratedCreaturesReferenceExistingLootTables()
        {
            var creatures = MigrationSeedCreatureCatalog.Create();
            var loot = MigrationSeedLootTableCatalog.Create();

            foreach (var creature in creatures.All)
            {
                Assert.IsTrue(creature.LootTableId.HasValue, creature.Id + " should reference a migration loot table.");
                Assert.IsTrue(loot.TryGet(creature.LootTableId.Value, out _), creature.Id + " references a missing loot table.");
            }
        }

        [Test]
        public void ChickenGuaranteedDropAlwaysRollsAndNoDropCanSuppressOrdinaryDrop()
        {
            var catalog = MigrationSeedLootTableCatalog.Create();
            Assert.IsTrue(catalog.TryGet(new ContentId("loot.chicken"), out var table));

            // Guaranteed feather quantity roll -> minimum 3. No-drop roll 0.10 < 0.20, so raw meat
            // is suppressed and no additional random values are required.
            var random = Sequence(0.0, 0.10);
            var result = LootTableRoller.Roll(table, random);

            Assert.AreEqual(1, result.Stacks.Count);
            Assert.AreEqual(new ContentId("feather"), result.Stacks[0].ItemId);
            Assert.AreEqual(3, result.Stacks[0].Quantity);
        }

        [Test]
        public void GoblinWeightedRollUsesRelativeWeightsAndQuantityRange()
        {
            var catalog = MigrationSeedLootTableCatalog.Create();
            Assert.IsTrue(catalog.TryGet(new ContentId("loot.goblin"), out var table));

            // 1) bones guaranteed quantity; 2) pass no-drop; 3) weighted roll near end selects
            // bronze helmet; 4) helmet quantity. The guaranteed bones remain in the same result.
            var result = LootTableRoller.Roll(table, Sequence(0.0, 0.90, 0.99, 0.0));

            Assert.AreEqual(2, result.Stacks.Count);
            Assert.AreEqual(new ContentId("bones"), result.Stacks[0].ItemId);
            Assert.AreEqual(1, result.Stacks[0].Quantity);
            Assert.AreEqual(new ContentId("bronze_helmet"), result.Stacks[1].ItemId);
            Assert.AreEqual(1, result.Stacks[1].Quantity);
        }

        [Test]
        public void CowRollCanProduceMaximumGuaranteedAndWeightedQuantities()
        {
            var catalog = MigrationSeedLootTableCatalog.Create();
            Assert.IsTrue(catalog.TryGet(new ContentId("loot.cow"), out var table));

            // cowhide qty, bones qty, pass no-drop, only weighted entry selection, raw-meat qty=max.
            var result = LootTableRoller.Roll(table, Sequence(0.9, 0.9, 0.9, 0.0, 0.999999));

            Assert.AreEqual(3, result.Stacks.Count);
            Assert.AreEqual(new ContentId("cowhide"), result.Stacks[0].ItemId);
            Assert.AreEqual(new ContentId("bones"), result.Stacks[1].ItemId);
            Assert.AreEqual(new ContentId("raw_meat"), result.Stacks[2].ItemId);
            Assert.AreEqual(2, result.Stacks[2].Quantity);
        }

        [Test]
        public void RollerAggregatesDuplicateItemEntriesRatherThanLosingEitherDrop()
        {
            var table = new LootTableDefinition(
                new ContentId("loot.test"),
                new[] { new LootEntryDefinition(new ContentId("coins"), 2, 2) },
                new[] { new LootEntryDefinition(new ContentId("coins"), 3, 3) },
                0.0);

            var result = LootTableRoller.Roll(table, Sequence(0.0, 0.5, 0.0, 0.0));
            Assert.AreEqual(1, result.Stacks.Count);
            Assert.AreEqual(new ContentId("coins"), result.Stacks[0].ItemId);
            Assert.AreEqual(5, result.Stacks[0].Quantity);
        }

        private static Func<double> Sequence(params double[] values)
        {
            var queue = new Queue<double>(values);
            return () =>
            {
                if (queue.Count == 0) throw new InvalidOperationException("Test RNG sequence was exhausted.");
                return queue.Dequeue();
            };
        }
    }
}
