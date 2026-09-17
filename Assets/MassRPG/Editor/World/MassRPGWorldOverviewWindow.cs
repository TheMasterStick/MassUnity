using System;
using System.Collections.Generic;
using System.IO;
using MassRPG.Core.Content;
using MassRPG.Core.World;
using MassRPG.Data.World;
using UnityEditor;
using UnityEngine;

namespace MassRPG.Editor.World
{
    /// <summary>
    /// Full Twin Lands overview. One overview pixel corresponds to one 512x512 canonical storage
    /// page, allowing the entire 180km world to be blocked out without instantiating billions of
    /// tiles. Macro strokes write compact single-run page documents; right-click selects a page and
    /// the explicit toolbar action opens the fine 1x1 editor without risking an accidental paint.
    /// </summary>
    public sealed class MassRPGWorldOverviewWindow : EditorWindow
    {
        private static readonly int[] MacroBrushPages = { 1, 3, 5, 9, 17 };
        private static readonly string[] MacroBrushLabels = { "1 page", "3 pages", "5 pages", "9 pages", "17 pages" };
        private static readonly string[] GroundPresets =
        {
            "ground.grass", "ground.dirt", "ground.sand", "ground.stone", "ground.snow", "ground.swamp", "ground.rock"
        };

        private Texture2D _texture;
        private IEnumerator<string> _scan;
        private int _scanProcessed;
        private int _scanDiscovered;
        private int _plane = WorldConstants.SurfacePlane;
        private int _storey;
        private string _groundId = "ground.grass";
        private short _elevation;
        private MacroPagePaintKind _paintKind = MacroPagePaintKind.Land;
        private int _brushIndex;
        private int _selectedPageX = -1;
        private int _selectedPageY = -1;
        private bool _painting;
        private readonly HashSet<int> _paintedThisStroke = new HashSet<int>();
        private string _status = "Ready";

        [MenuItem("MassRPG/World Overview %#g")]
        public static void Open()
        {
            var window = GetWindow<MassRPGWorldOverviewWindow>();
            window.titleContent = new GUIContent("MassRPG World Overview");
            window.minSize = new Vector2(820, 650);
            window.Show();
        }

        private void OnEnable()
        {
            CreateTexture();
            BeginScan();
            EditorApplication.update += ScanUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= ScanUpdate;
            if (_texture != null) DestroyImmediate(_texture);
        }

