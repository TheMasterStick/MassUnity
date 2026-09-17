using MassRPG.Core.Content;

namespace MassRPG.Data.Loot
{
    /// <summary>
    /// Representative browser-reference loot tables for the creatures already present in the C#
    /// migration seed catalog. These preserve old behavior for migration testing without declaring
    /// the values final MMO balance.
    /// </summary>
    public static class MigrationSeedLootTableCatalog
    {
        public static LootTableCatalog Create()
        {
            var catalog = new LootTableCatalog();

            catalog.Register(Table("loot.chicken", 0.20,
                Guaranteed(Entry("feather", 3, 8)),
                Weighted(Entry("raw_meat", 1, 1))));

            catalog.Register(Table("loot.cow", 0.10,
                Guaranteed(Entry("cowhide", 1, 1), Entry("bones", 1, 1)),
                Weighted(Entry("raw_meat", 1, 2))));

            catalog.Register(Table("loot.goblin", 0.35,
                Guaranteed(Entry("bones", 1, 1)),
                Weighted(
                    Entry("coins", 5, 25, 5),
                    Entry("bronze_sword", 1, 1, 1),
                    Entry("bronze_helmet", 1, 1, 1))));

            catalog.Register(Table("loot.wolf", 0.50,
                Guaranteed(Entry("bones", 1, 1)),
                Weighted(Entry("coins", 5, 20))));

            catalog.Register(Table("loot.dark_wizard", 0.40,
                Guaranteed(Entry("bones", 1, 1)),
                Weighted(
                    Entry("coins", 15, 50, 2),
                    Entry("uncut_sapphire", 1, 1, 1))));

            catalog.Register(Table("loot.hill_giant", 0.30,
                Guaranteed(Entry("bones", 1, 1)),
                Weighted(
                    Entry("coins", 30, 100, 3),
                    Entry("steel_bar", 1, 2, 1))));

            catalog.Register(Table("loot.lesser_demon", 0.30,
                Guaranteed(Entry("bones", 1, 1)),
                Weighted(
                    Entry("coins", 60, 220, 3),
                    Entry("adamant_ore", 1, 2, 1))));

            return catalog;
        }

        private static LootTableDefinition Table(
            string id,
            double noDropChance,
            LootEntryDefinition[] guaranteed,
            LootEntryDefinition[] weighted)
            => new LootTableDefinition(new ContentId(id), guaranteed, weighted, noDropChance);

        private static LootEntryDefinition Entry(string itemId, int min, int max, int weight = 1)
            => new LootEntryDefinition(new ContentId(itemId), min, max, weight);

        private static LootEntryDefinition[] Guaranteed(params LootEntryDefinition[] entries) => entries;
        private static LootEntryDefinition[] Weighted(params LootEntryDefinition[] entries) => entries;
    }
}
