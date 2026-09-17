using System;
using System.Collections.Generic;
using MassRPG.Client.Actors;
using MassRPG.Core.Content;
using MassRPG.Core.World;
using MassRPG.Data.World;
using UnityEngine;

namespace MassRPG.Client.World
{
    /// <summary>
    /// Streams disposable 64x64 terrain views around an authoritative logical actor. The authored
    /// 512x512 storage pages are loaded into a sparse AuthoredWorldPageStore only while nearby.
    /// This is the first client-side realization of the planned 5x5-ish chunk window rather than a
    /// giant Unity Terrain or one-GameObject-per-tile world.
    /// </summary>
    public sealed class LogicalTerrainChunkStreamer : MonoBehaviour
    {
        [SerializeField] private GridPresentationSpace presentationSpace;
        [SerializeField] private LogicalActorView focusActor;
        [SerializeField] private Material terrainMaterial;
        [SerializeField, Range(1, 4)] private int chunkRadius = 2;
        [SerializeField] private bool generateMeshColliders = true;
        [SerializeField] private bool floatingOrigin = true;
        [SerializeField, Min(128)] private int originShiftThresholdTiles = 1024;
        [SerializeField] private string defaultGroundId = "ground.default";
        [SerializeField] private string worldDataRootOverride = string.Empty;
        [SerializeField] private bool logMissingOrInvalidPages;

        private readonly Dictionary<RenderChunkKey, LogicalTerrainChunkView> _views = new Dictionary<RenderChunkKey, LogicalTerrainChunkView>();
        private readonly HashSet<WorldPageKey> _loadedPages = new HashSet<WorldPageKey>();
        private readonly HashSet<WorldPageKey> _knownMissingPages = new HashSet<WorldPageKey>();
        private AuthoredWorldPageStore _store;
        private RenderChunkKey _focusChunk;
        private bool _hasFocusChunk;
        private Material _runtimeFallbackMaterial;
        private bool _ownsStore = true;

        public AuthoredWorldPageStore LoadedWorld => _store;
        public int ActiveChunkCount => _views.Count;
        public int LoadedStoragePageCount => _store?.LoadedPageCount ?? 0;

        private void Awake() => EnsureInitialized();
        private void Update() => SyncToFocusNow(false);

        private void OnDestroy()
        {
            ClearViews();
            if (_runtimeFallbackMaterial != null)
            {
                if (Application.isPlaying) Destroy(_runtimeFallbackMaterial);
                else DestroyImmediate(_runtimeFallbackMaterial);
            }
        }

        public void Configure(GridPresentationSpace space, LogicalActorView actor, Material material = null)
            => Configure(space, actor, null, material);

        /// <summary>
        /// Uses an existing sparse world store when supplied. This is important for local-authority
        /// testing: pathfinding/combat and terrain streaming then observe the exact same loaded tile
        /// data rather than maintaining separate copies that could disagree at page boundaries.
        /// </summary>
        public void Configure(
            GridPresentationSpace space,
            LogicalActorView actor,
            AuthoredWorldPageStore sharedWorld,
            Material material = null)
        {
            presentationSpace = space;
            focusActor = actor;
            if (material != null) terrainMaterial = material;

            if (sharedWorld != null && !ReferenceEquals(_store, sharedWorld))
            {
                ClearViews();
                _store = sharedWorld;
                _ownsStore = false;
                RebuildLoadedPageIndex();
                _knownMissingPages.Clear();
                _hasFocusChunk = false;
            }
            else
            {
                EnsureInitialized();
            }

            SyncToFocusNow(true);
        }

        /// <summary>
        /// Synchronizes the loaded page/chunk window immediately instead of waiting for Unity's next
        /// component Update ordering. Authoritative test sessions call this directly after applying
        /// a new logical actor position, which avoids one-frame page-boundary disagreement.
        /// </summary>
        public void SyncToFocusNow(bool rebuildExisting = false)
        {
            if (focusActor == null || !focusActor.HasAuthoritativeState) return;
            EnsureInitialized();
            MaybeShiftOrigin(focusActor.LogicalLocation.Tile);

            var chunk = RenderChunkKey.FromLocation(focusActor.LogicalLocation);
            var movedChunk = !_hasFocusChunk || chunk != _focusChunk;
            if (!movedChunk && !rebuildExisting) return;

            _focusChunk = chunk;
            _hasFocusChunk = true;
            RefreshAroundFocus(rebuildExisting);
        }

        /// <summary>
        /// Manual development refresh after editing world page files while the scene is running.
        /// Missing-page cache is cleared and all visible meshes are rebuilt from the currently loaded
        /// authoritative store. An internally owned store also reloads its visible files from disk.
        /// </summary>
        public void RefreshNow()
        {
            EnsureInitialized();
            _knownMissingPages.Clear();
            SyncToFocusNow(true);
        }

