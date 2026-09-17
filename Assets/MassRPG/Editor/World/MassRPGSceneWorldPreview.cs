using System;
using System.Collections.Generic;
using MassRPG.Client.World;
using MassRPG.Core.Content;
using MassRPG.Core.World;
using MassRPG.Data.World;
using UnityEditor;
using UnityEngine;

namespace MassRPG.Editor.World
{
    /// <summary>Disposable Scene view visualization of canonical pages near one logical tile.</summary>
    [InitializeOnLoad]
    public static class MassRPGSceneWorldPreview
    {
        private const int Radius = 1;
        private const string SessionKey = "MassRPG.SceneWorldPreview.Enabled";
        private static readonly Dictionary<Vector2Int, GameObject> Chunks = new Dictionary<Vector2Int, GameObject>();
        private static readonly HashSet<WorldPageKey> Missing = new HashSet<WorldPageKey>();
        private static AuthoredWorldPageStore _store;
        private static GameObject _root;
        private static Material _material;
        private static GridCoord _origin;
        private static GridCoord _focus = new GridCoord(WorldConstants.WorldWidthTiles / 2, WorldConstants.WorldHeightTiles / 2);
        private static int _plane;
        private static int _storey;
        private static Vector2Int _chunk = new Vector2Int(-1, -1);
        private static bool _enabled;

        static MassRPGSceneWorldPreview()
        {
            EditorApplication.update += Update;
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;
            EditorApplication.quitting += Dispose;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            _enabled = SessionState.GetBool(SessionKey, false);
            _focus = new GridCoord(SessionState.GetInt(SessionKey + ".X", _focus.X),
                SessionState.GetInt(SessionKey + ".Y", _focus.Y));
            _origin = new GridCoord(SessionState.GetInt(SessionKey + ".OriginX", _focus.X / 64 * 64),
                SessionState.GetInt(SessionKey + ".OriginY", _focus.Y / 64 * 64));
            _plane = SessionState.GetInt(SessionKey + ".Plane", 0);
            _storey = SessionState.GetInt(SessionKey + ".Storey", 0);
        }

        public static GridCoord Focus => _focus;
        public static int Plane => _plane;
        public static int Storey => _storey;
        public static bool Enabled => _enabled;
        public static int ActiveChunkCount => Chunks.Count;

        [MenuItem("MassRPG/Scene World Preview/Enable")]
        private static void EnableMenu() => Enable();
        [MenuItem("MassRPG/Scene World Preview/Enable", true)]
        private static bool EnableMenuValid() => !_enabled;

        [MenuItem("MassRPG/Scene World Preview/Disable")]
        private static void DisableMenu() => Disable();
        [MenuItem("MassRPG/Scene World Preview/Disable", true)]
        private static bool DisableMenuValid() => _enabled;

        [MenuItem("MassRPG/Scene World Preview/Rebuild")]
        public static void Rebuild()
        {
            if (!_enabled) Enable();
            ClearChunks();
            _store = null;
            Missing.Clear();
            _chunk = new Vector2Int(-1, -1);
            Refresh();
        }

        [MenuItem("MassRPG/Scene World Preview/Open World Editor At Focus")]
        private static void OpenWorldEditor()
            => MassRPGWorldEditorWindow.OpenAt(_focus.X, _focus.Y, _plane, _storey);

        public static void Enable()
        {
            _enabled = true;
            SessionState.SetBool(SessionKey, true);
            JumpTo(_focus.X, _focus.Y, _plane, _storey);
        }

        public static void Disable()
        {
            _enabled = false;
            SessionState.SetBool(SessionKey, false);
            Dispose();
            SceneView.RepaintAll();
        }

        public static void JumpTo(int x, int y, int plane, int storey)
        {
            ClearChunks();
            _focus = new GridCoord(
                Mathf.Clamp(x, 0, WorldConstants.WorldWidthTiles - 1),
                Mathf.Clamp(y, 0, WorldConstants.WorldHeightTiles - 1));
            if (_plane != plane || _storey != storey)
            {
                ClearChunks();
                _store = null;
                Missing.Clear();
            }
            _plane = plane;
            _storey = Mathf.Max(0, storey);
            _enabled = true;
            SessionState.SetBool(SessionKey, true);
            var size = WorldConstants.DefaultRenderChunkSize;
            _origin = new GridCoord(_focus.X / size * size, _focus.Y / size * size);
            var view = SceneView.lastActiveSceneView;
            if (view != null)
            {
                view.pivot = new Vector3(_focus.X - _origin.X, 0f, _focus.Y - _origin.Y);
                view.size = 180f;
                view.Repaint();
            }
            _chunk = new Vector2Int(-1, -1);
            Refresh();
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode) Dispose();
            if (state == PlayModeStateChange.EnteredEditMode && _enabled) Refresh();
        }

