using System;
using System.Collections.Generic;
using MassRPG.Core.Content;
using MassRPG.Core.World;
using MassRPG.Data.World;
using MassRPG.EditorCore.World;
using UnityEditor;
using UnityEngine;

namespace MassRPG.Editor.World
{
    /// <summary>
    /// Tactile MassRPG world-authoring surface. It deliberately edits the canonical 1x1 logical
    /// tile data directly rather than Unity Terrain or scene GameObjects.
    /// </summary>
    public sealed class MassRPGWorldEditorWindow : EditorWindow
    {
        private enum WaterPaintMode { Water, DeepWater, Erase }
        private enum PathingPaintMode { Movement, LineOfSight, NoBuild }
        private enum EdgePaintMode { Ramp, MovementBarrier, FullBarrier, Clear }

        private static readonly int[] BrushSizes = { 1, 3, 5, 7, 11, 21, 41, 81, 161, 321 };
        private static readonly string[] BrushLabels = { "1", "3", "5", "7", "11", "21", "41", "81", "161", "321" };
        private static readonly string[] GroundPresets =
        {
            "ground.grass", "ground.dirt", "ground.sand", "ground.stone", "ground.snow", "ground.swamp", "ground.rock"
        };

        private AuthoredWorldPageStore _store;
        private WorldEditSession _session;
        private readonly HashSet<WorldPageKey> _diskChecked = new HashSet<WorldPageKey>();
        private readonly HashSet<GridCoord> _strokeTiles = new HashSet<GridCoord>();

        private WorldEditorMode _mode = WorldEditorMode.Terrain;
        private BrushShape _brushShape = BrushShape.Square;
        private WaterPaintMode _waterMode = WaterPaintMode.Water;
        private PathingPaintMode _pathingMode = PathingPaintMode.Movement;
        private EdgePaintMode _edgeMode = EdgePaintMode.Ramp;
        private int _brushIndex;
        private string _groundId = "ground.grass";
        private float _centerX = WorldConstants.WorldWidthTiles * 0.5f;
        private float _centerY = WorldConstants.WorldHeightTiles * 0.5f;
        private float _pixelsPerTile = 18f;
        private int _plane = WorldConstants.SurfacePlane;
        private int _storey;
        private bool _paintingStroke;
        private bool _strokeErase;
        private double _nextRecoveryAt;
        private string _status = "Ready";

        private GridCoord? _selectionStart;
        private GridCoord? _selectionEnd;
        private bool _selectionDragging;
        private WorldTileStamp _stamp;
        private bool _pasteStampMode;

        [MenuItem("MassRPG/World Editor %#m")]
        public static void Open()
        {
            var window = GetWindow<MassRPGWorldEditorWindow>();
            window.titleContent = new GUIContent("MassRPG World Editor");
            window.minSize = new Vector2(900, 600);
            window.Show();
        }

        public static void OpenAt(int x, int y, int plane = WorldConstants.SurfacePlane, int storey = 0)
        {
            var window = GetWindow<MassRPGWorldEditorWindow>();
            window.titleContent = new GUIContent("MassRPG World Editor");
            window.minSize = new Vector2(900, 600);
            window._centerX = Mathf.Clamp(x, 0, WorldConstants.WorldWidthTiles - 1);
            window._centerY = Mathf.Clamp(y, 0, WorldConstants.WorldHeightTiles - 1);
            window._plane = plane;
            window._storey = Mathf.Max(0, storey);
            window._status = $"Jumped to {Mathf.RoundToInt(window._centerX)}, {Mathf.RoundToInt(window._centerY)}.";
            window.Show();
            window.Focus();
            window.Repaint();
        }

        private void OnEnable()
        {
            EnsureSession();
            _nextRecoveryAt = EditorApplication.timeSinceStartup + 60.0;
            EditorApplication.update += EditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= EditorUpdate;
            if (_session != null && _session.DirtyPageCount > 0) SaveRecovery();
        }

        private void EnsureSession()
        {
            if (_store != null && _session != null) return;
            _store = new AuthoredWorldPageStore(new ContentId("ground.unpainted"));
            _session = new WorldEditSession(_store, 250);
            _diskChecked.Clear();
        }

        private void EditorUpdate()
        {
            if (_session == null || _session.DirtyPageCount == 0) return;
            if (EditorApplication.timeSinceStartup < _nextRecoveryAt) return;
            SaveRecovery();
            _nextRecoveryAt = EditorApplication.timeSinceStartup + 60.0;
        }

