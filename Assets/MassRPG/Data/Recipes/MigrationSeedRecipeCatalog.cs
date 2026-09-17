using System;
using MassRPG.Core.Content;
using MassRPG.Core.Skills;

namespace MassRPG.Data.Recipes
{
    /// <summary>
    /// Broad browser-parity recipe catalog used while the external published-data pipeline and
    /// Data Editor are built. Stable browser recipe IDs are retained for migration continuity.
    /// </summary>
    public static class MigrationSeedRecipeCatalog
    {
        private const int TickMilliseconds = 600;
        private static readonly ContentId Furnace = new ContentId("station.furnace");
        private static readonly ContentId Anvil = new ContentId("station.anvil");
        private static readonly ContentId Fire = new ContentId("station.fire");
        private static readonly ContentId Loom = new ContentId("station.loom");
        private static readonly ContentId Tannery = new ContentId("station.tannery");

        private static readonly MetalTier[] Metals =
        {
            new MetalTier("bronze", "Bronze", 1, 1),
            new MetalTier("iron", "Iron", 2, 15),
            new MetalTier("steel", "Steel", 3, 30),
            new MetalTier("mithril", "Mithril", 4, 50),
            new MetalTier("adamant", "Adamant", 5, 70),
            new MetalTier("rune", "Rune", 6, 85),
            new MetalTier("dragonite", "Dragonite", 7, 92)
        };

        private static readonly TreeTier[] Trees =
        {
            new TreeTier("normal", "Tree", 1, 25),
            new TreeTier("oak", "Oak", 15, 38),
            new TreeTier("willow", "Willow", 30, 68),
            new TreeTier("maple", "Maple", 45, 100),
            new TreeTier("yew", "Yew", 60, 175),
            new TreeTier("magic", "Magic", 75, 250)
        };

        private static readonly FishTier[] Fish =
        {
            new FishTier("shrimp", "Shrimp", 1, 10),
            new FishTier("sardine", "Sardine", 5, 15),
            new FishTier("trout", "Trout", 20, 25),
            new FishTier("salmon", "Salmon", 30, 42),
            new FishTier("lobster", "Lobster", 40, 60),
            new FishTier("swordfish", "Swordfish", 55, 90),
            new FishTier("shark", "Shark", 76, 130)
        };

        private static readonly HerbTier[] Herbs =
        {
            new HerbTier("guam", "Guam leaf", 3, 12),
            new HerbTier("marrentill", "Marrentill", 5, 14),
            new HerbTier("harralander", "Harralander", 9, 18),
            new HerbTier("ranarr", "Ranarr weed", 25, 30),
            new HerbTier("irit", "Irit leaf", 44, 48),
            new HerbTier("avantoe", "Avantoe", 50, 55)
        };

        private static readonly PotionTier[] Potions =
        {
            new PotionTier("attack_potion", "Attack potion", 1, 25, "guam", "eye_of_newt"),
            new PotionTier("antipoison", "Antipoison", 5, 30, "marrentill", "unicorn_horn_dust"),
            new PotionTier("strength_potion", "Strength potion", 12, 40, "harralander", "limpwurt_root"),
            new PotionTier("prayer_potion", "Prayer potion", 38, 87, "ranarr", "chocolate_dust")
        };

        public static RecipeCatalog Create()
        {
            var catalog = new RecipeCatalog();
            RegisterSmelting(catalog);
            RegisterSmithing(catalog);
            RegisterCooking(catalog);
            RegisterFletching(catalog);
            RegisterCrafting(catalog);
            RegisterHerblore(catalog);
            RegisterConstruction(catalog);
            return catalog;
        }

        private static void RegisterSmelting(RecipeCatalog catalog)
        {
            catalog.Register(Smelt("smelt_bronze_bar", "Bronze bar", 1, 6, 2, "bronze_bar",
                Ingredient("copper_ore", 1), Ingredient("tin_ore", 1)));
            catalog.Register(Smelt("smelt_iron_bar", "Iron bar", 15, 13, 2, "iron_bar", Ingredient("iron_ore", 1)));
            catalog.Register(Smelt("smelt_steel_bar", "Steel bar", 30, 17, 2, "steel_bar",
                Ingredient("iron_ore", 1), Ingredient("coal", 2)));
            catalog.Register(Smelt("smelt_mithril_bar", "Mithril bar", 50, 30, 3, "mithril_bar",
                Ingredient("mithril_ore", 1), Ingredient("coal", 4)));
            catalog.Register(Smelt("smelt_adamant_bar", "Adamant bar", 70, 38, 3, "adamant_bar",
                Ingredient("adamant_ore", 1), Ingredient("coal", 6)));
            catalog.Register(Smelt("smelt_rune_bar", "Rune bar", 85, 50, 3, "rune_bar",
                Ingredient("rune_ore", 1), Ingredient("coal", 8)));
            catalog.Register(Smelt("smelt_gold_bar", "Gold bar", 40, 23, 2, "gold_bar", Ingredient("gold_ore", 1)));
            catalog.Register(Smelt("smelt_silver_bar", "Silver bar", 20, 14, 2, "silver_bar", Ingredient("silver_ore", 1)));
            catalog.Register(Smelt("smelt_dragonite_bar", "Dragonite bar", 92, 75, 3, "dragonite_bar",
                Ingredient("dragonite_ore", 2), Ingredient("coal", 2)));
        }