        private static void Update()
        {
            if (!_enabled || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var view = SceneView.lastActiveSceneView;
            if (view != null && _root != null)
            {
                var pivot = view.pivot;
                var tile = new GridCoord(_origin.X + Mathf.RoundToInt(pivot.x),
                    _origin.Y + Mathf.RoundToInt(pivot.z));
                if (WorldConstants.IsInsideWorld(tile)) _focus = tile;
            }
            var next = new Vector2Int(_focus.X / WorldConstants.DefaultRenderChunkSize,
                _focus.Y / WorldConstants.DefaultRenderChunkSize);
            SaveSessionPosition();
            if (_root == null || next != _chunk) Refresh();
        }

        private static void SaveSessionPosition()
        {
            SessionState.SetInt(SessionKey + ".X", _focus.X);
            SessionState.SetInt(SessionKey + ".Y", _focus.Y);
            SessionState.SetInt(SessionKey + ".OriginX", _origin.X);
            SessionState.SetInt(SessionKey + ".OriginY", _origin.Y);
            SessionState.SetInt(SessionKey + ".Plane", _plane);
            SessionState.SetInt(SessionKey + ".Storey", _storey);
        }

        private static void Refresh()
        {
            if (!_enabled || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (_root == null)
            {
                _root = new GameObject("MassRPG Scene World Preview");
                _root.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
            }
            if (_store == null)
                _store = new AuthoredWorldPageStore(new ContentId("ground.unpainted"));
            if (_material == null)
            {
                var shader = Shader.Find("Hidden/MassRPG/SceneWorldPreviewVertexColor");
                if (shader == null) { Debug.LogError("MassRPG preview shader is missing."); return; }
                _material = new Material(shader) { name = "MassRPG Scene Preview", hideFlags = HideFlags.DontSave };
            }

            var size = WorldConstants.DefaultRenderChunkSize;
            var nextOrigin = new GridCoord(_focus.X / size * size, _focus.Y / size * size);
            if (_origin != nextOrigin)
            {
                var shift = new Vector3(nextOrigin.X - _origin.X, 0f, nextOrigin.Y - _origin.Y);
                _origin = nextOrigin;
                SaveSessionPosition();
                foreach (var chunk in Chunks.Values) if (chunk != null) chunk.transform.position -= shift;
                var view = SceneView.lastActiveSceneView;
                if (view != null) { view.pivot -= shift; view.Repaint(); }
            }

            _chunk = new Vector2Int(_focus.X / size, _focus.Y / size);
            var wanted = new HashSet<Vector2Int>();
            var pages = new HashSet<WorldPageKey>();
            for (var y = _chunk.y - Radius; y <= _chunk.y + Radius; y++)
            for (var x = _chunk.x - Radius; x <= _chunk.x + Radius; x++)
            {
                var start = new GridCoord(x * size, y * size);
                if (!WorldConstants.IsInsideWorld(start)) continue;
                var key = new Vector2Int(x, y);
                wanted.Add(key);
                // Include neighbour pages to preserve slopes and cliffs at page edges.
                for (var by = -1; by <= 1; by += 2)
                for (var bx = -1; bx <= 1; bx += 2)
                {
                    var tile = new GridCoord(start.X + (bx < 0 ? -1 : size),
                        start.Y + (by < 0 ? -1 : size));
                    if (WorldConstants.IsInsideWorld(tile))
                        pages.Add(new WorldPageKey(new WorldPageCoord(
                            tile.X / WorldConstants.DefaultStoragePageSize,
                            tile.Y / WorldConstants.DefaultStoragePageSize), _plane, _storey));
                }
            }
            foreach (var key in pages)
            {
                if (_store.TryGetPage(key, out _) || Missing.Contains(key)) continue;
                if (WorldPageJsonPersistence.TryLoad(key, out var document))
                {
                    try { _store.ImportPage(WorldPageCodec.Decode(document)); }
                    catch (Exception ex) { Debug.LogWarning("MassRPG preview page " + key + ": " + ex.Message); Missing.Add(key); }
                }
                else Missing.Add(key);
            }
            foreach (var pair in new List<KeyValuePair<Vector2Int, GameObject>>(Chunks))
            {
                if (wanted.Contains(pair.Key)) continue;
                var filter = pair.Value.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null) UnityEngine.Object.DestroyImmediate(filter.sharedMesh);
                UnityEngine.Object.DestroyImmediate(pair.Value);
                Chunks.Remove(pair.Key);
            }
            foreach (var key in wanted)
            {
                if (Chunks.ContainsKey(key)) continue;
                var page = new WorldPageKey(new WorldPageCoord(key.x * size / WorldConstants.DefaultStoragePageSize,
                    key.y * size / WorldConstants.DefaultStoragePageSize), _plane, _storey);
                if (!_store.TryGetPage(page, out _)) continue;
                var mesh = LogicalTerrainChunkMeshBuilder.Build(_store, key.x, key.y, _plane, _storey,
                    1f, 1f, size, true);
                mesh.hideFlags = HideFlags.DontSave;
                var chunk = new GameObject("Preview " + key.x + "," + key.y);
                chunk.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
                chunk.transform.SetParent(_root.transform, false);
                chunk.transform.position = new Vector3(key.x * size - _origin.X, _storey * 3f,
                    key.y * size - _origin.Y);
                chunk.AddComponent<MeshFilter>().sharedMesh = mesh;
                chunk.AddComponent<MeshRenderer>().sharedMaterial = _material;
                Chunks.Add(key, chunk);
            }
            foreach (var key in new List<WorldPageKey>(GetLoadedPageKeys()))
                if (!pages.Contains(key)) _store.UnloadPage(key);
            SceneView.RepaintAll();
        }

        private static IEnumerable<WorldPageKey> GetLoadedPageKeys()
        {
            foreach (var page in _store.LoadedPages) yield return page.Key;
        }

        private static void ClearChunks()
        {
            foreach (var chunk in Chunks.Values)
            {
                if (chunk == null) continue;
                var filter = chunk.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                    UnityEngine.Object.DestroyImmediate(filter.sharedMesh);
                UnityEngine.Object.DestroyImmediate(chunk);
            }
            Chunks.Clear();
        }

        private static void Dispose()
        {
            ClearChunks();
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
            if (_material != null) UnityEngine.Object.DestroyImmediate(_material);
            _root = null;
            _material = null;
            _store = null;
            Missing.Clear();
            _chunk = new Vector2Int(-1, -1);
        }
    }
}




