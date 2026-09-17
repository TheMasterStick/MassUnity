using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MassRPG.Editor.Data
{
    /// <summary>
    /// Home-side inspector for flexible definitions authored from the browser before a dedicated
    /// runtime schema exists. It is intentionally read-only: the arbitrary data object remains raw
    /// JSON until that content type is promoted into a typed MassRPG system.
    /// </summary>
    public sealed class MassRPGGenericDefinitionsWindow : EditorWindow
    {
        private IReadOnlyList<RepositoryGenericDefinitionRecord> _records = Array.Empty<RepositoryGenericDefinitionRecord>();
        private RepositoryGenericDefinitionRecord _selected;
        private string _search = string.Empty;
        private Vector2 _listScroll;
        private Vector2 _detailScroll;
        private bool _showRawJson;

        [MenuItem("MassRPG/Other Game Definitions", priority = 23)]
        public static void Open()
        {
            var window = GetWindow<MassRPGGenericDefinitionsWindow>();
            window.titleContent = new GUIContent("MassRPG Definitions");
            window.minSize = new Vector2(820f, 540f);
            window.Show();
        }

        private void OnEnable() => Reload();

        private void OnGUI()
        {
            DrawHeader();
            var body = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            var leftWidth = Mathf.Clamp(body.width * 0.40f, 320f, 460f);
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
                    GUILayout.Label("Other Game Definitions", EditorStyles.largeLabel);
                    GUILayout.Label(
                        "Flexible NPC/shop/quest/ability/lore/etc. records authored before a dedicated runtime schema exists. These are reviewable design data, not live-publishable simulation data.",
                        EditorStyles.wordWrappedMiniLabel);
                }
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Reload", GUILayout.Width(75f), GUILayout.Height(26f))) Reload();
                if (GUILayout.Button("Reveal folder", GUILayout.Width(95f), GUILayout.Height(26f)))
                {
                    Directory.CreateDirectory(RepositoryGenericDefinitionStore.DefinitionDraftRoot);
                    EditorUtility.RevealInFinder(RepositoryGenericDefinitionStore.DefinitionDraftRoot);
                }
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                var valid = _records.Count(record => record.IsValid);
                var ready = _records.Count(record => record.IsValid && record.ReadyForReview);
                var kinds = _records.Where(record => record.IsValid).Select(record => record.Kind).Distinct(StringComparer.OrdinalIgnoreCase).Count();
                GUILayout.Label($"{_records.Count} definition(s) · {valid} valid · {ready} ready · {kinds} kind(s)", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                GUILayout.Label("Search", GUILayout.Width(42f));
                _search = EditorGUILayout.TextField(_search, GUILayout.Width(240f));
            }
        }

        private void DrawList(Rect rect)
        {
            GUILayout.BeginArea(rect);
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
            var query = (_search ?? string.Empty).Trim();
            var any = false;
            for (var i = 0; i < _records.Count; i++)
            {
                var record = _records[i];
                if (!Matches(record, query)) continue;
                any = true;
                var selected = ReferenceEquals(record, _selected);
                var label = record.DisplayName + "\n" + record.Kind + " · " + record.Id;
                if (!record.IsValid) label += " · INVALID";
                else if (record.ReadyForReview) label += " · ready";
                if (GUILayout.Toggle(selected, label, "Button", GUILayout.Height(44f))) _selected = record;
            }

            if (!any)
            {
                EditorGUILayout.HelpBox(
                    _records.Count == 0
                        ? "No generic definitions yet. Create one in the Online Data Editor; it will appear here after Reload."
                        : "No definitions match this search.",
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
                GUILayout.Space(20f);
                GUILayout.Label("Select a game definition to inspect it.", EditorStyles.centeredGreyMiniLabel);
                EndDetails();
                return;
            }

            GUILayout.Space(6f);
            GUILayout.Label(_selected.DisplayName, EditorStyles.largeLabel);
            ReadOnly("Permanent ID", _selected.Id);
            ReadOnly("Kind", _selected.Kind);
            ReadOnly("State", _selected.ReadyForReview ? "Ready for review" : "Draft");
            ReadOnly("File", MakeRelative(_selected.FilePath));

            if (!_selected.IsValid)
            {
                EditorGUILayout.HelpBox(_selected.Error, MessageType.Error);
                if (GUILayout.Button("Reveal invalid JSON")) EditorUtility.RevealInFinder(_selected.FilePath);
                EndDetails();
                return;
            }

            var document = _selected.Document;
            if (!string.IsNullOrWhiteSpace(document.description))
            {
                Section("Description");
                EditorGUILayout.SelectableLabel(document.description, EditorStyles.textArea, GUILayout.MinHeight(65f));
            }

            if (document.tags != null && document.tags.Length > 0)
            {
                Section("Tags");
                ReadOnly("", string.Join(", ", document.tags));
            }

            if (!string.IsNullOrWhiteSpace(document.notes))
            {
                Section("Design notes");
                EditorGUILayout.SelectableLabel(document.notes, EditorStyles.textArea, GUILayout.MinHeight(100f));
            }

            Section("Presentation");
            var presentation = document.presentation;
            ReadOnly("Asset state", presentation?.assetState ?? RepositoryPresentationDraftStore.DefaultAssetState("definitions"));
            ReadOnly("Icon", EmptyDash(presentation?.iconAssetId));
            ReadOnly("Model", EmptyDash(presentation?.modelAssetId));
            ReadOnly("Portrait", EmptyDash(presentation?.portraitAssetId));
            ReadOnly("Animation set", EmptyDash(presentation?.animationSetAssetId));
            if (!string.IsNullOrWhiteSpace(presentation?.notes))
                EditorGUILayout.SelectableLabel(presentation.notes, EditorStyles.textArea, GUILayout.MinHeight(65f));

            GUILayout.Space(10f);
            _showRawJson = EditorGUILayout.Foldout(_showRawJson, "Raw definition JSON", true);
            if (_showRawJson)
            {
                EditorGUILayout.HelpBox(
                    "The free-form `data` object is shown raw so Unity cannot accidentally discard fields it does not understand.",
                    MessageType.None);
                EditorGUILayout.SelectableLabel(_selected.RawJson, EditorStyles.textArea, GUILayout.MinHeight(220f));
            }

            GUILayout.Space(10f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Copy permanent ID")) EditorGUIUtility.systemCopyBuffer = _selected.Id;
                if (GUILayout.Button("Reveal JSON")) EditorUtility.RevealInFinder(_selected.FilePath);
            }

            EditorGUILayout.HelpBox(
                "If this definition becomes runtime-relevant, promote it to a dedicated typed schema/system before publishing. The permanent ID can stay the same, so work-time design is not thrown away.",
                MessageType.Info);
            EndDetails();
        }

        private bool Matches(RepositoryGenericDefinitionRecord record, string query)
        {
            if (string.IsNullOrEmpty(query)) return true;
            if (record.Id.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (record.DisplayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (record.Kind.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (record.Document?.description?.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (record.Document?.notes?.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (record.Document?.tags != null && record.Document.tags.Any(tag => tag != null && tag.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)) return true;
            return false;
        }

        private void Reload()
        {
            var prior = _selected?.Id;
            _records = RepositoryGenericDefinitionStore.LoadAll();
            _selected = string.IsNullOrWhiteSpace(prior) ? null : _records.FirstOrDefault(record => record.Id == prior);
            Repaint();
        }

        private static string MakeRelative(string path)
        {
            var root = RepositoryDraftItemStore.RepositoryRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return path.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? path.Substring(root.Length) : path;
        }

        private static string EmptyDash(string value) => string.IsNullOrWhiteSpace(value) ? "—" : value;

        private void EndDetails()
        {
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private static void Section(string title)
        {
            GUILayout.Space(10f);
            GUILayout.Label(title, EditorStyles.boldLabel);
        }

        private static void ReadOnly(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (!string.IsNullOrEmpty(label)) GUILayout.Label(label, GUILayout.Width(125f));
                EditorGUILayout.SelectableLabel(value ?? string.Empty, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }
        }
    }
}
