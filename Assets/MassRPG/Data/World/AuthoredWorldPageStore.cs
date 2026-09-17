using System;
using System.Collections.Generic;
using MassRPG.Core.Content;
using MassRPG.Core.World;

namespace MassRPG.Data.World
{
    /// <summary>
    /// Sparse loaded-page world view. Semantic regions/POIs remain separate data layers; pages are
    /// only storage/streaming units for the exact logical tile surface.
    /// </summary>
    public sealed class AuthoredWorldPageStore : IGridTraversalMap, IRangedLineOfSightMap
    {
        private readonly Dictionary<WorldPageKey, AuthoredWorldPage> _pages = new Dictionary<WorldPageKey, AuthoredWorldPage>();
        private readonly ContentId _defaultGround;

        public AuthoredWorldPageStore(ContentId defaultGround, int pageSize = WorldConstants.DefaultStoragePageSize)
        {
            if (defaultGround.IsEmpty) throw new ArgumentException("A default ground id is required.", nameof(defaultGround));
            if (pageSize <= 0) throw new ArgumentOutOfRangeException(nameof(pageSize));
            _defaultGround = defaultGround;
            PageSize = pageSize;
        }

        public int PageSize { get; }
        public int LoadedPageCount => _pages.Count;
        public IEnumerable<AuthoredWorldPage> LoadedPages => _pages.Values;

        public AuthoredWorldPage GetOrCreatePage(GridLocation location)
        {
            if (!WorldAddressing.TryResolve(location, out var address, PageSize))
                throw new ArgumentOutOfRangeException(nameof(location), "Location is outside the authored world.");

            if (!_pages.TryGetValue(address.Key, out var page))
            {
                page = new AuthoredWorldPage(address.Key, PageSize, _defaultGround);
                _pages.Add(address.Key, page);
            }

            return page;
        }

        /// <summary>
        /// Installs a decoded page into the sparse store. Used by editor/runtime streaming so the
        /// complete 180k world never has to be loaded at once.
        /// </summary>
        public void ImportPage(AuthoredWorldPage page, bool replaceExisting = true)
        {
            if (page == null) throw new ArgumentNullException(nameof(page));
            if (page.PageSize != PageSize)
                throw new InvalidOperationException($"Page size {page.PageSize} does not match store page size {PageSize}.");
            if (_pages.ContainsKey(page.Key) && !replaceExisting)
                throw new InvalidOperationException($"World page '{page.Key}' is already loaded.");
            _pages[page.Key] = page;
        }

        public bool TryGetPage(WorldPageKey key, out AuthoredWorldPage page) => _pages.TryGetValue(key, out page);
        public bool UnloadPage(WorldPageKey key) => _pages.Remove(key);

        public bool TryGetCell(GridLocation location, out AuthoredTileCell cell)
        {
            if (!WorldAddressing.TryResolve(location, out var address, PageSize)
                || !_pages.TryGetValue(address.Key, out var page))
            {
                cell = default;
                return false;
            }

            cell = page.GetCell(address.LocalX, address.LocalY);
            return true;
        }

        public void SetCell(GridLocation location, AuthoredTileCell cell)
        {
            if (!WorldAddressing.TryResolve(location, out var address, PageSize))
                throw new ArgumentOutOfRangeException(nameof(location));
            GetOrCreatePage(location).SetCell(address.LocalX, address.LocalY, cell);
        }

        public void SetCardinalEdge(
            GridLocation from,
            GridLocation to,
            bool movementBlocked,
            bool lineOfSightBlocked,
            bool elevationTransition)
        {
            if (!from.SameLayer(to)) throw new ArgumentException("Cardinal edges cannot cross planes/storeys.");
            var fromEdge = CardinalEdges.Between(from.Tile, to.Tile);
            var toEdge = CardinalEdges.Opposite(fromEdge);
            UpdateEdge(from, fromEdge, movementBlocked, lineOfSightBlocked, elevationTransition);
            UpdateEdge(to, toEdge, movementBlocked, lineOfSightBlocked, elevationTransition);
        }

