using System;
using System.Collections.Generic;
using System.IO;
using MassRPG.Core.Content;
using MassRPG.Core.World;
using MassRPG.Data.World;
using MassRPG.Data.World.Semantics;
using MassRPG.EditorCore.World;
using UnityEditor;
using UnityEngine;

namespace MassRPG.Editor.World
{
    /// <summary>
    /// Tactile semantic-road authoring surface. Roads remain polylines plus width/routing metadata;
    /// the editor does not bake them into thousands of independent scene objects or fast-travel links.
    /// </summary>
    public sealed class MassRPGRoadEditorWindow : EditorWindow
    {
        private const float InspectorWidth = 280f;
        private readonly List<RoadDefinition> _savedRoads = new List<RoadDefinition>();
        private readonly List<string> _savedLabels = new List<string>();
        private readonly HashSet<WorldPageKey> _diskChecked = new HashSet<WorldPageKey>();
        private AuthoredWorldPageStore _store;
        private RoadAuthoringDraft _draft;
        private int _savedIndex = -1;
        private string _idText = "road.new";
        private string _nameText = "New Road";
        private string _surfaceGround = "ground.road.dirt";
        private int _widthTiles = 3;
        private bool _visibleOnMap = true;
        private bool _reduceAggressiveSpawns = true;
        private double _movementMultiplier = 1.0;
        private double _routeWeight = 0.92;
        private int _plane = WorldConstants.SurfacePlane;
        private float _centerX = WorldConstants.WorldWidthTiles * 0.5f;
        private float _centerY = WorldConstants.WorldHeightTiles * 0.5f;
        private float _pixelsPerTile = 12f;
        private string _status = "Left-click to add road control points. Shift+click removes the nearest point.";

        [MenuItem("MassRPG/Road Editor")]
        public static void Open()
        {
            var window = GetWindow<MassRPGRoadEditorWindow>();
            window.titleContent = new GUIContent("MassRPG Roads");
            window.minSize = new Vector2(950, 620);
            window.Show();
        }

        public static void OpenAt(int x, int y, int plane = WorldConstants.SurfacePlane)
        {
            var window = GetWindow<MassRPGRoadEditorWindow>();
            window.titleContent = new GUIContent("MassRPG Roads");
            window.minSize = new Vector2(950, 620);
            window._centerX = Mathf.Clamp(x, 0, WorldConstants.WorldWidthTiles - 1);
            window._centerY = Mathf.Clamp(y, 0, WorldConstants.WorldHeightTiles - 1);
            window._plane = plane;
            window.Show();
            window.Focus();
            window.Repaint();
        }

        private void OnEnable()
        {
            _store = new AuthoredWorldPageStore(new ContentId("ground.unpainted"));
            ReloadRoadList();
            NewDraft();
        }

