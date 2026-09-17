using System;
using System.Collections.Generic;
using MassRPG.Core.Content;
using MassRPG.Core.World;
using MassRPG.Data.Creatures;
using MassRPG.Data.World;
using MassRPG.Data.World.Semantics;
using UnityEditor;
using UnityEngine;

namespace MassRPG.Editor.World
{
    /// <summary>
    /// Authoring surface for ordinary creature populations. These are simulation-only regions and
    /// are never automatically exposed as player-map markers. Population is fixed authored capacity,
    /// while sleeping/materialization remains server runtime state.
    /// </summary>
    public sealed class MassRPGCreatureSpawnEditorWindow : EditorWindow
    {
        private enum ShapeMode { Circle, Polygon }
        private enum EditTarget { SpawnArea, PatrolRoute }

        private const float InspectorWidth = 330f;
        private readonly List<CreatureSpawnRegionDefinition> _saved = new List<CreatureSpawnRegionDefinition>();
        private readonly List<string> _labels = new List<string>();
        private readonly List<GridCoord> _areaPolygon = new List<GridCoord>();
        private readonly List<GridCoord> _patrolRoute = new List<GridCoord>();
        private readonly HashSet<WorldPageKey> _terrainChecked = new HashSet<WorldPageKey>();
        private AuthoredWorldPageStore _terrain;
        private int _selected = -1;
        private string _idText = "spawn.creature.new";
        private string _creatureId = "creature.wolf";
        private int _populationCap = 6;
        private int _respawnSeconds = 30;
        private CreatureRoamingMode _roamingMode = CreatureRoamingMode.FreeRoam;
        private ShapeMode _shapeMode = ShapeMode.Circle;
        private EditTarget _editTarget = EditTarget.SpawnArea;
        private GridCoord _circleCenter = new GridCoord(90000, 90000);
        private int _circleRadius = 12;
        private int _plane = WorldConstants.SurfacePlane;
        private int _storey;
        private float _centerX = WorldConstants.WorldWidthTiles * 0.5f;
        private float _centerY = WorldConstants.WorldHeightTiles * 0.5f;
        private float _pixelsPerTile = 10f;
        private string _status = "Define a population area. Spawn regions are system data, not public-map markers.";

        [MenuItem("MassRPG/Creature Spawn Editor")]
        public static void Open()
        {
            var window = GetWindow<MassRPGCreatureSpawnEditorWindow>();
            window.titleContent = new GUIContent("MassRPG Creature Spawns");
            window.minSize = new Vector2(1020, 670);
            window.Show();
        }

        private void OnEnable()
        {
            ResetTerrain();
            ReloadList();
            NewRegion();
        }

        private void ResetTerrain()
        {
            _terrain = new AuthoredWorldPageStore(new ContentId("ground.unpainted"));
            _terrainChecked.Clear();
        }

        private void OnGUI()
        {
            DrawTopToolbar();
            var canvas = new Rect(0f, 21f, Mathf.Max(100f, position.width - InspectorWidth), Mathf.Max(100f, position.height - 43f));
            var inspector = new Rect(canvas.xMax, 21f, InspectorWidth, position.height - 21f);
            EnsureVisibleTerrainLoaded(canvas);
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
                GUILayout.Label("Plane", GUILayout.Width(35));
                var plane = EditorGUILayout.IntField(_plane, EditorStyles.toolbarTextField, GUILayout.Width(38));
                GUILayout.Label("Floor", GUILayout.Width(32));
                var storey = Mathf.Max(0, EditorGUILayout.IntField(_storey, EditorStyles.toolbarTextField, GUILayout.Width(34)));
                if (x != Mathf.RoundToInt(_centerX) || y != Mathf.RoundToInt(_centerY))
                {
                    _centerX = Mathf.Clamp(x, 0, WorldConstants.WorldWidthTiles - 1);
                    _centerY = Mathf.Clamp(y, 0, WorldConstants.WorldHeightTiles - 1);
                }
                if (plane != _plane || storey != _storey)
                {
                    _plane = plane;
                    _storey = storey;
                    ResetTerrain();
                }
                GUILayout.Space(8);
                if (GUILayout.Button("Terrain", EditorStyles.toolbarButton, GUILayout.Width(55)))
                    MassRPGWorldEditorWindow.OpenAt(Mathf.RoundToInt(_centerX), Mathf.RoundToInt(_centerY), _plane, _storey);
                if (GUILayout.Button("Areas", EditorStyles.toolbarButton, GUILayout.Width(50))) MassRPGAreaEditorWindow.Open();
                GUILayout.FlexibleSpace();
                GUILayout.Label($"{_pixelsPerTile:0}px/tile", EditorStyles.miniLabel);
            }
        }

