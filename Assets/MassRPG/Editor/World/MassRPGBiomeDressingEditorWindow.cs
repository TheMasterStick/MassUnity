using System;
using System.Collections.Generic;
using System.Linq;
using MassRPG.Core.Content;
using MassRPG.Core.World;
using MassRPG.Data.World.Dressing;
using MassRPG.Data.World.Placements;
using MassRPG.Data.World.Semantics;
using UnityEditor;
using UnityEngine;

namespace MassRPG.Editor.World
{
    /// <summary>
    /// Authoring/preview window for deterministic biome scenery. Bulk forest/ground dressing is
    /// derived from profile + seed, while local density overrides and sparse suppressed placements
    /// preserve deliberate hand edits without materialising millions of authored rows.
    /// </summary>
    public sealed class MassRPGBiomeDressingEditorWindow : EditorWindow
    {
        private const float InspectorWidth = 390f;
        private readonly List<BiomeDressingProfile> _profiles = new List<BiomeDressingProfile>();
        private readonly List<WorldAreaDefinition> _biomes = new List<WorldAreaDefinition>();
        private readonly List<BiomeDressingPlacement> _preview = new List<BiomeDressingPlacement>();

        private int _selectedProfile = -1;
        private int _selectedBiome = -1;
        private BiomeDressingProfile _working;
        private BiomeDressingExceptionSet _exceptions = new BiomeDressingExceptionSet();

        private string _newProfileId = "dressing.forest.new";
        private uint _newSeed = 12345u;
        private string _entryId = "entry.tree";
        private string _entryDefinitionId = "resource.tree.oak";
        private WorldPlacementKind _entryKind = WorldPlacementKind.Resource;
        private DressingOccupancyChannel _entryChannel = DressingOccupancyChannel.Major;
        private double _entryDensity = 0.08;
        private int _entrySpacing = 2;
        private int _entryPriority;
        private int _entrySeedSalt;

        private string _overrideId = "override.clearing";
        private double _overrideMultiplier;
        private int _overrideRadius = 8;
        private GridCoord _overrideCenter = new GridCoord(90000, 90000);
        private int _overrideTargetIndex;

        private float _centerX = WorldConstants.WorldWidthTiles * 0.5f;
        private float _centerY = WorldConstants.WorldHeightTiles * 0.5f;
        private float _pixelsPerTile = 8f;
        private Vector2 _inspectorScroll;
        private string _status = "Select a biome and create or load a dressing profile.";

        [MenuItem("MassRPG/Biome Dressing Editor")]
        public static void Open()
        {
            var window = GetWindow<MassRPGBiomeDressingEditorWindow>();
            window.titleContent = new GUIContent("MassRPG Biome Dressing");
            window.minSize = new Vector2(1080, 680);
            window.Show();
        }

        private void OnEnable()
        {
            ReloadBiomes();
            ReloadProfiles();
        }

        private void OnGUI()
        {
            DrawTopToolbar();
            var canvas = new Rect(0f, 21f, Mathf.Max(100f, position.width - InspectorWidth), Mathf.Max(100f, position.height - 43f));
            var inspector = new Rect(canvas.xMax, 21f, InspectorWidth, position.height - 21f);
            DrawCanvas(canvas);
            DrawInspector(inspector);
            DrawStatus(new Rect(0f, canvas.yMax, canvas.width, 22f));
            HandleCanvasInput(canvas);
        }

        private void DrawTopToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("X", GUILayout.Width(12));
                var x = EditorGUILayout.IntField(Mathf.RoundToInt(_centerX), EditorStyles.toolbarTextField, GUILayout.Width(72));
                GUILayout.Label("Y", GUILayout.Width(12));
                var y = EditorGUILayout.IntField(Mathf.RoundToInt(_centerY), EditorStyles.toolbarTextField, GUILayout.Width(72));
                if (x != Mathf.RoundToInt(_centerX) || y != Mathf.RoundToInt(_centerY))
                {
                    _centerX = Mathf.Clamp(x, 0, WorldConstants.WorldWidthTiles - 1);
                    _centerY = Mathf.Clamp(y, 0, WorldConstants.WorldHeightTiles - 1);
                }

