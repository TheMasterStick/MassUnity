using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MassRPG.Data.Creatures;
using MassRPG.Data.Items;
using MassRPG.Data.Recipes;
using UnityEditor;
using UnityEngine;

namespace MassRPG.Editor.Data
{
    /// <summary>
    /// One-way migration helper that materializes the temporary hard-coded C# seed catalogs into
    /// repository JSON. Existing files are never overwritten, so browser-authored edits remain the
    /// source of truth once a seed has been materialized.
    /// </summary>
    public static class MigrationSeedDraftExporter
    {
        [Serializable]
        private sealed class BonusesJson
        {
            public int attack;
            public int strength;
            public int defence;
            public int rangedAttack;
            public int rangedStrength;
            public int magic;
        }

        [Serializable]
        private sealed class PresentationJson
        {
            public string assetState = "needs-assets";
            public string iconAssetId;
            public string modelAssetId;
            public string portraitAssetId;
            public string animationSetAssetId;
            public string notes = "Materialized from the temporary migration seed catalog. Replace/link production presentation assets when available.";
        }

        [Serializable]
        private sealed class ItemJson
        {
            public int schemaVersion = 1;
            public string id;
            public string displayName;
            public string description;
            public string type;
            public bool stackable;
            public int value;
            public string[] allowedEquipmentSlots;
            public bool twoHanded;
            public bool canDualWield;
            public string equipRequirementSkill;
            public int equipRequirementLevel;
            public int healAmount;
            public int toolTier;
            public string gatheringToolKind;
            public int attackIntervalMilliseconds;
            public int attackRangeTiles;
            public BonusesJson bonuses;
            public PresentationJson presentation;
            public string editorState = "draft";
        }

        [Serializable]
        private sealed class CreatureJson
        {
            public int schemaVersion = 1;
            public string id;
            public string displayName;
            public int combatLevel;
            public int maxHitpoints;
            public int attackLevel;
            public int strengthLevel;
            public int defenceLevel;
            public int attackBonus;
            public int strengthBonus;
            public int defenceBonus;
            public string combatStyle;
            public int attackIntervalMilliseconds;
            public int attackRangeTiles;
            public string disposition;
            public int footprintWidth;
            public int footprintHeight;
            public int aggroRadiusTiles;
            public int leashRadiusTiles;
            public bool persistentNamedInstance;
            public PresentationJson presentation;
            public string editorState = "draft";
        }

        [Serializable]
        private sealed class IngredientJson
        {
            public string itemId;
            public int quantity;
        }

        [Serializable]
        private sealed class RecipeJson
        {
            public int schemaVersion = 1;
            public string id;
            public string displayName;
            public string skill;
            public int levelRequired;
            public IngredientJson[] inputs;
            public string outputItemId;
            public int outputQuantity;
            public long xp;
            public int durationMilliseconds;
            public string category;
            public string stationId;
            public string toolRequiredId;
            public bool canBurn;
            public string failureOutputItemId;
            public double failureXpFraction;
            public PresentationJson presentation;
            public string editorState = "draft";
        }

        private sealed class ExportSummary
        {
            public int Created;
            public int SkippedExisting;
        }

        [MenuItem("MassRPG/Data/Materialize Migration Seed Drafts", priority = 40)]
        public static void MaterializeMigrationSeedDrafts()
        {
            if (!EditorUtility.DisplayDialog(
                    "Materialize migration seed drafts?",
                    "This creates repository JSON for the current hard-coded item, creature and recipe migration catalogs. Existing draft files are never overwritten.\n\nAfter committing these files, the Online Data Editor can edit the old migrated catalog just like newly authored content.",
                    "Create missing drafts",
                    "Cancel"))
                return;

            try
            {
                var items = ExportItems();
                var creatures = ExportCreatures();
                var recipes = ExportRecipes();
                var created = items.Created + creatures.Created + recipes.Created;
                var skipped = items.SkippedExisting + creatures.SkippedExisting + recipes.SkippedExisting;

                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog(
                    "Migration seed drafts materialized",
                    $"Created {created} repository draft files.\nSkipped {skipped} existing files without touching them.\n\nItems: {items.Created} created / {items.SkippedExisting} existing\nCreatures: {creatures.Created} / {creatures.SkippedExisting}\nRecipes: {recipes.Created} / {recipes.SkippedExisting}\n\nReview the generated ContentData/Drafts files, run validation, then commit them when ready.",
                    "OK");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("Migration seed export failed", ex.Message, "OK");
            }
        }