        private static void RegisterSmithing(RecipeCatalog catalog)
        {
            var pieces = new[]
            {
                new SmithPiece("sword", "sword", 1),
                new SmithPiece("dagger", "dagger", 1),
                new SmithPiece("helmet", "full helm", 1),
                new SmithPiece("platebody", "platebody", 5),
                new SmithPiece("platelegs", "platelegs", 3),
                new SmithPiece("shield", "kiteshield", 2),
                new SmithPiece("boots", "boots", 1),
                new SmithPiece("gloves", "gauntlets", 1),
                new SmithPiece("longsword", "longsword", 2),
                new SmithPiece("mace", "mace", 1),
                new SmithPiece("warhammer", "warhammer", 3),
                new SmithPiece("battleaxe", "battleaxe", 3),
                new SmithPiece("2h_sword", "two-handed sword", 3)
            };

            for (var i = 0; i < Metals.Length; i++)
            {
                var metal = Metals[i];
                for (var p = 0; p < pieces.Length; p++)
                {
                    var piece = pieces[p];
                    catalog.Register(new RecipeDefinition(
                        new ContentId("smith_" + metal.Id + "_" + piece.Suffix),
                        metal.Name + " " + piece.Label,
                        SkillId.Smithing,
                        metal.SmithLevel,
                        new[] { Ingredient(metal.Id + "_bar", piece.Bars) },
                        new ContentId(metal.Id + "_" + piece.Suffix),
                        1,
                        piece.Bars * (8 + i * 4),
                        (2 + piece.Bars) * TickMilliseconds,
                        "smithing",
                        Anvil,
                        new ContentId("hammer")));
                }
            }

            catalog.Register(new RecipeDefinition(
                new ContentId("smith_iron_nails"), "Iron nails", SkillId.Smithing, 15,
                new[] { Ingredient("iron_bar", 1) }, new ContentId("nails"), 15, 12,
                2 * TickMilliseconds, "smithing_misc", Anvil, new ContentId("hammer")));
        }

        private static void RegisterCooking(RecipeCatalog catalog)
        {
            for (var i = 0; i < Fish.Length; i++)
            {
                var fish = Fish[i];
                catalog.Register(new RecipeDefinition(
                    new ContentId("cook_" + fish.Id), fish.Name, SkillId.Cooking, fish.Level,
                    new[] { Ingredient("raw_" + fish.Id, 1) }, new ContentId("cooked_" + fish.Id), 1,
                    fish.Xp, 2 * TickMilliseconds, "cooking", Fire, null, true,
                    new ContentId("burnt_" + fish.Id)));
            }

            catalog.Register(new RecipeDefinition(
                new ContentId("cook_meat"), "Cooked meat", SkillId.Cooking, 1,
                new[] { Ingredient("raw_meat", 1) }, new ContentId("cooked_meat"), 1,
                15, 2 * TickMilliseconds, "cooking", Fire, null, true, new ContentId("burnt_meat")));
        }

