using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MassRPG.Editor.Data
{
    /// <summary>
    /// Home-PC workflow for attaching imported Unity assets to repository content drafted elsewhere.
    /// Dragging an asset here creates a stable repository AssetLink document and patches only the
    /// draft's presentation block; gameplay fields and flexible generic-definition data are untouched.
    /// </summary>
    public sealed class MassRPGPresentationAssetLinkerWindow : EditorWindow
    {
        private static readonly string[] StateLabels = { "Needs assets", "Placeholder", "Linked / unfinished", "Final", "Not required" };
        private static readonly string[] StateValues = { "needs-assets", "placeholder", "linked", "final", "not-required" };

        private IReadOnlyList<RepositoryPresentationDraftRecord> _records = Array.Empty<RepositoryPresentationDraftRecord>();
        private RepositoryPresentationDraftRecord _selected;
        private string _search = string.Empty;
        private Vector2 _listScroll;
        private Vector2 _detailScroll;
        private UnityEngine.Object _icon;
        private UnityEngine.Object _model;
        private UnityEngine.Object _portrait;
        private UnityEngine.Object _animationSet;
        private string _iconError = string.Empty;
        private string _modelError = string.Empty;
        private string _portraitError = string.Empty;
        private string _animationError = string.Empty;
        private string _notes = string.Empty;
        private int _assetStateIndex;
        private bool _clearEmptyAssignments;
        private string _message = string.Empty;
        private MessageType _messageType = MessageType.None;

        [MenuItem("MassRPG/Presentation Asset Linker", priority = 23)]
        public static void Open()
        {
            var window = GetWindow<MassRPGPresentationAssetLinkerWindow>();
            window.titleContent = new GUIContent("MassRPG Asset Linker");
            window.minSize = new Vector2(900f, 560f);
            window.Show();
        }

        private void OnEnable() => Reload();

        private void OnGUI()
        {
            DrawHeader();
            var body = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            var leftWidth = Mathf.Clamp(body.width * 0.38f, 330f, 455f);
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
                    GUILayout.Label("Presentation Asset Linker", EditorStyles.largeLabel);
                    GUILayout.Label(
                        "Import/create art normally, then attach it here. Stable MassRPG asset IDs are written back to the repository draft while Unity GUIDs are stored separately under ContentData/AssetLinks.",
                        EditorStyles.wordWrappedMiniLabel);
                }
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Reload", GUILayout.Width(75f), GUILayout.Height(26f))) Reload();
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label(_records.Count + " unfinished draft(s)", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                GUILayout.Label("Search", GUILayout.Width(42f));
                _search = EditorGUILayout.TextField(_search, GUILayout.Width(250f));
            }
        }

        private void DrawList(Rect rect)
        {
            GUILayout.BeginArea(rect);
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
            var query = (_search ?? string.Empty).Trim();
            var visible = _records.Where(record => Matches(record, query)).ToArray();
            if (visible.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    _records.Count == 0 ? "No unfinished presentation work is currently recorded." : "No entries match this search.",
                    MessageType.Info);
            }

            for (var i = 0; i < visible.Length; i++)
            {
                var record = visible[i];
                var selected = ReferenceEquals(record, _selected);
                var kind = string.IsNullOrWhiteSpace(record.DefinitionKind) ? string.Empty : " · " + record.DefinitionKind;
                var label = record.DisplayName + "\n" + record.Category + kind + " · " + record.AssetState;
                if (GUILayout.Toggle(selected, label, "Button", GUILayout.Height(44f)) && !selected) Select(record);
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
                GUILayout.Space(24f);
                GUILayout.Label("Select a content draft to link its presentation assets.", EditorStyles.centeredGreyMiniLabel);
                EndDetails();
                return;
            }

            GUILayout.Space(6f);
            GUILayout.Label(_selected.DisplayName, EditorStyles.largeLabel);
            ReadOnly("Permanent ID", _selected.Id);
            ReadOnly("Category", _selected.Category);
            if (!string.IsNullOrWhiteSpace(_selected.DefinitionKind)) ReadOnly("Definition kind", _selected.DefinitionKind);

            GUILayout.Space(10f);
            EditorGUILayout.HelpBox(
                "Asset fields accept any saved Unity project asset. Models, prefabs, textures/sprites, Animator Controllers and similar files can therefore be linked before final presentation-specific type restrictions are introduced.",
                MessageType.Info);

            _icon = AssetField("Icon", _icon, _selected.IconAssetId, _iconError);
            _model = AssetField("Model / prefab", _model, _selected.ModelAssetId, _modelError);
            _portrait = AssetField("Portrait", _portrait, _selected.PortraitAssetId, _portraitError);
            _animationSet = AssetField("Animation set", _animationSet, _selected.AnimationSetAssetId, _animationError);

            GUILayout.Space(10f);
            GUILayout.Label("Presentation state", EditorStyles.boldLabel);
            _assetStateIndex = EditorGUILayout.Popup(_assetStateIndex, StateLabels);
            _clearEmptyAssignments = EditorGUILayout.ToggleLeft(
                "Clear repository asset IDs for empty fields when saving",
                _clearEmptyAssignments);
            EditorGUILayout.HelpBox(
                _clearEmptyAssignments
                    ? "Empty Object fields will remove their current presentation references. Existing AssetLink documents are kept because other content may still reference them."
                    : "Empty Object fields preserve any existing presentation references. This is the safer default when only linking one missing asset.",
                MessageType.None);

            GUILayout.Space(8f);
            GUILayout.Label("Visual / asset notes", EditorStyles.boldLabel);
            _notes = EditorGUILayout.TextArea(_notes ?? string.Empty, GUILayout.MinHeight(90f));

            GUILayout.Space(10f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save presentation links", GUILayout.Height(32f))) Save();
                if (GUILayout.Button("Reveal draft JSON", GUILayout.Height(32f))) EditorUtility.RevealInFinder(_selected.FilePath);
            }

            if (!string.IsNullOrWhiteSpace(_message)) EditorGUILayout.HelpBox(_message, _messageType);

            EndDetails();
        }

        private UnityEngine.Object AssetField(string label, UnityEngine.Object value, string currentId, string loadError)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(label, GUILayout.Width(110f));
                    value = EditorGUILayout.ObjectField(value, typeof(UnityEngine.Object), false);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("Current ID", GUILayout.Width(110f));
                    EditorGUILayout.SelectableLabel(
                        string.IsNullOrWhiteSpace(currentId) ? "—" : currentId,
                        EditorStyles.textField,
                        GUILayout.Height(EditorGUIUtility.singleLineHeight));
                }
                if (!string.IsNullOrWhiteSpace(loadError)) EditorGUILayout.HelpBox(loadError, MessageType.Warning);
            }
            return value;
        }

        private void Save()
        {
            try
            {
                RepositoryPresentationDraftLinker.Save(
                    _selected,
                    _icon,
                    _model,
                    _portrait,
                    _animationSet,
                    StateValues[Mathf.Clamp(_assetStateIndex, 0, StateValues.Length - 1)],
                    _notes,
                    _clearEmptyAssignments);
                AssetDatabase.Refresh();
                _message = "Presentation links saved. The draft and ContentData/AssetLinks are now normal repository changes ready to review/commit.";
                _messageType = MessageType.Info;
                var id = _selected.Id;
                Reload();
                var record = _records.FirstOrDefault(item => item.Id == id);
                if (record != null) Select(record);
            }
            catch (Exception ex)
            {
                _message = ex.Message;
                _messageType = MessageType.Error;
            }
        }

        private void Select(RepositoryPresentationDraftRecord record)
        {
            _selected = record;
            _message = string.Empty;
            _messageType = MessageType.None;
            _notes = record?.Notes ?? string.Empty;
            _assetStateIndex = Mathf.Max(0, Array.IndexOf(StateValues, record?.AssetState ?? "needs-assets"));
            _clearEmptyAssignments = false;
            _icon = Load(record?.IconAssetId, out _iconError);
            _model = Load(record?.ModelAssetId, out _modelError);
            _portrait = Load(record?.PortraitAssetId, out _portraitError);
            _animationSet = Load(record?.AnimationSetAssetId, out _animationError);
            _detailScroll = Vector2.zero;
            Repaint();
        }

        private static UnityEngine.Object Load(string assetId, out string error)
            => RepositoryPresentationAssetLinkStore.LoadObject(assetId, out error);

        private void Reload()
        {
            var prior = _selected?.Id;
            _records = RepositoryPresentationDraftStore.LoadOpenAssetBacklog();
            _selected = null;
            if (!string.IsNullOrWhiteSpace(prior))
            {
                var found = _records.FirstOrDefault(record => record.Id == prior);
                if (found != null) Select(found);
            }
            Repaint();
        }

        private static bool Matches(RepositoryPresentationDraftRecord record, string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return true;
            return record.Id.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                || record.DisplayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                || record.Category.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                || record.DefinitionKind.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                || record.Notes.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void EndDetails()
        {
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private static void ReadOnly(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(label, GUILayout.Width(110f));
                EditorGUILayout.SelectableLabel(value ?? string.Empty, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }
        }
    }
}