        private void OnGUI()
        {
            EnsureSession();
            HandleKeyboardShortcuts(Event.current);
            DrawTopToolbar();
            DrawModeOptions();

            var toolbarHeight = GUILayoutUtility.GetLastRect().yMax + 4f;
            var canvas = new Rect(0f, toolbarHeight, position.width, Mathf.Max(0f, position.height - toolbarHeight - 22f));
            EnsureVisiblePagesLoaded(canvas);
            DrawCanvas(canvas);
            DrawStatusBar(canvas.yMax);
        }

        private void DrawTopToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                _mode = (WorldEditorMode)EditorGUILayout.EnumPopup(_mode, EditorStyles.toolbarPopup, GUILayout.Width(120));
                GUI.enabled = _mode != WorldEditorMode.Edges && _mode != WorldEditorMode.Selection;
                _brushIndex = EditorGUILayout.Popup(_brushIndex, BrushLabels, EditorStyles.toolbarPopup, GUILayout.Width(50));
                _brushShape = (BrushShape)EditorGUILayout.EnumPopup(_brushShape, EditorStyles.toolbarPopup, GUILayout.Width(70));
                GUI.enabled = true;

                GUILayout.Space(8);
                GUILayout.Label("X", GUILayout.Width(12));
                var x = EditorGUILayout.IntField(Mathf.RoundToInt(_centerX), EditorStyles.toolbarTextField, GUILayout.Width(70));
                GUILayout.Label("Y", GUILayout.Width(12));
                var y = EditorGUILayout.IntField(Mathf.RoundToInt(_centerY), EditorStyles.toolbarTextField, GUILayout.Width(70));
                if (x != Mathf.RoundToInt(_centerX) || y != Mathf.RoundToInt(_centerY))
                {
                    _centerX = Mathf.Clamp(x, 0, WorldConstants.WorldWidthTiles - 1);
                    _centerY = Mathf.Clamp(y, 0, WorldConstants.WorldHeightTiles - 1);
                }