        private static void RegisterFletching(RecipeCatalog catalog)
        {
            for (var i = 0; i < Trees.Length; i++)
            {
                var tree = Trees[i];
                var label = tree.Id == "normal" ? "Shortbow" : tree.Name + " shortbow";
                catalog.Register(new RecipeDefinition(
                    new ContentId("fletch_" + tree.Id + "_unstrung"), label + " (u)", SkillId.Fletching, tree.Level,
                    new[] { Ingredient(tree.Id + "_logs", 1) }, new ContentId(tree.Id + "_shortbow_u"), 1,
                    Round(tree.Xp * 0.5), 2 * TickMilliseconds, "fletching_bows", null, new ContentId("knife")));
                catalog.Register(new RecipeDefinition(
                    new ContentId("fletch_" + tree.Id + "_string"), label, SkillId.Fletching, tree.Level,
                    new[] { Ingredient(tree.Id + "_shortbow_u", 1), Ingredient("bow_string", 1) },
                    new ContentId(tree.Id + "_shortbow"), 1, Round(tree.Xp * 0.7), 2 * TickMilliseconds,
                    "fletching_bows"));
            }

            catalog.Register(new RecipeDefinition(
                new ContentId("fletch_arrow_shafts"), "Arrow shafts", SkillId.Fletching, 1,
                new[] { Ingredient("normal_logs", 1) }, new ContentId("arrow_shaft"), 15, 5,
                2 * TickMilliseconds, "fletching_arrows", null, new ContentId("knife")));
            catalog.Register(new RecipeDefinition(
                new ContentId("craft_bowstring"), "Bow string", SkillId.Crafting, 10,
                new[] { Ingredient("flax", 1) }, new ContentId("bow_string"), 1, 15,
                2 * TickMilliseconds, "crafting_misc", Loom));

            for (var i = 0; i < Metals.Length; i++)
            {
                var metal = Metals[i];
                catalog.Register(new RecipeDefinition(
                    new ContentId("fletch_" + metal.Id + "_arrows"), metal.Name + " arrows", SkillId.Fletching,
                    Math.Max(1, metal.Tier * 5),
                    new[] { Ingredient("arrow_shaft", 15), Ingredient("feather", 15), Ingredient(metal.Id + "_bar", 1) },
                    new ContentId(metal.Id + "_arrow"), 15, 15 * (2 + i), 3 * TickMilliseconds, "fletching_arrows"));
            }
        }

        private static void RegisterCrafting(RecipeCatalog catalog)
        {
            catalog.Register(new RecipeDefinition(
                new ContentId("tan_leather"), "Leather", SkillId.Crafting, 1,
                new[] { Ingredient("cowhide", 1) }, new ContentId("leather"), 1, 4,
                TickMilliseconds, "crafting_leather", Tannery));
            catalog.Register(Craft("craft_leather_body", "Leather body", SkillId.Crafting, 14, "leather", 3, "leather_body", 25, 3, "crafting_leather", "needle"));
            catalog.Register(Craft("craft_leather_chaps", "Leather chaps", SkillId.Crafting, 11, "leather", 2, "leather_chaps", 27, 2, "crafting_leather", "needle"));
            catalog.Register(Craft("craft_leather_gloves", "Leather gloves", SkillId.Crafting, 1, "leather", 1, "leather_gloves", 12, 1, "crafting_leather", "needle"));
            catalog.Register(Craft("craft_leather_boots", "Leather boots", SkillId.Crafting, 7, "leather", 1, "leather_boots", 14, 1, "crafting_leather", "needle"));

            RegisterGem(catalog, "sapphire", 1, 20);
            RegisterGem(catalog, "emerald", 27, 27);
            RegisterGem(catalog, "ruby", 34, 43);
            RegisterGem(catalog, "diamond", 43, 65);

            catalog.Register(Jewellery("craft_gold_ring", "Gold ring", 5, "gold_ring", 15, "ring_mold"));
            catalog.Register(new RecipeDefinition(
                new ContentId("craft_sapphire_ring"), "Sapphire ring", SkillId.Crafting, 20,
                new[] { Ingredient("gold_bar", 1), Ingredient("sapphire", 1) }, new ContentId("sapphire_ring"), 1,
                25, 2 * TickMilliseconds, "crafting_jewelry", Furnace, new ContentId("ring_mold")));
            catalog.Register(Jewellery("craft_gold_amulet", "Gold amulet", 7, "gold_amulet", 18, "amulet_mold"));
            catalog.Register(new RecipeDefinition(
                new ContentId("craft_ruby_amulet"), "Ruby amulet", SkillId.Crafting, 27,
                new[] { Ingredient("gold_bar", 1), Ingredient("ruby", 1) }, new ContentId("ruby_amulet"), 1,
                45, 2 * TickMilliseconds, "crafting_jewelry", Furnace, new ContentId("amulet_mold")));
        }

