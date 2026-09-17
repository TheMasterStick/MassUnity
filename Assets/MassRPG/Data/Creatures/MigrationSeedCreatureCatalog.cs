using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Creatures;

namespace MassRPG.Data.Creatures
{
    /// <summary>
    /// Small representative migration set for exercising the C# simulation. Values mirror useful
    /// browser reference content where possible but are not declared final MMO balance.
    /// </summary>
    public static class MigrationSeedCreatureCatalog
    {
        public static CreatureCatalog Create()
        {
            var catalog = new CreatureCatalog();
            catalog.Register(new CreatureDefinition(
                new ContentId("chicken"), "Chicken", 1, 3, 1, 1, 1, 0, 0, 0,
                CombatStyle.Melee, 2400, 1, CreatureDisposition.Passive, new CreatureFootprint(1, 1),
                lootTableId: new ContentId("loot.chicken")));
            catalog.Register(new CreatureDefinition(
                new ContentId("cow"), "Cow", 2, 8, 1, 1, 1, 0, 0, 0,
                CombatStyle.Melee, 3000, 1, CreatureDisposition.Neutral, new CreatureFootprint(1, 1),
                lootTableId: new ContentId("loot.cow")));
            catalog.Register(new CreatureDefinition(
                new ContentId("goblin"), "Goblin", 5, 13, 5, 5, 4, 2, 2, 1,
                CombatStyle.Melee, 2400, 1, CreatureDisposition.Aggressive, new CreatureFootprint(1, 1),
                lootTableId: new ContentId("loot.goblin")));
            catalog.Register(new CreatureDefinition(
                new ContentId("wolf"), "Wolf", 15, 30, 13, 13, 9, 6, 6, 3,
                CombatStyle.Melee, 1800, 1, CreatureDisposition.Aggressive, new CreatureFootprint(1, 1),
                lootTableId: new ContentId("loot.wolf")));
            catalog.Register(new CreatureDefinition(
                new ContentId("dark_wizard"), "Dark wizard", 22, 40, 22, 22, 12, 8, 8, 8,
                CombatStyle.Magic, 3000, 6, CreatureDisposition.Aggressive, new CreatureFootprint(1, 1),
                lootTableId: new ContentId("loot.dark_wizard")));
            catalog.Register(new CreatureDefinition(
                new ContentId("hill_giant"), "Hill giant", 28, 60, 24, 26, 18, 10, 12, 6,
                CombatStyle.Melee, 3000, 1, CreatureDisposition.Aggressive, new CreatureFootprint(2, 2),
                lootTableId: new ContentId("loot.hill_giant")));
            catalog.Register(new CreatureDefinition(
                new ContentId("lesser_demon"), "Lesser demon", 45, 100, 38, 40, 30, 16, 18, 12,
                CombatStyle.Melee, 2400, 1, CreatureDisposition.Aggressive, new CreatureFootprint(2, 2),
                lootTableId: new ContentId("loot.lesser_demon")));
            return catalog;
        }
    }
}
