using System;
using System.Collections.Generic;
using System.Linq;
using MassRPG.Data.Construction;
using MassRPG.Data.Creatures;
using MassRPG.Data.Items;
using MassRPG.Data.Recipes;
using MassRPG.Data.Skills;
using MassRPG.Data.Validation;
using UnityEditor;
using UnityEngine;

namespace MassRPG.Editor.Data
{
    /// <summary>
    /// Read-only front end over the migration/dev content catalogs. This is deliberately a browser
    /// before it becomes an editor: permanent IDs and cross-content references can be inspected
    /// safely without implying that in-memory seed definitions are the publishable data source.
    /// </summary>
    public sealed class MassRPGContentBrowserWindow : EditorWindow
    {
        private enum ContentKind
        {
            Items,
            Creatures,
            Recipes,
            Skills,
            BuildPieces,
            Resources
        }

        private sealed class BrowserRow
        {
            public string Id;
            public string Name;
            public string Category;
            public string SearchText;
            public object Value;
        }

        private readonly Dictionary<ContentKind, List<BrowserRow>> _rows =
            new Dictionary<ContentKind, List<BrowserRow>>();
        private List<ContentAuditIssue> _auditIssues = new List<ContentAuditIssue>();

        private ContentKind _kind = ContentKind.Items;
        private string _search = string.Empty;
        private Vector2 _listScroll;
        private Vector2 _detailScroll;
        private BrowserRow _selected;
        private bool _loaded;

        [MenuItem("MassRPG/Content Browser", priority = 20)]
        public static void Open()
        {
            var window = GetWindow<MassRPGContentBrowserWindow>();
            window.titleContent = new GUIContent("MassRPG Content");
            window.minSize = new Vector2(760f, 520f);
            window.Show();
        }

        private void OnEnable()
        {
            RebuildRows();
        }

        private void OnGUI()
        {
            if (!_loaded) RebuildRows();

            DrawHeader();
            DrawCategoryTabs();
            DrawSearchBar();
            DrawAuditStatus();

            var contentRect = GUILayoutUtility.GetRect(
                GUIContent.none,
                GUIStyle.none,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true));
            DrawSplitView(contentRect);
        }