        private void EnsureInitialized()
        {
            if (_store != null) return;
            if (!ContentId.TryCreate(defaultGroundId, out var ground)) ground = new ContentId("ground.default");
            _store = new AuthoredWorldPageStore(ground, WorldConstants.DefaultStoragePageSize);
            _ownsStore = true;
            _loadedPages.Clear();
        }

        private void RebuildLoadedPageIndex()
        {
            _loadedPages.Clear();
            if (_store == null) return;
            foreach (var page in _store.LoadedPages) _loadedPages.Add(page.Key);
        }

        private void RefreshAroundFocus(bool rebuildExisting)
        {
            if (!_hasFocusChunk || presentationSpace == null) return;

            var wantedChunks = new HashSet<RenderChunkKey>();
            for (var dy = -chunkRadius; dy <= chunkRadius; dy++)
            {
                for (var dx = -chunkRadius; dx <= chunkRadius; dx++)
                {
                    var key = new RenderChunkKey(_focusChunk.X + dx, _focusChunk.Y + dy, _focusChunk.Plane, _focusChunk.Storey);
                    if (key.IntersectsWorld()) wantedChunks.Add(key);
                }
            }

            var wantedPages = new HashSet<WorldPageKey>();
            foreach (var chunk in wantedChunks) AddPagesNeededByChunk(chunk, wantedPages);
            LoadWantedPages(wantedPages, rebuildExisting);
            RemoveUnwantedChunks(wantedChunks);
            CreateOrRebuildWantedChunks(wantedChunks, rebuildExisting);
            UnloadUnwantedPages(wantedPages);
        }

        private void LoadWantedPages(HashSet<WorldPageKey> wantedPages, bool forceReload)
        {
            foreach (var key in wantedPages)
            {
                // Shared authoritative stores may contain mutable runtime state in the future. Do not
                // throw their existing pages away merely because the presentation requested a rebuild.
                if (forceReload && _ownsStore && _loadedPages.Contains(key))
                {
                    _store.UnloadPage(key);
                    _loadedPages.Remove(key);
                }
                if (_loadedPages.Contains(key) || _knownMissingPages.Contains(key)) continue;

                if (RepositoryWorldPageSource.TryLoad(
                    key,
                    _store.PageSize,
                    out var page,
                    out var error,
                    string.IsNullOrWhiteSpace(worldDataRootOverride) ? null : worldDataRootOverride))
                {
                    _store.ImportPage(page, true);
                    _loadedPages.Add(key);
                }
                else
                {
                    _knownMissingPages.Add(key);
                    if (logMissingOrInvalidPages && !string.IsNullOrWhiteSpace(error))
                        Debug.LogWarning("MassRPG world page '" + key + "' could not be loaded: " + error, this);
                }
            }
        }

        private void CreateOrRebuildWantedChunks(HashSet<RenderChunkKey> wantedChunks, bool rebuildExisting)
        {
            foreach (var key in wantedChunks)
            {
                var primaryPage = PageForTile(key.StartTile, key.Plane, key.Storey);
                if (!_loadedPages.Contains(primaryPage)) continue;

                if (_views.TryGetValue(key, out var existing))
                {
                    if (rebuildExisting) existing.Rebuild(_store, key.X, key.Y, key.Plane, key.Storey);
                    continue;
                }

                var gameObject = new GameObject("Terrain Chunk " + key.X + "," + key.Y + " p" + key.Plane + " s" + key.Storey);
                gameObject.transform.SetParent(transform, true);
                gameObject.AddComponent<MeshFilter>();
                gameObject.AddComponent<MeshRenderer>();
                if (generateMeshColliders) gameObject.AddComponent<MeshCollider>();
                var view = gameObject.AddComponent<LogicalTerrainChunkView>();
                view.Configure(presentationSpace, ResolveTerrainMaterial(), generateMeshColliders);
                view.Rebuild(_store, key.X, key.Y, key.Plane, key.Storey);
                _views.Add(key, view);
            }
        }

        private void RemoveUnwantedChunks(HashSet<RenderChunkKey> wantedChunks)
        {
            if (_views.Count == 0) return;
            var remove = new List<RenderChunkKey>();
            foreach (var pair in _views) if (!wantedChunks.Contains(pair.Key)) remove.Add(pair.Key);
            for (var i = 0; i < remove.Count; i++)
            {
                var key = remove[i];
                var view = _views[key];
                _views.Remove(key);
                if (view != null)
                {
                    if (Application.isPlaying) Destroy(view.gameObject);
                    else DestroyImmediate(view.gameObject);
                }
            }
        }

