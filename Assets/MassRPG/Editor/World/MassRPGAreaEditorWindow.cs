using System;
using System.Collections.Generic;
using MassRPG.Core.Content;
using MassRPG.Core.World;
using MassRPG.Data.World;
using MassRPG.Data.World.Semantics;
using UnityEditor;
using UnityEngine;

namespace MassRPG.Editor.World
{
    /// <summary>
    /// Polygon/circle editor for overlapping semantic layers: named regions, biomes, creature and
    /// resource zones, level bands, faction territory, no-build/PvP/dungeon areas and custom layers.
    /// These areas never partition the storage grid; multiple definitions may overlap freely.
    /// </summary>
    public sealed class MassRPGAreaEditorWindow : EditorWindow
    {
        private const float InspectorWidth = 300f;
        private readonly List<WorldAreaDefinition> _saved = new List<WorldAreaDefinition>();
        private readonly List<string> _labels = new List<string>();
        private readonly List<GridCoord> _polygon = new List<GridCoord>();
        private readonly HashSet<WorldPageKey> _diskChecked = new HashSet<WorldPageKey>();
        private AuthoredWorldPageStore _store;
        private int _selected = -1;
        private string _idText = "region.new";
        private string _nameText = "New Area";
        private WorldAreaKind _kind = WorldAreaKind.NamedRegion;
        private PlayerMapVisibility _visibility = PlayerMapVisibility.Public;
        private WorldAreaShapeKind _shapeKind = WorldAreaShapeKind.Polygon;
        private int _plane = WorldConstants.SurfacePlane;
        private GridCoord _circleCenter = new GridCoord(90000, 90000);
        private int _circleRadius = 50;
        private float _centerX = WorldConstants.WorldWidthTiles * 0.5f;
        private float _centerY = WorldConstants.WorldHeightTiles * 0.5f;
        private float _pixelsPerTile = 9f;
        private string _status = "Polygon: left-click adds vertices, Shift+click removes nearest. Circle: left-click sets centre.";

        [MenuItem("MassRPG/Area Editor")]
        public static void Open()
        {
            var window = GetWindow<MassRPGAreaEditorWindow>();
            window.titleContent = new GUIContent("MassRPG Areas");
            window.minSize = new Vector2(980, 640);
            window.Show();
        }

        private void OnEnable()
        {
            ResetStore();
            ReloadList();
            NewArea();
        }

        private void ResetStore()
        {
            _store = new AuthoredWorldPageStore(new ContentId("ground.unpainted"));
            _diskChecked.Clear();
        }

        private void OnGUI()
        {
            DrawTopToolbar();
            var canvas = new Rect(0f, 21f, Mathf.Max(100f, position.width - InspectorWidth), Mathf.Max(100f, position.height - 43f));
            var inspector = new Rect(canvas.xMax, 21f, InspectorWidth, position.height - 21f);
            EnsureVisiblePagesLoaded(canvas);
            DrawCanvas(canvas);
            DrawInspector(inspector);
            DrawStatus(new Rect(0, canvas.yMax, canvas.width, 22f));
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
                if (x != Mathf.RoundToInt(_centerX) || y != Mathf.RoundToInt(_centerY))
                {
                    _centerX = Mathf.Clamp(x, 0, WorldConstants.WorldWidthTiles - 1);
                    _centerY = Mathf.Clamp(y, 0, WorldConstants.WorldHeightTiles - 1);
                }
                if (plane != _plane)
                {
                    _plane = plane;
                    ResetStore();
                }

                GUILayout.Space(10);
                if (GUILayout.Button("World Overview", EditorStyles.toolbarButton, GUILayout.Width(92))) MassRPGWorldOverviewWindow.Open();
                if (GUILayout.Button("1x1 Terrain", EditorStyles.toolbarButton, GUILayout.Width(72)))
                    MassRPGWorldEditorWindow.OpenAt(Mathf.RoundToInt(_centerX), Mathf.RoundToInt(_centerY), _plane, 0);
                GUILayout.FlexibleSpace();
                GUILayout.Label($"{_pixelsPerTile:0}px/tile", EditorStyles.miniLabel);
            }
        }

