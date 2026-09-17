using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Creatures;
using MassRPG.Core.Resources;
using MassRPG.Core.Skills;
using MassRPG.Data.Creatures;
using MassRPG.Data.Recipes;
using MassRPG.Data.Resources;
using UnityEngine;

namespace MassRPG.Editor.Data
{
    [Serializable]
    public sealed class CreatureDraftJson
    {
        public int schemaVersion = 1;
        public string id = string.Empty;
        public string displayName = string.Empty;
        public int combatLevel = 1;
        public int maxHitpoints = 1;
        public int attackLevel = 1;
        public int strengthLevel = 1;
        public int defenceLevel = 1;
        public int attackBonus;
        public int strengthBonus;
        public int defenceBonus;
        public string combatStyle = "Melee";
        public int attackIntervalMilliseconds = 2400;
        public int attackRangeTiles = 1;
        public string disposition = "Neutral";
        public int footprintWidth = 1;
        public int footprintHeight = 1;
        public int aggroRadiusTiles = 4;
        public int leashRadiusTiles = 8;
        public bool persistentNamedInstance;
        public string editorState = "draft";
    }

    [Serializable]
    public sealed class ResourceDraftJson
    {
        public int schemaVersion = 1;
        public string id = string.Empty;
        public string displayName = string.Empty;
        public string gatheringSkill = "Mining";
        public int requiredLevel = 1;
        public int experience;
        public string yieldItemId = string.Empty;
        public int respawnSeconds;
        public string availabilityMode = "Personal";
        public int minimumYield = 1;
        public int maximumYield = 1;
        public string requiredToolKind = "None";
        public int minimumToolTier;
        public string editorState = "draft";
    }

    [Serializable]
    public sealed class RecipeDraftIngredientJson
    {
        public string itemId = string.Empty;
        public int quantity = 1;
    }

    [Serializable]
    public sealed class RecipeDraftJson
    {
        public int schemaVersion = 1;
        public string id = string.Empty;
        public string displayName = string.Empty;
        public string skill = "Cooking";
        public int levelRequired = 1;
        public RecipeDraftIngredientJson[] inputs = Array.Empty<RecipeDraftIngredientJson>();
        public string outputItemId = string.Empty;
        public int outputQuantity = 1;
        public long xp;
        public int durationMilliseconds = 1200;
        public string category = string.Empty;
        public string stationId;
        public string toolRequiredId;
        public bool canBurn;
        public string failureOutputItemId;
        public double failureXpFraction = 0.10;
        public string editorState = "draft";
    }

    public sealed class RepositoryDraftRecord<TDefinition> where TDefinition : class
    {
        public RepositoryDraftRecord(string filePath, string id, string displayName, string editorState, TDefinition definition, string error)
        {
            FilePath = filePath ?? string.Empty;
            Id = id ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            EditorState = editorState ?? "draft";
            Definition = definition;
            Error = error ?? string.Empty;
        }

        public string FilePath { get; }
        public string Id { get; }
        public string DisplayName { get; }
        public string EditorState { get; }
        public TDefinition Definition { get; }
        public string Error { get; }
        public bool IsValid => Definition != null && string.IsNullOrEmpty(Error);
        public bool ReadyForReview => string.Equals(EditorState, "ready-for-review", StringComparison.Ordinal);
    }

    /// <summary>
    /// Unity readers for browser-authored creature/resource/recipe drafts. The conversion step is
    /// intentionally strict so malformed repository JSON cannot quietly become runtime content.
    /// </summary>
    public static class RepositoryDraftGameDataStore
    {
        public static string CreatureDraftRoot => Path.Combine(RepositoryDraftItemStore.RepositoryRoot, "ContentData", "Drafts", "creatures");
        public static string ResourceDraftRoot => Path.Combine(RepositoryDraftItemStore.RepositoryRoot, "ContentData", "Drafts", "resources");
        public static string RecipeDraftRoot => Path.Combine(RepositoryDraftItemStore.RepositoryRoot, "ContentData", "Drafts", "recipes");

        public static IReadOnlyList<RepositoryDraftRecord<CreatureDefinition>> LoadCreatures()
			=> Load<CreatureDraftJson, CreatureDefinition>(
                CreatureDraftRoot,
                json => JsonUtility.FromJson<CreatureDraftJson>(json),
                document => document?.id,
                document => document?.displayName,
                document => document?.editorState,
                TryConvertCreature);

        public static IReadOnlyList<RepositoryDraftRecord<ResourceDefinition>> LoadResources()
			=> Load<ResourceDraftJson, ResourceDefinition>(
                ResourceDraftRoot,
                json => JsonUtility.FromJson<ResourceDraftJson>(json),
                document => document?.id,
                document => document?.displayName,
                document => document?.editorState,
                TryConvertResource);

        public static IReadOnlyList<RepositoryDraftRecord<RecipeDefinition>> LoadRecipes()
			=> Load<RecipeDraftJson, RecipeDefinition>(
                RecipeDraftRoot,
                json => JsonUtility.FromJson<RecipeDraftJson>(json),
                document => document?.id,
                document => document?.displayName,
                document => document?.editorState,
                TryConvertRecipe);

