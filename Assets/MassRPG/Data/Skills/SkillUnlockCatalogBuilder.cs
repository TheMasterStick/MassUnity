using System;
using System.Collections.Generic;
using System.Text;
using MassRPG.Core.Content;
using MassRPG.Core.Resources;
using MassRPG.Core.Skills;
using MassRPG.Data.Construction;
using MassRPG.Data.Items;
using MassRPG.Data.Recipes;
using MassRPG.Data.Resources;

namespace MassRPG.Data.Skills
{
    /// <summary>
    /// Builds the migrated skillbook ledger from published gameplay data instead of hard-coded UI
    /// arrays. Recipes, item requirements, gathering resources and modular construction pieces can
    /// all feed the same ledger used by the Unity client.
    /// </summary>
    public static class SkillUnlockCatalogBuilder
    {
        public static SkillUnlockCatalog Build(
            RecipeCatalog recipes,
            ItemCatalog items,
            ResourceCatalog resources = null,
            BuildPieceCatalog buildPieces = null)
        {
            if (recipes == null) throw new ArgumentNullException(nameof(recipes));
            if (items == null) throw new ArgumentNullException(nameof(items));

            var catalog = new SkillUnlockCatalog();
            AddRecipeUnlocks(catalog, recipes, items);
            AddItemRequirementUnlocks(catalog, items);
            if (resources != null) AddGatheringUnlocks(catalog, resources, items);
            if (buildPieces != null) AddConstructionUnlocks(catalog, buildPieces, items);
            return catalog;
        }

        private static void AddRecipeUnlocks(
            SkillUnlockCatalog catalog,
            RecipeCatalog recipes,
            ItemCatalog items)
        {
            // Several recipes may represent alternate assembly routes for the same item. The old
            // browser skillbook intentionally showed that unlock once; preserve that behavior by
            // deduplicating on skill + level + output identity rather than recipe identity.
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var recipe in recipes.All)
            {
                var key = recipe.Skill + "|" + recipe.LevelRequired + "|" + recipe.OutputItemId.Value;
                if (!seen.Add(key)) continue;

                var outputName = NameOf(items, recipe.OutputItemId);
                catalog.Register(new SkillUnlockDefinition(
                    recipe.Skill,
                    recipe.LevelRequired,
                    SkillUnlockKind.Recipe,
                    FriendlyCategory(recipe.Category),
                    outputName,
                    BuildRecipeDetail(recipe, items),
                    recipe.Id));
            }
        }

        private static void AddItemRequirementUnlocks(SkillUnlockCatalog catalog, ItemCatalog items)
        {
            foreach (var item in items.All)
            {
                if (!item.EquipRequirementSkill.HasValue) continue;

                var verb = item.Type == ItemType.Ammunition ? "Use " : "Equip ";
                catalog.Register(new SkillUnlockDefinition(
                    item.EquipRequirementSkill.Value,
                    item.EquipRequirementLevel,
                    SkillUnlockKind.ItemRequirement,
                    ItemRequirementCategory(item),
                    verb + item.DisplayName,
                    item.Description,
                    item.Id));
            }
        }

        private static void AddGatheringUnlocks(
            SkillUnlockCatalog catalog,
            ResourceCatalog resources,
            ItemCatalog items)
        {
            foreach (var resource in resources.All)
            {
                var detail = new StringBuilder();
                detail.Append(resource.Experience).Append(" XP");
                if (resource.RequiredToolKind != GatheringToolKind.None)
                {
                    detail.Append(" · Requires ")
                        .Append(Humanize(resource.RequiredToolKind.ToString()))
                        .Append(" tier ")
                        .Append(resource.MinimumToolTier);
                }
                detail.Append(" · Yields ").Append(NameOf(items, resource.YieldItemId));

                catalog.Register(new SkillUnlockDefinition(
                    resource.GatheringSkill,
                    resource.RequiredLevel,
                    SkillUnlockKind.Gathering,
                    GatheringCategory(resource.GatheringSkill),
                    GatheringVerb(resource.GatheringSkill) + " " + resource.DisplayName,
                    detail.ToString(),
                    resource.Id));
            }
        }

