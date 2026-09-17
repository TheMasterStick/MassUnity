using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MassRPG.Editor.Data
{
    /// <summary>
    /// Home-development bridge for content authored without Unity/Blender access. It shows every
    /// repository draft whose presentation assets are unfinished so models/icons/portraits/animation
    /// sets can be produced and linked later without recreating gameplay data.
    /// </summary>
    public sealed class MassRPGAssetBacklogWindow : EditorWindow
    {
        private IReadOnlyList<RepositoryPresentationDraftRecord> _records = Array.Empty<RepositoryPresentationDraftRecord>();
        private RepositoryPresentationDraftRecord _selected;
        private string _search = string.Empty;
        private int _categoryFilter;
        private int _stateFilter;
        private Vector2 _listScroll;
        private Vector2 _detailScroll;

        private static readonly string[] CategoryOptions =
        {
            "All categories", "Items", "Creatures", "Resources", "Recipes", "Other definitions"
        };

        private static readonly string[] CategoryValues =
        {
            string.Empty, "items", "creatures", "resources", "recipes", "definitions"
        };

        private static readonly string[] StateOptions =
        {
            "All unfinished", "Needs assets", "Placeholder", "Linked / unfinished"
        };

        private static readonly string[] StateValues =
        {
            string.Empty, "needs-assets", "placeholder", "linked"
        };

        [MenuItem("MassRPG/Asset Backlog", priority = 22)]
        public static void Open()
        {
            var window = GetWindow<MassRPGAssetBacklogWindow>();
            window.titleContent = new GUIContent("MassRPG Assets");
            window.minSize = new Vector2(820f, 520f);
            window.Show();
        }

        private void OnEnable() => Reload();

        private void OnGUI()
        {
            DrawHeader();
            var body = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            var leftWidth = Mathf.Clamp(body.width * 0.42f, 330f, 470f);
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
                    GUILayout.Label("Presentation Asset Backlog", EditorStyles.largeLabel);
                    GUILayout.Label(
                        "Content created from the browser can deliberately arrive without art. This window is the home/Unity checklist for models, icons, portraits and animation sets that still need work.",
                        EditorStyles.wordWrappedMiniLabel);
                }
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Reload", GUILayout.Width(75f), GUILayout.Height(26f))) Reload();
                if (GUILayout.Button("Reveal drafts", GUILayout.Width(100f), GUILayout.Height(26f)))
                    EditorUtility.RevealInFinder(RepositoryPresentationDraftStore.DraftRoot);
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                var needs = _records.Count(record => record.AssetState == "needs-assets");
                var placeholder = _records.Count(record => record.AssetState == "placeholder");
                var linked = _records.Count(record => record.AssetState == "linked");
                GUILayout.Label($"{_records.Count} open · {needs} need assets · {placeholder} placeholders · {linked} linked/unfinished", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                _categoryFilter = EditorGUILayout.Popup(_categoryFilter, CategoryOptions, GUILayout.Width(135f));
                _stateFilter = EditorGUILayout.Popup(_stateFilter, StateOptions, GUILayout.Width(145f));
                _search = EditorGUILayout.TextField(_search, GUILayout.Width(205f));
            }
        }

        private void DrawList(Rect rect)
        {
            GUILayout.BeginArea(rect);
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
            var visible = _records.Where(Matches).ToArray();
            if (visible.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    _records.Count == 0
                        ? "No unfinished presentation assets are currently recorded."
                        : "No backlog entries match the current filters.",
                    MessageType.Info);
            }

            for (var i = 0; i < visible.Length; i++)
            {
                var record = visible[i];
                var selected = ReferenceEquals(record, _selected);
                var category = FriendlyCategory(record.Category);
                var kind = string.IsNullOrWhiteSpace(record.DefinitionKind) ? string.Empty : " · " + record.DefinitionKind;
                var label = record.DisplayName + "\n" + category + kind + " · " + FriendlyState(record.AssetState);
                if (GUILayout.Toggle(selected, label, "Button", GUILayout.Height(44f)))
                    _selected = record;
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
                GUILayout.Space(20f);
                GUILayout.Label("Select an unfinished asset entry.", EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }

            GUILayout.Space(6f);
            GUILayout.Label(_selected.DisplayName, EditorStyles.largeLabel);
            ReadOnly("Permanent ID", _selected.Id);
            ReadOnly("Category", FriendlyCategory(_selected.Category));
            if (!string.IsNullOrWhiteSpace(_selected.DefinitionKind)) ReadOnly("Definition kind", _selected.DefinitionKind);
            ReadOnly("Asset state", FriendlyState(_selected.AssetState));
            ReadOnly("Draft file", MakeRelative(_selected.FilePath));

            Section("Linked presentation IDs");
            ReadOnly("Icon", EmptyDash(_selected.IconAssetId));
            ReadOnly("Model", EmptyDash(_selected.ModelAssetId));
            ReadOnly("Portrait", EmptyDash(_selected.PortraitAssetId));
            ReadOnly("Animation set", EmptyDash(_selected.AnimationSetAssetId));

            Section("Authoring notes");
            if (string.IsNullOrWhiteSpace(_selected.Notes))
                EditorGUILayout.HelpBox("No visual notes were supplied. The gameplay entry can still be inspected in its repository draft.", MessageType.None);
            else
                EditorGUILayout.SelectableLabel(_selected.Notes, EditorStyles.textArea, GUILayout.MinHeight(90f));

            GUILayout.Space(10f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Copy permanent ID")) EditorGUIUtility.systemCopyBuffer = _selected.Id;
                if (GUILayout.Button("Reveal JSON")) EditorUtility.RevealInFinder(_selected.FilePath);
            }

            EditorGUILayout.HelpBox(
                "Asset assignment is deliberately kept separate from gameplay authoring. The next Unity-side step is a linker that writes stable asset IDs back to this same presentation block after models/icons are imported.",
                MessageType.Info);

            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private bool Matches(RepositoryPresentationDraftRecord record)
        {
            var category = CategoryValues[Mathf.Clamp(_categoryFilter, 0, CategoryValues.Length - 1)];
            if (!string.IsNullOrEmpty(category) && !string.Equals(record.Category, category, StringComparison.Ordinal)) return false;

            var state = StateValues[Mathf.Clamp(_stateFilter, 0, StateValues.Length - 1)];
            if (!string.IsNullOrEmpty(state) && !string.Equals(record.AssetState, state, StringComparison.Ordinal)) return false;

            var query = (_search ?? string.Empty).Trim();
            if (query.Length == 0) return true;
            return record.Id.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                || record.DisplayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                || record.Category.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                || record.DefinitionKind.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                || record.Notes.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void Reload()
        {
            var prior = _selected?.Id;
            _records = RepositoryPresentationDraftStore.LoadOpenAssetBacklog();
            _selected = string.IsNullOrWhiteSpace(prior) ? null : _records.FirstOrDefault(record => record.Id == prior);
            Repaint();
        }

        private static string FriendlyCategory(string category)
        {
            switch (category)
            {
                case "items": return "Items & Equipment";
                case "creatures": return "Creatures";
                case "resources": return "Resources";
                case "recipes": return "Recipes";
                case "definitions": return "Other Definitions";
                default: return category ?? string.Empty;
            }
        }

        private static string FriendlyState(string state)
        {
            switch (state)
            {
                case "needs-assets": return "Needs assets";
                case "placeholder": return "Placeholder";
                case "linked": return "Linked, not final";
                case "final": return "Final";
                case "not-required": return "Not required";
                default: return state ?? string.Empty;
            }
        }

        private static string MakeRelative(string path)
        {
            var root = RepositoryDraftItemStore.RepositoryRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return path.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? path.Substring(root.Length) : path;
        }

        private static string EmptyDash(string value) => string.IsNullOrWhiteSpace(value) ? "—" : value;

        private static void Section(string title)
        {
            GUILayout.Space(10f);
            GUILayout.Label(title, EditorStyles.boldLabel);
        }

        private static void ReadOnly(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(label, GUILayout.Width(125f));
                EditorGUILayout.SelectableLabel(value ?? string.Empty, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }
        }
    }
}