        public static CreatureCatalog CreateResolvedCreatureCatalog(out IReadOnlyList<RepositoryDraftRecord<CreatureDefinition>> records)
        {
            records = LoadCreatures();
            var resolved = new Dictionary<ContentId, CreatureDefinition>();
            foreach (var seed in MigrationSeedCreatureCatalog.Create().All) resolved[seed.Id] = seed;
            for (var i = 0; i < records.Count; i++) if (records[i].IsValid) resolved[records[i].Definition.Id] = records[i].Definition;
            var catalog = new CreatureCatalog();
            foreach (var definition in resolved.Values.OrderBy(value => value.Id.Value, StringComparer.Ordinal)) catalog.Register(definition);
            return catalog;
        }

        public static ResourceCatalog CreateDraftResourceCatalog(out IReadOnlyList<RepositoryDraftRecord<ResourceDefinition>> records)
        {
            records = LoadResources();
            var catalog = new ResourceCatalog();
            for (var i = 0; i < records.Count; i++) if (records[i].IsValid) catalog.Register(records[i].Definition);
            return catalog;
        }

        public static RecipeCatalog CreateResolvedRecipeCatalog(out IReadOnlyList<RepositoryDraftRecord<RecipeDefinition>> records)
        {
            records = LoadRecipes();
            var resolved = new Dictionary<ContentId, RecipeDefinition>();
            foreach (var seed in MigrationSeedRecipeCatalog.Create().All) resolved[seed.Id] = seed;
            for (var i = 0; i < records.Count; i++) if (records[i].IsValid) resolved[records[i].Definition.Id] = records[i].Definition;
            var catalog = new RecipeCatalog();
            foreach (var definition in resolved.Values.OrderBy(value => value.Id.Value, StringComparer.Ordinal)) catalog.Register(definition);
            return catalog;
        }

        private delegate bool Converter<TDocument, TDefinition>(TDocument document, out TDefinition definition, out string error)
            where TDocument : class
            where TDefinition : class;

