using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MassRPG.Data.Creatures;
using MassRPG.Data.Items;
using MassRPG.Data.Recipes;
using MassRPG.Data.Resources;
using UnityEditor;
using UnityEngine;

namespace MassRPG.Editor.Data
{
    /// <summary>
    /// Unity-side view of repository drafts written by the browser/Codespaces editor. All current
    /// online-editor content types are reconstructed into the same C# definitions used by gameplay.
    /// </summary>
    public sealed class MassRPGRepositoryDraftsWindow : EditorWindow
    {
        private enum DraftKind
        {
            Items,
            Creatures,
            Resources,
            Recipes
        }

        private sealed class DraftRow
        {
            public string FilePath;
            public string Id;
            public string DisplayName;
            public string State;
            public object Definition;
            public string Error;
            public bool IsValid => Definition != null && string.IsNullOrEmpty(Error);
            public bool Ready => string.Equals(State, "ready-for-review", StringComparison.Ordinal);
        }

        private DraftKind _kind;
        private IReadOnlyList<DraftRow> _rows = Array.Empty<DraftRow>();
        private DraftRow _selected;
        private string _search = string.Empty;
        private Vector2 _listScroll;
        private Vector2 _detailScroll;

        [MenuItem("MassRPG/Repository Drafts", priority = 21)]
        public static void Open()
        {
            var window = GetWindow<MassRPGRepositoryDraftsWindow>();
            window.titleContent = new GUIContent("MassRPG Drafts");
            window.minSize = new Vector2(790f, 520f);
            window.Show();
        }

        private void OnEnable() => Reload();

        private void OnGUI()
        {
            DrawHeader();
            var body = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            var leftWidth = Mathf.Clamp(body.width * 0.40f, 310f, 440f);
            DrawList(new Rect(body.x, body.y, leftWidth, body.height));
            EditorGUI.DrawRect(new Rect(body.x + leftWidth, body.y, 1f, body.height), new Color(0f, 0f, 0f, 0.35f));
            DrawDetails(new Rect(body.x + leftWidth + 1f, body.y, body.width - leftWidth - 1f, body.height));
        }

        private void DrawHeader()
        {
            GUILayout.Space(6f);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Label("Repository Content Drafts", EditorStyles.largeLabel);
                    GUILayout.Label(
                        "Same repository JSON used by the browser/Codespaces editor. Drafts are reconstructed into MassRPG C# definitions here, but remain separate from live-published MMO data.",
                        EditorStyles.wordWrappedMiniLabel);
                }
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Reload", GUILayout.Width(75f), GUILayout.Height(26f))) Reload();
                if (GUILayout.Button("Reveal folder", GUILayout.Width(95f), GUILayout.Height(26f))) RevealCurrentFolder();
            }

            var next = (DraftKind)GUILayout.Toolbar((int)_kind, new[] { "Items", "Creatures", "Resources", "Recipes" }, GUILayout.Height(24f));
            if (next != _kind)
            {
                _kind = next;
                _selected = null;
                _listScroll = Vector2.zero;
                _detailScroll = Vector2.zero;
                Reload();
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                var valid = _rows.Count(row => row.IsValid);
                var ready = _rows.Count(row => row.IsValid && row.Ready);
                var invalid = _rows.Count - valid;
                GUILayout.Label($"{_rows.Count} draft(s) · {valid} valid · {ready} ready for review · {invalid} invalid", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                GUILayout.Label("Search", GUILayout.Width(42f));
                _search = EditorGUILayout.TextField(_search, GUILayout.Width(230f));
            }
        }

