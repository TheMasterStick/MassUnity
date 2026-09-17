using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Core.Resources;
using MassRPG.Core.Skills;
using MassRPG.Data.Items;
using UnityEngine;

namespace MassRPG.Editor.Data
{
    [Serializable]
    public sealed class ItemDraftCombatBonusesJson
    {
        public int attack;
        public int strength;
        public int defence;
        public int rangedAttack;
        public int rangedStrength;
        public int magic;
    }

    [Serializable]
    public sealed class ItemDraftJson
    {
        public int schemaVersion = 1;
        public string id = string.Empty;
        public string displayName = string.Empty;
        public string description = string.Empty;
        public string type = "Miscellaneous";
        public bool stackable;
        public int value;
        public string[] allowedEquipmentSlots = Array.Empty<string>();
        public bool twoHanded;
        public bool canDualWield;
        public string equipRequirementSkill;
        public int equipRequirementLevel = 1;
        public int healAmount;
        public int toolTier;
        public string gatheringToolKind = "None";
        public int attackIntervalMilliseconds;
        public int attackRangeTiles;
        public ItemDraftCombatBonusesJson bonuses = new ItemDraftCombatBonusesJson();
        public string editorState = "draft";
    }

    public sealed class RepositoryItemDraftRecord
    {
        public RepositoryItemDraftRecord(string filePath, ItemDraftJson document, ItemDefinition definition, string error)
        {
            FilePath = filePath ?? string.Empty;
            Document = document;
            Definition = definition;
            Error = error ?? string.Empty;
        }

        public string FilePath { get; }
        public ItemDraftJson Document { get; }
        public ItemDefinition Definition { get; }
        public string Error { get; }
        public bool IsValid => Definition != null && string.IsNullOrEmpty(Error);
        public bool ReadyForReview => Document != null && string.Equals(Document.editorState, "ready-for-review", StringComparison.Ordinal);
        public string Id => Document?.id ?? Path.GetFileNameWithoutExtension(FilePath);
        public string DisplayName => Document?.displayName ?? "(invalid draft)";
    }

    /// <summary>
    /// Unity-side bridge for the same repository JSON written by the browser/Codespaces Data Editor.
    /// Drafts remain external to Assets so large authored content libraries do not become Unity assets.
    /// </summary>
    public static class RepositoryDraftItemStore
    {
        public static string RepositoryRoot
            => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        public static string ItemDraftRoot
            => Path.Combine(RepositoryRoot, "ContentData", "Drafts", "items");

