using System;
using System.Reflection;
using MassRPG.Client.Testing;
using MassRPG.Client.World;
using MassRPG.Core.Content;
using MassRPG.Core.World;
using MassRPG.Data.World;
using UnityEditor;
using UnityEngine;

namespace MassRPG.Editor.World
{
    /// <summary>
    /// Bridges an editor coordinate into an in-process authoritative test session. The request is
    /// kept in Unity SessionState so it survives the editor's play-mode domain reload but never
    /// contaminates canonical MassRPG world data or a player save.
    /// </summary>
    [InitializeOnLoad]
    public static class MassRPGPlayFromHereCoordinator
    {
        private const string PendingKey = "MassRPG.PlayFromHere.Pending";
        private const string XKey = "MassRPG.PlayFromHere.X";
        private const string YKey = "MassRPG.PlayFromHere.Y";
        private const string PlaneKey = "MassRPG.PlayFromHere.Plane";
        private const string StoreyKey = "MassRPG.PlayFromHere.Storey";

        static MassRPGPlayFromHereCoordinator()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public static bool HasPendingRequest => SessionState.GetBool(PendingKey, false);

        [MenuItem("MassRPG/Play From Here %#p", priority = 3)]
        public static void StartFromOpenWorldEditor()
        {
            if (!TryGetOpenWorldEditorLocation(out var editor, out var location))
            {
                EditorUtility.DisplayDialog(
                    "MassRPG Play From Here",
                    "Open the MassRPG 1x1 World Editor first. Its current X/Y/Plane/Floor coordinate is used as the launch point.",
                    "OK");
                return;
            }

            // Play From Here should test exactly what the designer currently sees. Save any dirty
            // pages first; this calls the World Editor's existing production save rather than
            // inventing a second persistence path for the test harness.
            var save = typeof(MassRPGWorldEditorWindow).GetMethod(
                "SaveProduction",
                BindingFlags.Instance | BindingFlags.NonPublic);
            save?.Invoke(editor, null);
            Start(location);
        }

        [MenuItem("MassRPG/Play From Here %#p", true)]
        private static bool ValidateStartFromOpenWorldEditor()
            => !EditorApplication.isPlayingOrWillChangePlaymode
                && Resources.FindObjectsOfTypeAll<MassRPGWorldEditorWindow>().Length > 0;

        public static void Start(GridLocation location)
        {
            if (!WorldConstants.IsInsideWorld(location.Tile))
                throw new ArgumentOutOfRangeException(nameof(location));
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            SessionState.SetBool(PendingKey, true);
            SessionState.SetInt(XKey, location.Tile.X);
            SessionState.SetInt(YKey, location.Tile.Y);
            SessionState.SetInt(PlaneKey, location.Plane);
            SessionState.SetInt(StoreyKey, location.Storey);
            EditorApplication.EnterPlaymode();
        }

        public static void CancelPending()
        {
            SessionState.EraseBool(PendingKey);
            SessionState.EraseInt(XKey);
            SessionState.EraseInt(YKey);
            SessionState.EraseInt(PlaneKey);
            SessionState.EraseInt(StoreyKey);
        }

        private static bool TryGetOpenWorldEditorLocation(
            out MassRPGWorldEditorWindow editor,
            out GridLocation location)
        {
            editor = EditorWindow.focusedWindow as MassRPGWorldEditorWindow;
            if (editor == null)
            {
                var windows = Resources.FindObjectsOfTypeAll<MassRPGWorldEditorWindow>();
                editor = windows.Length > 0 ? windows[0] : null;
            }

            if (editor == null)
            {
                location = default;
                return false;
            }

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var type = typeof(MassRPGWorldEditorWindow);
            var centerX = type.GetField("_centerX", flags);
            var centerY = type.GetField("_centerY", flags);
            var plane = type.GetField("_plane", flags);
            var storey = type.GetField("_storey", flags);
            if (centerX == null || centerY == null || plane == null || storey == null)
            {
                location = default;
                return false;
            }

            var x = Mathf.Clamp(Mathf.RoundToInt((float)centerX.GetValue(editor)), 0, WorldConstants.WorldWidthTiles - 1);
            var y = Mathf.Clamp(Mathf.RoundToInt((float)centerY.GetValue(editor)), 0, WorldConstants.WorldHeightTiles - 1);
            location = new GridLocation(
                new GridCoord(x, y),
                (int)plane.GetValue(editor),
                Math.Max(0, (int)storey.GetValue(editor)));
            return true;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode && HasPendingRequest)
                return;

            if (change == PlayModeStateChange.EnteredPlayMode && HasPendingRequest)
            {
                try
                {
                    StartRuntimeSession(ReadLocation());
                }
                catch (Exception ex)
                {
                    Debug.LogError("MassRPG Play From Here failed to initialize: " + ex);
                }
                finally
                {
                    CancelPending();
                }
                return;
            }

            if (change == PlayModeStateChange.EnteredEditMode)
                CancelPending();
        }

        private static GridLocation ReadLocation()
            => new GridLocation(
                new GridCoord(SessionState.GetInt(XKey, WorldConstants.WorldWidthTiles / 2),
                    SessionState.GetInt(YKey, WorldConstants.WorldHeightTiles / 2)),
                SessionState.GetInt(PlaneKey, WorldConstants.SurfacePlane),
                SessionState.GetInt(StoreyKey, 0));

        private static void StartRuntimeSession(GridLocation spawn)
        {
            var store = LoadNearbyPages(spawn);
            var root = new GameObject("MassRPG Play From Here");
            var session = root.AddComponent<LocalPlayTestSession>();
            session.Initialize(store, spawn);
            Debug.Log($"MassRPG Play From Here started at {spawn.Tile.X}, {spawn.Tile.Y}, plane {spawn.Plane}, floor {spawn.Storey}. Loaded {store.LoadedPageCount} authored page(s); runtime streamer will continue page loading as the player moves.");
        }

        private static AuthoredWorldPageStore LoadNearbyPages(GridLocation spawn)
        {
            var store = new AuthoredWorldPageStore(new ContentId("ground.unpainted"));
            var pageSize = store.PageSize;
            var centerPageX = spawn.Tile.X / pageSize;
            var centerPageY = spawn.Tile.Y / pageSize;
            var maxPageX = (WorldConstants.WorldWidthTiles - 1) / pageSize;
            var maxPageY = (WorldConstants.WorldHeightTiles - 1) / pageSize;

            // Seed a margin before LocalGameAuthority is constructed so the first click can pathfind
            // immediately. Once Play Mode is alive, LogicalTerrainChunkStreamer loads/unloads the same
            // shared store through RepositoryWorldPageSource as the authoritative player moves.
            for (var py = Math.Max(0, centerPageY - 1); py <= Math.Min(maxPageY, centerPageY + 1); py++)
            {
                for (var px = Math.Max(0, centerPageX - 1); px <= Math.Min(maxPageX, centerPageX + 1); px++)
                {
                    var key = new WorldPageKey(new WorldPageCoord(px, py), spawn.Plane, spawn.Storey);
                    if (!RepositoryWorldPageSource.TryLoad(key, pageSize, out var page, out var error))
                    {
                        if (!string.IsNullOrWhiteSpace(error))
                            Debug.LogWarning("MassRPG Play From Here skipped invalid world page " + key + ": " + error);
                        continue;
                    }
                    store.ImportPage(page);
                }
            }
            return store;
        }
    }
}