        private void DrawHeader()
        {
            GUILayout.Space(6f);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(8f);
                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Label("MassRPG Content Browser", EditorStyles.largeLabel);
                    GUILayout.Label(
                        "Read-only migration/dev content. Permanent IDs are canonical references; editing and publishing are separate upcoming steps.",
                        EditorStyles.wordWrappedMiniLabel);
                }
                GUILayout.Space(8f);
            }
            GUILayout.Space(4f);
        }

        private void DrawCategoryTabs()
        {
            var labels = new[] { "Items", "Creatures", "Recipes", "Skills", "Build Pieces", "Resources" };
            var next = (ContentKind)GUILayout.Toolbar((int)_kind, labels, GUILayout.Height(24f));
            if (next == _kind) return;

            _kind = next;
            _selected = null;
            _listScroll = Vector2.zero;
            _detailScroll = Vector2.zero;
        }

        private void DrawSearchBar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("Search", GUILayout.Width(48f));
                var next = EditorGUILayout.TextField(_search);
                if (!string.Equals(next, _search, StringComparison.Ordinal))
                {
                    _search = next;
                    _listScroll = Vector2.zero;
                }

                var count = FilteredRows().Count;
                GUILayout.Label($"{count} / {RowsFor(_kind).Count}", EditorStyles.miniLabel, GUILayout.Width(70f));

                GUI.enabled = !string.IsNullOrEmpty(_search);
                if (GUILayout.Button("Clear", GUILayout.Width(48f)))
                {
                    _search = string.Empty;
                    GUI.FocusControl(null);
                }
                GUI.enabled = true;

                if (GUILayout.Button("Refresh", GUILayout.Width(58f))) RebuildRows();
            }
        }

        private void DrawAuditStatus()
        {
            var errors = _auditIssues.Count(issue => issue.Severity == ContentAuditSeverity.Error);
            var warnings = _auditIssues.Count(issue => issue.Severity == ContentAuditSeverity.Warning);
            var infos = _auditIssues.Count(issue => issue.Severity == ContentAuditSeverity.Info);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                var text = errors == 0 && warnings == 0
                    ? $"Reference audit: no errors or warnings{(infos > 0 ? $" · {infos} info" : string.Empty)}"
                    : $"Reference audit: {errors} error(s) · {warnings} warning(s) · {infos} info";
                GUILayout.Label(text, EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                GUI.enabled = _auditIssues.Count > 0;
                if (GUILayout.Button("Log issues", GUILayout.Width(74f)))
                {
                    for (var i = 0; i < _auditIssues.Count; i++)
                    {
                        var issue = _auditIssues[i];
                        if (issue.Severity == ContentAuditSeverity.Error) Debug.LogError(issue.ToString());
                        else if (issue.Severity == ContentAuditSeverity.Warning) Debug.LogWarning(issue.ToString());
                        else Debug.Log(issue.ToString());
                    }
                }
                GUI.enabled = true;
            }
        }

        private void DrawSplitView(Rect rect)
        {
            if (rect.width <= 1f || rect.height <= 1f) return;

            var leftWidth = Mathf.Clamp(rect.width * 0.40f, 285f, 430f);
            var listRect = new Rect(rect.x, rect.y, leftWidth, rect.height);
            var dividerRect = new Rect(listRect.xMax, rect.y, 1f, rect.height);
            var detailRect = new Rect(dividerRect.xMax, rect.y, rect.width - leftWidth - 1f, rect.height);

            EditorGUI.DrawRect(dividerRect, new Color(0f, 0f, 0f, 0.35f));
            DrawList(listRect);
            DrawDetails(detailRect);
        }

        private void DrawList(Rect rect)
        {
            GUILayout.BeginArea(rect);
            var filtered = FilteredRows();

            if (_kind == ContentKind.Resources && RowsFor(_kind).Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "The ResourceDefinition data model exists, but this branch has no centralized migration seed ResourceCatalog yet. No placeholder resources are being invented here.",
                    MessageType.Info);
            }
            else if (filtered.Count == 0)
            {
                EditorGUILayout.HelpBox("No content matches this search.", MessageType.Info);
            }

            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
            for (var i = 0; i < filtered.Count; i++)
                DrawRow(filtered[i]);
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawRow(BrowserRow row)
        {
            var selected = ReferenceEquals(row, _selected);
            var style = new GUIStyle(EditorStyles.helpBox);
            if (selected) style.normal.background = Texture2D.grayTexture;
            var issueCount = _auditIssues.Count(issue => issue.OwnerId.Value == row.Id);

            using (new EditorGUILayout.VerticalScope(style))
            {
                var rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(38f), GUILayout.ExpandWidth(true));
                if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
                {
                    _selected = row;
                    _detailScroll = Vector2.zero;
                    Event.current.Use();
                    Repaint();
                }

                var titleRect = new Rect(rect.x + 5f, rect.y + 2f, rect.width - 10f, 17f);
                var subtitleRect = new Rect(rect.x + 5f, rect.y + 19f, rect.width - 10f, 16f);
                GUI.Label(titleRect, issueCount > 0 ? $"{row.Name}  [{issueCount} issue(s)]" : row.Name, EditorStyles.boldLabel);
                GUI.Label(subtitleRect, $"{row.Id}  ·  {row.Category}", EditorStyles.miniLabel);
            }
        }

        private void DrawDetails(Rect rect)
        {
            GUILayout.BeginArea(rect);
            _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);

            if (_selected == null || !RowsFor(_kind).Contains(_selected))
            {
                GUILayout.Space(16f);
                GUILayout.Label("Select an entry to inspect it.", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                GUILayout.Space(6f);
                DrawIdentity(_selected);
                DrawSelectedIssues(_selected);
                GUILayout.Space(8f);
                DrawTypedDetails(_selected.Value);
            }

            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawSelectedIssues(BrowserRow row)
        {
            var issues = _auditIssues.Where(issue => issue.OwnerId.Value == row.Id).ToList();
            for (var i = 0; i < issues.Count; i++)
            {
                var issue = issues[i];
                var type = issue.Severity == ContentAuditSeverity.Error ? MessageType.Error
                    : issue.Severity == ContentAuditSeverity.Warning ? MessageType.Warning
                    : MessageType.Info;
                EditorGUILayout.HelpBox($"{issue.Field}: {issue.Message}", type);
            }
        }

        private static void DrawIdentity(BrowserRow row)
        {
            GUILayout.Label(row.Name, EditorStyles.largeLabel);
            EditorGUILayout.Space(2f);
            ReadOnlyField("Permanent ID", row.Id);
            ReadOnlyField("Category", row.Category);
            EditorGUILayout.HelpBox(
                "Permanent IDs are save/network/data references and remain independent from player-visible names.",
                MessageType.None);
        }

        private static void DrawTypedDetails(object value)
        {
            if (value is ItemDefinition item) DrawItem(item);
            else if (value is CreatureDefinition creature) DrawCreature(creature);
            else if (value is RecipeDefinition recipe) DrawRecipe(recipe);
            else if (value is SkillDefinition skill) DrawSkill(skill);
            else if (value is BuildPieceDefinition piece) DrawBuildPiece(piece);
        }

        private static void DrawItem(ItemDefinition item)
        {
            Section("Item");
            ReadOnlyField("Type", item.Type.ToString());
            ReadOnlyField("Stackable", YesNo(item.Stackable));
            ReadOnlyField("Value", item.Value.ToString());
            if (!string.IsNullOrWhiteSpace(item.Description)) ReadOnlyMultiline("Description", item.Description);

            Section("Equipment / use");
            ReadOnlyField("Slots", item.AllowedEquipmentSlots.Length == 0
                ? "—"
                : string.Join(", ", item.AllowedEquipmentSlots.Select(slot => slot.ToString())));
            ReadOnlyField("Two-handed", YesNo(item.TwoHanded));
            ReadOnlyField("Dual wield", YesNo(item.CanDualWield));
            ReadOnlyField("Equip requirement", item.EquipRequirementSkill.HasValue
                ? $"{item.EquipRequirementSkill.Value} {item.EquipRequirementLevel}"
                : "—");
            ReadOnlyField("Heal amount", item.HealAmount > 0 ? item.HealAmount.ToString() : "—");
            ReadOnlyField("Gathering tool", item.GatheringToolKind.ToString());
            ReadOnlyField("Tool tier", item.ToolTier > 0 ? item.ToolTier.ToString() : "—");
            ReadOnlyField("Attack interval", item.AttackIntervalMilliseconds > 0
                ? $"{item.AttackIntervalMilliseconds} ms"
                : "style default");
            ReadOnlyField("Attack range", item.AttackRangeTiles > 0
                ? $"{item.AttackRangeTiles} tile(s)"
                : "style default");

            Section("Combat bonuses");
            var b = item.Bonuses;
            ReadOnlyField("Attack / Strength / Defence", $"{b.Attack} / {b.Strength} / {b.Defence}");
            ReadOnlyField("Ranged attack / strength", $"{b.RangedAttack} / {b.RangedStrength}");
            ReadOnlyField("Magic", b.Magic.ToString());
        }

        private static void DrawCreature(CreatureDefinition creature)
        {
            Section("Creature");
            ReadOnlyField("Combat level", creature.CombatLevel.ToString());
            ReadOnlyField("Hitpoints", creature.MaxHitpoints.ToString());
            ReadOnlyField("Attack / Strength / Defence", $"{creature.AttackLevel} / {creature.StrengthLevel} / {creature.DefenceLevel}");
            ReadOnlyField("Attack / Strength / Defence bonus", $"{creature.AttackBonus} / {creature.StrengthBonus} / {creature.DefenceBonus}");
            ReadOnlyField("Combat style", creature.CombatStyle.ToString());
            ReadOnlyField("Attack interval", $"{creature.AttackIntervalMilliseconds} ms");
            ReadOnlyField("Attack range", $"{creature.AttackRangeTiles} tile(s)");
            ReadOnlyField("Disposition", creature.Disposition.ToString());
            ReadOnlyField("Footprint", creature.Footprint.ToString());
            ReadOnlyField("Aggro radius", $"{creature.AggroRadiusTiles} tile(s)");
            ReadOnlyField("Leash radius", $"{creature.LeashRadiusTiles} tile(s)");
            ReadOnlyField("Persistent named instance", YesNo(creature.PersistentNamedInstance));
        }

        private static void DrawRecipe(RecipeDefinition recipe)
        {
            Section("Recipe");
            ReadOnlyField("Skill", recipe.Skill.ToString());
            ReadOnlyField("Level required", recipe.LevelRequired.ToString());
            ReadOnlyField("XP", recipe.Xp.ToString());
            ReadOnlyField("Duration", $"{recipe.DurationMilliseconds} ms");
            ReadOnlyField("Station", recipe.StationId.HasValue ? recipe.StationId.Value.Value : "—");
            ReadOnlyField("Tool", recipe.ToolRequiredId.HasValue ? recipe.ToolRequiredId.Value.Value : "—");

            Section("Inputs");
            for (var i = 0; i < recipe.Inputs.Count; i++)
            {
                var input = recipe.Inputs[i];
                ReadOnlyField($"Input {i + 1}", $"{input.Quantity} × {input.ItemId.Value}");
            }

            Section("Output");
            ReadOnlyField("Success", $"{recipe.OutputQuantity} × {recipe.OutputItemId.Value}");
            ReadOnlyField("Can burn/fail", YesNo(recipe.CanBurn));
            if (recipe.FailureOutputItemId.HasValue)
                ReadOnlyField("Failure output", recipe.FailureOutputItemId.Value.Value);
            if (recipe.CanBurn)
                ReadOnlyField("Failure XP", $"{recipe.FailureXpFraction:P0} of normal");
        }

        private static void DrawSkill(SkillDefinition skill)
        {
            Section("Skill");
            ReadOnlyField("Enum key", skill.Skill.ToString());
            ReadOnlyField("Category", skill.Category.ToString());
            ReadOnlyField("Starting level", skill.StartingLevel.ToString());
        }

        private static void DrawBuildPiece(BuildPieceDefinition piece)
        {
            Section("Build piece");
            ReadOnlyField("Kind", piece.Kind.ToString());
            ReadOnlyField("Placement", piece.PlacementMode.ToString());
            ReadOnlyField("Occupancy layer", piece.OccupancyLayer.ToString());
            ReadOnlyField("Construction level", piece.ConstructionLevel.ToString());
            ReadOnlyField("Construction XP", piece.ConstructionXp.ToString());
            ReadOnlyField("Footprint", $"{piece.FootprintWidth} × {piece.FootprintHeight}");
            ReadOnlyField("Rotation", YesNo(piece.AllowsRotation));
            ReadOnlyField("Support", piece.SupportRequirement.ToString());
            ReadOnlyField("Station", piece.StationId.HasValue ? piece.StationId.Value.Value : "—");

            Section("Material cost");
            if (piece.Costs.Count == 0)
                ReadOnlyField("Cost", "—");
            else
            {
                for (var i = 0; i < piece.Costs.Count; i++)
                {
                    var cost = piece.Costs[i];
                    ReadOnlyField($"Material {i + 1}", $"{cost.Quantity} × {cost.ItemId.Value}");
                }
            }
        }

        private void RebuildRows()
        {
            _rows.Clear();
            foreach (ContentKind kind in Enum.GetValues(typeof(ContentKind)))
                _rows.Add(kind, new List<BrowserRow>());

            var items = MigrationSeedItemCatalog.Create();
            foreach (var item in items.All.OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase))
            {
                Add(ContentKind.Items, item.Id.Value, item.DisplayName, item.Type.ToString(), item,
                    item.Description,
                    item.GatheringToolKind.ToString(),
                    item.EquipRequirementSkill?.ToString());
            }

            var creatures = MigrationSeedCreatureCatalog.Create();
            foreach (var creature in creatures.All.OrderBy(creature => creature.DisplayName, StringComparer.OrdinalIgnoreCase))
            {
                Add(ContentKind.Creatures, creature.Id.Value, creature.DisplayName,
                    $"Level {creature.CombatLevel} · {creature.Disposition}", creature,
                    creature.CombatStyle.ToString(), creature.Footprint.ToString());
            }

            var recipes = MigrationSeedRecipeCatalog.Create();
            foreach (var recipe in recipes.All.OrderBy(recipe => recipe.DisplayName, StringComparer.OrdinalIgnoreCase))
            {
                Add(ContentKind.Recipes, recipe.Id.Value, recipe.DisplayName,
                    $"{recipe.Skill} {recipe.LevelRequired} · {recipe.Category}", recipe,
                    recipe.OutputItemId.Value,
                    recipe.StationId.HasValue ? recipe.StationId.Value.Value : string.Empty,
                    recipe.ToolRequiredId.HasValue ? recipe.ToolRequiredId.Value.Value : string.Empty);
            }

            foreach (var skill in SkillCatalog.All.OrderBy(skill => skill.DisplayName, StringComparer.OrdinalIgnoreCase))
            {
                Add(ContentKind.Skills, skill.Id.Value, skill.DisplayName, skill.Category.ToString(), skill,
                    skill.Skill.ToString(), skill.StartingLevel.ToString());
            }

            var buildPieces = MigrationSeedBuildPieceCatalog.Create();
            foreach (var piece in buildPieces.All.OrderBy(piece => piece.DisplayName, StringComparer.OrdinalIgnoreCase))
            {
                Add(ContentKind.BuildPieces, piece.Id.Value, piece.DisplayName,
                    $"{piece.Kind} · level {piece.ConstructionLevel}", piece,
                    piece.PlacementMode.ToString(), piece.OccupancyLayer.ToString(), piece.SupportRequirement.ToString());
            }

            _auditIssues = ContentCatalogAudit.Audit(
                items,
                creatures,
                recipes,
                SkillCatalog.All,
                buildPieces).ToList();

            // ResourceDefinition/ResourceCatalog already exist, but this branch deliberately has no
            // MigrationSeedResourceCatalog. Keep the category visible and honest instead of creating
            // fake data that could accidentally become canonical.
            _loaded = true;
            if (_selected != null && !RowsFor(_kind).Contains(_selected)) _selected = null;
            Repaint();
        }

        private void Add(ContentKind kind, string id, string name, string category, object value, params string[] extraSearch)
        {
            var parts = new List<string> { id ?? string.Empty, name ?? string.Empty, category ?? string.Empty };
            if (extraSearch != null) parts.AddRange(extraSearch.Where(valuePart => !string.IsNullOrWhiteSpace(valuePart)));
            _rows[kind].Add(new BrowserRow
            {
                Id = id ?? string.Empty,
                Name = string.IsNullOrWhiteSpace(name) ? id : name,
                Category = category ?? string.Empty,
                SearchText = string.Join(" ", parts).ToLowerInvariant(),
                Value = value
            });
        }

        private List<BrowserRow> FilteredRows()
        {
            var rows = RowsFor(_kind);
            if (string.IsNullOrWhiteSpace(_search)) return rows;

            var terms = _search.Trim()
                .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(term => term.ToLowerInvariant())
                .ToArray();
            if (terms.Length == 0) return rows;
            return rows.Where(row => terms.All(term => row.SearchText.Contains(term))).ToList();
        }

        private List<BrowserRow> RowsFor(ContentKind kind)
            => _rows.TryGetValue(kind, out var rows) ? rows : new List<BrowserRow>();

        private static void Section(string title)
        {
            GUILayout.Space(6f);
            GUILayout.Label(title, EditorStyles.boldLabel);
        }

        private static void ReadOnlyField(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(label, GUILayout.Width(155f));
                EditorGUILayout.SelectableLabel(value ?? string.Empty, EditorStyles.textField, GUILayout.Height(18f));
            }
        }

        private static void ReadOnlyMultiline(string label, string value)
        {
            GUILayout.Label(label, EditorStyles.miniBoldLabel);
            EditorGUILayout.SelectableLabel(value ?? string.Empty, EditorStyles.textArea, GUILayout.MinHeight(46f));
        }

        private static string YesNo(bool value) => value ? "Yes" : "No";
    }
}