        private void DrawInspector(Rect rect)
        {
            GUILayout.BeginArea(rect, EditorStyles.helpBox);
            GUILayout.Label("Creature Population", EditorStyles.boldLabel);

            GUI.enabled = _saved.Count > 0;
            var labels = _labels.Count > 0 ? _labels.ToArray() : new[] { "No saved populations" };
            var next = EditorGUILayout.Popup(Mathf.Max(0, _selected), labels);
            GUI.enabled = true;
            if (_saved.Count > 0 && next != _selected)
            {
                _selected = next;
                LoadRegion(_saved[_selected]);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("New")) NewRegion();
                if (GUILayout.Button("Reload")) ReloadList();
            }

            _idText = EditorGUILayout.TextField("Stable ID", _idText);
            _creatureId = EditorGUILayout.TextField("Creature ID", _creatureId);
            _populationCap = Mathf.Max(1, EditorGUILayout.IntField("Population cap", _populationCap));
            _respawnSeconds = Mathf.Max(1, EditorGUILayout.IntField("Respawn seconds", _respawnSeconds));
            _roamingMode = (CreatureRoamingMode)EditorGUILayout.EnumPopup("Roaming", _roamingMode);
            _shapeMode = (ShapeMode)EditorGUILayout.EnumPopup("Area shape", _shapeMode);

            if (_roamingMode == CreatureRoamingMode.FixedSpawnPoint)
            {
                _shapeMode = ShapeMode.Circle;
                _circleRadius = 0;
                EditorGUILayout.HelpBox("Fixed Spawn Point uses the circle centre as the exact spawn tile (radius 0).", MessageType.None);
            }
            else if (_shapeMode == ShapeMode.Circle)
            {
                _circleRadius = Mathf.Max(0, EditorGUILayout.IntField("Area radius", _circleRadius));
            }
            else
            {
                EditorGUILayout.LabelField("Area vertices", _areaPolygon.Count.ToString());
            }