        private void DrawInspector(Rect rect)
        {
            GUILayout.BeginArea(rect, EditorStyles.helpBox);
            GUILayout.Label("Semantic Area", EditorStyles.boldLabel);

            GUI.enabled = _saved.Count > 0;
            var labels = _labels.Count > 0 ? _labels.ToArray() : new[] { "No saved areas" };
            var next = EditorGUILayout.Popup(Mathf.Max(0, _selected), labels);
            GUI.enabled = true;
            if (_saved.Count > 0 && next != _selected)
            {
                _selected = next;
                LoadArea(_saved[_selected]);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("New")) NewArea();
                if (GUILayout.Button("Reload")) ReloadList();
            }

            EditorGUILayout.Space(4);
            _idText = EditorGUILayout.TextField("Stable ID", _idText);
            _nameText = EditorGUILayout.TextField("Name", _nameText);
            _kind = (WorldAreaKind)EditorGUILayout.EnumPopup("Kind", _kind);
            _visibility = (PlayerMapVisibility)EditorGUILayout.EnumPopup("Player map", _visibility);
            _shapeKind = (WorldAreaShapeKind)EditorGUILayout.EnumPopup("Shape", _shapeKind);

            EditorGUILayout.Space(5);
            if (_shapeKind == WorldAreaShapeKind.Circle)
            {
                EditorGUILayout.LabelField("Centre", $"{_circleCenter.X}, {_circleCenter.Y}");
                _circleRadius = Mathf.Max(0, EditorGUILayout.IntField("Radius (tiles)", _circleRadius));
                EditorGUILayout.HelpBox("Left-click the map to move the circle centre. Radius is exact logical tiles.", MessageType.None);
            }
            else
            {
                EditorGUILayout.LabelField("Vertices", _polygon.Count.ToString());
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUI.enabled = _polygon.Count > 0;
                    if (GUILayout.Button("Undo Vertex")) { _polygon.RemoveAt(_polygon.Count - 1); Repaint(); }
                    if (GUILayout.Button("Clear")) { _polygon.Clear(); Repaint(); }
                    GUI.enabled = true;
                }
                EditorGUILayout.HelpBox("Left-click adds polygon vertices. Shift+click removes the nearest vertex. Polygon closes automatically on save/preview.", MessageType.None);
            }

            EditorGUILayout.Space(6);
            GUI.enabled = CanSave();
            if (GUILayout.Button("Save Area", GUILayout.Height(28))) SaveArea();
            GUI.enabled = _selected >= 0 && _selected < _saved.Count;
            if (GUILayout.Button("Delete Saved Area")) DeleteSelected();
            GUI.enabled = true;

            EditorGUILayout.Space(8);
            GUILayout.Label("Layer behavior", EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox(
                "Areas overlap freely. A named region can overlap a biome, kingdom, creature level area, spawn zone and no-build zone. Storage-page borders do not affect these shapes.",
                MessageType.Info);
            GUILayout.FlexibleSpace();
            GUILayout.Label("WorldData/Semantics/Areas", EditorStyles.miniLabel);
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
            DrawSavedAreas(bounds);
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
                    if (!_store.TryGetCell(new GridLocation(new GridCoord(x, y), _plane, 0), out var cell)) continue;
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

        private void DrawSavedAreas(TileBounds bounds)
        {
            Handles.BeginGUI();
            var old = Handles.color;
            for (var i = 0; i < _saved.Count; i++)
            {
                var area = _saved[i];
                if (area.Plane != _plane) continue;
                Handles.color = i == _selected ? new Color(0.95f, 0.65f, 0.22f, 0.75f) : AreaColor(area.Kind, 0.42f);
                DrawShape(area.Shape, bounds, false);
            }
            Handles.color = old;
            Handles.EndGUI();
        }

        private void DrawDraft(TileBounds bounds)
        {
            Handles.BeginGUI();
            var old = Handles.color;
            Handles.color = AreaColor(_kind, 0.95f);
            if (_shapeKind == WorldAreaShapeKind.Circle)
            {
                DrawCircle(_circleCenter, _circleRadius, bounds);
            }
            else if (_polygon.Count > 0)
            {
                var vectors = new List<Vector3>(_polygon.Count + 1);
                for (var i = 0; i < _polygon.Count; i++) vectors.Add(TileCenter(_polygon[i], bounds));
                if (_polygon.Count >= 3) vectors.Add(vectors[0]);
                if (vectors.Count >= 2) Handles.DrawAAPolyLine(3f, vectors.ToArray());
                for (var i = 0; i < _polygon.Count; i++)
                    Handles.DrawSolidDisc(TileCenter(_polygon[i], bounds), Vector3.forward, Mathf.Clamp(_pixelsPerTile * 0.16f, 2f, 6f));
            }
            Handles.color = old;
            Handles.EndGUI();
        }

        private void DrawShape(WorldAreaShape shape, TileBounds bounds, bool fill)
        {
            if (shape is CircleAreaShape circle)
            {
                DrawCircle(circle.Center, circle.RadiusTiles, bounds);
                return;
            }
            if (!(shape is PolygonAreaShape polygon) || polygon.Points.Count < 2) return;
            var vectors = new Vector3[polygon.Points.Count + 1];
            for (var i = 0; i < polygon.Points.Count; i++) vectors[i] = TileCenter(polygon.Points[i], bounds);
            vectors[vectors.Length - 1] = vectors[0];
            Handles.DrawAAPolyLine(2f, vectors);
        }

        private void DrawCircle(GridCoord center, int radius, TileBounds bounds)
        {
            var p = TileCenter(center, bounds);
            Handles.DrawWireDisc(p, Vector3.forward, radius * _pixelsPerTile);
            Handles.DrawSolidDisc(p, Vector3.forward, Mathf.Clamp(_pixelsPerTile * 0.14f, 2f, 5f));
        }

        private void HandleCanvasInput(Rect canvas)
        {
            var e = Event.current;
            if (!canvas.Contains(e.mousePosition)) return;
            var local = e.mousePosition - canvas.position;
            if (e.type == EventType.ScrollWheel)
            {
                _pixelsPerTile = Mathf.Clamp(_pixelsPerTile - e.delta.y, 1.5f, 36f);
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

            if (_shapeKind == WorldAreaShapeKind.Circle)
            {
                _circleCenter = tile;
                _status = $"Circle centre set to {tile.X}, {tile.Y}.";
            }
            else if (e.shift && _polygon.Count > 0)
            {
                _polygon.RemoveAt(NearestVertex(tile));
                _status = "Removed nearest polygon vertex.";
            }
            else
            {
                if (_polygon.Count == 0 || _polygon[_polygon.Count - 1] != tile) _polygon.Add(tile);
                _status = $"Added vertex {_polygon.Count - 1} at {tile.X}, {tile.Y}.";
            }
            e.Use(); Repaint();
        }

        private int NearestVertex(GridCoord tile)
        {
            var nearest = 0;
            var best = long.MaxValue;
            for (var i = 0; i < _polygon.Count; i++)
            {
                var dx = (long)_polygon[i].X - tile.X;
                var dy = (long)_polygon[i].Y - tile.Y;
                var d = dx * dx + dy * dy;
                if (d >= best) continue;
                best = d;
                nearest = i;
            }
            return nearest;
        }

        private bool CanSave() => _shapeKind == WorldAreaShapeKind.Circle || _polygon.Count >= 3;

        private void SaveArea()
        {
            if (!ContentId.TryCreate(_idText, out var id))
            {
                _status = "Invalid stable area ID.";
                return;
            }
            WorldAreaShape shape;
            if (_shapeKind == WorldAreaShapeKind.Circle) shape = new CircleAreaShape(_circleCenter, _circleRadius);
            else if (_polygon.Count >= 3) shape = new PolygonAreaShape(_polygon);
            else { _status = "Polygon requires at least three vertices."; return; }

            var area = new WorldAreaDefinition(id, _nameText, _kind, shape, _visibility, _plane);
            WorldAreaJsonPersistence.Save(area);
            _status = $"Saved {_kind} '{_nameText}'.";
            ReloadList(id);
        }

        private void DeleteSelected()
        {
            if (_selected < 0 || _selected >= _saved.Count) return;
            var area = _saved[_selected];
            if (!EditorUtility.DisplayDialog("Delete Area", $"Delete '{area.DisplayName}'?", "Delete", "Cancel")) return;
            WorldAreaJsonPersistence.Delete(area.Id);
            ReloadList();
            NewArea();
        }

        private void NewArea()
        {
            _selected = -1;
            _polygon.Clear();
            _circleCenter = new GridCoord(Mathf.RoundToInt(_centerX), Mathf.RoundToInt(_centerY));
            _status = "New area. Add a polygon or set a circle centre.";
            Repaint();
        }

        private void ReloadList(ContentId? select = null)
        {
            _saved.Clear();
            _labels.Clear();
            _selected = -1;
            foreach (var file in WorldAreaJsonPersistence.EnumerateFiles())
                if (WorldAreaJsonPersistence.TryLoad(file, out var area)) _saved.Add(area);
            _saved.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
            for (var i = 0; i < _saved.Count; i++)
            {
                _labels.Add($"{_saved[i].DisplayName} [{_saved[i].Kind}]");
                if (select.HasValue && _saved[i].Id == select.Value) _selected = i;
            }
            if (_selected >= 0) LoadArea(_saved[_selected]);
            Repaint();
        }

        private void LoadArea(WorldAreaDefinition area)
        {
            _idText = area.Id.Value;
            _nameText = area.DisplayName;
            _kind = area.Kind;
            _visibility = area.MapVisibility;
            _plane = area.Plane;
            _polygon.Clear();
            if (area.Shape is CircleAreaShape circle)
            {
                _shapeKind = WorldAreaShapeKind.Circle;
                _circleCenter = circle.Center;
                _circleRadius = circle.RadiusTiles;
                _centerX = circle.Center.X;
                _centerY = circle.Center.Y;
            }
            else if (area.Shape is PolygonAreaShape polygon)
            {
                _shapeKind = WorldAreaShapeKind.Polygon;
                for (var i = 0; i < polygon.Points.Count; i++) _polygon.Add(polygon.Points[i]);
                if (_polygon.Count > 0) { _centerX = _polygon[0].X; _centerY = _polygon[0].Y; }
            }
            _status = $"Loaded {area.DisplayName}.";
            ResetStore();
        }

        private void EnsureVisiblePagesLoaded(Rect canvas)
        {
            var bounds = VisibleBounds(new Rect(0, 0, canvas.width, canvas.height));
            var minX = Mathf.Clamp(bounds.MinX, 0, WorldConstants.WorldWidthTiles - 1);
            var minY = Mathf.Clamp(bounds.MinY, 0, WorldConstants.WorldHeightTiles - 1);
            var maxX = Mathf.Clamp(bounds.MaxX, 0, WorldConstants.WorldWidthTiles - 1);
            var maxY = Mathf.Clamp(bounds.MaxY, 0, WorldConstants.WorldHeightTiles - 1);
            var size = _store.PageSize;
            for (var py = minY / size; py <= maxY / size; py++)
            {
                for (var px = minX / size; px <= maxX / size; px++)
                {
                    var key = new WorldPageKey(new WorldPageCoord(px, py), _plane, 0);
                    if (!_diskChecked.Add(key)) continue;
                    if (!WorldPageJsonPersistence.TryLoad(key, out var document)) continue;
                    try { _store.ImportPage(WorldPageCodec.Decode(document)); }
                    catch (Exception ex) { Debug.LogError($"Failed to load page {key} behind area editor: {ex}"); }
                }
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

        private static Color AreaColor(WorldAreaKind kind, float alpha)
        {
            var hue = (((int)kind * 0.137f) + 0.06f) % 1f;
            var c = Color.HSVToRGB(hue, 0.72f, 0.95f);
            c.a = alpha;
            return c;
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