                GUILayout.Space(8);
                if (GUILayout.Button("Area Editor", EditorStyles.toolbarButton, GUILayout.Width(78))) MassRPGAreaEditorWindow.Open();
                if (GUILayout.Button("Placements", EditorStyles.toolbarButton, GUILayout.Width(78))) MassRPGPlacementEditorWindow.Open();
                if (GUILayout.Button("1x1 Terrain", EditorStyles.toolbarButton, GUILayout.Width(78)))
                    MassRPGWorldEditorWindow.OpenAt(Mathf.RoundToInt(_centerX), Mathf.RoundToInt(_centerY), CurrentPlane(), 0);
                GUILayout.FlexibleSpace();
                GUILayout.Label($"{_pixelsPerTile:0.0}px/tile", EditorStyles.miniLabel);
            }
        }

        private void DrawInspector(Rect rect)
        {
            GUILayout.BeginArea(rect, EditorStyles.helpBox);
            _inspectorScroll = EditorGUILayout.BeginScrollView(_inspectorScroll);

            GUILayout.Label("Deterministic Biome Dressing", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Normal trees/scenery are derived from the biome profile and seed. Local exclusions, density changes and individual removals are saved as sparse authoring exceptions.",
                MessageType.Info);

            DrawProfileSection();
            if (_working != null)
            {
                DrawEntrySection();
                DrawOverrideSection();
                DrawExceptionSection();
            }

            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawProfileSection()
        {
            GUILayout.Space(4);
            GUILayout.Label("Profile", EditorStyles.miniBoldLabel);

            var profileLabels = _profiles.Count == 0
                ? new[] { "No saved profiles" }
                : _profiles.Select(p => p.Id.Value).ToArray();
            GUI.enabled = _profiles.Count > 0;
            var nextProfile = EditorGUILayout.Popup("Saved", Mathf.Max(0, _selectedProfile), profileLabels);
            GUI.enabled = true;
            if (_profiles.Count > 0 && nextProfile != _selectedProfile)
            {
                _selectedProfile = nextProfile;
                LoadWorking(_profiles[_selectedProfile]);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reload")) { ReloadBiomes(); ReloadProfiles(); }
                GUI.enabled = _working != null;
                if (GUILayout.Button("Save")) SaveWorking();
                GUI.enabled = true;
            }

            var biomeLabels = _biomes.Count == 0
                ? new[] { "No authored biome areas" }
                : _biomes.Select(b => b.DisplayName + " [" + b.Id.Value + "]").ToArray();
            GUI.enabled = _working == null && _biomes.Count > 0;
            _selectedBiome = EditorGUILayout.Popup("Biome", Mathf.Clamp(_selectedBiome, 0, Mathf.Max(0, _biomes.Count - 1)), biomeLabels);
            GUI.enabled = true;

            if (_working == null)
            {
                _newProfileId = EditorGUILayout.TextField("Stable ID", _newProfileId);
                var seedText = EditorGUILayout.TextField("Seed", _newSeed.ToString());
                uint parsedSeed;
                if (uint.TryParse(seedText, out parsedSeed)) _newSeed = parsedSeed;
                GUI.enabled = _biomes.Count > 0 && ContentId.IsValid(_newProfileId);
                if (GUILayout.Button("Create Profile", GUILayout.Height(25))) CreateProfile();
                GUI.enabled = true;
            }
            else
            {
                EditorGUILayout.LabelField("Stable ID", _working.Id.Value);
                EditorGUILayout.LabelField("Biome area", _working.BiomeAreaId.Value);
                var seedText = EditorGUILayout.TextField("Seed", _working.Seed.ToString());
                uint parsedSeed;
                if (uint.TryParse(seedText, out parsedSeed)) _working.Seed = parsedSeed;
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Centre on Biome")) CentreOnBiome();
                    if (GUILayout.Button("Close Profile")) { _working = null; _selectedProfile = -1; _preview.Clear(); }
                }
            }
        }

        private void DrawEntrySection()
        {
            GUILayout.Space(10);
            GUILayout.Label("Dressing Entries", EditorStyles.miniBoldLabel);
            for (var i = 0; i < _working.Entries.Count; i++)
            {
                var entry = _working.Entries[i];
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label(entry.Id.Value, EditorStyles.boldLabel);
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("Remove", GUILayout.Width(62)))
                        {
                            _working.RemoveEntry(entry.Id);
                            RebuildPreview();
                            GUIUtility.ExitGUI();
                        }
                    }

                    var definitionText = EditorGUILayout.TextField("Definition", entry.DefinitionId.Value);
                    ContentId definitionId;
                    if (ContentId.TryCreate(definitionText, out definitionId)) entry.DefinitionId = definitionId;
                    entry.PlacementKind = (WorldPlacementKind)EditorGUILayout.EnumPopup("Placement", entry.PlacementKind);
                    entry.Channel = (DressingOccupancyChannel)EditorGUILayout.EnumPopup("Channel", entry.Channel);
                    entry.DensityPerTile = Math.Max(0.0, Math.Min(1.0, EditorGUILayout.DoubleField("Density / tile", entry.DensityPerTile)));
                    entry.MinimumSpacingTiles = Mathf.Clamp(EditorGUILayout.IntField("Min spacing", entry.MinimumSpacingTiles), 0, 64);
                    entry.Priority = EditorGUILayout.IntField("Priority", entry.Priority);
                    var saltText = EditorGUILayout.TextField("Seed salt", entry.SeedSalt.ToString());
                    uint salt;
                    if (uint.TryParse(saltText, out salt)) entry.SeedSalt = salt;
                }
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("Add Entry", EditorStyles.miniBoldLabel);
                _entryId = EditorGUILayout.TextField("Entry ID", _entryId);
                _entryDefinitionId = EditorGUILayout.TextField("Definition", _entryDefinitionId);
                _entryKind = (WorldPlacementKind)EditorGUILayout.EnumPopup("Placement", _entryKind);
                _entryChannel = (DressingOccupancyChannel)EditorGUILayout.EnumPopup("Channel", _entryChannel);
                _entryDensity = Math.Max(0.0, Math.Min(1.0, EditorGUILayout.DoubleField("Density / tile", _entryDensity)));
                _entrySpacing = Mathf.Clamp(EditorGUILayout.IntField("Min spacing", _entrySpacing), 0, 64);
                _entryPriority = EditorGUILayout.IntField("Priority", _entryPriority);
                _entrySeedSalt = Mathf.Max(0, EditorGUILayout.IntField("Seed salt", _entrySeedSalt));
                GUI.enabled = ContentId.IsValid(_entryId) && ContentId.IsValid(_entryDefinitionId);
                if (GUILayout.Button("Add Entry")) AddEntry();
                GUI.enabled = true;
            }
        }

        private void DrawOverrideSection()
        {
            GUILayout.Space(10);
            GUILayout.Label("Local Density Overrides", EditorStyles.miniBoldLabel);
            for (var i = 0; i < _working.DensityOverrides.Count; i++)
            {
                var item = _working.DensityOverrides[i];
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    GUILayout.Label(item.Id.Value, GUILayout.Width(120));
                    item.DensityMultiplier = Math.Max(0.0, Math.Min(8.0, EditorGUILayout.DoubleField(item.DensityMultiplier, GUILayout.Width(55))));
                    GUILayout.Label(item.TargetEntryId.HasValue ? item.TargetEntryId.Value.Value : "All", EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("X", GUILayout.Width(24)))
                    {
                        _working.RemoveDensityOverride(item.Id);
                        RebuildPreview();
                        GUIUtility.ExitGUI();
                    }
                }
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("Add Circle Override", EditorStyles.miniBoldLabel);
                _overrideId = EditorGUILayout.TextField("Override ID", _overrideId);
                _overrideMultiplier = Math.Max(0.0, Math.Min(8.0, EditorGUILayout.DoubleField("Density multiplier", _overrideMultiplier)));
                _overrideRadius = Mathf.Max(0, EditorGUILayout.IntField("Radius", _overrideRadius));
                EditorGUILayout.LabelField("Centre", _overrideCenter.X + ", " + _overrideCenter.Y);

                var targets = new List<string> { "All entries" };
                for (var i = 0; i < _working.Entries.Count; i++) targets.Add(_working.Entries[i].Id.Value);
                _overrideTargetIndex = Mathf.Clamp(EditorGUILayout.Popup("Target", _overrideTargetIndex, targets.ToArray()), 0, targets.Count - 1);
                EditorGUILayout.HelpBox("Left-click the preview sets the centre for the next density override. Multiplier 0 makes a deterministic clearing/exclusion area.", MessageType.None);
                GUI.enabled = ContentId.IsValid(_overrideId);
                if (GUILayout.Button("Add Override")) AddOverride();
                GUI.enabled = true;
            }
        }

        private void DrawExceptionSection()
        {
            GUILayout.Space(10);
            GUILayout.Label("Manual Generated-Object Removals", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField("Suppressed placements", _exceptions.SuppressedCount.ToString());
            EditorGUILayout.HelpBox("Right-click a generated preview point to suppress that exact deterministic placement. This saves only the exception, not the whole biome.", MessageType.None);
            GUI.enabled = _exceptions.SuppressedCount > 0;
            if (GUILayout.Button("Clear All Suppressions"))
            {
                _exceptions = new BiomeDressingExceptionSet();
                BiomeDressingJsonPersistence.SaveExceptions(_working.Id, _exceptions);
                RebuildPreview();
            }
            GUI.enabled = true;
        }

        private void DrawCanvas(Rect canvas)
        {
            EditorGUI.DrawRect(canvas, new Color(0.095f, 0.10f, 0.11f));
            GUI.BeginGroup(canvas);
            var local = new Rect(0, 0, canvas.width, canvas.height);
            var bounds = VisibleBounds(local);
            DrawGrid(local, bounds);
            DrawBiome(bounds);
            RebuildPreview(bounds);
            DrawOverrides(bounds);
            DrawPlacements(bounds);
            GUI.EndGroup();
        }

        private void DrawGrid(Rect local, DressingTileBounds bounds)
        {
            if (_pixelsPerTile < 8f) return;
            Handles.BeginGUI();
            var old = Handles.color;
            Handles.color = new Color(1f, 1f, 1f, 0.055f);
            for (var x = bounds.MinX; x <= bounds.MaxX + 1; x++)
            {
                var px = (x - bounds.MinX) * _pixelsPerTile;
                Handles.DrawLine(new Vector3(px, 0), new Vector3(px, local.height));
            }
            for (var y = bounds.MinY; y <= bounds.MaxY + 1; y++)
            {
                var py = (y - bounds.MinY) * _pixelsPerTile;
                Handles.DrawLine(new Vector3(0, py), new Vector3(local.width, py));
            }
            Handles.color = old;
            Handles.EndGUI();
        }

        private void DrawBiome(DressingTileBounds bounds)
        {
            var biome = CurrentBiome();
            if (biome == null) return;
            Handles.BeginGUI();
            var old = Handles.color;
            Handles.color = new Color(0.25f, 0.8f, 0.42f, 0.9f);
            DrawShape(biome.Shape, bounds, 3f);
            Handles.color = old;
            Handles.EndGUI();
        }

        private void DrawOverrides(DressingTileBounds bounds)
        {
            if (_working == null) return;
            Handles.BeginGUI();
            var old = Handles.color;
            for (var i = 0; i < _working.DensityOverrides.Count; i++)
            {
                var item = _working.DensityOverrides[i];
                Handles.color = item.DensityMultiplier == 0.0
                    ? new Color(1f, 0.32f, 0.2f, 0.9f)
                    : new Color(1f, 0.75f, 0.18f, 0.9f);
                DrawShape(item.Shape, bounds, 2f);
            }
            Handles.color = new Color(0.8f, 0.8f, 1f, 0.7f);
            DrawShape(new CircleAreaShape(_overrideCenter, _overrideRadius), bounds, 1f);
            Handles.color = old;
            Handles.EndGUI();
        }

        private void DrawPlacements(DressingTileBounds bounds)
        {
            Handles.BeginGUI();
            var old = Handles.color;
            for (var i = 0; i < _preview.Count; i++)
            {
                var item = _preview[i];
                switch (item.Channel)
                {
                    case DressingOccupancyChannel.Major: Handles.color = new Color(0.25f, 0.9f, 0.32f, 0.92f); break;
                    case DressingOccupancyChannel.Minor: Handles.color = new Color(0.35f, 0.72f, 1f, 0.88f); break;
                    default: Handles.color = new Color(0.9f, 0.85f, 0.35f, 0.80f); break;
                }
                var p = TileCenter(item.Key.Anchor.Tile, bounds);
                var radius = item.Channel == DressingOccupancyChannel.Major ? 3.2f : 2.0f;
                Handles.DrawSolidDisc(p, Vector3.forward, radius);
            }
            Handles.color = old;
            Handles.EndGUI();
        }

        private void DrawShape(WorldAreaShape shape, DressingTileBounds bounds, float width)
        {
            var circle = shape as CircleAreaShape;
            if (circle != null)
            {
                Handles.DrawWireDisc(TileCenter(circle.Center, bounds), Vector3.forward, circle.RadiusTiles * _pixelsPerTile);
                return;
            }

            var polygon = shape as PolygonAreaShape;
            if (polygon == null || polygon.Points.Count < 2) return;
            var points = new Vector3[polygon.Points.Count + 1];
            for (var i = 0; i < polygon.Points.Count; i++) points[i] = TileCenter(polygon.Points[i], bounds);
            points[points.Length - 1] = points[0];
            Handles.DrawAAPolyLine(width, points);
        }

        private void HandleCanvasInput(Rect canvas)
        {
            var e = Event.current;
            if (!canvas.Contains(e.mousePosition)) return;
            var local = e.mousePosition - canvas.position;

            if (e.type == EventType.ScrollWheel)
            {
                _pixelsPerTile = Mathf.Clamp(_pixelsPerTile - e.delta.y * 0.8f, 2.5f, 32f);
                e.Use(); Repaint(); return;
            }

            if ((e.button == 2 || (e.button == 0 && e.alt)) && e.type == EventType.MouseDrag)
            {
                _centerX = Mathf.Clamp(_centerX - e.delta.x / _pixelsPerTile, 0, WorldConstants.WorldWidthTiles - 1);
                _centerY = Mathf.Clamp(_centerY - e.delta.y / _pixelsPerTile, 0, WorldConstants.WorldHeightTiles - 1);
                e.Use(); Repaint(); return;
            }

            if (e.type != EventType.MouseDown || e.alt) return;
            var bounds = VisibleBounds(new Rect(0, 0, canvas.width, canvas.height));
            var tile = new GridCoord(
                Mathf.Clamp(bounds.MinX + Mathf.FloorToInt(local.x / _pixelsPerTile), 0, WorldConstants.WorldWidthTiles - 1),
                Mathf.Clamp(bounds.MinY + Mathf.FloorToInt(local.y / _pixelsPerTile), 0, WorldConstants.WorldHeightTiles - 1));

            if (e.button == 0)
            {
                _overrideCenter = tile;
                _status = $"Density-override centre set to {tile.X}, {tile.Y}.";
                e.Use(); Repaint(); return;
            }

            if (e.button == 1 && _working != null)
            {
                var nearest = FindPlacementNear(tile);
                if (nearest != null)
                {
                    _exceptions.Suppress(nearest.Key);
                    BiomeDressingJsonPersistence.SaveExceptions(_working.Id, _exceptions);
                    _status = $"Suppressed generated {nearest.Key.EntryId.Value} at {tile.X}, {tile.Y}.";
                    e.Use(); Repaint();
                }
            }
        }

        private BiomeDressingPlacement FindPlacementNear(GridCoord tile)
        {
            BiomeDressingPlacement best = null;
            var bestDistance = int.MaxValue;
            for (var i = 0; i < _preview.Count; i++)
            {
                var p = _preview[i];
                var dx = Math.Abs(p.Key.Anchor.Tile.X - tile.X);
                var dy = Math.Abs(p.Key.Anchor.Tile.Y - tile.Y);
                var d = Math.Max(dx, dy);
                if (d > 1 || d >= bestDistance) continue;
                bestDistance = d;
                best = p;
            }
            return best;
        }

        private void CreateProfile()
        {
            if (_biomes.Count == 0 || _selectedBiome < 0 || _selectedBiome >= _biomes.Count) return;
            ContentId id;
            if (!ContentId.TryCreate(_newProfileId, out id)) { _status = "Invalid dressing profile ID."; return; }
            _working = new BiomeDressingProfile(id, _biomes[_selectedBiome].Id, _newSeed);
            _exceptions = new BiomeDressingExceptionSet();
            CentreOnBiome();
            _status = "Created unsaved biome dressing profile.";
        }

        private void AddEntry()
        {
            ContentId entryId;
            ContentId definitionId;
            if (!ContentId.TryCreate(_entryId, out entryId) || !ContentId.TryCreate(_entryDefinitionId, out definitionId)) return;
            try
            {
                _working.AddEntry(new BiomeDressingEntry(
                    entryId,
                    definitionId,
                    _entryKind,
                    _entryDensity,
                    _entrySpacing,
                    _entryChannel,
                    _entryPriority,
                    unchecked((uint)_entrySeedSalt)));
                _status = $"Added dressing entry '{entryId.Value}'.";
                RebuildPreview();
            }
            catch (Exception ex)
            {
                _status = ex.Message;
            }
        }

        private void AddOverride()
        {
            ContentId id;
            if (!ContentId.TryCreate(_overrideId, out id)) return;
            ContentId? target = null;
            if (_overrideTargetIndex > 0 && _overrideTargetIndex - 1 < _working.Entries.Count)
                target = _working.Entries[_overrideTargetIndex - 1].Id;
            try
            {
                _working.AddDensityOverride(new BiomeDressingDensityOverride(
                    id,
                    new CircleAreaShape(_overrideCenter, _overrideRadius),
                    _overrideMultiplier,
                    target));
                _status = $"Added density override '{id.Value}'.";
                RebuildPreview();
            }
            catch (Exception ex)
            {
                _status = ex.Message;
            }
        }

        private void SaveWorking()
        {
            if (_working == null) return;
            BiomeDressingJsonPersistence.SaveProfile(_working);
            BiomeDressingJsonPersistence.SaveExceptions(_working.Id, _exceptions);
            _status = $"Saved dressing profile '{_working.Id.Value}'.";
            ReloadProfiles(_working.Id);
        }

        private void LoadWorking(BiomeDressingProfile profile)
        {
            _working = profile;
            _exceptions = BiomeDressingJsonPersistence.LoadExceptions(profile.Id);
            _selectedBiome = _biomes.FindIndex(b => b.Id == profile.BiomeAreaId);
            CentreOnBiome();
            _status = $"Loaded '{profile.Id.Value}'.";
        }

        private void ReloadProfiles(ContentId? select = null)
        {
            _profiles.Clear();
            _selectedProfile = -1;
            foreach (var file in BiomeDressingJsonPersistence.EnumerateProfileFiles())
                if (BiomeDressingJsonPersistence.TryLoadProfile(file, out var profile)) _profiles.Add(profile);
            _profiles.Sort((a, b) => a.Id.CompareTo(b.Id));
            if (select.HasValue)
            {
                for (var i = 0; i < _profiles.Count; i++)
                    if (_profiles[i].Id == select.Value) { _selectedProfile = i; LoadWorking(_profiles[i]); break; }
            }
        }

        private void ReloadBiomes()
        {
            _biomes.Clear();
            foreach (var file in WorldAreaJsonPersistence.EnumerateFiles())
            {
                WorldAreaDefinition area;
                if (WorldAreaJsonPersistence.TryLoad(file, out area) && area.Kind == WorldAreaKind.Biome)
                    _biomes.Add(area);
            }
            _biomes.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
            _selectedBiome = _biomes.Count > 0 ? Mathf.Clamp(_selectedBiome, 0, _biomes.Count - 1) : -1;
        }

        private WorldAreaDefinition CurrentBiome()
        {
            if (_working != null)
            {
                for (var i = 0; i < _biomes.Count; i++)
                    if (_biomes[i].Id == _working.BiomeAreaId) return _biomes[i];
                return null;
            }
            return _selectedBiome >= 0 && _selectedBiome < _biomes.Count ? _biomes[_selectedBiome] : null;
        }

        private int CurrentPlane()
        {
            var biome = CurrentBiome();
            return biome != null ? biome.Plane : WorldConstants.SurfacePlane;
        }

        private void CentreOnBiome()
        {
            var biome = CurrentBiome();
            if (biome == null) return;
            var bounds = DressingTileBounds.FromShape(biome.Shape);
            _centerX = (bounds.MinX + bounds.MaxX) * 0.5f;
            _centerY = (bounds.MinY + bounds.MaxY) * 0.5f;
            _overrideCenter = new GridCoord(Mathf.RoundToInt(_centerX), Mathf.RoundToInt(_centerY));
            Repaint();
        }

        private void RebuildPreview()
        {
            Repaint();
        }

        private void RebuildPreview(DressingTileBounds bounds)
        {
            _preview.Clear();
            var biome = CurrentBiome();
            if (_working == null || biome == null) return;
            var generated = BiomeDressingGenerator.Generate(_working, biome.Shape, bounds, biome.Plane, 0, _exceptions);
            for (var i = 0; i < generated.Count; i++) _preview.Add(generated[i]);
        }

        private DressingTileBounds VisibleBounds(Rect local)
        {
            var width = Mathf.CeilToInt(local.width / _pixelsPerTile) + 2;
            var height = Mathf.CeilToInt(local.height / _pixelsPerTile) + 2;
            var minX = Mathf.Clamp(Mathf.FloorToInt(_centerX - width * 0.5f), 0, WorldConstants.WorldWidthTiles - 1);
            var minY = Mathf.Clamp(Mathf.FloorToInt(_centerY - height * 0.5f), 0, WorldConstants.WorldHeightTiles - 1);
            var maxX = Mathf.Clamp(minX + width, minX, WorldConstants.WorldWidthTiles - 1);
            var maxY = Mathf.Clamp(minY + height, minY, WorldConstants.WorldHeightTiles - 1);
            return new DressingTileBounds(minX, minY, maxX, maxY);
        }

        private Vector3 TileCenter(GridCoord tile, DressingTileBounds bounds)
            => new Vector3(
                (tile.X - bounds.MinX + 0.5f) * _pixelsPerTile,
                (tile.Y - bounds.MinY + 0.5f) * _pixelsPerTile,
                0f);

        private void DrawStatus(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.14f, 0.14f, 0.15f));
            GUI.Label(new Rect(rect.x + 6f, rect.y + 2f, rect.width - 12f, rect.height - 4f), _status, EditorStyles.miniLabel);
        }
    }
}