        private void CreateTexture()
        {
            if (_texture != null) DestroyImmediate(_texture);
            _texture = new Texture2D(WorldMacroPagePainter.PageColumns, WorldMacroPagePainter.PageRows, TextureFormat.RGBA32, false)
            {
                name = "MassRPG World Overview",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var blank = new Color32(31, 33, 36, 255);
            var pixels = new Color32[_texture.width * _texture.height];
            for (var i = 0; i < pixels.Length; i++) pixels[i] = blank;
            _texture.SetPixels32(pixels);
            _texture.Apply(false, false);
        }

        private void BeginScan()
        {
            CreateTexture();
            _scanProcessed = 0;
            _scanDiscovered = 0;
            _scan = WorldPageJsonPersistence.EnumeratePageFiles(_plane, _storey).GetEnumerator();
            _status = "Scanning authored pages...";
        }

        private void ScanUpdate()
        {
            if (_scan == null) return;
            var changed = false;
            const int budget = 24;
            for (var i = 0; i < budget; i++)
            {
                bool hasNext;
                try { hasNext = _scan.MoveNext(); }
                catch (Exception ex)
                {
                    Debug.LogError("MassRPG overview scan failed: " + ex);
                    _scan = null;
                    _status = "Overview scan failed. See Console.";
                    Repaint();
                    return;
                }

                if (!hasNext)
                {
                    _scan.Dispose();
                    _scan = null;
                    if (changed) _texture.Apply(false, false);
                    _status = $"Overview ready. {_scanDiscovered:N0} authored page(s).";
                    Repaint();
                    return;
                }

                _scanProcessed++;
                var file = _scan.Current;
                if (!WorldPageJsonPersistence.TryParsePageKeyFromFile(file, _plane, _storey, out var key)) continue;
                if (!WorldPageJsonPersistence.TryLoad(key, out var document)) continue;
                ApplySummary(WorldPageOverviewSummary.FromDocument(document), false);
                _scanDiscovered++;
                changed = true;
            }

            if (changed)
            {
                _texture.Apply(false, false);
                _status = $"Scanning... {_scanProcessed:N0} file(s), {_scanDiscovered:N0} page(s) loaded.";
                Repaint();
            }
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawPaintOptions();
            EditorGUILayout.HelpBox(
                "Macro paint is for the rough whole-world pass. Each painted square replaces one complete 512x512 canonical page with a uniform compressed page. Left-click/drag paints; right-click selects a page; use 'Open 1x1 Detail Here' to enter fine editing. Macro writes are deliberately not local-undoable; Git/history is the safety net.",
                MessageType.Info);

            var top = GUILayoutUtility.GetLastRect().yMax + 4f;
            var statusHeight = 22f;
            var canvas = FitAspect(new Rect(8f, top, position.width - 16f, Mathf.Max(100f, position.height - top - statusHeight - 8f)));
            DrawOverview(canvas);
            DrawStatus(canvas.yMax + 2f);
            HandleInput(canvas);
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Plane", GUILayout.Width(35));
                var plane = EditorGUILayout.IntField(_plane, EditorStyles.toolbarTextField, GUILayout.Width(38));
                GUILayout.Label("Floor", GUILayout.Width(32));
                var storey = Mathf.Max(0, EditorGUILayout.IntField(_storey, EditorStyles.toolbarTextField, GUILayout.Width(32)));
                if (plane != _plane || storey != _storey)
                {
                    _plane = plane;
                    _storey = storey;
                    BeginScan();
                }

                GUILayout.Space(8);
                if (GUILayout.Button("Rescan", EditorStyles.toolbarButton, GUILayout.Width(58))) BeginScan();
                GUILayout.FlexibleSpace();

                GUI.enabled = _selectedPageX >= 0 && _selectedPageY >= 0;
                if (GUILayout.Button("Open 1x1 Detail Here", EditorStyles.toolbarButton, GUILayout.Width(135))) OpenSelectedDetail();
                GUI.enabled = true;
            }
        }

        private void DrawPaintOptions()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                _paintKind = (MacroPagePaintKind)EditorGUILayout.EnumPopup(_paintKind, GUILayout.Width(92));
                _brushIndex = EditorGUILayout.Popup(_brushIndex, MacroBrushLabels, GUILayout.Width(85));
                GUILayout.Label("Ground", GUILayout.Width(45));
                _groundId = EditorGUILayout.TextField(_groundId, GUILayout.Width(150));
                GUILayout.Label("Elev", GUILayout.Width(30));
                _elevation = (short)Mathf.Clamp(EditorGUILayout.IntField(_elevation, GUILayout.Width(45)), short.MinValue, short.MaxValue);