        public static IReadOnlyList<RepositoryItemDraftRecord> LoadAll()
        {
            if (!Directory.Exists(ItemDraftRoot)) return Array.Empty<RepositoryItemDraftRecord>();

            var files = Directory.GetFiles(ItemDraftRoot, "*.json", SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            var result = new List<RepositoryItemDraftRecord>(files.Length);
            var seenIds = new HashSet<ContentId>();

            for (var i = 0; i < files.Length; i++)
            {
                var file = files[i];
                ItemDraftJson document = null;
                try
                {
                    document = JsonUtility.FromJson<ItemDraftJson>(File.ReadAllText(file));
                    if (document == null) throw new InvalidDataException("JSON did not produce an item document.");
                    if (!TryConvert(document, out var definition, out var error))
                    {
                        result.Add(new RepositoryItemDraftRecord(file, document, null, error));
                        continue;
                    }

                    if (!seenIds.Add(definition.Id))
                    {
                        result.Add(new RepositoryItemDraftRecord(
                            file,
                            document,
                            null,
                            $"Duplicate draft permanent ID '{definition.Id}'. One content ID may have only one draft file."));
                        continue;
                    }

                    var fileId = Path.GetFileNameWithoutExtension(file);
                    if (!string.Equals(fileId, definition.Id.Value, StringComparison.Ordinal))
                    {
                        result.Add(new RepositoryItemDraftRecord(
                            file,
                            document,
                            null,
                            $"Filename '{fileId}.json' does not match permanent ID '{definition.Id.Value}'."));
                        continue;
                    }

                    result.Add(new RepositoryItemDraftRecord(file, document, definition, string.Empty));
                }
                catch (Exception ex)
                {
                    result.Add(new RepositoryItemDraftRecord(file, document, null, ex.Message));
                }
            }

            return result
                .OrderBy(record => record.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(record => record.Id, StringComparer.Ordinal)
                .ToArray();
        }

        public static ItemCatalog CreateResolvedCatalog(out IReadOnlyList<RepositoryItemDraftRecord> records)
        {
            records = LoadAll();
            var resolved = new Dictionary<ContentId, ItemDefinition>();

            foreach (var seed in MigrationSeedItemCatalog.Create().All)
                resolved[seed.Id] = seed;

            for (var i = 0; i < records.Count; i++)
            {
                var record = records[i];
                if (!record.IsValid) continue;
                resolved[record.Definition.Id] = record.Definition;
            }

            var catalog = new ItemCatalog();
            foreach (var definition in resolved.Values.OrderBy(value => value.Id.Value, StringComparer.Ordinal))
                catalog.Register(definition);
            return catalog;
        }

        public static bool TryConvert(ItemDraftJson document, out ItemDefinition definition, out string error)
        {
            definition = null;
            error = string.Empty;
            if (document == null)
            {
                error = "Item draft is null.";
                return false;
            }
            if (document.schemaVersion != 1)
            {
                error = $"Unsupported item draft schemaVersion {document.schemaVersion}; expected 1.";
                return false;
            }
            if (!ContentId.TryCreate(document.id, out var id))
            {
                error = "Permanent ID is invalid.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(document.displayName))
            {
                error = "Display name is required.";
                return false;
            }
            if (!Enum.TryParse(document.type, false, out ItemType type))
            {
                error = $"Unknown item type '{document.type}'.";
                return false;
            }
            if (!Enum.TryParse(document.gatheringToolKind ?? "None", false, out GatheringToolKind gatheringToolKind))
            {
                error = $"Unknown gathering tool kind '{document.gatheringToolKind}'.";
                return false;
            }
            if (document.value < 0 || document.healAmount < 0 || document.toolTier < 0
                || document.attackIntervalMilliseconds < 0 || document.attackRangeTiles < 0)
            {
                error = "Value, healing, tool tier, attack interval and attack range cannot be negative.";
                return false;
            }
            if (document.equipRequirementLevel < 1 || document.equipRequirementLevel > 300)
            {
                error = "Equip requirement level must be between 1 and 300.";
                return false;
            }

            SkillId? requirementSkill = null;
            if (!string.IsNullOrEmpty(document.equipRequirementSkill))
            {
                if (!Enum.TryParse(document.equipRequirementSkill, false, out SkillId parsedSkill))
                {
                    error = $"Unknown equip requirement skill '{document.equipRequirementSkill}'.";
                    return false;
                }
                requirementSkill = parsedSkill;
            }

            var slotNames = document.allowedEquipmentSlots ?? Array.Empty<string>();
            var slots = new EquipmentSlot[slotNames.Length];
            var seenSlots = new HashSet<EquipmentSlot>();
            for (var i = 0; i < slotNames.Length; i++)
            {
                if (!Enum.TryParse(slotNames[i], false, out EquipmentSlot slot)
                    || slot == EquipmentSlot.Weapon || slot == EquipmentSlot.Shield)
                {
                    // Weapon/Shield aliases have the same numeric values as MainHand/OffHand, so
                    // compare original text as well rather than accepting the retired names.
                    if (!string.Equals(slotNames[i], "MainHand", StringComparison.Ordinal)
                        && !string.Equals(slotNames[i], "OffHand", StringComparison.Ordinal))
                    {
                        error = $"Unknown or retired equipment slot '{slotNames[i]}'.";
                        return false;
                    }
                }
                if (!seenSlots.Add(slot))
                {
                    error = $"Duplicate equipment slot '{slotNames[i]}'.";
                    return false;
                }
                slots[i] = slot;
            }

            if (document.twoHanded && document.canDualWield)
            {
                error = "A two-handed item cannot also be dual-wieldable.";
                return false;
            }
            if ((document.twoHanded || document.canDualWield) && !slots.Contains(EquipmentSlot.MainHand))
            {
                error = "Two-handed and dual-wieldable items must allow MainHand.";
                return false;
            }

            var bonus = document.bonuses ?? new ItemDraftCombatBonusesJson();
            definition = new ItemDefinition(
                id,
                document.displayName,
                type,
                document.stackable,
                document.value,
                document.description ?? string.Empty,
                slots,
                document.twoHanded,
                new CombatBonuses
                {
                    Attack = bonus.attack,
                    Strength = bonus.strength,
                    Defence = bonus.defence,
                    RangedAttack = bonus.rangedAttack,
                    RangedStrength = bonus.rangedStrength,
                    Magic = bonus.magic
                },
                requirementSkill,
                document.equipRequirementLevel,
                document.healAmount,
                document.toolTier,
                gatheringToolKind,
                document.canDualWield)
            {
                AttackIntervalMilliseconds = document.attackIntervalMilliseconds,
                AttackRangeTiles = document.attackRangeTiles
            };
            return true;
        }
    }
}