        private void OnGUI()
        {
            DrawTopToolbar();
            var canvas = new Rect(0f, 21f, Mathf.Max(100f, position.width - InspectorWidth), Mathf.Max(100f, position.height - 43f));
            var inspector = new Rect(canvas.xMax, 21f, InspectorWidth, position.height - 21f);
            EnsureVisiblePagesLoaded(canvas);
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
                if (x != Mathf.RoundToInt(_centerX) || y != Mathf.RoundToInt(_centerY))
                {
                    _centerX = Mathf.Clamp(x, 0, WorldConstants.WorldWidthTiles - 1);
                    _centerY = Mathf.Clamp(y, 0, WorldConstants.WorldHeightTiles - 1);
                }
                if (plane != _plane)
                {
                    _plane = plane;
                    _diskChecked.Clear();
                    _store = new AuthoredWorldPageStore(new ContentId("ground.unpainted"));
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
            GUILayout.Label("Road", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = _savedRoads.Count > 0;
                var labels = _savedLabels.Count > 0 ? _savedLabels.ToArray() : new[] { "No saved roads" };
                var next = EditorGUILayout.Popup(Mathf.Max(0, _savedIndex), labels);
                GUI.enabled = true;
                if (_savedRoads.Count > 0 && next != _savedIndex)
                {
                    _savedIndex = next;
                    LoadRoad(_savedRoads[_savedIndex]);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("New")) NewDraft();
                if (GUILayout.Button("Reload")) ReloadRoadList();
            }

            EditorGUILayout.Space(4);
            _idText = EditorGUILayout.TextField("Stable ID", _idText);
            _nameText = EditorGUILayout.TextField("Name", _nameText);
            _widthTiles = EditorGUILayout.IntSlider("Width (tiles)", _widthTiles, 1, 32);
            _surfaceGround = EditorGUILayout.TextField("Surface ground", _surfaceGround);
            _visibleOnMap = EditorGUILayout.Toggle("Visible on map", _visibleOnMap);
            _reduceAggressiveSpawns = EditorGUILayout.Toggle("Fewer hostile spawns", _reduceAggressiveSpawns);
            _movementMultiplier = EditorGUILayout.Slider("Move multiplier", (float)_movementMultiplier, 1f, 1.25f);
            _routeWeight = EditorGUILayout.Slider("Route preference", (float)_routeWeight, 0.5f, 1.25f);

            EditorGUILayout.Space(8);
            GUILayout.Label($"Control points: {(_draft != null ? _draft.Points.Count : 0)}", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Left click adds a control point. Shift+click removes the nearest point. Middle mouse or Alt+drag pans; mouse wheel zooms. Roads guide routing and world presentation; they are not fast travel.",
                MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = _draft != null && _draft.Points.Count > 0;
                if (GUILayout.Button("Undo Point"))
                {
                    _draft.RemovePoint(_draft.Points.Count - 1);
                    Repaint();
                }
                if (GUILayout.Button("Clear Points"))
                {
                    _draft.ClearPoints();
                    Repaint();
                }
                GUI.enabled = true;
            }

            GUI.enabled = _draft != null && _draft.Points.Count >= 2;
            if (GUILayout.Button("Save Road", GUILayout.Height(28))) SaveDraft();
            GUI.enabled = _savedIndex >= 0 && _savedIndex < _savedRoads.Count;
            if (GUILayout.Button("Delete Saved Road")) DeleteSelected();
            GUI.enabled = true;

            GUILayout.FlexibleSpace();
            GUILayout.Label("Semantic source", EditorStyles.miniBoldLabel);
            GUILayout.Label("WorldData/Semantics/Roads", EditorStyles.miniLabel);
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
            DrawSavedRoads(bounds);
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
                    var location = new GridLocation(new GridCoord(x, y), _plane, 0);
                    if (!_store.TryGetCell(location, out var cell)) continue;
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
            Handles.color = new Color(1f, 1f, 1f, 0.08f);
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

        private void DrawSavedRoads(TileBounds bounds)
        {
            Handles.BeginGUI();
            var old = Handles.color;
            for (var i = 0; i < _savedRoads.Count; i++)
            {
                var road = _savedRoads[i];
                if (road.Plane != _plane) continue;
                Handles.color = i == _savedIndex ? new Color(0.95f, 0.75f, 0.25f, 0.7f) : new Color(0.62f, 0.57f, 0.45f, 0.45f);
                DrawRoadPolyline(road.Points, road.WidthTiles, bounds);
            }
            Handles.color = old;
            Handles.EndGUI();
        }

        private void DrawDraft(TileBounds bounds)
        {
            if (_draft == null || _draft.Points.Count == 0) return;
            Handles.BeginGUI();
            var old = Handles.color;
            Handles.color = new Color(1f, 0.82f, 0.22f, 0.95f);
            if (_draft.Points.Count >= 2) DrawRoadPolyline(_draft.Points, _widthTiles, bounds);
            for (var i = 0; i < _draft.Points.Count; i++)
            {
                var p = TileCenter(_draft.Points[i], bounds);
                Handles.DrawSolidDisc(p, Vector3.forward, Mathf.Clamp(_pixelsPerTile * 0.18f, 2f, 7f));
                if (_pixelsPerTile >= 12f)
                    GUI.Label(new Rect(p.x + 5, p.y - 9, 38, 18), i.ToString(), EditorStyles.miniLabel);
            }
            Handles.color = old;
            Handles.EndGUI();
        }

        private void DrawRoadPolyline(IReadOnlyList<GridCoord> points, int widthTiles, TileBounds bounds)
        {
            if (points.Count < 2) return;
            var vectors = new Vector3[points.Count];
            for (var i = 0; i < points.Count; i++) vectors[i] = TileCenter(points[i], bounds);
            Handles.DrawAAPolyLine(Mathf.Max(2f, widthTiles * _pixelsPerTile), vectors);
        }

        private void HandleCanvasInput(Rect canvas)
        {
            var e = Event.current;
            if (!canvas.Contains(e.mousePosition)) return;
            var local = e.mousePosition - canvas.position;

            if (e.type == EventType.ScrollWheel)
            {
                _pixelsPerTile = Mathf.Clamp(_pixelsPerTile - e.delta.y * 1.25f, 2f, 40f);
                e.Use();
                Repaint();
                return;
            }
            if ((e.button == 2 || (e.button == 0 && e.alt)) && e.type == EventType.MouseDrag)
            {
                _centerX = Mathf.Clamp(_centerX - e.delta.x / _pixelsPerTile, 0, WorldConstants.WorldWidthTiles - 1);
                _centerY = Mathf.Clamp(_centerY - e.delta.y / _pixelsPerTile, 0, WorldConstants.WorldHeightTiles - 1);
                e.Use();
                Repaint();
                return;
            }
            if (e.button != 0 || e.alt || e.type != EventType.MouseDown) return;

            var bounds = VisibleBounds(new Rect(0, 0, canvas.width, canvas.height));
            var tile = new GridCoord(
                bounds.MinX + Mathf.FloorToInt(local.x / _pixelsPerTile),
                bounds.MinY + Mathf.FloorToInt(local.y / _pixelsPerTile));
            if (!WorldConstants.IsInsideWorld(tile)) return;

            if (_draft == null) NewDraft();
            if (e.shift && _draft.Points.Count > 0)
            {
                var nearest = NearestPointIndex(tile);
                _draft.RemovePoint(nearest);
                _status = "Removed road control point.";
            }
            else
            {
                SyncDraftMetadata();
                _draft.AddPoint(tile);
                _status = $"Added road point {_draft.Points.Count - 1} at {tile.X}, {tile.Y}.";
            }
            e.Use();
            Repaint();
        }

        private int NearestPointIndex(GridCoord tile)
        {
            var nearest = 0;
            var best = long.MaxValue;
            for (var i = 0; i < _draft.Points.Count; i++)
            {
                var dx = (long)_draft.Points[i].X - tile.X;
                var dy = (long)_draft.Points[i].Y - tile.Y;
                var distance = dx * dx + dy * dy;
                if (distance >= best) continue;
                best = distance;
                nearest = i;
            }
            return nearest;
        }

        private void NewDraft()
        {
            _savedIndex = -1;
            if (!ContentId.TryCreate(_idText, out var id)) id = new ContentId("road.new");
            _draft = new RoadAuthoringDraft(id, _nameText, _plane);
            _status = "New road draft. Left-click to add control points.";
        }

        private void SyncDraftMetadata()
        {
            if (_draft == null) return;
            _draft.DisplayName = _nameText;
            _draft.WidthTiles = _widthTiles;
            _draft.SurfaceGroundId = ContentId.TryCreate(_surfaceGround, out var surface) ? surface : (ContentId?)null;
            _draft.VisibleOnPlayerMap = _visibleOnMap;
            _draft.ReduceAggressiveSpawns = _reduceAggressiveSpawns;
            _draft.MovementSpeedMultiplier = _movementMultiplier;
            _draft.RoutePreferenceWeight = _routeWeight;
        }

        private void SaveDraft()
        {
            if (!ContentId.TryCreate(_idText, out var id))
            {
                _status = "Invalid road ID. Use lower-case stable ContentId characters.";
                return;
            }
            if (_draft == null || _draft.Points.Count < 2)
            {
                _status = "A road needs at least two control points.";
                return;
            }

            // Stable ID and plane are immutable on a draft; recreate around the edited metadata when needed.
            if (_draft.Id != id || _draft.Plane != _plane)
            {
                var replacement = new RoadAuthoringDraft(id, _nameText, _plane);
                for (var i = 0; i < _draft.Points.Count; i++) replacement.AddPoint(_draft.Points[i]);
                _draft = replacement;
            }
            SyncDraftMetadata();
            var road = _draft.Build();
            RoadJsonPersistence.Save(road);
            _status = $"Saved {road.DisplayName} ({road.Points.Count} control points).";
            ReloadRoadList(road.Id);
        }

        private void DeleteSelected()
        {
            if (_savedIndex < 0 || _savedIndex >= _savedRoads.Count) return;
            var road = _savedRoads[_savedIndex];
            if (!EditorUtility.DisplayDialog("Delete Road", $"Delete '{road.DisplayName}'?", "Delete", "Cancel")) return;
            RoadJsonPersistence.Delete(road.Id);
            _status = $"Deleted {road.DisplayName}.";
            ReloadRoadList();
            NewDraft();
        }

        private void ReloadRoadList(ContentId? select = null)
        {
            _savedRoads.Clear();
            _savedLabels.Clear();
            _savedIndex = -1;
            foreach (var file in RoadJsonPersistence.EnumerateFiles())
            {
                if (!RoadJsonPersistence.TryLoad(file, out var road)) continue;
                _savedRoads.Add(road);
            }
            _savedRoads.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
            for (var i = 0; i < _savedRoads.Count; i++)
            {
                _savedLabels.Add(string.IsNullOrWhiteSpace(_savedRoads[i].DisplayName) ? _savedRoads[i].Id.Value : _savedRoads[i].DisplayName);
                if (select.HasValue && _savedRoads[i].Id == select.Value) _savedIndex = i;
            }
            if (_savedIndex >= 0) LoadRoad(_savedRoads[_savedIndex]);
            Repaint();
        }

        private void LoadRoad(RoadDefinition road)
        {
            _draft = RoadAuthoringDraft.FromDefinition(road);
            _idText = road.Id.Value;
            _nameText = road.DisplayName;
            _surfaceGround = road.SurfaceGroundId.HasValue ? road.SurfaceGroundId.Value.Value : string.Empty;
            _widthTiles = road.WidthTiles;
            _visibleOnMap = road.VisibleOnPlayerMap;
            _reduceAggressiveSpawns = road.ReduceAggressiveSpawns;
            _movementMultiplier = road.MovementSpeedMultiplier;
            _routeWeight = road.RoutePreferenceWeight;
            _plane = road.Plane;
            if (road.Points.Count > 0)
            {
                _centerX = road.Points[0].X;
                _centerY = road.Points[0].Y;
            }
            _status = $"Loaded {road.DisplayName}.";
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
                    catch (Exception ex) { Debug.LogError($"Failed to load world page {key} behind road editor: {ex}"); }
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
                var hash = id.GetHashCode();
                var hue = ((hash & 0x7fffffff) % 1000) / 1000f;
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