                for (var i = 0; i < GroundPresets.Length; i++)
                {
                    var label = GroundPresets[i].Substring("ground.".Length);
                    if (GUILayout.Button(label, GUILayout.Height(18))) _groundId = GroundPresets[i];
                }
            }
        }

        private void DrawOverview(Rect canvas)
        {
            EditorGUI.DrawRect(new Rect(canvas.x - 1, canvas.y - 1, canvas.width + 2, canvas.height + 2), new Color(0.07f, 0.07f, 0.075f));
            if (_texture != null) GUI.DrawTexture(canvas, _texture, ScaleMode.StretchToFill, false);

            Handles.BeginGUI();
            var old = Handles.color;
            Handles.color = new Color(1f, 1f, 1f, 0.13f);
            for (var p = 0; p <= WorldMacroPagePainter.PageColumns; p += 16)
            {
                var x = canvas.x + p / (float)WorldMacroPagePainter.PageColumns * canvas.width;
                Handles.DrawLine(new Vector3(x, canvas.y), new Vector3(x, canvas.yMax));
            }
            for (var p = 0; p <= WorldMacroPagePainter.PageRows; p += 16)
            {
                var y = canvas.y + p / (float)WorldMacroPagePainter.PageRows * canvas.height;
                Handles.DrawLine(new Vector3(canvas.x, y), new Vector3(canvas.xMax, y));
            }
            Handles.color = old;
            Handles.EndGUI();

            if (_selectedPageX >= 0 && _selectedPageY >= 0)
            {
                var x0 = canvas.x + _selectedPageX / (float)WorldMacroPagePainter.PageColumns * canvas.width;
                var y0 = canvas.y + _selectedPageY / (float)WorldMacroPagePainter.PageRows * canvas.height;
                var x1 = canvas.x + (_selectedPageX + 1f) / WorldMacroPagePainter.PageColumns * canvas.width;
                var y1 = canvas.y + (_selectedPageY + 1f) / WorldMacroPagePainter.PageRows * canvas.height;
                var r = new Rect(x0, y0, Mathf.Max(2f, x1 - x0), Mathf.Max(2f, y1 - y0));
                EditorGUI.DrawRect(new Rect(r.x - 2, r.y - 2, r.width + 4, 2), Color.white);
                EditorGUI.DrawRect(new Rect(r.x - 2, r.yMax, r.width + 4, 2), Color.white);
                EditorGUI.DrawRect(new Rect(r.x - 2, r.y, 2, r.height), Color.white);
                EditorGUI.DrawRect(new Rect(r.xMax, r.y, 2, r.height), Color.white);
            }
        }

        private void HandleInput(Rect canvas)
        {
            var e = Event.current;
            if (!canvas.Contains(e.mousePosition)) return;
            if (!TryMousePage(canvas, e.mousePosition, out var px, out var py)) return;

            if (e.type == EventType.MouseDown && e.button == 1)
            {
                _selectedPageX = px;
                _selectedPageY = py;
                _status = SelectedStatus();
                e.Use();
                Repaint();
                return;
            }

            if (e.button != 0 || e.alt) return;
            if (e.type == EventType.MouseDown)
            {
                if (_paintKind != MacroPagePaintKind.Unpainted && !ContentId.TryCreate(_groundId, out _))
                {
                    _status = "Invalid ground ID.";
                    e.Use();
                    return;
                }
                _painting = true;
                _paintedThisStroke.Clear();
                PaintMacroBrush(px, py);
                e.Use();
            }
            else if (_painting && e.type == EventType.MouseDrag)
            {
                PaintMacroBrush(px, py);
                e.Use();
            }
            else if (_painting && (e.type == EventType.MouseUp || e.rawType == EventType.MouseUp))
            {
                _painting = false;
                _status = $"Macro stroke wrote {_paintedThisStroke.Count:N0} page(s). Right-click selects; toolbar button opens 1x1 detail.";
                _paintedThisStroke.Clear();
                e.Use();
            }
        }

        private void PaintMacroBrush(int centerX, int centerY)
        {
            var size = MacroBrushPages[_brushIndex];
            var radius = size / 2;
            var changedTexture = false;
            for (var y = centerY - radius; y <= centerY + radius; y++)
            {
                if (y < 0 || y >= WorldMacroPagePainter.PageRows) continue;
                for (var x = centerX - radius; x <= centerX + radius; x++)
                {
                    if (x < 0 || x >= WorldMacroPagePainter.PageColumns) continue;
                    var index = y * WorldMacroPagePainter.PageColumns + x;
                    if (!_paintedThisStroke.Add(index)) continue;
                    var key = new WorldPageKey(new WorldPageCoord(x, y), _plane, _storey);

                    if (_paintKind == MacroPagePaintKind.Unpainted)
                    {
                        var path = WorldPageJsonPersistence.FilePath(key);
                        if (File.Exists(path)) File.Delete(path);
                        SetPagePixel(x, y, new Color32(31, 33, 36, 255));
                    }
                    else
                    {
                        var document = WorldMacroPagePainter.CreateUniformPage(
                            x, y, _plane, _storey, new ContentId(_groundId), _elevation, _paintKind);
                        WorldPageJsonPersistence.Save(document);
                        ApplySummary(WorldPageOverviewSummary.FromDocument(document), false);
                    }
                    changedTexture = true;
                }
            }

            if (changedTexture)
            {
                _texture.Apply(false, false);
                _status = $"Painting... {_paintedThisStroke.Count:N0} page(s) in this stroke.";
                Repaint();
            }
        }

        private void ApplySummary(WorldPageOverviewSummary summary, bool apply = true)
        {
            Color32 color;
            if (summary.IsMostlyDeepWater) color = new Color32(18, 55, 101, 255);
            else if (summary.IsMostlyWater) color = new Color32(28, 105, 155, 255);
            else color = GroundColor(summary.DominantGround, summary.MaximumElevation);
            SetPagePixel(summary.PageX, summary.PageY, color);
            if (apply) _texture.Apply(false, false);
        }

        private void SetPagePixel(int pageX, int pageY, Color32 color)
        {
            if (_texture == null || pageX < 0 || pageY < 0 || pageX >= _texture.width || pageY >= _texture.height) return;
            // Texture pixel coordinates grow upward; flip Y so overview coordinates match the detail editor/map convention.
            _texture.SetPixel(pageX, _texture.height - 1 - pageY, color);
        }

        private void OpenSelectedDetail()
        {
            if (_selectedPageX < 0 || _selectedPageY < 0) return;
            var size = WorldConstants.DefaultStoragePageSize;
            var x = Mathf.Clamp(_selectedPageX * size + size / 2, 0, WorldConstants.WorldWidthTiles - 1);
            var y = Mathf.Clamp(_selectedPageY * size + size / 2, 0, WorldConstants.WorldHeightTiles - 1);
            MassRPGWorldEditorWindow.OpenAt(x, y, _plane, _storey);
        }

        private string SelectedStatus()
        {
            if (_selectedPageX < 0) return _status;
            var size = WorldConstants.DefaultStoragePageSize;
            return $"Selected page {_selectedPageX}, {_selectedPageY} — tiles {_selectedPageX * size:N0}..{Math.Min(WorldConstants.WorldWidthTiles - 1, (_selectedPageX + 1) * size - 1):N0}, {_selectedPageY * size:N0}..{Math.Min(WorldConstants.WorldHeightTiles - 1, (_selectedPageY + 1) * size - 1):N0}.";
        }

        private void DrawStatus(float y)
        {
            var rect = new Rect(8f, y, position.width - 16f, 20f);
            GUI.Label(rect, _status, EditorStyles.miniLabel);
        }

        private static bool TryMousePage(Rect canvas, Vector2 mouse, out int pageX, out int pageY)
        {
            pageX = Mathf.FloorToInt((mouse.x - canvas.x) / canvas.width * WorldMacroPagePainter.PageColumns);
            pageY = Mathf.FloorToInt((mouse.y - canvas.y) / canvas.height * WorldMacroPagePainter.PageRows);
            return pageX >= 0 && pageY >= 0 && pageX < WorldMacroPagePainter.PageColumns && pageY < WorldMacroPagePainter.PageRows;
        }

        private static Rect FitAspect(Rect available)
        {
            var aspect = WorldConstants.WorldWidthTiles / (float)WorldConstants.WorldHeightTiles;
            var width = available.width;
            var height = width / aspect;
            if (height > available.height)
            {
                height = available.height;
                width = height * aspect;
            }
            return new Rect(available.x + (available.width - width) * 0.5f, available.y + (available.height - height) * 0.5f, width, height);
        }

        private static Color32 GroundColor(string id, short elevation)
        {
            if (string.IsNullOrEmpty(id) || id == "ground.unpainted") return new Color32(45, 47, 50, 255);
            if (id.Contains("snow")) return new Color32(200, 211, 214, 255);
            if (id.Contains("sand")) return new Color32(178, 157, 102, 255);
            if (id.Contains("swamp")) return new Color32(74, 91, 61, 255);
            if (id.Contains("stone") || id.Contains("rock")) return new Color32(105, 105, 101, 255);
            if (id.Contains("dirt")) return new Color32(111, 82, 57, 255);
            if (id.Contains("grass"))
            {
                var lift = Mathf.Clamp(elevation * 4, -20, 35);
                return new Color32((byte)Mathf.Clamp(75 + lift, 0, 255), (byte)Mathf.Clamp(116 + lift, 0, 255), (byte)Mathf.Clamp(67 + lift, 0, 255), 255);
            }

            unchecked
            {
                uint hash = 2166136261;
                for (var i = 0; i < id.Length; i++) { hash ^= id[i]; hash *= 16777619; }
                var hue = (hash % 1000) / 1000f;
                var c = Color.HSVToRGB(hue, 0.35f, 0.62f);
                return (Color32)c;
            }
        }
    }
}
