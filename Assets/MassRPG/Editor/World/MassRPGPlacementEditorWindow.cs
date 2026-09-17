using System;
using System.Collections.Generic;
using MassRPG.Core.Content;
using MassRPG.Core.World;
using MassRPG.Data.World;
using MassRPG.Data.World.Placements;
using UnityEditor;
using UnityEngine;

namespace MassRPG.Editor.World
{
    /// <summary>
    /// Exact anchored placement editor for gameplay objects/resources and decorative doodads.
    /// Placement pages stream independently from terrain pages and save only changed page files.
    /// </summary>
    public sealed class MassRPGPlacementEditorWindow : EditorWindow
    {
        private const float InspectorWidth = 300f;
        private readonly Dictionary<WorldPageKey, List<WorldPlacementRecord>> _placements = new Dictionary<WorldPageKey, List<WorldPlacementRecord>>();
        private readonly HashSet<WorldPageKey> _placementChecked = new HashSet<WorldPageKey>();
        private readonly HashSet<WorldPageKey> _dirtyPlacementPages = new HashSet<WorldPageKey>();
        private readonly HashSet<WorldPageKey> _terrainChecked = new HashSet<WorldPageKey>();
        private AuthoredWorldPageStore _terrain;
        private string _definitionId = "resource.tree.normal";
        private WorldPlacementKind _kind = WorldPlacementKind.Resource;
        private float _offsetX;
        private float _offsetY;
        private float _heightOffset;
        private float _yaw;
        private int _plane = WorldConstants.SurfacePlane;
        private int _storey;
        private float _centerX = WorldConstants.WorldWidthTiles * 0.5f;
        private float _centerY = WorldConstants.WorldHeightTiles * 0.5f;
        private float _pixelsPerTile = 14f;
        private string _status = "Left-click places. Right-click removes the latest placement anchored on that tile. Ctrl+S saves changed placement pages.";

        [MenuItem("MassRPG/Placement Editor")]
        public static void Open()
        {
            var window = GetWindow<MassRPGPlacementEditorWindow>();
            window.titleContent = new GUIContent("MassRPG Placements");
            window.minSize = new Vector2(980, 640);
            window.Show();
        }

        private void OnEnable() => ResetLoadedData();

        private void OnDisable()
        {
            if (_dirtyPlacementPages.Count > 0)
                Debug.LogWarning("MassRPG Placement Editor closed with unsaved placement pages. Reopen the window before discarding the Unity session if those edits matter.");
        }

        private void ResetLoadedData()
        {
            _terrain = new AuthoredWorldPageStore(new ContentId("ground.unpainted"));
            _terrainChecked.Clear();
            _placementChecked.Clear();
            _placements.Clear();
            _dirtyPlacementPages.Clear();
        }