        private static void AddConstructionUnlocks(
            SkillUnlockCatalog catalog,
            BuildPieceCatalog buildPieces,
            ItemCatalog items)
        {
            foreach (var piece in buildPieces.All)
            {
                var detail = new StringBuilder();
                for (var i = 0; i < piece.Costs.Count; i++)
                {
                    if (i > 0) detail.Append(", ");
                    var cost = piece.Costs[i];
                    detail.Append(cost.Quantity).Append('x').Append(' ').Append(NameOf(items, cost.ItemId));
                }
                if (piece.ConstructionXp > 0)
                    detail.Append(" · ").Append(piece.ConstructionXp).Append(" XP");

                catalog.Register(new SkillUnlockDefinition(
                    SkillId.Construction,
                    piece.ConstructionLevel,
                    SkillUnlockKind.Construction,
                    ConstructionCategory(piece.Kind),
                    "Build " + piece.DisplayName,
                    detail.ToString(),
                    piece.Id));
            }
        }

        private static string BuildRecipeDetail(RecipeDefinition recipe, ItemCatalog items)
        {
            var text = new StringBuilder();
            for (var i = 0; i < recipe.Inputs.Count; i++)
            {
                if (i > 0) text.Append(", ");
                var input = recipe.Inputs[i];
                text.Append(input.Quantity).Append('x').Append(' ').Append(NameOf(items, input.ItemId));
            }

            if (recipe.ToolRequiredId.HasValue)
                text.Append(" · Tool: ").Append(NameOf(items, recipe.ToolRequiredId.Value));
            if (recipe.StationId.HasValue)
                text.Append(" · Station: ").Append(Humanize(recipe.StationId.Value.Value));
            if (recipe.Xp > 0)
                text.Append(" · ").Append(recipe.Xp).Append(" XP");
            return text.ToString();
        }

        private static string NameOf(ItemCatalog items, ContentId itemId)
            => items.TryGetDefinition(itemId, out var item) && !string.IsNullOrWhiteSpace(item.DisplayName)
                ? item.DisplayName
                : Humanize(itemId.Value);

        private static string ItemRequirementCategory(ItemDefinition item)
        {
            switch (item.Type)
            {
                case ItemType.Weapon: return "Weapons";
                case ItemType.Armor: return "Armour";
                case ItemType.Ammunition: return "Ammunition";
                case ItemType.Tool: return "Tools";
                default: return "Equipment";
            }
        }

        private static string GatheringCategory(SkillId skill)
        {
            switch (skill)
            {
                case SkillId.Woodcutting: return "Trees";
                case SkillId.Mining: return "Ores";
                case SkillId.Fishing: return "Fish";
                case SkillId.Farming: return "Crops";
                default: return "Resources";
            }
        }

        private static string GatheringVerb(SkillId skill)
        {
            switch (skill)
            {
                case SkillId.Woodcutting: return "Cut";
                case SkillId.Mining: return "Mine";
                case SkillId.Fishing: return "Catch";
                case SkillId.Farming: return "Harvest";
                default: return "Gather";
            }
        }

        private static string ConstructionCategory(BuildPieceKind kind)
        {
            switch (kind)
            {
                case BuildPieceKind.Workstation: return "Workstations";
                case BuildPieceKind.Container: return "Storage";
                case BuildPieceKind.Bed: return "Furniture";
                case BuildPieceKind.Decoration: return "Decoration";
                default: return "Structures";
            }
        }

        private static string FriendlyCategory(string category)
        {
            if (string.IsNullOrWhiteSpace(category)) return "Recipes";
            switch (category)
            {
                case "smelting": return "Smelting";
                case "smithing_misc": return "Materials";
                case "smithing_ammo": return "Ammunition";
                case "cooking": return "Food";
                case "fletching_bows": return "Bows";
                case "fletching_arrows": return "Arrows";
                case "crafting_leather": return "Leather";
                case "crafting_gems": return "Gems";
                case "crafting_jewelry": return "Jewellery";
                case "crafting_misc": return "Materials";
                case "construction": return "Materials";
                case "herblore_clean": return "Herbs";
                case "herblore_potions": return "Potions";
                default: return Humanize(category);
            }
        }

        private static string Humanize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var source = value.Replace('.', ' ').Replace('_', ' ').Replace('-', ' ').Replace('/', ' ');
            var text = new StringBuilder(source.Length);
            var capitalize = true;
            for (var i = 0; i < source.Length; i++)
            {
                var c = source[i];
                if (char.IsWhiteSpace(c))
                {
                    text.Append(c);
                    capitalize = true;
                    continue;
                }
                text.Append(capitalize ? char.ToUpperInvariant(c) : c);
                capitalize = false;
            }
            return text.ToString();
        }
    }
}