        private static ExportSummary ExportItems()
        {
            var root = RepositoryDraftItemStore.ItemDraftRoot;
            Directory.CreateDirectory(root);
            var summary = new ExportSummary();
            foreach (var item in MigrationSeedItemCatalog.Create().All.OrderBy(value => value.Id.Value, StringComparer.Ordinal))
            {
                var file = Path.Combine(root, item.Id.Value + ".json");
                if (File.Exists(file))
                {
                    summary.SkippedExisting++;
                    continue;
                }

                var document = new ItemJson
                {
                    id = item.Id.Value,
                    displayName = item.DisplayName,
                    description = item.Description,
                    type = item.Type.ToString(),
                    stackable = item.Stackable,
                    value = item.Value,
                    allowedEquipmentSlots = item.AllowedEquipmentSlots.Select(slot => slot.ToString()).Distinct().ToArray(),
                    twoHanded = item.TwoHanded,
                    canDualWield = item.CanDualWield,
                    equipRequirementSkill = item.EquipRequirementSkill.HasValue ? item.EquipRequirementSkill.Value.ToString() : null,
                    equipRequirementLevel = item.EquipRequirementLevel,
                    healAmount = item.HealAmount,
                    toolTier = item.ToolTier,
                    gatheringToolKind = item.GatheringToolKind.ToString(),
                    attackIntervalMilliseconds = item.AttackIntervalMilliseconds,
                    attackRangeTiles = item.AttackRangeTiles,
                    bonuses = new BonusesJson
                    {
                        attack = item.Bonuses.Attack,
                        strength = item.Bonuses.Strength,
                        defence = item.Bonuses.Defence,
                        rangedAttack = item.Bonuses.RangedAttack,
                        rangedStrength = item.Bonuses.RangedStrength,
                        magic = item.Bonuses.Magic
                    },
                    presentation = new PresentationJson()
                };
                Write(file, document);
                summary.Created++;
            }
            return summary;
        }

        private static ExportSummary ExportCreatures()
        {
            var root = RepositoryDraftGameDataStore.CreatureDraftRoot;
            Directory.CreateDirectory(root);
            var summary = new ExportSummary();
            foreach (var creature in MigrationSeedCreatureCatalog.Create().All.OrderBy(value => value.Id.Value, StringComparer.Ordinal))
            {
                var file = Path.Combine(root, creature.Id.Value + ".json");
                if (File.Exists(file))
                {
                    summary.SkippedExisting++;
                    continue;
                }

                var document = new CreatureJson
                {
                    id = creature.Id.Value,
                    displayName = creature.DisplayName,
                    combatLevel = creature.CombatLevel,
                    maxHitpoints = creature.MaxHitpoints,
                    attackLevel = creature.AttackLevel,
                    strengthLevel = creature.StrengthLevel,
                    defenceLevel = creature.DefenceLevel,
                    attackBonus = creature.AttackBonus,
                    strengthBonus = creature.StrengthBonus,
                    defenceBonus = creature.DefenceBonus,
                    combatStyle = creature.CombatStyle.ToString(),
                    attackIntervalMilliseconds = creature.AttackIntervalMilliseconds,
                    attackRangeTiles = creature.AttackRangeTiles,
                    disposition = creature.Disposition.ToString(),
                    footprintWidth = creature.Footprint.Width,
                    footprintHeight = creature.Footprint.Height,
                    aggroRadiusTiles = creature.AggroRadiusTiles,
                    leashRadiusTiles = creature.LeashRadiusTiles,
                    persistentNamedInstance = creature.PersistentNamedInstance,
                    presentation = new PresentationJson()
                };
                Write(file, document);
                summary.Created++;
            }
            return summary;
        }

        private static ExportSummary ExportRecipes()
        {
            var root = RepositoryDraftGameDataStore.RecipeDraftRoot;
            Directory.CreateDirectory(root);
            var summary = new ExportSummary();
            foreach (var recipe in MigrationSeedRecipeCatalog.Create().All.OrderBy(value => value.Id.Value, StringComparer.Ordinal))
            {
                var file = Path.Combine(root, recipe.Id.Value + ".json");
                if (File.Exists(file))
                {
                    summary.SkippedExisting++;
                    continue;
                }

                var inputs = new IngredientJson[recipe.Inputs.Count];
                for (var i = 0; i < recipe.Inputs.Count; i++)
                {
                    inputs[i] = new IngredientJson
                    {
                        itemId = recipe.Inputs[i].ItemId.Value,
                        quantity = recipe.Inputs[i].Quantity
                    };
                }

                var document = new RecipeJson
                {
                    id = recipe.Id.Value,
                    displayName = recipe.DisplayName,
                    skill = recipe.Skill.ToString(),
                    levelRequired = recipe.LevelRequired,
                    inputs = inputs,
                    outputItemId = recipe.OutputItemId.Value,
                    outputQuantity = recipe.OutputQuantity,
                    xp = recipe.Xp,
                    durationMilliseconds = recipe.DurationMilliseconds,
                    category = recipe.Category,
                    stationId = recipe.StationId.HasValue ? recipe.StationId.Value.Value : null,
                    toolRequiredId = recipe.ToolRequiredId.HasValue ? recipe.ToolRequiredId.Value.Value : null,
                    canBurn = recipe.CanBurn,
                    failureOutputItemId = recipe.FailureOutputItemId.HasValue ? recipe.FailureOutputItemId.Value.Value : null,
                    failureXpFraction = recipe.FailureXpFraction,
                    presentation = new PresentationJson
                    {
                        assetState = "not-required",
                        notes = "Materialized from the temporary migration seed catalog. Recipe presentation normally comes from its inputs/outputs/station rather than a standalone model."
                    }
                };
                Write(file, document);
                summary.Created++;
            }
            return summary;
        }

        private static void Write<T>(string path, T document)
        {
            File.WriteAllText(path, JsonUtility.ToJson(document, true) + Environment.NewLine);
        }
    }
}
