using System;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Core.Resources;
using MassRPG.Core.Skills;

namespace MassRPG.Data.Items
{
    /// <summary>
    /// Broad browser-parity development catalog. Stable browser IDs are intentionally retained
    /// while content is moved into versionable MassRPG data. This remains migration/test content,
    /// not a declaration that every value is final live-MMO balance.
    /// </summary>
    public static class MigrationSeedItemCatalog
    {
        private const int CombatTickMilliseconds = 600;

        private static readonly MetalTier[] Metals =
        {
            new MetalTier("bronze", "Bronze", 1, 1, 4, 1),
            new MetalTier("iron", "Iron", 2, 15, 12, 1),
            new MetalTier("steel", "Steel", 3, 30, 30, 5),
            new MetalTier("mithril", "Mithril", 4, 50, 90, 20),
            new MetalTier("adamant", "Adamant", 5, 70, 200, 30),
            new MetalTier("rune", "Rune", 6, 85, 500, 40),
            new MetalTier("dragonite", "Dragonite", 7, 92, 650, 60)
        };

        private static readonly TreeTier[] Trees =
        {
            new TreeTier("normal", "Tree", 1, 25, 2, 1),
            new TreeTier("oak", "Oak", 15, 38, 8, 5),
            new TreeTier("willow", "Willow", 30, 68, 15, 20),
            new TreeTier("maple", "Maple", 45, 100, 30, 30),
            new TreeTier("yew", "Yew", 60, 175, 90, 40),
            new TreeTier("magic", "Magic", 75, 250, 250, 50)
        };