        private static void RegisterHerblore(RecipeCatalog catalog)
        {
            for (var i = 0; i < Herbs.Length; i++)
            {
                var herb = Herbs[i];
                catalog.Register(new RecipeDefinition(
                    new ContentId("clean_" + herb.Id), "Clean " + herb.Name.ToLowerInvariant(), SkillId.Herblore, herb.Level,
                    new[] { Ingredient("grimy_" + herb.Id, 1) }, new ContentId("clean_" + herb.Id), 1,
                    Round(herb.Xp * 0.3), TickMilliseconds, "herblore_clean"));
            }

            for (var i = 0; i < Potions.Length; i++)
            {
                var potion = Potions[i];
                catalog.Register(new RecipeDefinition(
                    new ContentId("brew_" + potion.Id), potion.Name, SkillId.Herblore, potion.Level,
                    new[] { Ingredient("clean_" + potion.Herb, 1), Ingredient("vial_of_water", 1), Ingredient(potion.Secondary, 1) },
                    new ContentId(potion.Id), 1, potion.Xp, 2 * TickMilliseconds, "herblore_potions", null,
                    new ContentId("pestle_and_mortar")));
            }
        }

        private static void RegisterConstruction(RecipeCatalog catalog)
        {
            catalog.Register(new RecipeDefinition(
                new ContentId("cut_plank"), "Plank", SkillId.Construction, 1,
                new[] { Ingredient("normal_logs", 1) }, new ContentId("plank"), 1, 5,
                TickMilliseconds, "construction", null, new ContentId("saw")));
        }

        private static RecipeDefinition Smelt(string id, string name, int level, long xp, int ticks, string output, params RecipeIngredient[] inputs)
            => new RecipeDefinition(new ContentId(id), name, SkillId.Smithing, level, inputs,
                new ContentId(output), 1, xp, ticks * TickMilliseconds, "smelting", Furnace);

        private static RecipeDefinition Craft(
            string id, string name, SkillId skill, int level, string input, int inputQty, string output,
            long xp, int ticks, string category, string tool)
            => new RecipeDefinition(new ContentId(id), name, skill, level,
                new[] { Ingredient(input, inputQty) }, new ContentId(output), 1, xp,
                ticks * TickMilliseconds, category, null, new ContentId(tool));

        private static RecipeDefinition Jewellery(string id, string name, int level, string output, long xp, string tool)
            => new RecipeDefinition(new ContentId(id), name, SkillId.Crafting, level,
                new[] { Ingredient("gold_bar", 1) }, new ContentId(output), 1, xp,
                2 * TickMilliseconds, "crafting_jewelry", Furnace, new ContentId(tool));

        private static void RegisterGem(RecipeCatalog catalog, string id, int level, long xp)
            => catalog.Register(new RecipeDefinition(
                new ContentId("cut_" + id), "Cut " + id, SkillId.Crafting, level,
                new[] { Ingredient("uncut_" + id, 1) }, new ContentId(id), 1, xp,
                TickMilliseconds, "crafting_gems", null, new ContentId("chisel")));

        private static RecipeIngredient Ingredient(string itemId, int quantity)
            => new RecipeIngredient(new ContentId(itemId), quantity);

        private static int Round(double value) => (int)Math.Floor(value + 0.5);

        private readonly struct MetalTier
        {
            public MetalTier(string id, string name, int tier, int smithLevel)
            { Id = id; Name = name; Tier = tier; SmithLevel = smithLevel; }
            public string Id { get; }
            public string Name { get; }
            public int Tier { get; }
            public int SmithLevel { get; }
        }

        private readonly struct TreeTier
        {
            public TreeTier(string id, string name, int level, int xp)
            { Id = id; Name = name; Level = level; Xp = xp; }
            public string Id { get; }
            public string Name { get; }
            public int Level { get; }
            public int Xp { get; }
        }

        private readonly struct FishTier
        {
            public FishTier(string id, string name, int level, int xp)
            { Id = id; Name = name; Level = level; Xp = xp; }
            public string Id { get; }
            public string Name { get; }
            public int Level { get; }
            public int Xp { get; }
        }

        private readonly struct HerbTier
        {
            public HerbTier(string id, string name, int level, int xp)
            { Id = id; Name = name; Level = level; Xp = xp; }
            public string Id { get; }
            public string Name { get; }
            public int Level { get; }
            public int Xp { get; }
        }

        private readonly struct PotionTier
        {
            public PotionTier(string id, string name, int level, int xp, string herb, string secondary)
            { Id = id; Name = name; Level = level; Xp = xp; Herb = herb; Secondary = secondary; }
            public string Id { get; }
            public string Name { get; }
            public int Level { get; }
            public int Xp { get; }
            public string Herb { get; }
            public string Secondary { get; }
        }

        private readonly struct SmithPiece
        {
            public SmithPiece(string suffix, string label, int bars)
            { Suffix = suffix; Label = label; Bars = bars; }
            public string Suffix { get; }
            public string Label { get; }
            public int Bars { get; }
        }
    }
}