        public bool IsWalkable(GridLocation location)
        {
            if (!TryGetCell(location, out var cell)) return false;
            return (cell.Flags & (TileFlags.MovementBlocked | TileFlags.Water | TileFlags.DeepWater)) == 0;
        }

        /// <summary>
        /// Base terrain buildability. Water is intrinsically non-buildable and therefore does not
        /// need the editor to duplicate a NoBuild flag merely to enforce construction rules.
        /// Semantic no-build areas are applied by the authoritative plot-placement adapter.
        /// </summary>
        public bool IsBuildable(GridLocation location)
        {
            if (!TryGetCell(location, out var cell)) return false;
            return (cell.Flags & (TileFlags.NoBuild | TileFlags.Water | TileFlags.DeepWater)) == 0;
        }

        public int GetLogicalElevation(GridLocation location)
            => TryGetCell(location, out var cell) ? cell.Elevation : 0;

        public bool CanTraverseCardinalEdge(GridLocation from, GridLocation to)
        {
            if (!from.SameLayer(to)) return false;
            CardinalEdgeMask fromEdge;
            try { fromEdge = CardinalEdges.Between(from.Tile, to.Tile); }
            catch (ArgumentException) { return false; }

            if (!IsWalkable(to)) return false;
            if (!TryGetCell(from, out var fromCell) || !TryGetCell(to, out var toCell)) return false;
            var toEdge = CardinalEdges.Opposite(fromEdge);
            if ((fromCell.MovementBlockedEdges & fromEdge) != 0 || (toCell.MovementBlockedEdges & toEdge) != 0)
                return false;

            if (fromCell.Elevation == toCell.Elevation) return true;
            return (fromCell.ElevationTransitionEdges & fromEdge) != 0
                && (toCell.ElevationTransitionEdges & toEdge) != 0;
        }

        public bool IsRangedLineOfSightBlockingTile(GridLocation location)
            => !TryGetCell(location, out var cell) || (cell.Flags & TileFlags.RangedLineOfSightBlocked) != 0;

        public bool IsRangedLineOfSightBlockedCardinalEdge(GridLocation from, GridLocation to)
        {
            if (!from.SameLayer(to)) return true;
            CardinalEdgeMask fromEdge;
            try { fromEdge = CardinalEdges.Between(from.Tile, to.Tile); }
            catch (ArgumentException) { return true; }

            if (!TryGetCell(from, out var fromCell) || !TryGetCell(to, out var toCell)) return true;
            var toEdge = CardinalEdges.Opposite(fromEdge);
            return (fromCell.LineOfSightBlockedEdges & fromEdge) != 0
                || (toCell.LineOfSightBlockedEdges & toEdge) != 0;
        }

        public bool BlocksRangedLineOfSight(GridLocation from, GridLocation to)
            => IsRangedLineOfSightBlockingTile(to) || IsRangedLineOfSightBlockedCardinalEdge(from, to);

        private void UpdateEdge(
            GridLocation location,
            CardinalEdgeMask edge,
            bool movementBlocked,
            bool lineOfSightBlocked,
            bool elevationTransition)
        {
            if (!WorldAddressing.TryResolve(location, out var address, PageSize))
                throw new ArgumentOutOfRangeException(nameof(location));
            var page = GetOrCreatePage(location);
            var cell = page.GetCell(address.LocalX, address.LocalY);
            var movement = SetBit(cell.MovementBlockedEdges, edge, movementBlocked);
            var los = SetBit(cell.LineOfSightBlockedEdges, edge, lineOfSightBlocked);
            var transitions = SetBit(cell.ElevationTransitionEdges, edge, elevationTransition);
            page.SetEdgeMasks(address.LocalX, address.LocalY, movement, los, transitions);
        }

        private static CardinalEdgeMask SetBit(CardinalEdgeMask current, CardinalEdgeMask edge, bool enabled)
            => enabled ? current | edge : current & ~edge;
    }
}