        private void OnGUI()
        {
            HandleShortcuts(Event.current);
            DrawTopToolbar();
            var canvas = new Rect(0, 21, Mathf.Max(100, position.width - InspectorWidth), Mathf.Max(100, position.height - 43));
            var inspector = new Rect(canvas.xMax, 21, InspectorWidth, position.height - 21);
            EnsureVisibleDataLoaded(canvas);
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
                    if (_dirtyPlacementPages.Count > 0)
                    {
                        _status = "Save placement edits before changing plane/floor.";
                    }
                    else
                    {
                        _plane = plane; _storey = storey; ResetLoadedData();
                    }
                }
                GUILayout.Space(8);
                if (GUILayout.Button("Terrain", EditorStyles.toolbarButton, GUILayout.Width(55)))
                    MassRPGWorldEditorWindow.OpenAt(Mathf.RoundToInt(_centerX), Mathf.RoundToInt(_centerY), _plane, _storey);
                if (GUILayout.Button("Overview", EditorStyles.toolbarButton, GUILayout.Width(65))) MassRPGWorldOverviewWindow.Open();
                GUILayout.FlexibleSpace();
                GUI.enabled = _dirtyPlacementPages.Count > 0;
                if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(45))) SaveDirtyPages();
                GUI.enabled = true;
            }
        }

        private void DrawInspector(Rect rect)
        {
            GUILayout.BeginArea(rect, EditorStyles.helpBox);
            GUILayout.Label("Placement Palette", EditorStyles.boldLabel);
            _kind = (WorldPlacementKind)EditorGUILayout.EnumPopup("Kind", _kind);
            _definitionId = EditorGUILayout.TextField("Definition ID", _definitionId);
            EditorGUILayout.Space(5);
            GUILayout.Label("Visual offset inside anchor", EditorStyles.miniBoldLabel);
            _offsetX = EditorGUILayout.Slider("Offset X", _offsetX, -0.49f, 0.49f);
            _offsetY = EditorGUILayout.Slider("Offset Y", _offsetY, -0.49f, 0.49f);
            _heightOffset = EditorGUILayout.FloatField("Height offset", _heightOffset);
            _yaw = EditorGUILayout.Slider("Yaw", _yaw, -180f, 180f);

            EditorGUILayout.Space(7);
            EditorGUILayout.HelpBox(
                "Gameplay objects and resources always retain an exact 1x1 anchor/footprint reference. Their model can lean or offset inside the anchor. Doodads are decorative and may later receive freer placement tools.",
                MessageType.Info);

            if (_kind == WorldPlacementKind.Resource)
                EditorGUILayout.HelpBox("Use this for deliberately placed rare trees, ore veins, fishing/resource anchors and similar nodes. Normal biome trees can still be deterministic rather than saved one-by-one.", MessageType.None);

            if (GUILayout.Button("Reset visual transform"))
            {
                _offsetX = _offsetY = _heightOffset = _yaw = 0f;
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label($"Dirty pages: {_dirtyPlacementPages.Count}", EditorStyles.miniBoldLabel);
            GUILayout.Label("WorldData/Placements", EditorStyles.miniLabel);
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
            DrawPlacements(bounds);
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

        private void DrawPlacements(TileBounds bounds)
        {
            Handles.BeginGUI();
            var old = Handles.color;
            foreach (var pair in _placements)
            {
                if (pair.Key.Plane != _plane || pair.Key.Storey != _storey) continue;
                var list = pair.Value;
                for (var i = 0; i < list.Count; i++)
                {
                    var p = list[i];
                    if (!p.Enabled) continue;
                    if (p.Anchor.Tile.X < bounds.MinX || p.Anchor.Tile.X > bounds.MaxX || p.Anchor.Tile.Y < bounds.MinY || p.Anchor.Tile.Y > bounds.MaxY) continue;
                    Handles.color = PlacementColor(p.Kind);
                    var center = TileCenter(p.Anchor.Tile, bounds);
                    center.x += p.OffsetX * _pixelsPerTile;
                    center.y += p.OffsetY * _pixelsPerTile;
                    var radius = Mathf.Clamp(_pixelsPerTile * (p.Kind == WorldPlacementKind.Doodad ? 0.13f : 0.22f), 2f, 8f);
                    Handles.DrawSolidDisc(center, Vector3.forward, radius);
                    if (_pixelsPerTile >= 18f)
                        GUI.Label(new Rect(center.x + 4, center.y - 9, 110, 18), ShortId(p.DefinitionId.Value), EditorStyles.miniLabel);
                }
            }
            Handles.color = old;
            Handles.EndGUI();
        }

        private void HandleCanvasInput(Rect canvas)
        {
            var e = Event.current;
            if (!canvas.Contains(e.mousePosition)) return;
            var local = e.mousePosition - canvas.position;
            if (e.type == EventType.ScrollWheel)
            {
                _pixelsPerTile = Mathf.Clamp(_pixelsPerTile - e.delta.y * 1.2f, 3f, 42f);
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
            var tile = new GridCoord(bounds.MinX + Mathf.FloorToInt(local.x / _pixelsPerTile), bounds.MinY + Mathf.FloorToInt(local.y / _pixelsPerTile));
            if (!WorldConstants.IsInsideWorld(tile)) return;
            var location = new GridLocation(tile, _plane, _storey);

            if (e.button == 0)
            {
                Place(location);
                e.Use(); Repaint();
            }
            else if (e.button == 1)
            {
                RemoveLatestAt(location);
                e.Use(); Repaint();
            }
        }

        private void Place(GridLocation location)
        {
            if (!ContentId.TryCreate(_definitionId, out var definition))
            {
                _status = "Invalid definition ID.";
                return;
            }
            if (!WorldAddressing.TryResolve(location, out var address)) return;
            EnsurePlacementPageLoaded(address.Key);
            var instance = new ContentId("placement." + Guid.NewGuid().ToString("N"));
            var placement = new WorldPlacementRecord(instance, definition, _kind, location)
            {
                OffsetX = _offsetX,
                OffsetY = _offsetY,
                VisualHeightOffset = _heightOffset,
                YawDegrees = _yaw
            };
            _placements[address.Key].Add(placement);
            _dirtyPlacementPages.Add(address.Key);
            _status = $"Placed {_definitionId} at {location.Tile.X}, {location.Tile.Y}.";
        }

        private void RemoveLatestAt(GridLocation location)
        {
            if (!WorldAddressing.TryResolve(location, out var address)) return;
            EnsurePlacementPageLoaded(address.Key);
            var list = _placements[address.Key];
            for (var i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].Anchor != location) continue;
                var id = list[i].DefinitionId;
                list.RemoveAt(i);
                _dirtyPlacementPages.Add(address.Key);
                _status = $"Removed {id} at {location.Tile.X}, {location.Tile.Y}.";
                return;
            }
            _status = "No placement anchored on that tile.";
        }

        private void EnsureVisibleDataLoaded(Rect canvas)
        {
            var bounds = VisibleBounds(new Rect(0, 0, canvas.width, canvas.height));
            var minX = Mathf.Clamp(bounds.MinX, 0, WorldConstants.WorldWidthTiles - 1);
            var minY = Mathf.Clamp(bounds.MinY, 0, WorldConstants.WorldHeightTiles - 1);
            var maxX = Mathf.Clamp(bounds.MaxX, 0, WorldConstants.WorldWidthTiles - 1);
            var maxY = Mathf.Clamp(bounds.MaxY, 0, WorldConstants.WorldHeightTiles - 1);
            var size = WorldConstants.DefaultStoragePageSize;
            for (var py = minY / size; py <= maxY / size; py++)
                for (var px = minX / size; px <= maxX / size; px++)
                {
                    var key = new WorldPageKey(new WorldPageCoord(px, py), _plane, _storey);
                    if (_terrainChecked.Add(key) && WorldPageJsonPersistence.TryLoad(key, out var terrainDocument))
                    {
                        try { _terrain.ImportPage(WorldPageCodec.Decode(terrainDocument)); }
                        catch (Exception ex) { Debug.LogError($"Failed to load terrain page {key}: {ex}"); }
                    }
                    EnsurePlacementPageLoaded(key);
                }
        }

        private void EnsurePlacementPageLoaded(WorldPageKey key)
        {
            if (!_placementChecked.Add(key)) return;
            var list = new List<WorldPlacementRecord>();
            if (WorldPlacementJsonPersistence.TryLoad(key, out var document))
            {
                try { list = WorldPlacementPageCodec.Decode(document); }
                catch (Exception ex) { Debug.LogError($"Failed to decode placement page {key}: {ex}"); }
            }
            _placements[key] = list;
        }

        private void SaveDirtyPages()
        {
            if (_dirtyPlacementPages.Count == 0) { _status = "Nothing to save."; return; }
            var saved = 0;
            var keys = new List<WorldPageKey>(_dirtyPlacementPages);
            for (var i = 0; i < keys.Count; i++)
            {
                var key = keys[i];
                if (!_placements.TryGetValue(key, out var list)) list = new List<WorldPlacementRecord>();
                WorldPlacementJsonPersistence.Save(WorldPlacementPageCodec.Encode(key, list));
                saved++;
            }
            _dirtyPlacementPages.Clear();
            _status = $"Saved {saved} changed placement page(s).";
        }

        private void HandleShortcuts(Event e)
        {
            if (e.type == EventType.KeyDown && (e.control || e.command) && e.keyCode == KeyCode.S)
            {
                SaveDirtyPages();
                e.Use();
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

        private static string ShortId(string id)
        {
            if (string.IsNullOrEmpty(id)) return string.Empty;
            var dot = id.LastIndexOf('.');
            var slash = id.LastIndexOf('/');
            var index = Math.Max(dot, slash);
            return index >= 0 && index + 1 < id.Length ? id.Substring(index + 1) : id;
        }

        private static Color PlacementColor(WorldPlacementKind kind)
        {
            switch (kind)
            {
                case WorldPlacementKind.Resource: return new Color(0.2f, 0.9f, 0.35f, 0.95f);
                case WorldPlacementKind.Doodad: return new Color(0.55f, 0.75f, 1f, 0.75f);
                case WorldPlacementKind.NpcAnchor: return new Color(1f, 0.8f, 0.2f, 0.95f);
                case WorldPlacementKind.TransportNode: return new Color(0.95f, 0.3f, 1f, 0.95f);
                default: return new Color(1f, 0.55f, 0.2f, 0.95f);
            }
        }

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