        private void DrawList(Rect rect)
        {
            GUILayout.BeginArea(rect);
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
            var query = (_search ?? string.Empty).Trim();
            var any = false;
            for (var i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                if (!Matches(row, query)) continue;
                any = true;
                var selected = ReferenceEquals(row, _selected);
                var label = row.DisplayName + "\n" + row.Id;
                if (!row.IsValid) label += "  · INVALID";
                else if (row.Ready) label += "  · ready";
                if (GUILayout.Toggle(selected, label, "Button", GUILayout.Height(42f))) _selected = row;
            }

            if (!any)
            {
                EditorGUILayout.HelpBox(
                    _rows.Count == 0
                        ? $"No repository {_kind.ToString().ToLowerInvariant()} drafts yet. Author one in the Online Data Editor and Reload."
                        : "No drafts match this search.",
                    MessageType.Info);
            }
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawDetails(Rect rect)
        {
            GUILayout.BeginArea(rect);
            _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);
            if (_selected == null)
            {
                GUILayout.Space(18f);
                GUILayout.Label("Select a repository draft to inspect it.", EditorStyles.centeredGreyMiniLabel);
                EndDetails();
                return;
            }

            GUILayout.Space(6f);
            GUILayout.Label(_selected.DisplayName, EditorStyles.largeLabel);
            ReadOnly("Permanent ID", _selected.Id);
            ReadOnly("File", MakeRelative(_selected.FilePath));
            ReadOnly("State", _selected.Ready ? "Ready for review" : "Draft");

            if (!_selected.IsValid)
            {
                EditorGUILayout.HelpBox(_selected.Error, MessageType.Error);
                if (GUILayout.Button("Reveal invalid JSON file")) EditorUtility.RevealInFinder(_selected.FilePath);
                EndDetails();
                return;
            }

            if (_selected.Definition is ItemDefinition item) DrawItem(item);
            else if (_selected.Definition is CreatureDefinition creature) DrawCreature(creature);
            else if (_selected.Definition is ResourceDefinition resource) DrawResource(resource);
            else if (_selected.Definition is RecipeDefinition recipe) DrawRecipe(recipe);

            GUILayout.Space(12f);
            EditorGUILayout.HelpBox(
                "This object was reconstructed from repository JSON rather than the hard-coded migration catalog. The Online Editor ↔ Unity bridge is therefore using one source of truth.",
                MessageType.Info);
            if (GUILayout.Button("Reveal JSON file")) EditorUtility.RevealInFinder(_selected.FilePath);
            EndDetails();
        }

        private static void DrawItem(ItemDefinition item)
        {
            Section("Core item data");
            ReadOnly("Type", item.Type.ToString());
            ReadOnly("Stackable", YesNo(item.Stackable));
            ReadOnly("Value", item.Value.ToString());
            if (!string.IsNullOrWhiteSpace(item.Description)) ReadOnlyMultiline("Description", item.Description);
            Section("Equipment / use");
            ReadOnly("Slots", item.AllowedEquipmentSlots.Length == 0 ? "—" : string.Join(", ", item.AllowedEquipmentSlots.Select(slot => slot.ToString())));
            ReadOnly("Two-handed / Dual wield", $"{YesNo(item.TwoHanded)} / {YesNo(item.CanDualWield)}");
            ReadOnly("Equip requirement", item.EquipRequirementSkill.HasValue ? $"{item.EquipRequirementSkill.Value} {item.EquipRequirementLevel}" : "—");
            ReadOnly("Gathering tool", item.GatheringToolKind == Core.Resources.GatheringToolKind.None ? "—" : $"{item.GatheringToolKind}, tier {item.ToolTier}");
            ReadOnly("Attack interval / range", $"{item.AttackIntervalMilliseconds} ms / {item.AttackRangeTiles} tiles");
            Section("Combat bonuses");
            ReadOnly("Attack / Strength / Defence", $"{item.Bonuses.Attack} / {item.Bonuses.Strength} / {item.Bonuses.Defence}");
            ReadOnly("Ranged attack / strength", $"{item.Bonuses.RangedAttack} / {item.Bonuses.RangedStrength}");
            ReadOnly("Magic", item.Bonuses.Magic.ToString());
        }

        private static void DrawCreature(CreatureDefinition creature)
        {
            Section("Creature");
            ReadOnly("Combat level / HP", $"{creature.CombatLevel} / {creature.MaxHitpoints}");
            ReadOnly("Attack / Strength / Defence", $"{creature.AttackLevel} / {creature.StrengthLevel} / {creature.DefenceLevel}");
            ReadOnly("Bonuses", $"{creature.AttackBonus} / {creature.StrengthBonus} / {creature.DefenceBonus}");
            ReadOnly("Combat style", creature.CombatStyle.ToString());
            ReadOnly("Attack interval / range", $"{creature.AttackIntervalMilliseconds} ms / {creature.AttackRangeTiles} tiles");
            ReadOnly("Disposition", creature.Disposition.ToString());
            ReadOnly("Footprint", creature.Footprint.ToString());
            ReadOnly("Aggro / leash", $"{creature.AggroRadiusTiles} / {creature.LeashRadiusTiles} tiles");
            ReadOnly("Persistent named identity", YesNo(creature.PersistentNamedInstance));
        }

        private static void DrawResource(ResourceDefinition resource)
        {
            Section("Gathering resource");
            ReadOnly("Skill / level", $"{resource.GatheringSkill} {resource.RequiredLevel}");
            ReadOnly("XP", resource.Experience.ToString());
            ReadOnly("Yield item", resource.YieldItemId.Value);
            ReadOnly("Yield quantity", resource.MinimumYield == resource.MaximumYield ? resource.MinimumYield.ToString() : $"{resource.MinimumYield}-{resource.MaximumYield}");
            ReadOnly("Respawn", resource.RespawnSeconds + " sec");
            ReadOnly("Availability", resource.AvailabilityMode.ToString());
            ReadOnly("Tool", resource.RequiredToolKind == Core.Resources.GatheringToolKind.None ? "None" : $"{resource.RequiredToolKind}, tier {resource.MinimumToolTier}+");
        }