        private static readonly FishTier[] Fish =
        {
            new FishTier("shrimp", "Shrimp", 1, 10, 3),
            new FishTier("sardine", "Sardine", 5, 15, 4),
            new FishTier("trout", "Trout", 20, 25, 7),
            new FishTier("salmon", "Salmon", 30, 42, 9),
            new FishTier("lobster", "Lobster", 40, 60, 12),
            new FishTier("swordfish", "Swordfish", 55, 90, 16),
            new FishTier("shark", "Shark", 76, 130, 22)
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

        public static ItemCatalog Create()
        {
            var catalog = new ItemCatalog();

            RegisterStack(catalog, "coins", "Coins", ItemType.Currency, 1, "The realm's currency.");
            RegisterMetals(catalog);
            RegisterTools(catalog);
            RegisterWood(catalog);
            RegisterFish(catalog);
            RegisterFarmingAndHerblore(catalog);
            RegisterCrafting(catalog);
            RegisterFletching(catalog);
            RegisterConstructionAndFood(catalog);

            return catalog;
        }

        private static void RegisterMetals(ItemCatalog catalog)
        {
            RegisterStack(catalog, "copper_ore", "Copper ore", ItemType.Resource, 3, "Used with tin to smith bronze.");
            RegisterStack(catalog, "tin_ore", "Tin ore", ItemType.Resource, 3, "Used with copper to smith bronze.");
            RegisterStack(catalog, "coal", "Coal", ItemType.Resource, 5, "Fuel for smelting stronger metals.");

            for (var i = 0; i < Metals.Length; i++)
            {
                var metal = Metals[i];
                if (metal.Id != "bronze" && metal.Id != "steel")
                    RegisterStack(catalog, metal.Id + "_ore", metal.Name + " ore", ItemType.Resource, metal.Value,
                        "Raw " + metal.Name.ToLowerInvariant() + " ore, smelted into a bar.");
                RegisterStack(catalog, metal.Id + "_bar", metal.Name + " bar", ItemType.Material, metal.Value * 3,
                    "A bar of " + metal.Name.ToLowerInvariant() + ", smithed into equipment.");

                var attack = 4 + i * 6;
                var strength = 3 + i * 6;
                RegisterWeapon(catalog, metal.Id + "_sword", metal.Name + " sword", metal.Value * 8,
                    "A " + metal.Name.ToLowerInvariant() + " sword.", attack, strength, metal.CombatLevel, 4, false);
                RegisterWeapon(catalog, metal.Id + "_dagger", metal.Name + " dagger", metal.Value * 5,
                    "A fast " + metal.Name.ToLowerInvariant() + " dagger.", Round(attack * 0.7), Round(strength * 0.6), metal.CombatLevel, 3, false);
                RegisterWeapon(catalog, metal.Id + "_longsword", metal.Name + " longsword", metal.Value * 10,
                    "A deliberate, heavier sword made from " + metal.Name.ToLowerInvariant() + ".", Round(attack * 1.05), Round(strength * 1.15), metal.CombatLevel, 5, false);
                RegisterWeapon(catalog, metal.Id + "_mace", metal.Name + " mace", metal.Value * 7,
                    "A balanced crushing weapon made from " + metal.Name.ToLowerInvariant() + ".", Round(attack * 0.90), Round(strength * 1.10), metal.CombatLevel, 4, false);
                RegisterWeapon(catalog, metal.Id + "_warhammer", metal.Name + " warhammer", metal.Value * 11,
                    "A slow heavy crushing weapon made from " + metal.Name.ToLowerInvariant() + ".", Round(attack * 0.80), Round(strength * 1.35), metal.CombatLevel, 6, false);
                RegisterWeapon(catalog, metal.Id + "_battleaxe", metal.Name + " battleaxe", metal.Value * 12,
                    "A heavy chopping weapon made from " + metal.Name.ToLowerInvariant() + ".", Round(attack * 0.95), Round(strength * 1.45), metal.CombatLevel, 6, false);
                RegisterWeapon(catalog, metal.Id + "_2h_sword", metal.Name + " two-handed sword", metal.Value * 14,
                    "A very slow but powerful two-handed sword made from " + metal.Name.ToLowerInvariant() + ".", Round(attack * 1.10), Round(strength * 1.70), metal.CombatLevel, 7, true);

                var defence = 3 + i * 4;
                RegisterArmor(catalog, metal.Id + "_helmet", metal.Name + " full helm", metal.Value * 4, EquipmentSlot.Head,
                    Round(defence * 0.4), metal.CombatLevel);
                RegisterArmor(catalog, metal.Id + "_platebody", metal.Name + " platebody", metal.Value * 10, EquipmentSlot.Chest,
                    defence, metal.CombatLevel);
                RegisterArmor(catalog, metal.Id + "_platelegs", metal.Name + " platelegs", metal.Value * 7, EquipmentSlot.Legs,
                    Round(defence * 0.7), metal.CombatLevel);
                RegisterArmor(catalog, metal.Id + "_shield", metal.Name + " kiteshield", metal.Value * 6, EquipmentSlot.Shield,
                    Round(defence * 0.6), metal.CombatLevel);
                RegisterArmor(catalog, metal.Id + "_boots", metal.Name + " boots", metal.Value * 2, EquipmentSlot.Boots,
                    Round(defence * 0.2), metal.CombatLevel);
                RegisterArmor(catalog, metal.Id + "_gloves", metal.Name + " gauntlets", metal.Value * 2, EquipmentSlot.Hands,
                    Round(defence * 0.2), metal.CombatLevel);

                catalog.Register(new ItemDefinition(
                    new ContentId(metal.Id + "_arrow"), metal.Name + " arrow", ItemType.Ammunition, true,
                    Math.Max(1, Round(metal.Value * 0.3)), "Arrows tipped with " + metal.Name.ToLowerInvariant() + ".",
                    null, false, new CombatBonuses { RangedStrength = 2 + i * 3 }, SkillId.Ranged, metal.CombatLevel));
            }

            RegisterStack(catalog, "gold_ore", "Gold ore", ItemType.Resource, 20, "Smelted into gold bars for jewellery.");
            RegisterStack(catalog, "silver_ore", "Silver ore", ItemType.Resource, 15, "Smelted into silver bars for jewellery.");
            RegisterStack(catalog, "gold_bar", "Gold bar", ItemType.Material, 60, "Used in jewellery crafting.");
            RegisterStack(catalog, "silver_bar", "Silver bar", ItemType.Material, 45, "Used in silver crafting.");
        }

        private static void RegisterTools(ItemCatalog catalog)
        {
            RegisterGatheringWeapon(catalog, "bronze_hatchet", "Bronze hatchet", GatheringToolKind.Hatchet, 1, 20, 2, 1);
            RegisterGatheringWeapon(catalog, "iron_hatchet", "Iron hatchet", GatheringToolKind.Hatchet, 2, 50, 5, 3);
            RegisterGatheringWeapon(catalog, "steel_hatchet", "Steel hatchet", GatheringToolKind.Hatchet, 3, 120, 10, 6);
            RegisterGatheringWeapon(catalog, "mithril_hatchet", "Mithril hatchet", GatheringToolKind.Hatchet, 4, 300, 16, 10);
            RegisterGatheringWeapon(catalog, "adamant_hatchet", "Adamant hatchet", GatheringToolKind.Hatchet, 5, 700, 24, 15);
            RegisterGatheringWeapon(catalog, "rune_hatchet", "Rune hatchet", GatheringToolKind.Hatchet, 6, 1600, 34, 22);
            RegisterGatheringWeapon(catalog, "bronze_pickaxe", "Bronze pickaxe", GatheringToolKind.Pickaxe, 1, 20, 2, 1);
            RegisterGatheringWeapon(catalog, "iron_pickaxe", "Iron pickaxe", GatheringToolKind.Pickaxe, 2, 50, 5, 3);
            RegisterGatheringWeapon(catalog, "steel_pickaxe", "Steel pickaxe", GatheringToolKind.Pickaxe, 3, 120, 10, 6);
            RegisterGatheringWeapon(catalog, "mithril_pickaxe", "Mithril pickaxe", GatheringToolKind.Pickaxe, 4, 300, 16, 10);
            RegisterGatheringWeapon(catalog, "adamant_pickaxe", "Adamant pickaxe", GatheringToolKind.Pickaxe, 5, 700, 24, 15);
            RegisterGatheringWeapon(catalog, "rune_pickaxe", "Rune pickaxe", GatheringToolKind.Pickaxe, 6, 1600, 34, 22);

            RegisterGatheringTool(catalog, "small_fishing_net", "Small fishing net", GatheringToolKind.FishingNet, 1, 15, "Catch small fish.");
            RegisterGatheringTool(catalog, "fishing_rod", "Fishing rod", GatheringToolKind.FishingRod, 2, 25, "Catch fish with bait.");
            RegisterStack(catalog, "fishing_bait", "Fishing bait", ItemType.Material, 1, "Bait for a fishing rod.");
            RegisterGatheringTool(catalog, "lobster_pot", "Lobster pot", GatheringToolKind.LobsterPot, 3, 40, "Catch lobsters at deep water.");
            RegisterGatheringTool(catalog, "harpoon", "Harpoon", GatheringToolKind.Harpoon, 4, 60, "Catch large fish.");

            RegisterSimpleTool(catalog, "tinderbox", "Tinderbox", 5, "Light fires from logs.");
            RegisterSimpleTool(catalog, "hammer", "Hammer", 8, "Smith bars into equipment.");
            RegisterSimpleTool(catalog, "chisel", "Chisel", 5, "Cut gems and carve items.");
            RegisterSimpleTool(catalog, "needle", "Needle", 3, "Craft leather armor.");
            RegisterSimpleTool(catalog, "saw", "Saw", 6, "Cut planks for construction.");
            RegisterSimpleTool(catalog, "spade", "Spade", 4, "Plant and harvest crops.");
            RegisterSimpleTool(catalog, "seed_dibber", "Seed dibber", 4, "Plant seeds in farming patches.");
            RegisterSimpleTool(catalog, "pestle_and_mortar", "Pestle and mortar", 5, "Prepare ingredients for Herblore.");
        }

        private static void RegisterWood(ItemCatalog catalog)
        {
            for (var i = 0; i < Trees.Length; i++)
            {
                var tree = Trees[i];
                var logName = tree.Id == "normal" ? "Logs" : tree.Name + " logs";
                RegisterStack(catalog, tree.Id + "_logs", logName, ItemType.Resource, tree.Value,
                    "Wood useful for Firemaking, Fletching and Construction.");
            }
            RegisterStack(catalog, "plank", "Plank", ItemType.Material, 10, "Sawn wood used in Construction.");
        }

        private static void RegisterFish(ItemCatalog catalog)
        {
            for (var i = 0; i < Fish.Length; i++)
            {
                var fish = Fish[i];
                RegisterFood(catalog, "raw_" + fish.Id, "Raw " + fish.Name.ToLowerInvariant(), Round(fish.Heal * 4.0), 1,
                    "Best cooked before eating.");
                RegisterFood(catalog, "cooked_" + fish.Id, fish.Name, Round(fish.Heal * 4.0 * 1.6), fish.Heal,
                    "A tasty cooked fish.");
                RegisterFood(catalog, "burnt_" + fish.Id, "Burnt " + fish.Name.ToLowerInvariant(), 0, 0,
                    "Burnt beyond edibility.");
            }
        }

        private static void RegisterFarmingAndHerblore(ItemCatalog catalog)
        {
            RegisterSeedAndCrop(catalog, "potato", "Potato", 8, 4);
            RegisterSeedAndCrop(catalog, "onion", "Onion", 10, 3);
            RegisterSeedAndCrop(catalog, "cabbage", "Cabbage", 12, 3);
            RegisterSeedAndCrop(catalog, "sweetcorn", "Sweetcorn", 17, 6);
            RegisterSeedAndCrop(catalog, "strawberry", "Strawberry", 25, 8);
            RegisterSeedAndCrop(catalog, "watermelon", "Watermelon", 48, 14);

            for (var i = 0; i < Herbs.Length; i++)
            {
                var herb = Herbs[i];
                RegisterStack(catalog, herb.Id + "_seed", herb.Name + " seed", ItemType.Seed, 4,
                    "Plant on an herb patch.");
                RegisterStack(catalog, "grimy_" + herb.Id, "Grimy " + herb.Name.ToLowerInvariant(), ItemType.Material, herb.Xp * 4,
                    "An unclean herb.");
                RegisterStack(catalog, "clean_" + herb.Id, herb.Name, ItemType.Material, herb.Xp * 6,
                    "A cleaned herb ready for Herblore.");
            }

            RegisterStack(catalog, "vial_of_water", "Vial of water", ItemType.Material, 3, "The base of most potions.");
            RegisterStack(catalog, "eye_of_newt", "Eye of newt", ItemType.Material, 3, "A potion secondary.");
            RegisterStack(catalog, "unicorn_horn_dust", "Unicorn horn dust", ItemType.Material, 15, "A potion secondary.");
            RegisterStack(catalog, "limpwurt_root", "Limpwurt root", ItemType.Material, 20, "A potion secondary.");
            RegisterStack(catalog, "chocolate_dust", "Chocolate dust", ItemType.Material, 5, "A potion secondary.");
            for (var i = 0; i < Potions.Length; i++)
            {
                var potion = Potions[i];
                RegisterStack(catalog, potion.Id, potion.Name, ItemType.Potion, potion.Xp * 5, "Drink to gain a temporary effect.");
            }
        }

        private static void RegisterCrafting(ItemCatalog catalog)
        {
            RegisterStack(catalog, "cowhide", "Cowhide", ItemType.Material, 8, "Tan into leather at a tannery.");
            RegisterStack(catalog, "leather", "Leather", ItemType.Material, 15, "Craft into armor with a needle.");
            RegisterArmor(catalog, "leather_body", "Leather body", 40, EquipmentSlot.Chest, 4, 1, null);
            RegisterArmor(catalog, "leather_chaps", "Leather chaps", 30, EquipmentSlot.Legs, 3, 1, null);
            RegisterArmor(catalog, "leather_gloves", "Leather gloves", 10, EquipmentSlot.Hands, 1, 1, null);
            RegisterArmor(catalog, "leather_boots", "Leather boots", 10, EquipmentSlot.Boots, 1, 1, null);

            RegisterGemPair(catalog, "sapphire", "Sapphire", 15, 25);
            RegisterGemPair(catalog, "emerald", "Emerald", 30, 50);
            RegisterGemPair(catalog, "ruby", "Ruby", 50, 85);
            RegisterGemPair(catalog, "diamond", "Diamond", 100, 160);
            RegisterSimpleTool(catalog, "ring_mold", "Ring mold", 10, "Mold gold into rings.");
            RegisterSimpleTool(catalog, "amulet_mold", "Amulet mold", 10, "Mold gold into amulets.");

            catalog.Register(new ItemDefinition(
                new ContentId("gold_ring"), "Gold ring", ItemType.Armor, false, 100, "A plain gold ring.",
                new[] { EquipmentSlot.Ring1, EquipmentSlot.Ring2 }));
            catalog.Register(new ItemDefinition(
                new ContentId("sapphire_ring"), "Sapphire ring", ItemType.Armor, false, 150, "A gold ring set with sapphire.",
                new[] { EquipmentSlot.Ring1, EquipmentSlot.Ring2 }, false, new CombatBonuses { Magic = 2 }));
            catalog.Register(new ItemDefinition(
                new ContentId("gold_amulet"), "Gold amulet", ItemType.Armor, false, 150, "A plain gold amulet.",
                new[] { EquipmentSlot.Amulet }));
            catalog.Register(new ItemDefinition(
                new ContentId("ruby_amulet"), "Ruby amulet", ItemType.Armor, false, 300, "A gold amulet set with ruby.",
                new[] { EquipmentSlot.Amulet }, false, new CombatBonuses { Strength = 4 }));
        }

        private static void RegisterFletching(ItemCatalog catalog)
        {
            RegisterSimpleTool(catalog, "knife", "Knife", 6, "Whittle logs into bows and arrow shafts.");
            RegisterStack(catalog, "bow_string", "Bow string", ItemType.Material, 15, "Strung onto a bow to complete it.");
            RegisterStack(catalog, "flax", "Flax", ItemType.Resource, 3, "Spun into bow string.");
            RegisterStack(catalog, "feather", "Feather", ItemType.Material, 1, "Fletched onto arrow shafts.");
            RegisterStack(catalog, "arrow_shaft", "Arrow shaft", ItemType.Material, 1, "Used to make arrows.");

            for (var i = 0; i < Trees.Length; i++)
            {
                var tree = Trees[i];
                var label = tree.Id == "normal" ? "Shortbow" : tree.Name + " shortbow";
                catalog.Register(new ItemDefinition(
                    new ContentId(tree.Id + "_shortbow_u"), label + " (u)", ItemType.Material, false, tree.Value * 4,
                    "An unstrung shortbow."));
                var bow = new ItemDefinition(
                    new ContentId(tree.Id + "_shortbow"), label, ItemType.Weapon, false, tree.Value * 8,
                    "A finished shortbow.", new[] { EquipmentSlot.Weapon }, true,
                    new CombatBonuses { RangedAttack = 4 + i * 7, RangedStrength = 2 + i * 4 },
                    SkillId.Ranged, tree.RangedLevel);
                bow.AttackIntervalMilliseconds = 5 * CombatTickMilliseconds;
                bow.AttackRangeTiles = 6;
                catalog.Register(bow);
            }
        }

        private static void RegisterConstructionAndFood(ItemCatalog catalog)
        {
            RegisterStack(catalog, "stone", "Stone", ItemType.Resource, 3, "Quarried stone for building.");
            RegisterStack(catalog, "nails", "Nails", ItemType.Material, 2, "Iron nails for construction.");
            RegisterFood(catalog, "raw_meat", "Raw meat", 4, 1, "Cook it before eating.");
            RegisterFood(catalog, "cooked_meat", "Cooked meat", 10, 6, "A hearty meal.");
            RegisterFood(catalog, "burnt_meat", "Burnt meat", 0, 0, "Charred and inedible.");
            RegisterFood(catalog, "bread", "Bread", 6, 5, "Simple travel food.");
            RegisterStack(catalog, "bones", "Bones", ItemType.Material, 1, "Could be useful for something later.");
        }

        private static void RegisterWeapon(
            ItemCatalog catalog,
            string id,
            string name,
            int value,
            string description,
            int attack,
            int strength,
            int requirementLevel,
            int speedTicks,
            bool twoHanded)
        {
            var item = new ItemDefinition(
                new ContentId(id), name, ItemType.Weapon, false, value, description,
                new[] { EquipmentSlot.Weapon }, twoHanded,
                new CombatBonuses { Attack = attack, Strength = strength }, SkillId.Attack, requirementLevel);
            item.AttackIntervalMilliseconds = speedTicks * CombatTickMilliseconds;
            item.AttackRangeTiles = 1;
            catalog.Register(item);
        }

        private static void RegisterArmor(
            ItemCatalog catalog,
            string id,
            string name,
            int value,
            EquipmentSlot slot,
            int defence,
            int requirementLevel,
            SkillId? requirementSkill = SkillId.Defence)
        {
            catalog.Register(new ItemDefinition(
                new ContentId(id), name, ItemType.Armor, false, value, "Protective equipment.",
                new[] { slot }, false, new CombatBonuses { Defence = defence }, requirementSkill, requirementLevel));
        }

        private static void RegisterGatheringWeapon(
            ItemCatalog catalog,
            string id,
            string name,
            GatheringToolKind kind,
            int tier,
            int value,
            int attack,
            int strength)
        {
            var item = new ItemDefinition(
                new ContentId(id), name, ItemType.Tool, false, value, "A gathering tool.",
                new[] { EquipmentSlot.Weapon }, false,
                new CombatBonuses { Attack = attack, Strength = strength }, null, 1, 0, tier, kind);
            item.AttackIntervalMilliseconds = 5 * CombatTickMilliseconds;
            item.AttackRangeTiles = 1;
            catalog.Register(item);
        }

        private static void RegisterGatheringTool(
            ItemCatalog catalog,
            string id,
            string name,
            GatheringToolKind kind,
            int tier,
            int value,
            string description)
            => catalog.Register(new ItemDefinition(
                new ContentId(id), name, ItemType.Tool, false, value, description,
                null, false, null, null, 1, 0, tier, kind));

        private static void RegisterSimpleTool(ItemCatalog catalog, string id, string name, int value, string description)
            => catalog.Register(new ItemDefinition(new ContentId(id), name, ItemType.Tool, false, value, description));

        private static void RegisterStack(ItemCatalog catalog, string id, string name, ItemType type, int value, string description)
            => catalog.Register(new ItemDefinition(new ContentId(id), name, type, true, value, description));

        private static void RegisterFood(ItemCatalog catalog, string id, string name, int value, int heal, string description)
            => catalog.Register(new ItemDefinition(new ContentId(id), name, ItemType.Food, true, value, description, null, false, null, null, 1, heal));

        private static void RegisterSeedAndCrop(ItemCatalog catalog, string id, string name, int xp, int heal)
        {
            RegisterStack(catalog, id + "_seed", name + " seed", ItemType.Seed, 2, "Plant on a farming patch.");
            RegisterFood(catalog, id, name, xp * 3, heal, "A fresh crop.");
        }

        private static void RegisterGemPair(ItemCatalog catalog, string id, string name, int uncutValue, int cutValue)
        {
            RegisterStack(catalog, "uncut_" + id, "Uncut " + name.ToLowerInvariant(), ItemType.Material, uncutValue, "Cut with a chisel.");
            RegisterStack(catalog, id, name, ItemType.Material, cutValue, "A cut gem.");
        }

        private static int Round(double value) => (int)Math.Floor(value + 0.5);

        private readonly struct MetalTier
        {
            public MetalTier(string id, string name, int tier, int smithLevel, int value, int combatLevel)
            { Id = id; Name = name; Tier = tier; SmithLevel = smithLevel; Value = value; CombatLevel = combatLevel; }
            public string Id { get; }
            public string Name { get; }
            public int Tier { get; }
            public int SmithLevel { get; }
            public int Value { get; }
            public int CombatLevel { get; }
        }

        private readonly struct TreeTier
        {
            public TreeTier(string id, string name, int level, int xp, int value, int rangedLevel)
            { Id = id; Name = name; Level = level; Xp = xp; Value = value; RangedLevel = rangedLevel; }
            public string Id { get; }
            public string Name { get; }
            public int Level { get; }
            public int Xp { get; }
            public int Value { get; }
            public int RangedLevel { get; }
        }

        private readonly struct FishTier
        {
            public FishTier(string id, string name, int level, int xp, int heal)
            { Id = id; Name = name; Level = level; Xp = xp; Heal = heal; }
            public string Id { get; }
            public string Name { get; }
            public int Level { get; }
            public int Xp { get; }
            public int Heal { get; }
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
    }
}