        private void UnloadUnwantedPages(HashSet<WorldPageKey> wantedPages)
        {
            if (_loadedPages.Count == 0) return;
            var remove = new List<WorldPageKey>();
            foreach (var page in _loadedPages) if (!wantedPages.Contains(page)) remove.Add(page);
            for (var i = 0; i < remove.Count; i++)
            {
                _store.UnloadPage(remove[i]);
                _loadedPages.Remove(remove[i]);
            }
        }

        private void ClearViews()
        {
            foreach (var pair in _views)
            {
                if (pair.Value == null) continue;
                if (Application.isPlaying) Destroy(pair.Value.gameObject);
                else DestroyImmediate(pair.Value.gameObject);
            }
            _views.Clear();
        }

        private void AddPagesNeededByChunk(RenderChunkKey chunk, HashSet<WorldPageKey> target)
        {
            // One-tile border lets cliff faces inspect the neighbour cell at storage-page boundaries.
            var start = chunk.StartTile;
            var endX = start.X + WorldConstants.DefaultRenderChunkSize - 1;
            var endY = start.Y + WorldConstants.DefaultRenderChunkSize - 1;
            AddPageIfInside(new GridCoord(start.X - 1, start.Y - 1), chunk.Plane, chunk.Storey, target);
            AddPageIfInside(new GridCoord(endX + 1, start.Y - 1), chunk.Plane, chunk.Storey, target);
            AddPageIfInside(new GridCoord(start.X - 1, endY + 1), chunk.Plane, chunk.Storey, target);
            AddPageIfInside(new GridCoord(endX + 1, endY + 1), chunk.Plane, chunk.Storey, target);
            AddPageIfInside(start, chunk.Plane, chunk.Storey, target);
        }

        private void AddPageIfInside(GridCoord tile, int plane, int storey, HashSet<WorldPageKey> target)
        {
            if (!WorldConstants.IsInsideWorld(tile)) return;
            target.Add(PageForTile(tile, plane, storey));
        }

        private WorldPageKey PageForTile(GridCoord tile, int plane, int storey)
            => new WorldPageKey(
                new WorldPageCoord(tile.X / _store.PageSize, tile.Y / _store.PageSize),
                plane,
                storey);

        private void MaybeShiftOrigin(GridCoord focusTile)
        {
            if (!floatingOrigin || presentationSpace == null) return;
            var origin = presentationSpace.OriginTile;
            var dx = Math.Abs(focusTile.X - origin.X);
            var dy = Math.Abs(focusTile.Y - origin.Y);
            if (dx < originShiftThresholdTiles && dy < originShiftThresholdTiles) return;

            var size = WorldConstants.DefaultRenderChunkSize;
            var snapped = new GridCoord((focusTile.X / size) * size, (focusTile.Y / size) * size);
            presentationSpace.SetOrigin(snapped);
        }

        private Material ResolveTerrainMaterial()
        {
            if (terrainMaterial != null) return terrainMaterial;
            if (_runtimeFallbackMaterial != null) return _runtimeFallbackMaterial;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return null;
            _runtimeFallbackMaterial = new Material(shader) { name = "MassRPG Runtime Terrain Fallback" };
            return _runtimeFallbackMaterial;
        }

        private readonly struct RenderChunkKey : IEquatable<RenderChunkKey>
        {
            public RenderChunkKey(int x, int y, int plane, int storey)
            {
                X = x;
                Y = y;
                Plane = plane;
                Storey = storey;
            }

            public int X { get; }
            public int Y { get; }
            public int Plane { get; }
            public int Storey { get; }
            public GridCoord StartTile => new GridCoord(X * WorldConstants.DefaultRenderChunkSize, Y * WorldConstants.DefaultRenderChunkSize);

            public static RenderChunkKey FromLocation(GridLocation location)
                => new RenderChunkKey(
                    location.Tile.X / WorldConstants.DefaultRenderChunkSize,
                    location.Tile.Y / WorldConstants.DefaultRenderChunkSize,
                    location.Plane,
                    location.Storey);

            public bool IntersectsWorld()
            {
                var start = StartTile;
                var end = new GridCoord(
                    start.X + WorldConstants.DefaultRenderChunkSize - 1,
                    start.Y + WorldConstants.DefaultRenderChunkSize - 1);
                return WorldConstants.IsInsideWorld(start) || WorldConstants.IsInsideWorld(end);
            }

            public bool Equals(RenderChunkKey other)
                => X == other.X && Y == other.Y && Plane == other.Plane && Storey == other.Storey;
            public override bool Equals(object obj) => obj is RenderChunkKey other && Equals(other);
            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = X;
                    hash = (hash * 397) ^ Y;
                    hash = (hash * 397) ^ Plane;
                    hash = (hash * 397) ^ Storey;
                    return hash;
                }
            }
            public static bool operator ==(RenderChunkKey left, RenderChunkKey right) => left.Equals(right);
            public static bool operator !=(RenderChunkKey left, RenderChunkKey right) => !left.Equals(right);
        }
    }
}