        private static IReadOnlyList<RepositoryDraftRecord<TDefinition>> Load<TDocument, TDefinition>(
            string root,
            Func<string, TDocument> parse,
            Func<TDocument, string> idOf,
            Func<TDocument, string> nameOf,
            Func<TDocument, string> stateOf,
            Converter<TDocument, TDefinition> converter)
            where TDocument : class
            where TDefinition : class
        {
            if (!Directory.Exists(root)) return Array.Empty<RepositoryDraftRecord<TDefinition>>();
            var files = Directory.GetFiles(root, "*.json", SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            var records = new List<RepositoryDraftRecord<TDefinition>>(files.Length);
            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (var i = 0; i < files.Length; i++)
            {
                var file = files[i];
                TDocument document = null;
                try
                {
                    document = parse(File.ReadAllText(file));
                    if (document == null) throw new InvalidDataException("JSON did not produce a draft document.");
                    var id = idOf(document) ?? string.Empty;
                    var name = nameOf(document) ?? "(unnamed draft)";
                    var state = stateOf(document) ?? "draft";
                    if (!converter(document, out var definition, out var error))
                    {
                        records.Add(new RepositoryDraftRecord<TDefinition>(file, id, name, state, null, error));
                        continue;
                    }
                    if (!seen.Add(id))
                    {
                        records.Add(new RepositoryDraftRecord<TDefinition>(file, id, name, state, null, $"Duplicate draft permanent ID '{id}'."));
                        continue;
                    }
                    var fileId = Path.GetFileNameWithoutExtension(file);
                    if (!string.Equals(fileId, id, StringComparison.Ordinal))
                    {
                        records.Add(new RepositoryDraftRecord<TDefinition>(file, id, name, state, null, $"Filename '{fileId}.json' does not match permanent ID '{id}'."));
                        continue;
                    }
                    records.Add(new RepositoryDraftRecord<TDefinition>(file, id, name, state, definition, string.Empty));
                }
                catch (Exception ex)
                {
                    records.Add(new RepositoryDraftRecord<TDefinition>(
                        file,
                        document == null ? Path.GetFileNameWithoutExtension(file) : idOf(document),
                        document == null ? "(invalid draft)" : nameOf(document),
                        document == null ? "draft" : stateOf(document),
                        null,
                        ex.Message));
                }
            }

            return records
                .OrderBy(record => record.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(record => record.Id, StringComparer.Ordinal)
                .ToArray();
        }

        private static bool TryConvertCreature(CreatureDraftJson document, out CreatureDefinition definition, out string error)
        {
            definition = null;
            if (!ValidateIdentity(document?.schemaVersion ?? 0, document?.id, document?.displayName, out var id, out error)) return false;
            if (!Enum.TryParse(document.combatStyle, false, out CombatStyle style)) return Fail($"Unknown combat style '{document.combatStyle}'.", out definition, out error);
            if (!Enum.TryParse(document.disposition, false, out CreatureDisposition disposition)) return Fail($"Unknown disposition '{document.disposition}'.", out definition, out error);
            try
            {
                definition = new CreatureDefinition(
                    id,
                    document.displayName,
                    document.combatLevel,
                    document.maxHitpoints,
                    document.attackLevel,
                    document.strengthLevel,
                    document.defenceLevel,
                    document.attackBonus,
                    document.strengthBonus,
                    document.defenceBonus,
                    style,
                    document.attackIntervalMilliseconds,
                    document.attackRangeTiles,
                    disposition,
                    new CreatureFootprint(document.footprintWidth, document.footprintHeight),
                    document.aggroRadiusTiles,
                    document.leashRadiusTiles,
                    document.persistentNamedInstance);
                error = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryConvertResource(ResourceDraftJson document, out ResourceDefinition definition, out string error)
        {
            definition = null;
            if (!ValidateIdentity(document?.schemaVersion ?? 0, document?.id, document?.displayName, out var id, out error)) return false;
            if (!ContentId.TryCreate(document.yieldItemId, out var yieldItemId)) return Fail("Yield item ID is invalid.", out definition, out error);
            if (!Enum.TryParse(document.gatheringSkill, false, out SkillId skill)) return Fail($"Unknown gathering skill '{document.gatheringSkill}'.", out definition, out error);
            if (!Enum.TryParse(document.availabilityMode, false, out ResourceAvailabilityMode availability)) return Fail($"Unknown availability mode '{document.availabilityMode}'.", out definition, out error);
            if (!Enum.TryParse(document.requiredToolKind, false, out GatheringToolKind toolKind)) return Fail($"Unknown gathering tool kind '{document.requiredToolKind}'.", out definition, out error);
            try
            {
                definition = new ResourceDefinition(
                    id,
                    document.displayName,
                    skill,
                    document.requiredLevel,
                    document.experience,
                    yieldItemId,
                    document.respawnSeconds,
                    availability,
                    document.minimumYield,
                    document.maximumYield,
                    toolKind,
                    document.minimumToolTier);
                error = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryConvertRecipe(RecipeDraftJson document, out RecipeDefinition definition, out string error)
        {
            definition = null;
            if (!ValidateIdentity(document?.schemaVersion ?? 0, document?.id, document?.displayName, out var id, out error)) return false;
            if (!Enum.TryParse(document.skill, false, out SkillId skill)) return Fail($"Unknown recipe skill '{document.skill}'.", out definition, out error);
            if (!ContentId.TryCreate(document.outputItemId, out var outputId)) return Fail("Output item ID is invalid.", out definition, out error);
            var sourceInputs = document.inputs ?? Array.Empty<RecipeDraftIngredientJson>();
            if (sourceInputs.Length == 0) return Fail("A recipe requires at least one input.", out definition, out error);
            var inputs = new RecipeIngredient[sourceInputs.Length];
            for (var i = 0; i < sourceInputs.Length; i++)
            {
                var input = sourceInputs[i];
                if (input == null || !ContentId.TryCreate(input.itemId, out var itemId) || input.quantity < 1)
                    return Fail($"Recipe input {i + 1} is invalid.", out definition, out error);
                inputs[i] = new RecipeIngredient(itemId, input.quantity);
            }

            if (!TryOptionalId(document.stationId, out var stationId, out error)) return false;
            if (!TryOptionalId(document.toolRequiredId, out var toolRequiredId, out error)) return false;
            if (!TryOptionalId(document.failureOutputItemId, out var failureOutputId, out error)) return false;
            if (document.canBurn && !failureOutputId.HasValue) return Fail("Burnable/failable recipe requires a failure output item.", out definition, out error);

            try
            {
                definition = new RecipeDefinition(
                    id,
                    document.displayName,
                    skill,
                    document.levelRequired,
                    inputs,
                    outputId,
                    document.outputQuantity,
                    document.xp,
                    document.durationMilliseconds,
                    document.category ?? string.Empty,
                    stationId,
                    toolRequiredId,
                    document.canBurn,
                    failureOutputId,
                    document.failureXpFraction);
                error = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool ValidateIdentity(int schemaVersion, string rawId, string displayName, out ContentId id, out string error)
        {
            id = default;
            if (schemaVersion != 1)
            {
                error = $"Unsupported schemaVersion {schemaVersion}; expected 1.";
                return false;
            }
            if (!ContentId.TryCreate(rawId, out id))
            {
                error = "Permanent ID is invalid.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(displayName))
            {
                error = "Display name is required.";
                return false;
            }
            error = string.Empty;
            return true;
        }

        private static bool TryOptionalId(string raw, out ContentId? value, out string error)
        {
            value = null;
            if (string.IsNullOrEmpty(raw))
            {
                error = string.Empty;
                return true;
            }
            if (!ContentId.TryCreate(raw, out var id))
            {
                error = $"Optional content ID '{raw}' is invalid.";
                return false;
            }
            value = id;
            error = string.Empty;
            return true;
        }

        private static bool Fail<T>(string message, out T definition, out string error) where T : class
        {
            definition = null;
            error = message;
            return false;
        }
    }
}
