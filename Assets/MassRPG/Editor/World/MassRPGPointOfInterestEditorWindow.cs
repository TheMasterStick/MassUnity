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
    /// POI authoring keeps the visible/map footprint separate from the protection/no-build footprint.
    /// A small shrine or dungeon marker can therefore reserve a larger or differently shaped area.
    /// </summary>
    public sealed class MassRPGPointOfInterestEditorWindow : EditorWindow
    {
        private enum EditTarget { Center, VisibleFootprint, ProtectionFootprint }
        private enum FootprintMode { None, Circle, Polygon }

        private const float InspectorWidth = 320f;
        private readonly List<PointOfInterestDefinition> _saved = new List<PointOfInterestDefinition>();
        private readonly List<string> _labels = new List<string>();
        private readonly List<GridCoord> _visiblePolygon = new List<GridCoord>();
        private readonly List<GridCoord> _protectionPolygon = new List<GridCoord>();
        private readonly HashSet<WorldPageKey> _diskChecked = new HashSet<WorldPageKey>();
        private AuthoredWorldPageStore _store;
        private int _selected = -1;
        private string _idText = "poi.new";
        private string _nameText = "New POI";
        private PointOfInterestKind _kind = PointOfInterestKind.Landmark;
        private MapMarkerCategory _marker = MapMarkerCategory.Landmark;
        private PlayerMapVisibility _visibility = PlayerMapVisibility.Public;
        private GridLocation _center = new GridLocation(new GridCoord(90000, 90000), WorldConstants.SurfacePlane, 0);
        private FootprintMode _visibleMode = FootprintMode.Circle;
        private FootprintMode _protectionMode = FootprintMode.Circle;
        private int _visibleRadius = 8;
        private int _protectionRadius = 16;
        private EditTarget _editTarget = EditTarget.Center;
        private string _serviceTags = string.Empty;
        private float _centerX = WorldConstants.WorldWidthTiles * 0.5f;
        private float _centerY = WorldConstants.WorldHeightTiles * 0.5f;
        private float _pixelsPerTile = 10f;
        private string _status = "Choose Center, Visible Footprint or Protection Footprint, then click the map.";

        [MenuItem("MassRPG/POI Editor")]
        public static void Open()
        {
            var window = GetWindow<MassRPGPointOfInterestEditorWindow>();
            window.titleContent = new GUIContent("MassRPG POIs");
            window.minSize = new Vector2(1000, 660);
            window.Show();
        }

        private void OnEnable()
        {
            ResetStore();
            ReloadList();
            NewPoi();
        }

        private void ResetStore()
        {
            _store = new AuthoredWorldPageStore(new ContentId("ground.unpainted"));
            _diskChecked.Clear();
        }

        private void OnGUI()
        {
            DrawTopToolbar();
            var canvas = new Rect(0, 21, Mathf.Max(100, position.width - InspectorWidth), Mathf.Max(100, position.height - 43));
            var inspector = new Rect(canvas.xMax, 21, InspectorWidth, position.height - 21);
            EnsureVisiblePagesLoaded(canvas);
            DrawCanvas(canvas);
            DrawInspector(inspector);
            DrawStatus(new Rect(0, canvas.yMax, canvas.width, 22));
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
                var plane = EditorGUILayout.IntField(_center.Plane, EditorStyles.toolbarTextField, GUILayout.Width(38));
                GUILayout.Label("Floor", GUILayout.Width(32));
                var storey = Mathf.Max(0, EditorGUILayout.IntField(_center.Storey, EditorStyles.toolbarTextField, GUILayout.Width(34)));
                if (x != Mathf.RoundToInt(_centerX) || y != Mathf.RoundToInt(_centerY))
                {
                    _centerX = Mathf.Clamp(x, 0, WorldConstants.WorldWidthTiles - 1);
                    _centerY = Mathf.Clamp(y, 0, WorldConstants.WorldHeightTiles - 1);
                }
                if (plane != _center.Plane || storey != _center.Storey)
                {
                    _center = new GridLocation(_center.Tile, plane, storey);
                    ResetStore();
                }
                GUILayout.Space(8);
                if (GUILayout.Button("Overview", EditorStyles.toolbarButton, GUILayout.Width(65))) MassRPGWorldOverviewWindow.Open();
                if (GUILayout.Button("1x1 Terrain", EditorStyles.toolbarButton, GUILayout.Width(72)))
                    MassRPGWorldEditorWindow.OpenAt(Mathf.RoundToInt(_centerX), Mathf.RoundToInt(_centerY), _center.Plane, _center.Storey);
                GUILayout.FlexibleSpace();
                GUILayout.Label($"{_pixelsPerTile:0}px/tile", EditorStyles.miniLabel);
            }
        }

        private void DrawInspector(Rect rect)
        {
            GUILayout.BeginArea(rect, EditorStyles.helpBox);
            GUILayout.Label("Point of Interest", EditorStyles.boldLabel);
            GUI.enabled = _saved.Count > 0;
            var labels = _labels.Count > 0 ? _labels.ToArray() : new[] { "No saved POIs" };
            var next = EditorGUILayout.Popup(Mathf.Max(0, _selected), labels);
            GUI.enabled = true;
            if (_saved.Count > 0 && next != _selected)
            {
                _selected = next;
                LoadPoi(_saved[_selected]);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("New")) NewPoi();
                if (GUILayout.Button("Reload")) ReloadList();
            }

            _idText = EditorGUILayout.TextField("Stable ID", _idText);
            _nameText = EditorGUILayout.TextField("Name", _nameText);
            _kind = (PointOfInterestKind)EditorGUILayout.EnumPopup("Kind", _kind);
            _marker = (MapMarkerCategory)EditorGUILayout.EnumPopup("Map category", _marker);
            _visibility = (PlayerMapVisibility)EditorGUILayout.EnumPopup("Map visibility", _visibility);
            EditorGUILayout.LabelField("Centre", $"{_center.Tile.X}, {_center.Tile.Y}   plane {_center.Plane} floor {_center.Storey}");

            EditorGUILayout.Space(6);
            GUILayout.Label("Map interaction", EditorStyles.miniBoldLabel);
            _editTarget = (EditTarget)GUILayout.Toolbar((int)_editTarget, new[] { "Centre", "Visible", "Protection" });

            EditorGUILayout.Space(5);
            _visibleMode = (FootprintMode)EditorGUILayout.EnumPopup("Visible footprint", _visibleMode);
            if (_visibleMode == FootprintMode.Circle) _visibleRadius = Mathf.Max(0, EditorGUILayout.IntField("Visible radius", _visibleRadius));
            else if (_visibleMode == FootprintMode.Polygon) DrawPolygonButtons(_visiblePolygon, "visible");

            _protectionMode = (FootprintMode)EditorGUILayout.EnumPopup("Protection footprint", _protectionMode);
            if (_protectionMode == FootprintMode.Circle) _protectionRadius = Mathf.Max(0, EditorGUILayout.IntField("Protection radius", _protectionRadius));
            else if (_protectionMode == FootprintMode.Polygon) DrawPolygonButtons(_protectionPolygon, "protection");

            EditorGUILayout.Space(5);
            _serviceTags = EditorGUILayout.TextField("Service tags", _serviceTags);
            EditorGUILayout.HelpBox("Comma-separated stable IDs, e.g. service.bank, service.inn, service.blacksmith. Public service filters can use these without marking every building on the map.", MessageType.None);

            GUI.enabled = FootprintsValid();
            if (GUILayout.Button("Save POI", GUILayout.Height(28))) SavePoi();
            GUI.enabled = _selected >= 0 && _selected < _saved.Count;
            if (GUILayout.Button("Delete Saved POI")) DeleteSelected();
            GUI.enabled = true;

            EditorGUILayout.Space(7);
            EditorGUILayout.HelpBox("Visible and protected footprints are deliberately separate. POI protection can be a quick radius or a hand-drawn polygon and does not need to match the icon/visible footprint.", MessageType.Info);
            GUILayout.FlexibleSpace();
            GUILayout.Label("WorldData/Semantics/PointsOfInterest", EditorStyles.miniLabel);
            GUILayout.EndArea();
        }

        private void DrawPolygonButtons(List<GridCoord> polygon, string label)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"{label} vertices", polygon.Count.ToString());
                GUI.enabled = polygon.Count > 0;
                if (GUILayout.Button("Undo", GUILayout.Width(45))) polygon.RemoveAt(polygon.Count - 1);
                if (GUILayout.Button("Clear", GUILayout.Width(45))) polygon.Clear();
                GUI.enabled = true;
            }
        }

        private void DrawCanvas(Rect canvas)
        {
            EditorGUI.DrawRect(canvas, new Color(0.105f, 0.11f, 0.12f));
            GUI.BeginGroup(canvas);
            var local = new Rect(0, 0, canvas.width, canvas.height);
            var bounds = VisibleBounds(local);
            DrawTerrain(bounds);
            DrawGrid(local, bounds);
            DrawSavedPois(bounds);
            DrawCurrentPoi(bounds);
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
                    if (!_store.TryGetCell(new GridLocation(new GridCoord(x, y), _center.Plane, _center.Storey), out var cell)) continue;
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

        private void DrawSavedPois(TileBounds bounds)
        {
            Handles.BeginGUI();
            var old = Handles.color;
            for (var i = 0; i < _saved.Count; i++)
            {
                var poi = _saved[i];
                if (poi.Center.Plane != _center.Plane || poi.Center.Storey != _center.Storey) continue;
                Handles.color = i == _selected ? new Color(1f, 0.75f, 0.2f, 0.9f) : new Color(0.85f, 0.85f, 0.9f, 0.55f);
                var p = TileCenter(poi.Center.Tile, bounds);
                Handles.DrawSolidDisc(p, Vector3.forward, Mathf.Clamp(_pixelsPerTile * 0.22f, 2f, 7f));
            }
            Handles.color = old;
            Handles.EndGUI();
        }

        private void DrawCurrentPoi(TileBounds bounds)
        {
            Handles.BeginGUI();
            var old = Handles.color;
            Handles.color = new Color(0.25f, 0.9f, 1f, 0.95f);
            DrawFootprint(_visibleMode, _visibleRadius, _visiblePolygon, bounds);
            Handles.color = new Color(1f, 0.35f, 0.2f, 0.9f);
            DrawFootprint(_protectionMode, _protectionRadius, _protectionPolygon, bounds);
            Handles.color = Color.white;
            var p = TileCenter(_center.Tile, bounds);
            Handles.DrawSolidDisc(p, Vector3.forward, Mathf.Clamp(_pixelsPerTile * 0.24f, 3f, 8f));
            Handles.color = old;
            Handles.EndGUI();
        }

        private void DrawFootprint(FootprintMode mode, int radius, List<GridCoord> polygon, TileBounds bounds)
        {
            if (mode == FootprintMode.None) return;
            if (mode == FootprintMode.Circle)
            {
                Handles.DrawWireDisc(TileCenter(_center.Tile, bounds), Vector3.forward, radius * _pixelsPerTile);
                return;
            }
            if (polygon.Count < 2) return;
            var points = new Vector3[polygon.Count + (polygon.Count >= 3 ? 1 : 0)];
            for (var i = 0; i < polygon.Count; i++) points[i] = TileCenter(polygon[i], bounds);
            if (polygon.Count >= 3) points[points.Length - 1] = points[0];
            Handles.DrawAAPolyLine(3f, points);
        }

        private void HandleCanvasInput(Rect canvas)
        {
            var e = Event.current;
            if (!canvas.Contains(e.mousePosition)) return;
            var local = e.mousePosition - canvas.position;
            if (e.type == EventType.ScrollWheel)
            {
                _pixelsPerTile = Mathf.Clamp(_pixelsPerTile - e.delta.y, 1.5f, 40f);
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
            var tile = new GridCoord(bounds.MinX + Mathf.FloorToInt(local.x / _pixelsPerTile), bounds.MinY + Mathf.FloorToInt(local.y / _pixelsPerTile));
            if (!WorldConstants.IsInsideWorld(tile)) return;

            if (_editTarget == EditTarget.Center)
            {
                _center = new GridLocation(tile, _center.Plane, _center.Storey);
                _status = $"POI centre set to {tile.X}, {tile.Y}.";
            }
            else
            {
                var mode = _editTarget == EditTarget.VisibleFootprint ? _visibleMode : _protectionMode;
                var polygon = _editTarget == EditTarget.VisibleFootprint ? _visiblePolygon : _protectionPolygon;
                if (mode == FootprintMode.Circle)
                {
                    var dx = (long)tile.X - _center.Tile.X;
                    var dy = (long)tile.Y - _center.Tile.Y;
                    var radius = (int)Math.Round(Math.Sqrt(dx * dx + dy * dy));
                    if (_editTarget == EditTarget.VisibleFootprint) _visibleRadius = radius;
                    else _protectionRadius = radius;
                    _status = $"{_editTarget} radius set to {radius} tiles.";
                }
                else if (mode == FootprintMode.Polygon)
                {
                    if (e.shift && polygon.Count > 0) polygon.RemoveAt(NearestVertex(polygon, tile));
                    else if (polygon.Count == 0 || polygon[polygon.Count - 1] != tile) polygon.Add(tile);
                    _status = $"{_editTarget} polygon now has {polygon.Count} vertices.";
                }
            }
            e.Use(); Repaint();
        }

        private static int NearestVertex(List<GridCoord> polygon, GridCoord tile)
        {
            var nearest = 0;
            var best = long.MaxValue;
            for (var i = 0; i < polygon.Count; i++)
            {
                var dx = (long)polygon[i].X - tile.X;
                var dy = (long)polygon[i].Y - tile.Y;
                var d = dx * dx + dy * dy;
                if (d >= best) continue;
                best = d; nearest = i;
            }
            return nearest;
        }

        private bool FootprintsValid()
            => (_visibleMode != FootprintMode.Polygon || _visiblePolygon.Count >= 3)
            && (_protectionMode != FootprintMode.Polygon || _protectionPolygon.Count >= 3);

        private void SavePoi()
        {
            if (!ContentId.TryCreate(_idText, out var id)) { _status = "Invalid stable POI ID."; return; }
            var poi = new PointOfInterestDefinition(id, _nameText, _kind, _center, _marker, _visibility)
            {
                VisibleFootprint = MakeShape(_visibleMode, _visibleRadius, _visiblePolygon),
                ProtectionFootprint = MakeShape(_protectionMode, _protectionRadius, _protectionPolygon)
            };
            var tags = _serviceTags.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < tags.Length; i++)
            {
                var raw = tags[i].Trim();
                if (raw.Length == 0) continue;
                if (!ContentId.TryCreate(raw, out var tag)) { _status = $"Invalid service tag '{raw}'."; return; }
                poi.AddServiceTag(tag);
            }
            PointOfInterestJsonPersistence.Save(poi);
            _status = $"Saved POI '{poi.DisplayName}'.";
            ReloadList(id);
        }

        private WorldAreaShape MakeShape(FootprintMode mode, int radius, List<GridCoord> polygon)
        {
            if (mode == FootprintMode.None) return null;
            if (mode == FootprintMode.Circle) return new CircleAreaShape(_center.Tile, radius);
            return polygon.Count >= 3 ? new PolygonAreaShape(polygon) : null;
        }

        private void DeleteSelected()
        {
            if (_selected < 0 || _selected >= _saved.Count) return;
            var poi = _saved[_selected];
            if (!EditorUtility.DisplayDialog("Delete POI", $"Delete '{poi.DisplayName}'?", "Delete", "Cancel")) return;
            PointOfInterestJsonPersistence.Delete(poi.Id);
            ReloadList();
            NewPoi();
        }

        private void NewPoi()
        {
            _selected = -1;
            _visiblePolygon.Clear();
            _protectionPolygon.Clear();
            _center = new GridLocation(new GridCoord(Mathf.RoundToInt(_centerX), Mathf.RoundToInt(_centerY)), _center.Plane, _center.Storey);
            _status = "New POI. Click the map to position its centre.";
            Repaint();
        }

        private void ReloadList(ContentId? select = null)
        {
            _saved.Clear(); _labels.Clear(); _selected = -1;
            foreach (var file in PointOfInterestJsonPersistence.EnumerateFiles())
                if (PointOfInterestJsonPersistence.TryLoad(file, out var poi)) _saved.Add(poi);
            _saved.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
            for (var i = 0; i < _saved.Count; i++)
            {
                _labels.Add($"{_saved[i].DisplayName} [{_saved[i].Kind}]");
                if (select.HasValue && _saved[i].Id == select.Value) _selected = i;
            }
            if (_selected >= 0) LoadPoi(_saved[_selected]);
            Repaint();
        }

        private void LoadPoi(PointOfInterestDefinition poi)
        {
            _idText = poi.Id.Value;
            _nameText = poi.DisplayName;
            _kind = poi.Kind;
            _marker = poi.MarkerCategory;
            _visibility = poi.MapVisibility;
            _center = poi.Center;
            _centerX = poi.Center.Tile.X;
            _centerY = poi.Center.Tile.Y;
            _visiblePolygon.Clear(); _protectionPolygon.Clear();
            LoadShape(poi.VisibleFootprint, ref _visibleMode, ref _visibleRadius, _visiblePolygon);
            LoadShape(poi.ProtectionFootprint, ref _protectionMode, ref _protectionRadius, _protectionPolygon);
            var tags = new string[poi.ServiceTags.Count];
            for (var i = 0; i < tags.Length; i++) tags[i] = poi.ServiceTags[i].Value;
            _serviceTags = string.Join(", ", tags);
            _status = $"Loaded {poi.DisplayName}.";
            ResetStore();
        }

        private static void LoadShape(WorldAreaShape shape, ref FootprintMode mode, ref int radius, List<GridCoord> polygon)
        {
            polygon.Clear();
            if (shape == null) { mode = FootprintMode.None; return; }
            if (shape is CircleAreaShape circle) { mode = FootprintMode.Circle; radius = circle.RadiusTiles; return; }
            if (shape is PolygonAreaShape poly)
            {
                mode = FootprintMode.Polygon;
                for (var i = 0; i < poly.Points.Count; i++) polygon.Add(poly.Points[i]);
            }
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
                for (var px = minX / size; px <= maxX / size; px++)
                {
                    var key = new WorldPageKey(new WorldPageCoord(px, py), _center.Plane, _center.Storey);
                    if (!_diskChecked.Add(key)) continue;
                    if (!WorldPageJsonPersistence.TryLoad(key, out var document)) continue;
                    try { _store.ImportPage(WorldPageCodec.Decode(document)); }
                    catch (Exception ex) { Debug.LogError($"Failed to load page {key} behind POI editor: {ex}"); }
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