            EditorGUILayout.Space(5);
            if (_roamingMode == CreatureRoamingMode.PatrolRoute)
            {
                _editTarget = (EditTarget)GUILayout.Toolbar((int)_editTarget, new[] { "Spawn Area", "Patrol Route" });
                EditorGUILayout.LabelField("Patrol points", _patrolRoute.Count.ToString());
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUI.enabled = _patrolRoute.Count > 0;
                    if (GUILayout.Button("Undo Patrol")) _patrolRoute.RemoveAt(_patrolRoute.Count - 1);
                    if (GUILayout.Button("Clear Patrol")) _patrolRoute.Clear();
                    GUI.enabled = true;
                }
            }
            else
            {
                _editTarget = EditTarget.SpawnArea;
            }

            if (_shapeMode == ShapeMode.Polygon)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUI.enabled = _areaPolygon.Count > 0;
                    if (GUILayout.Button("Undo Area")) _areaPolygon.RemoveAt(_areaPolygon.Count - 1);
                    if (GUILayout.Button("Clear Area")) _areaPolygon.Clear();
                    GUI.enabled = true;
                }
            }

            EditorGUILayout.Space(7);
            EditorGUILayout.HelpBox(
                "The cap is the authored normal population. It does not scale upward merely because more players arrive. Sleeping preserves logical population/timers; ordinary exact creature identities may rematerialize later.",
                MessageType.Info);
            EditorGUILayout.HelpBox(
                "Left-click edits the selected Spawn Area or Patrol Route. Shift+click removes the nearest polygon/patrol point. Creature populations are intentionally not player-map data.",
                MessageType.None);

            GUI.enabled = CanSave();
            if (GUILayout.Button("Save Population", GUILayout.Height(28))) SaveRegion();
            GUI.enabled = _selected >= 0 && _selected < _saved.Count;
            if (GUILayout.Button("Delete Saved Population")) DeleteSelected();
            GUI.enabled = true;

            GUILayout.FlexibleSpace();
            GUILayout.Label("WorldData/Creatures/SpawnRegions", EditorStyles.miniLabel);
            GUILayout.EndArea();
        }

        private void DrawCanvas(Rect canvas)
        {
            EditorGUI.DrawRect(canvas, new Color(0.105f, 0.11f, 0.12f));
            GUI.BeginGroup(canvas);
            var local = new Rect(0, 0, canvas.width, canvas.height);
            var bounds = VisibleBounds(local);
            DrawTerrain(bounds);
            DrawGrid(local, bounds);
            DrawSavedRegions(bounds);
            DrawDraft(bounds);
            GUI.EndGroup();
        }

        private void DrawTerrain(TileBounds bounds)
        {
            for (var y = bounds.MinY; y <= bounds.MaxY; y++)
            {
                if (y < 0 || y >= WorldConstants.WorldHeightTiles) continue;
                for (var x = bounds.MinX; x <= bounds.MaxX; x++)
                {
                    if (x < 0 || x >= WorldConstants.WorldWidthTiles) continue;
                    if (!_terrain.TryGetCell(new GridLocation(new GridCoord(x, y), _plane, _storey), out var cell)) continue;
                    var rect = TileRect(x, y, bounds);
                    EditorGUI.DrawRect(rect, GroundColor(cell.GroundId));
                    if ((cell.Flags & TileFlags.DeepWater) != 0) EditorGUI.DrawRect(rect, new Color(0.05f, 0.18f, 0.35f, 0.82f));
                    else if ((cell.Flags & TileFlags.Water) != 0) EditorGUI.DrawRect(rect, new Color(0.08f, 0.33f, 0.52f, 0.70f));
                }
            }
        }

        private void DrawGrid(Rect local, TileBounds bounds)
        {
            if (_pixelsPerTile < 10f) return;
            Handles.BeginGUI();
            var old = Handles.color;
            Handles.color = new Color(1f, 1f, 1f, 0.07f);
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

        private void DrawSavedRegions(TileBounds bounds)
        {
            Handles.BeginGUI();
            var old = Handles.color;
            for (var i = 0; i < _saved.Count; i++)
            {
                var region = _saved[i];
                if (region.Plane != _plane || region.Storey != _storey) continue;
                Handles.color = i == _selected ? new Color(1f, 0.55f, 0.16f, 0.75f) : new Color(0.95f, 0.22f, 0.16f, 0.34f);
                DrawShape(region.Area, bounds);
                if (region.RoamingMode == CreatureRoamingMode.PatrolRoute && region.PatrolRoute.Count >= 2)
                    DrawPolyline(region.PatrolRoute, bounds, 2f);
            }
            Handles.color = old;
            Handles.EndGUI();
        }

        private void DrawDraft(TileBounds bounds)
        {
            Handles.BeginGUI();
            var old = Handles.color;
            Handles.color = new Color(1f, 0.35f, 0.16f, 0.95f);
            if (_shapeMode == ShapeMode.Circle)
            {
                var center = TileCenter(_circleCenter, bounds);
                Handles.DrawWireDisc(center, Vector3.forward, _circleRadius * _pixelsPerTile);
                Handles.DrawSolidDisc(center, Vector3.forward, Mathf.Clamp(_pixelsPerTile * 0.14f, 2f, 5f));
            }
            else if (_areaPolygon.Count > 0)
            {
                DrawPolylineClosed(_areaPolygon, bounds, 3f);
                for (var i = 0; i < _areaPolygon.Count; i++)
                    Handles.DrawSolidDisc(TileCenter(_areaPolygon[i], bounds), Vector3.forward, Mathf.Clamp(_pixelsPerTile * 0.12f, 2f, 5f));
            }

            if (_roamingMode == CreatureRoamingMode.PatrolRoute && _patrolRoute.Count > 0)
            {
                Handles.color = new Color(1f, 0.85f, 0.18f, 0.95f);
                DrawPolyline(_patrolRoute, bounds, 3f);
                for (var i = 0; i < _patrolRoute.Count; i++)
                    Handles.DrawSolidDisc(TileCenter(_patrolRoute[i], bounds), Vector3.forward, Mathf.Clamp(_pixelsPerTile * 0.1f, 2f, 4f));
            }
            Handles.color = old;
            Handles.EndGUI();
        }

        private void DrawShape(WorldAreaShape shape, TileBounds bounds)
        {
            if (shape is CircleAreaShape circle)
            {
                Handles.DrawWireDisc(TileCenter(circle.Center, bounds), Vector3.forward, circle.RadiusTiles * _pixelsPerTile);
            }
            else if (shape is PolygonAreaShape polygon)
            {
                DrawPolylineClosed(polygon.Points, bounds, 2f);
            }
        }

        private void DrawPolyline(IReadOnlyList<GridCoord> points, TileBounds bounds, float width)
        {
            if (points.Count < 2) return;
            var vectors = new Vector3[points.Count];
            for (var i = 0; i < points.Count; i++) vectors[i] = TileCenter(points[i], bounds);
            Handles.DrawAAPolyLine(width, vectors);
        }

        private void DrawPolylineClosed(IReadOnlyList<GridCoord> points, TileBounds bounds, float width)
        {
            if (points.Count < 2) return;
            var count = points.Count >= 3 ? points.Count + 1 : points.Count;
            var vectors = new Vector3[count];
            for (var i = 0; i < points.Count; i++) vectors[i] = TileCenter(points[i], bounds);
            if (points.Count >= 3) vectors[vectors.Length - 1] = vectors[0];
            Handles.DrawAAPolyLine(width, vectors);
        }

        private void HandleCanvasInput(Rect canvas)
        {
            var e = Event.current;
            if (!canvas.Contains(e.mousePosition)) return;
            var local = e.mousePosition - canvas.position;
            if (e.type == EventType.ScrollWheel)
            {
                _pixelsPerTile = Mathf.Clamp(_pixelsPerTile - e.delta.y, 1.5f, 38f);
                e.Use(); Repaint(); return;
            }
            if ((e.button == 2 || (e.button == 0 && e.alt)) && e.type == EventType.MouseDrag)
            {
                _centerX = Mathf.Clamp(_centerX - e.delta.x / _pixelsPerTile, 0, WorldConstants.WorldWidthTiles - 1);
                _centerY = Mathf.Clamp(_centerY - e.delta.y / _pixelsPerTile, 0, WorldConstants.WorldHeightTiles - 1);
                e.Use(); Repaint(); return;
            }
            if (e.button != 0 || e.alt || e.type != EventType.MouseDown) return;

            var bounds = VisibleBounds(new Rect(0, 0, canvas.width, canvas.height));
            var tile = new GridCoord(
                bounds.MinX + Mathf.FloorToInt(local.x / _pixelsPerTile),
                bounds.MinY + Mathf.FloorToInt(local.y / _pixelsPerTile));
            if (!WorldConstants.IsInsideWorld(tile)) return;

            if (_editTarget == EditTarget.PatrolRoute && _roamingMode == CreatureRoamingMode.PatrolRoute)
            {
                if (e.shift && _patrolRoute.Count > 0) _patrolRoute.RemoveAt(Nearest(_patrolRoute, tile));
                else if (_patrolRoute.Count == 0 || _patrolRoute[_patrolRoute.Count - 1] != tile) _patrolRoute.Add(tile);
                _status = $"Patrol route has {_patrolRoute.Count} point(s).";
            }
            else if (_shapeMode == ShapeMode.Circle)
            {
                _circleCenter = tile;
                _status = $"Spawn-area centre set to {tile.X}, {tile.Y}.";
            }
            else
            {
                if (e.shift && _areaPolygon.Count > 0) _areaPolygon.RemoveAt(Nearest(_areaPolygon, tile));
                else if (_areaPolygon.Count == 0 || _areaPolygon[_areaPolygon.Count - 1] != tile) _areaPolygon.Add(tile);
                _status = $"Spawn area has {_areaPolygon.Count} polygon vertices.";
            }
            e.Use(); Repaint();
        }

        private static int Nearest(List<GridCoord> points, GridCoord tile)
        {
            var nearest = 0;
            var best = long.MaxValue;
            for (var i = 0; i < points.Count; i++)
            {
                var dx = (long)points[i].X - tile.X;
                var dy = (long)points[i].Y - tile.Y;
                var d = dx * dx + dy * dy;
                if (d >= best) continue;
                best = d; nearest = i;
            }
            return nearest;
        }

        private bool CanSave()
            => (_shapeMode == ShapeMode.Circle || _areaPolygon.Count >= 3)
            && (_roamingMode != CreatureRoamingMode.PatrolRoute || _patrolRoute.Count >= 2);

        private void SaveRegion()
        {
            if (!ContentId.TryCreate(_idText, out var id)) { _status = "Invalid stable spawn-region ID."; return; }
            if (!ContentId.TryCreate(_creatureId, out var creatureId)) { _status = "Invalid creature definition ID."; return; }
            WorldAreaShape shape = _shapeMode == ShapeMode.Circle
                ? (WorldAreaShape)new CircleAreaShape(_circleCenter, _circleRadius)
                : new PolygonAreaShape(_areaPolygon);
            var region = new CreatureSpawnRegionDefinition(
                id,
                creatureId,
                shape,
                _plane,
                _storey,
                _populationCap,
                checked((long)_respawnSeconds * 1000L),
                _roamingMode,
                _roamingMode == CreatureRoamingMode.PatrolRoute ? _patrolRoute : null);
            CreatureSpawnRegionJsonPersistence.Save(region);
            _status = $"Saved creature population '{id}'.";
            ReloadList(id);
        }

        private void DeleteSelected()
        {
            if (_selected < 0 || _selected >= _saved.Count) return;
            var region = _saved[_selected];
            if (!EditorUtility.DisplayDialog("Delete Creature Population", $"Delete '{region.Id}'?", "Delete", "Cancel")) return;
            CreatureSpawnRegionJsonPersistence.Delete(region.Id);
            ReloadList();
            NewRegion();
        }

        private void NewRegion()
        {
            _selected = -1;
            _areaPolygon.Clear();
            _patrolRoute.Clear();
            _circleCenter = new GridCoord(Mathf.RoundToInt(_centerX), Mathf.RoundToInt(_centerY));
            _status = "New creature population. Define its spawn/roam area.";
            Repaint();
        }

        private void ReloadList(ContentId? select = null)
        {
            _saved.Clear(); _labels.Clear(); _selected = -1;
            foreach (var file in CreatureSpawnRegionJsonPersistence.EnumerateFiles())
                if (CreatureSpawnRegionJsonPersistence.TryLoad(file, out var region)) _saved.Add(region);
            _saved.Sort((a, b) => string.Compare(a.Id.Value, b.Id.Value, StringComparison.OrdinalIgnoreCase));
            for (var i = 0; i < _saved.Count; i++)
            {
                _labels.Add($"{_saved[i].Id.Value} · {_saved[i].CreatureDefinitionId.Value} ({_saved[i].PopulationCap})");
                if (select.HasValue && _saved[i].Id == select.Value) _selected = i;
            }
            if (_selected >= 0) LoadRegion(_saved[_selected]);
            Repaint();
        }

        private void LoadRegion(CreatureSpawnRegionDefinition region)
        {
            _idText = region.Id.Value;
            _creatureId = region.CreatureDefinitionId.Value;
            _populationCap = region.PopulationCap;
            _respawnSeconds = (int)Math.Max(1, Math.Min(int.MaxValue, region.RespawnIntervalMilliseconds / 1000L));
            _roamingMode = region.RoamingMode;
            _plane = region.Plane;
            _storey = region.Storey;
            _areaPolygon.Clear(); _patrolRoute.Clear();
            if (region.Area is CircleAreaShape circle)
            {
                _shapeMode = ShapeMode.Circle;
                _circleCenter = circle.Center;
                _circleRadius = circle.RadiusTiles;
                _centerX = circle.Center.X; _centerY = circle.Center.Y;
            }
            else if (region.Area is PolygonAreaShape polygon)
            {
                _shapeMode = ShapeMode.Polygon;
                for (var i = 0; i < polygon.Points.Count; i++) _areaPolygon.Add(polygon.Points[i]);
                if (_areaPolygon.Count > 0) { _centerX = _areaPolygon[0].X; _centerY = _areaPolygon[0].Y; }
            }
            for (var i = 0; i < region.PatrolRoute.Count; i++) _patrolRoute.Add(region.PatrolRoute[i]);
            _status = $"Loaded {region.Id}.";
            ResetTerrain();
        }

        private void EnsureVisibleTerrainLoaded(Rect canvas)
        {
            var bounds = VisibleBounds(new Rect(0, 0, canvas.width, canvas.height));
            var minX = Mathf.Clamp(bounds.MinX, 0, WorldConstants.WorldWidthTiles - 1);
            var minY = Mathf.Clamp(bounds.MinY, 0, WorldConstants.WorldHeightTiles - 1);
            var maxX = Mathf.Clamp(bounds.MaxX, 0, WorldConstants.WorldWidthTiles - 1);
            var maxY = Mathf.Clamp(bounds.MaxY, 0, WorldConstants.WorldHeightTiles - 1);
            var size = _terrain.PageSize;
            for (var py = minY / size; py <= maxY / size; py++)
                for (var px = minX / size; px <= maxX / size; px++)
                {
                    var key = new WorldPageKey(new WorldPageCoord(px, py), _plane, _storey);
                    if (!_terrainChecked.Add(key)) continue;
                    if (!WorldPageJsonPersistence.TryLoad(key, out var document)) continue;
                    try { _terrain.ImportPage(WorldPageCodec.Decode(document)); }
                    catch (Exception ex) { Debug.LogError($"Failed to load terrain page {key} behind spawn editor: {ex}"); }
                }
        }

        private void DrawStatus(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.13f, 0.13f, 0.13f));
            GUI.Label(new Rect(rect.x + 6, rect.y + 2, rect.width - 12, 18), _status, EditorStyles.miniLabel);
        }

        private TileBounds VisibleBounds(Rect rect)
        {
            var columns = Mathf.CeilToInt(rect.width / _pixelsPerTile) + 2;
            var rows = Mathf.CeilToInt(rect.height / _pixelsPerTile) + 2;
            var minX = Mathf.FloorToInt(_centerX - columns * 0.5f);
            var minY = Mathf.FloorToInt(_centerY - rows * 0.5f);
            return new TileBounds(minX, minY, minX + columns, minY + rows);
        }

        private Rect TileRect(int x, int y, TileBounds bounds)
            => new Rect((x - bounds.MinX) * _pixelsPerTile, (y - bounds.MinY) * _pixelsPerTile, _pixelsPerTile, _pixelsPerTile);
        private Vector3 TileCenter(GridCoord tile, TileBounds bounds)
            => new Vector3((tile.X - bounds.MinX + 0.5f) * _pixelsPerTile, (tile.Y - bounds.MinY + 0.5f) * _pixelsPerTile, 0);

        private static Color GroundColor(ContentId id)
        {
            if (id.IsEmpty || id.Value == "ground.unpainted") return new Color(0.16f, 0.16f, 0.17f);
            unchecked
            {
                var hue = ((id.GetHashCode() & 0x7fffffff) % 1000) / 1000f;
                return Color.HSVToRGB(hue, 0.33f, 0.56f);
            }
        }

        private readonly struct TileBounds
        {
            public TileBounds(int minX, int minY, int maxX, int maxY)
            { MinX = minX; MinY = minY; MaxX = maxX; MaxY = maxY; }
            public int MinX { get; }
            public int MinY { get; }
            public int MaxX { get; }
            public int MaxY { get; }
        }
    }
}