                GUILayout.Label("Plane", GUILayout.Width(35));
                _plane = EditorGUILayout.IntField(_plane, EditorStyles.toolbarTextField, GUILayout.Width(35));
                GUILayout.Label("Floor", GUILayout.Width(32));
                _storey = Mathf.Max(0, EditorGUILayout.IntField(_storey, EditorStyles.toolbarTextField, GUILayout.Width(30)));

                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Overview", EditorStyles.toolbarButton, GUILayout.Width(65))) MassRPGWorldOverviewWindow.Open();
                if (GUILayout.Button("Preview Here", EditorStyles.toolbarButton, GUILayout.Width(90)))
                    MassRPGSceneWorldPreview.JumpTo(Mathf.RoundToInt(_centerX), Mathf.RoundToInt(_centerY), _plane, _storey);
                if (GUILayout.Button("Preview Focus", EditorStyles.toolbarButton, GUILayout.Width(95)))
                    OpenAt(MassRPGSceneWorldPreview.Focus.X, MassRPGSceneWorldPreview.Focus.Y,
                        MassRPGSceneWorldPreview.Plane, MassRPGSceneWorldPreview.Storey);
                GUI.enabled = _session.CanUndo;
                if (GUILayout.Button("Undo", EditorStyles.toolbarButton, GUILayout.Width(45))) { _session.Undo(); Repaint(); }
                GUI.enabled = _session.CanRedo;
                if (GUILayout.Button("Redo", EditorStyles.toolbarButton, GUILayout.Width(45))) { _session.Redo(); Repaint(); }
                GUI.enabled = true;
                if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(45))) SaveProduction();
            }
        }

        private void DrawModeOptions()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                if (_mode != WorldEditorMode.Edges && _mode != WorldEditorMode.Selection)
                    GUILayout.Label($"Brush {BrushSizes[_brushIndex]}x{BrushSizes[_brushIndex]}", GUILayout.Width(105));
                else if (_mode == WorldEditorMode.Selection)
                    GUILayout.Label("Select / Stamp", GUILayout.Width(105));
                else
                    GUILayout.Label("Edge tool", GUILayout.Width(105));

                switch (_mode)
                {
                    case WorldEditorMode.Terrain:
                        GUILayout.Label("Ground ID", GUILayout.Width(60));
                        _groundId = EditorGUILayout.TextField(_groundId, GUILayout.Width(170));
                        for (var i = 0; i < GroundPresets.Length; i++)
                        {
                            var label = GroundPresets[i].Substring("ground.".Length);
                            if (GUILayout.Button(label, GUILayout.Height(18))) _groundId = GroundPresets[i];
                        }
                        break;

                    case WorldEditorMode.Elevation:
                        GUILayout.Label("Paint: left = raise +1, Shift+left = lower -1. Logical elevation remains integer/discrete.");
                        break;

                    case WorldEditorMode.Water:
                        _waterMode = (WaterPaintMode)GUILayout.Toolbar((int)_waterMode, new[] { "Water", "Deep Water", "Erase" }, GUILayout.Width(260));
                        GUILayout.Label("Water inherently blocks walking and building; explicit pathing/no-build paint remains independent.");
                        break;

                    case WorldEditorMode.Edges:
                        _edgeMode = (EdgePaintMode)GUILayout.Toolbar((int)_edgeMode,
                            new[] { "Ramp", "Fence / movement", "Wall / movement + LOS", "Clear" }, GUILayout.Width(520));
                        GUILayout.Label("Click a tile edge. Shift removes a ramp/barrier.");
                        break;

                    case WorldEditorMode.Selection:
                        GUILayout.Label(SelectionSummary(), GUILayout.Width(180));
                        GUI.enabled = HasSelection();
                        if (GUILayout.Button("Capture Stamp", GUILayout.Width(95))) CaptureSelection();
                        GUI.enabled = _stamp != null;
                        var pasteLabel = _pasteStampMode && _stamp != null
                            ? $"Pasting {_stamp.Width}x{_stamp.Height}"
                            : _stamp != null ? $"Paste {_stamp.Width}x{_stamp.Height}" : "Paste Stamp";
                        _pasteStampMode = GUILayout.Toggle(_pasteStampMode && _stamp != null, pasteLabel, GUI.skin.button, GUILayout.Width(110));
                        GUI.enabled = HasSelection() || _stamp != null;
                        if (GUILayout.Button("Clear", GUILayout.Width(55)))
                        {
                            _selectionStart = null;
                            _selectionEnd = null;
                            _stamp = null;
                            _pasteStampMode = false;
                            _status = "Selection and stamp cleared.";
                        }
                        GUI.enabled = true;
                        GUILayout.Label("Drag to select. Stamps copy terrain/elevation/tile flags, not wall/ramp edges.");
                        break;

                    case WorldEditorMode.Pathing:
                        _pathingMode = (PathingPaintMode)GUILayout.Toolbar((int)_pathingMode, new[] { "Movement", "Ranged LOS", "No Build" }, GUILayout.Width(300));
                        GUILayout.Label("Shift+paint erases the selected flag.");
                        break;

                    default:
                        GUILayout.Label("This authoring layer has a data foundation but its tactile painting tool is not wired into this window yet.");
                        break;
                }
            }
        }

        private void DrawCanvas(Rect canvas)
        {
            if (canvas.width <= 0 || canvas.height <= 0) return;
            EditorGUI.DrawRect(canvas, new Color(0.105f, 0.11f, 0.12f));

            GUI.BeginGroup(canvas);
            var localRect = new Rect(0, 0, canvas.width, canvas.height);
            var bounds = VisibleBounds(localRect);
            DrawTiles(localRect, bounds);
            DrawGrid(localRect, bounds);
            DrawAuthoredEdges(localRect, bounds);
            DrawStrokePreview(localRect, bounds);
            DrawSelection(bounds);
            GUI.EndGroup();

            HandleCanvasInput(canvas, bounds);
        }

        private void DrawTiles(Rect localRect, TileBounds bounds)
        {
            for (var y = bounds.MinY; y <= bounds.MaxY; y++)
            {
                if (y < 0 || y >= WorldConstants.WorldHeightTiles) continue;
                for (var x = bounds.MinX; x <= bounds.MaxX; x++)
                {
                    if (x < 0 || x >= WorldConstants.WorldWidthTiles) continue;
                    var location = Loc(x, y);
                    var rect = TileRect(x, y, bounds);
                    if (!_store.TryGetCell(location, out var cell)) continue;

                    EditorGUI.DrawRect(rect, GroundColor(cell.GroundId));
                    if ((cell.Flags & TileFlags.DeepWater) != 0)
                        EditorGUI.DrawRect(rect, new Color(0.05f, 0.18f, 0.35f, 0.82f));
                    else if ((cell.Flags & TileFlags.Water) != 0)
                        EditorGUI.DrawRect(rect, new Color(0.08f, 0.33f, 0.52f, 0.70f));

                    if ((cell.Flags & TileFlags.MovementBlocked) != 0)
                        EditorGUI.DrawRect(rect, new Color(0.55f, 0.08f, 0.08f, 0.20f));
                    if ((cell.Flags & TileFlags.NoBuild) != 0)
                        EditorGUI.DrawRect(rect, new Color(0.65f, 0.45f, 0.05f, 0.16f));

                    if (_pixelsPerTile >= 26f && cell.Elevation != 0)
                        GUI.Label(rect, cell.Elevation.ToString(), EditorStyles.miniLabel);
                }
            }
        }

        private void DrawGrid(Rect localRect, TileBounds bounds)
        {
            if (_pixelsPerTile < 10f) return;
            Handles.BeginGUI();
            var previous = Handles.color;
            Handles.color = new Color(1f, 1f, 1f, _pixelsPerTile >= 18f ? 0.11f : 0.055f);

            for (var x = bounds.MinX; x <= bounds.MaxX + 1; x++)
            {
                var px = (x - bounds.MinX) * _pixelsPerTile;
                Handles.DrawLine(new Vector3(px, 0), new Vector3(px, localRect.height));
            }
            for (var y = bounds.MinY; y <= bounds.MaxY + 1; y++)
            {
                var py = (y - bounds.MinY) * _pixelsPerTile;
                Handles.DrawLine(new Vector3(0, py), new Vector3(localRect.width, py));
            }
            Handles.color = previous;
            Handles.EndGUI();
        }

        private void DrawAuthoredEdges(Rect localRect, TileBounds bounds)
        {
            if (_pixelsPerTile < 8f) return;
            Handles.BeginGUI();
            var old = Handles.color;
            for (var y = bounds.MinY; y <= bounds.MaxY; y++)
            {
                if (y < 0 || y >= WorldConstants.WorldHeightTiles) continue;
                for (var x = bounds.MinX; x <= bounds.MaxX; x++)
                {
                    if (x < 0 || x >= WorldConstants.WorldWidthTiles) continue;
                    if (!_store.TryGetCell(Loc(x, y), out var cell)) continue;
                    DrawCellEdge(cell, CardinalEdgeMask.East, x, y, bounds);
                    DrawCellEdge(cell, CardinalEdgeMask.South, x, y, bounds);
                    if (x == 0) DrawCellEdge(cell, CardinalEdgeMask.West, x, y, bounds);
                    if (y == 0) DrawCellEdge(cell, CardinalEdgeMask.North, x, y, bounds);
                }
            }
            Handles.color = old;
            Handles.EndGUI();
        }

        private void DrawCellEdge(AuthoredTileCell cell, CardinalEdgeMask edge, int x, int y, TileBounds bounds)
        {
            var ramp = (cell.ElevationTransitionEdges & edge) != 0;
            var move = (cell.MovementBlockedEdges & edge) != 0;
            var los = (cell.LineOfSightBlockedEdges & edge) != 0;
            if (!ramp && !move && !los) return;

            Handles.color = ramp ? new Color(0.25f, 0.95f, 0.35f, 0.95f)
                : los ? new Color(0.95f, 0.22f, 0.20f, 0.95f)
                : new Color(1f, 0.68f, 0.13f, 0.95f);
            var rect = TileRect(x, y, bounds);
            Vector3 a;
            Vector3 b;
            switch (edge)
            {
                case CardinalEdgeMask.North: a = new Vector3(rect.xMin, rect.yMin); b = new Vector3(rect.xMax, rect.yMin); break;
                case CardinalEdgeMask.East: a = new Vector3(rect.xMax, rect.yMin); b = new Vector3(rect.xMax, rect.yMax); break;
                case CardinalEdgeMask.South: a = new Vector3(rect.xMin, rect.yMax); b = new Vector3(rect.xMax, rect.yMax); break;
                case CardinalEdgeMask.West: a = new Vector3(rect.xMin, rect.yMin); b = new Vector3(rect.xMin, rect.yMax); break;
                default: return;
            }
            Handles.DrawAAPolyLine(3f, a, b);
        }

        private void DrawStrokePreview(Rect localRect, TileBounds bounds)
        {
            if (_strokeTiles.Count == 0) return;
            foreach (var tile in _strokeTiles)
            {
                if (tile.X < bounds.MinX || tile.X > bounds.MaxX || tile.Y < bounds.MinY || tile.Y > bounds.MaxY) continue;
                EditorGUI.DrawRect(TileRect(tile.X, tile.Y, bounds), new Color(1f, 1f, 1f, 0.22f));
            }
        }

        private void DrawSelection(TileBounds bounds)
        {
            if (!_selectionStart.HasValue || !_selectionEnd.HasValue) return;
            var a = _selectionStart.Value;
            var b = _selectionEnd.Value;
            var minX = Math.Min(a.X, b.X);
            var maxX = Math.Max(a.X, b.X);
            var minY = Math.Min(a.Y, b.Y);
            var maxY = Math.Max(a.Y, b.Y);
            if (maxX < bounds.MinX || minX > bounds.MaxX || maxY < bounds.MinY || minY > bounds.MaxY) return;

            var rect = new Rect(
                (minX - bounds.MinX) * _pixelsPerTile,
                (minY - bounds.MinY) * _pixelsPerTile,
                (maxX - minX + 1) * _pixelsPerTile,
                (maxY - minY + 1) * _pixelsPerTile);
            EditorGUI.DrawRect(rect, new Color(0.25f, 0.65f, 1f, 0.13f));
            Handles.BeginGUI();
            var old = Handles.color;
            Handles.color = _pasteStampMode ? new Color(1f, 0.75f, 0.2f, 0.95f) : new Color(0.3f, 0.78f, 1f, 0.95f);
            var points = new[]
            {
                new Vector3(rect.xMin, rect.yMin),
                new Vector3(rect.xMax, rect.yMin),
                new Vector3(rect.xMax, rect.yMax),
                new Vector3(rect.xMin, rect.yMax),
                new Vector3(rect.xMin, rect.yMin)
            };
            Handles.DrawAAPolyLine(2f, points);
            Handles.color = old;
            Handles.EndGUI();
        }

        private void HandleCanvasInput(Rect canvas, TileBounds bounds)
        {
            var e = Event.current;
            if (!canvas.Contains(e.mousePosition)) return;
            var local = e.mousePosition - canvas.position;

            if (e.type == EventType.ScrollWheel)
            {
                _pixelsPerTile = Mathf.Clamp(_pixelsPerTile - e.delta.y * 1.5f, 6f, 48f);
                e.Use();
                Repaint();
                return;
            }

            if ((e.button == 2 || (e.button == 0 && e.alt)) && e.type == EventType.MouseDrag)
            {
                _centerX = Mathf.Clamp(_centerX - e.delta.x / _pixelsPerTile, 0f, WorldConstants.WorldWidthTiles - 1f);
                _centerY = Mathf.Clamp(_centerY - e.delta.y / _pixelsPerTile, 0f, WorldConstants.WorldHeightTiles - 1f);
                e.Use();
                Repaint();
                return;
            }

            var tile = new GridCoord(
                bounds.MinX + Mathf.FloorToInt(local.x / _pixelsPerTile),
                bounds.MinY + Mathf.FloorToInt(local.y / _pixelsPerTile));

            if (_mode == WorldEditorMode.Selection)
            {
                HandleSelectionInput(e, tile);
                return;
            }

            if (_mode == WorldEditorMode.Edges)
            {
                if (e.button == 0 && !e.alt && e.type == EventType.MouseDown
                    && TryNearestEdge(local, bounds, out var from, out var to))
                {
                    ApplyEdgeTool(from, to, e.shift);
                    e.Use();
                    Repaint();
                }
                return;
            }

            if (e.button == 1 && e.type == EventType.MouseDown)
            {
                Eyedrop(tile);
                e.Use();
                return;
            }

            if (e.button != 0 || e.alt) return;
            if (e.type == EventType.MouseDown)
            {
                _paintingStroke = true;
                _strokeErase = e.shift;
                _strokeTiles.Clear();
                AddBrushToStroke(tile);
                e.Use();
                Repaint();
            }
            else if (_paintingStroke && e.type == EventType.MouseDrag)
            {
                AddBrushToStroke(tile);
                e.Use();
                Repaint();
            }
            else if (_paintingStroke && (e.type == EventType.MouseUp || e.rawType == EventType.MouseUp))
            {
                ApplyStroke();
                _paintingStroke = false;
                _strokeTiles.Clear();
                e.Use();
                Repaint();
            }
        }

        private void HandleSelectionInput(Event e, GridCoord tile)
        {
            if (!WorldConstants.IsInsideWorld(tile)) return;

            if (e.button == 1 && e.type == EventType.MouseDown)
            {
                if (_pasteStampMode)
                {
                    _pasteStampMode = false;
                    _status = "Stamp paste mode cancelled.";
                    e.Use();
                    Repaint();
                }
                return;
            }

            if (e.button != 0 || e.alt) return;
            if (e.type == EventType.MouseDown)
            {
                if (_pasteStampMode && _stamp != null)
                {
                    PasteStamp(tile);
                    e.Use();
                    Repaint();
                    return;
                }

                _selectionDragging = true;
                _selectionStart = tile;
                _selectionEnd = tile;
                _status = "Selecting tiles...";
                e.Use();
                Repaint();
            }
            else if (_selectionDragging && e.type == EventType.MouseDrag)
            {
                _selectionEnd = tile;
                e.Use();
                Repaint();
            }
            else if (_selectionDragging && (e.type == EventType.MouseUp || e.rawType == EventType.MouseUp))
            {
                _selectionEnd = tile;
                _selectionDragging = false;
                _status = "Selected " + SelectionSummary() + ". Capture it as a reusable stamp or drag a new selection.";
                e.Use();
                Repaint();
            }
        }

        private void CaptureSelection()
        {
            if (!HasSelection()) return;
            var a = _selectionStart.Value;
            var b = _selectionEnd.Value;
            var minX = Math.Min(a.X, b.X);
            var maxX = Math.Max(a.X, b.X);
            var minY = Math.Min(a.Y, b.Y);
            var maxY = Math.Max(a.Y, b.Y);
            var width = maxX - minX + 1;
            var height = maxY - minY + 1;
            if (width > 256 || height > 256)
            {
                _status = $"Stamp selection is {width}x{height}. Keep reusable stamps at 256x256 tiles or smaller.";
                return;
            }

            for (var y = minY; y <= maxY; y++)
                for (var x = minX; x <= maxX; x++)
                    _store.GetOrCreatePage(Loc(x, y));

            try
            {
                _stamp = WorldTileStamp.Capture(
                    _store,
                    Loc(minX, minY),
                    Loc(maxX, maxY),
                    false);
                _pasteStampMode = false;
                _status = $"Captured {_stamp.Width}x{_stamp.Height} tile stamp. Edge barriers/ramps were intentionally excluded.";
            }
            catch (Exception ex)
            {
                _stamp = null;
                _pasteStampMode = false;
                _status = "Stamp capture failed: " + ex.Message;
            }
        }

        private void PasteStamp(GridCoord topLeft)
        {
            if (_stamp == null) return;
            var changed = _stamp.Paste(_session, Loc(topLeft.X, topLeft.Y));
            _status = changed > 0
                ? $"Pasted {_stamp.Width}x{_stamp.Height} stamp at {topLeft.X}, {topLeft.Y}; {changed:N0} tile(s) changed."
                : "Stamp paste made no changes.";
        }

        private bool HasSelection() => _selectionStart.HasValue && _selectionEnd.HasValue;

        private string SelectionSummary()
        {
            if (!HasSelection()) return "No selection";
            var a = _selectionStart.Value;
            var b = _selectionEnd.Value;
            return $"{Math.Abs(a.X - b.X) + 1}x{Math.Abs(a.Y - b.Y) + 1} tiles";
        }

        private bool TryNearestEdge(Vector2 local, TileBounds bounds, out GridLocation from, out GridLocation to)
        {
            var tileX = bounds.MinX + Mathf.FloorToInt(local.x / _pixelsPerTile);
            var tileY = bounds.MinY + Mathf.FloorToInt(local.y / _pixelsPerTile);
            var tile = new GridCoord(tileX, tileY);
            from = default;
            to = default;
            if (!WorldConstants.IsInsideWorld(tile)) return false;

            var fx = local.x / _pixelsPerTile - Mathf.Floor(local.x / _pixelsPerTile);
            var fy = local.y / _pixelsPerTile - Mathf.Floor(local.y / _pixelsPerTile);
            var min = fx;
            var neighbor = new GridCoord(tile.X - 1, tile.Y);
            if (1f - fx < min) { min = 1f - fx; neighbor = new GridCoord(tile.X + 1, tile.Y); }
            if (fy < min) { min = fy; neighbor = new GridCoord(tile.X, tile.Y - 1); }
            if (1f - fy < min) neighbor = new GridCoord(tile.X, tile.Y + 1);
            if (!WorldConstants.IsInsideWorld(neighbor)) return false;

            from = new GridLocation(tile, _plane, _storey);
            to = new GridLocation(neighbor, _plane, _storey);
            return true;
        }

        private void ApplyEdgeTool(GridLocation from, GridLocation to, bool erase)
        {
            bool changed;
            switch (_edgeMode)
            {
                case EdgePaintMode.Ramp:
                    changed = erase
                        ? WorldEdgeEditing.TryRemoveRamp(_session, from, to)
                        : WorldEdgeEditing.TryPlaceRamp(_session, from, to);
                    _status = changed
                        ? (erase ? "Removed ramp." : "Placed ramp transition.")
                        : "Ramp requires two cardinally adjacent tiles differing by exactly one logical elevation.";
                    return;

                case EdgePaintMode.MovementBarrier:
                    changed = WorldEdgeEditing.SetBarrier(_session, from, to, !erase, false);
                    _status = changed ? (erase ? "Cleared movement barrier." : "Placed movement-only edge (fence/hedge style).") : "Edge made no change.";
                    return;

                case EdgePaintMode.FullBarrier:
                    changed = WorldEdgeEditing.SetBarrier(_session, from, to, !erase, !erase);
                    _status = changed ? (erase ? "Cleared full barrier." : "Placed movement + ranged LOS barrier (wall style).") : "Edge made no change.";
                    return;

                default:
                    var barrier = WorldEdgeEditing.SetBarrier(_session, from, to, false, false);
                    var ramp = WorldEdgeEditing.TryRemoveRamp(_session, from, to);
                    _status = barrier || ramp ? "Cleared authored edge." : "Nothing authored on that edge.";
                    return;
            }
        }

        private void AddBrushToStroke(GridCoord center)
        {
            if (!WorldConstants.IsInsideWorld(center)) return;
            var location = new GridLocation(center, _plane, _storey);
            foreach (var cell in WorldBrush.Cells(location, BrushSizes[_brushIndex], _brushShape))
                if (WorldConstants.IsInsideWorld(cell.Tile)) _strokeTiles.Add(cell.Tile);
        }

        private void ApplyStroke()
        {
            if (_strokeTiles.Count == 0) return;
            if (_mode == WorldEditorMode.Terrain && !ContentId.TryCreate(_groundId, out _))
            {
                _status = "Invalid ground ID. Use lower-case a-z, 0-9, '.', '_', '-' or '/'.";
                return;
            }

            var edits = new List<WorldCellEdit>(_strokeTiles.Count);
            foreach (var tile in _strokeTiles)
            {
                var location = new GridLocation(tile, _plane, _storey);
                _store.GetOrCreatePage(location);
                _store.TryGetCell(location, out var cell);
                edits.Add(new WorldCellEdit(location, PaintCell(cell)));
            }

            var changed = _session.Apply(StrokeLabel(), edits);
            _status = changed > 0 ? $"Edited {changed:N0} tile(s). {_session.DirtyPageCount} page(s) dirty." : "Stroke made no changes.";
        }

        private AuthoredTileCell PaintCell(AuthoredTileCell cell)
        {
            switch (_mode)
            {
                case WorldEditorMode.Terrain:
                    return Copy(cell, groundId: new ContentId(_groundId));

                case WorldEditorMode.Elevation:
                    return Copy(cell, elevation: checked((short)(cell.Elevation + (_strokeErase ? -1 : 1))));

                case WorldEditorMode.Water:
                {
                    var flags = cell.Flags & ~(TileFlags.Water | TileFlags.DeepWater);
                    if (_waterMode == WaterPaintMode.Water) flags |= TileFlags.Water;
                    else if (_waterMode == WaterPaintMode.DeepWater) flags |= TileFlags.DeepWater;
                    return Copy(cell, flags: flags);
                }

                case WorldEditorMode.Pathing:
                {
                    var bit = _pathingMode == PathingPaintMode.Movement ? TileFlags.MovementBlocked
                        : _pathingMode == PathingPaintMode.LineOfSight ? TileFlags.RangedLineOfSightBlocked
                        : TileFlags.NoBuild;
                    var flags = _strokeErase ? cell.Flags & ~bit : cell.Flags | bit;
                    return Copy(cell, flags: flags);
                }

                default:
                    return cell;
            }
        }

        private void Eyedrop(GridCoord tile)
        {
            var location = new GridLocation(tile, _plane, _storey);
            if (!_store.TryGetCell(location, out var cell)) return;
            _groundId = cell.GroundId.Value;
            _status = $"Sampled {cell.GroundId} at {tile.X}, {tile.Y} (elevation {cell.Elevation}).";
            Repaint();
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
                    var key = new WorldPageKey(new WorldPageCoord(px, py), _plane, _storey);
                    if (_diskChecked.Contains(key)) continue;
                    _diskChecked.Add(key);
                    if (!WorldPageJsonPersistence.TryLoad(key, out var document)) continue;
                    try
                    {
                        _store.ImportPage(WorldPageCodec.Decode(document));
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Failed to load MassRPG world page {key}: {ex}");
                    }
                }
            }
        }

        private void SaveProduction()
        {
            if (_session.DirtyPageCount == 0)
            {
                _status = "Nothing to save.";
                return;
            }

            var documents = _session.BuildDirtyPageDocuments();
            for (var i = 0; i < documents.Count; i++) WorldPageJsonPersistence.Save(documents[i]);
            _session.MarkAllSaved();
            _status = $"Saved {documents.Count} changed page(s) to repository WorldData/Pages.";
        }

        private void SaveRecovery()
        {
            if (_session == null || _session.DirtyPageCount == 0) return;
            var documents = _session.BuildDirtyPageDocuments();
            for (var i = 0; i < documents.Count; i++) WorldPageJsonPersistence.Save(documents[i], WorldPageJsonPersistence.RecoveryRoot);
            _status = $"Autosaved recovery copy for {documents.Count} dirty page(s).";
            Repaint();
        }

        private void HandleKeyboardShortcuts(Event e)
        {
            if (e.type != EventType.KeyDown) return;
            if ((e.control || e.command) && e.keyCode == KeyCode.S)
            {
                SaveProduction();
                e.Use();
            }
            else if ((e.control || e.command) && e.keyCode == KeyCode.Z && !e.shift)
            {
                if (_session.Undo()) _status = "Undo: " + _session.RedoLabel;
                e.Use();
                Repaint();
            }
            else if ((e.control || e.command) && ((e.keyCode == KeyCode.Z && e.shift) || e.keyCode == KeyCode.Y))
            {
                if (_session.Redo()) _status = "Redo: " + _session.UndoLabel;
                e.Use();
                Repaint();
            }
        }

        private void DrawStatusBar(float y)
        {
            var rect = new Rect(0, y, position.width, 22f);
            EditorGUI.DrawRect(rect, new Color(0.13f, 0.13f, 0.13f));
            var center = $"Center {Mathf.RoundToInt(_centerX)}, {Mathf.RoundToInt(_centerY)}   Zoom {_pixelsPerTile:0}px/tile   Dirty pages {_session.DirtyPageCount}";
            GUI.Label(new Rect(6, y + 2, position.width * 0.55f, 18), _status, EditorStyles.miniLabel);
            GUI.Label(new Rect(position.width * 0.55f, y + 2, position.width * 0.44f, 18), center, EditorStyles.miniLabel);
        }

        private TileBounds VisibleBounds(Rect localRect)
        {
            var columns = Mathf.CeilToInt(localRect.width / _pixelsPerTile) + 2;
            var rows = Mathf.CeilToInt(localRect.height / _pixelsPerTile) + 2;
            var minX = Mathf.FloorToInt(_centerX - columns * 0.5f);
            var minY = Mathf.FloorToInt(_centerY - rows * 0.5f);
            return new TileBounds(minX, minY, minX + columns, minY + rows);
        }

        private Rect TileRect(int x, int y, TileBounds bounds)
            => new Rect((x - bounds.MinX) * _pixelsPerTile, (y - bounds.MinY) * _pixelsPerTile, _pixelsPerTile, _pixelsPerTile);

        private GridLocation Loc(int x, int y) => new GridLocation(new GridCoord(x, y), _plane, _storey);

        private static AuthoredTileCell Copy(
            AuthoredTileCell cell,
            ContentId? groundId = null,
            short? elevation = null,
            TileFlags? flags = null)
            => new AuthoredTileCell(
                groundId ?? cell.GroundId,
                elevation ?? cell.Elevation,
                flags ?? cell.Flags,
                cell.MovementBlockedEdges,
                cell.LineOfSightBlockedEdges,
                cell.ElevationTransitionEdges);

        private string StrokeLabel()
        {
            switch (_mode)
            {
                case WorldEditorMode.Terrain: return "Paint terrain";
                case WorldEditorMode.Elevation: return _strokeErase ? "Lower elevation" : "Raise elevation";
                case WorldEditorMode.Water: return "Paint water";
                case WorldEditorMode.Pathing: return _strokeErase ? "Erase pathing" : "Paint pathing";
                default: return "World edit";
            }
        }

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
            {
                MinX = minX;
                MinY = minY;
                MaxX = maxX;
                MaxY = maxY;
            }
            public int MinX { get; }
            public int MinY { get; }
            public int MaxX { get; }
            public int MaxY { get; }
        }
    }
}