        private static void DrawRecipe(RecipeDefinition recipe)
        {
            Section("Production recipe");
            ReadOnly("Skill / level", $"{recipe.Skill} {recipe.LevelRequired}");
            ReadOnly("Category", string.IsNullOrWhiteSpace(recipe.Category) ? "—" : recipe.Category);
            ReadOnly("XP / duration", $"{recipe.Xp} / {recipe.DurationMilliseconds} ms");
            ReadOnly("Station", recipe.StationId.HasValue ? recipe.StationId.Value.Value : "—");
            ReadOnly("Tool", recipe.ToolRequiredId.HasValue ? recipe.ToolRequiredId.Value.Value : "—");
            Section("Inputs");
            for (var i = 0; i < recipe.Inputs.Count; i++) ReadOnly($"Input {i + 1}", $"{recipe.Inputs[i].Quantity} × {recipe.Inputs[i].ItemId.Value}");
            Section("Output");
            ReadOnly("Success", $"{recipe.OutputQuantity} × {recipe.OutputItemId.Value}");
            ReadOnly("Can fail/burn", YesNo(recipe.CanBurn));
            if (recipe.FailureOutputItemId.HasValue) ReadOnly("Failure output", recipe.FailureOutputItemId.Value.Value);
            if (recipe.CanBurn) ReadOnly("Failure XP", recipe.FailureXpFraction.ToString("P0") + " of normal");
        }

        private void Reload()
        {
            var prior = _selected?.Id;
            switch (_kind)
            {
                case DraftKind.Items:
                    _rows = RepositoryDraftItemStore.LoadAll().Select(item => new DraftRow
                    {
                        FilePath = item.FilePath, Id = item.Id, DisplayName = item.DisplayName, State = item.Document?.editorState ?? "draft",
                        Definition = item.Definition, Error = item.Error
                    }).ToArray();
                    break;
                case DraftKind.Creatures:
                    _rows = Wrap(RepositoryDraftGameDataStore.LoadCreatures());
                    break;
                case DraftKind.Resources:
                    _rows = Wrap(RepositoryDraftGameDataStore.LoadResources());
                    break;
                default:
                    _rows = Wrap(RepositoryDraftGameDataStore.LoadRecipes());
                    break;
            }
            _selected = string.IsNullOrEmpty(prior) ? null : _rows.FirstOrDefault(row => row.Id == prior);
            Repaint();
        }

        private static IReadOnlyList<DraftRow> Wrap<T>(IReadOnlyList<RepositoryDraftRecord<T>> records) where T : class
        {
            var result = new DraftRow[records.Count];
            for (var i = 0; i < records.Count; i++)
            {
                var row = records[i];
                result[i] = new DraftRow
                {
                    FilePath = row.FilePath,
                    Id = row.Id,
                    DisplayName = row.DisplayName,
                    State = row.EditorState,
                    Definition = row.Definition,
                    Error = row.Error
                };
            }
            return result;
        }

        private void RevealCurrentFolder()
        {
            var path = _kind == DraftKind.Items ? RepositoryDraftItemStore.ItemDraftRoot
                : _kind == DraftKind.Creatures ? RepositoryDraftGameDataStore.CreatureDraftRoot
                : _kind == DraftKind.Resources ? RepositoryDraftGameDataStore.ResourceDraftRoot
                : RepositoryDraftGameDataStore.RecipeDraftRoot;
            Directory.CreateDirectory(path);
            EditorUtility.RevealInFinder(path);
        }

        private static bool Matches(DraftRow row, string query)
        {
            if (string.IsNullOrEmpty(query)) return true;
            return row.Id.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                || row.DisplayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                || row.Definition?.GetType().Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string MakeRelative(string path)
        {
            var root = RepositoryDraftItemStore.RepositoryRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return path.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? path.Substring(root.Length) : path;
        }

        private void EndDetails()
        {
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private static string YesNo(bool value) => value ? "Yes" : "No";

        private static void Section(string title)
        {
            GUILayout.Space(10f);
            GUILayout.Label(title, EditorStyles.boldLabel);
        }

        private static void ReadOnly(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(label, GUILayout.Width(155f));
                EditorGUILayout.SelectableLabel(value ?? string.Empty, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }
        }

        private static void ReadOnlyMultiline(string label, string value)
        {
            GUILayout.Label(label);
            EditorGUILayout.SelectableLabel(value ?? string.Empty, EditorStyles.textArea, GUILayout.MinHeight(55f));
        }
    }
}
