using System;
using System.Collections.Generic;
using MassRPG.Core.Construction;
using MassRPG.Core.World;

namespace MassRPG.Server.Construction
{
    public interface IPlotPlacementMap
    {
        bool CanReserveForPlayerPlot(GridLocation location);
    }

    /// <summary>
    /// Authoritative plot registry. Placement checks the full future Large-tier reservation, not
    /// merely the currently owned Small footprint, preventing neighboring plots from blocking each
    /// other's legitimate upgrades later. Upgrade shapes remain caller/data driven so final Small,
    /// Medium and Large dimensions can change without rewriting persistence.
    /// </summary>
    public sealed class PlayerPlotRegistry
    {
        private readonly Dictionary<Guid, PlayerPlotState> _plots = new Dictionary<Guid, PlayerPlotState>();
        private readonly Dictionary<PlotTileKey, Guid> _reservedByTile = new Dictionary<PlotTileKey, Guid>();
        private readonly IPlotPlacementMap _placementMap;

        public PlayerPlotRegistry(IPlotPlacementMap placementMap)
        {
            _placementMap = placementMap ?? throw new ArgumentNullException(nameof(placementMap));
        }

        public IEnumerable<PlayerPlotState> All => _plots.Values;

        public bool TryGet(Guid plotId, out PlayerPlotState plot) => _plots.TryGetValue(plotId, out plot);

        public bool TryGetPlotAt(GridLocation location, out PlayerPlotState plot)
        {
            foreach (var candidate in _plots.Values)
            {
                if (candidate.Plane == location.Plane && candidate.Contains(location.Tile))
                {
                    plot = candidate;
                    return true;
                }
            }
            plot = null;
            return false;
        }

        public bool CanCharacterEnter(Guid characterId, GridLocation location)
        {
            if (!TryGetPlotAt(location, out var plot)) return true;
            return plot.CanEnter(characterId);
        }

        public PlotPlacementResult TryPlace(
            Guid plotId,
            Guid ownerCharacterId,
            int plane,
            IEnumerable<GridCoord> initialClaimedTiles,
            IEnumerable<GridCoord> maximumReservedTiles)
        {
            if (_plots.ContainsKey(plotId)) return PlotPlacementResult.Fail("duplicate_plot");
            if (initialClaimedTiles == null || maximumReservedTiles == null)
                return PlotPlacementResult.Fail("missing_footprint");

            var claimed = new HashSet<GridCoord>(initialClaimedTiles);
            var reserved = new HashSet<GridCoord>(maximumReservedTiles);
            if (claimed.Count == 0 || reserved.Count == 0) return PlotPlacementResult.Fail("empty_footprint");
            if (!reserved.IsSupersetOf(claimed)) return PlotPlacementResult.Fail("claim_outside_reservation");

            foreach (var tile in reserved)
            {
                if (!WorldConstants.IsInsideWorld(tile)) return PlotPlacementResult.Fail("out_of_bounds");
                var location = new GridLocation(tile, plane, 0);
                if (!_placementMap.CanReserveForPlayerPlot(location)) return PlotPlacementResult.Fail("protected_or_invalid_ground");
                if (_reservedByTile.ContainsKey(new PlotTileKey(plane, tile))) return PlotPlacementResult.Fail("plot_reservation_overlap");
            }

            var plot = new PlayerPlotState(plotId, ownerCharacterId, plane);
            plot.InitializeReservedTiles(reserved);
            plot.SetClaimedTiles(claimed);
            _plots.Add(plotId, plot);
            foreach (var tile in reserved) _reservedByTile.Add(new PlotTileKey(plane, tile), plotId);
            return PlotPlacementResult.Ok(plot);
        }

        public PlotUpgradeResult TryUpgrade(
            Guid plotId,
            PlotTier targetTier,
            IEnumerable<GridCoord> targetClaimedTiles)
        {
            if (!_plots.TryGetValue(plotId, out var plot)) return PlotUpgradeResult.Fail("unknown_plot");
            if (targetClaimedTiles == null) return PlotUpgradeResult.Fail("missing_footprint");
            if ((int)targetTier != (int)plot.Tier + 1) return PlotUpgradeResult.Fail("invalid_tier_progression");

            var target = new HashSet<GridCoord>(targetClaimedTiles);
            if (target.Count == 0) return PlotUpgradeResult.Fail("empty_footprint");
            foreach (var existing in plot.ClaimedTiles)
                if (!target.Contains(existing)) return PlotUpgradeResult.Fail("upgrade_cannot_remove_claimed_land");
            foreach (var tile in target)
                if (!plot.Reserves(tile)) return PlotUpgradeResult.Fail("upgrade_outside_reservation");

            plot.ApplyUpgrade(targetTier, target);
            return PlotUpgradeResult.Ok(plot);
        }

        public bool Remove(Guid plotId)
        {
            if (!_plots.TryGetValue(plotId, out var plot)) return false;
            foreach (var tile in plot.ReservedTiles) _reservedByTile.Remove(new PlotTileKey(plot.Plane, tile));
            _plots.Remove(plotId);
            return true;
        }

        private readonly struct PlotTileKey : IEquatable<PlotTileKey>
        {
            public PlotTileKey(int plane, GridCoord tile) { Plane = plane; Tile = tile; }
            public int Plane { get; }
            public GridCoord Tile { get; }
            public bool Equals(PlotTileKey other) => Plane == other.Plane && Tile == other.Tile;
            public override bool Equals(object obj) => obj is PlotTileKey other && Equals(other);
            public override int GetHashCode() => unchecked((Plane * 397) ^ Tile.GetHashCode());
        }
    }

    public readonly struct PlotPlacementResult
    {
        private PlotPlacementResult(bool success, string code, PlayerPlotState plot)
        {
            Success = success;
            Code = code ?? string.Empty;
            Plot = plot;
        }

        public bool Success { get; }
        public string Code { get; }
        public PlayerPlotState Plot { get; }

        public static PlotPlacementResult Ok(PlayerPlotState plot) => new PlotPlacementResult(true, "ok", plot);
        public static PlotPlacementResult Fail(string code) => new PlotPlacementResult(false, code, null);
    }

    public readonly struct PlotUpgradeResult
    {
        private PlotUpgradeResult(bool success, string code, PlayerPlotState plot)
        {
            Success = success;
            Code = code ?? string.Empty;
            Plot = plot;
        }

        public bool Success { get; }
        public string Code { get; }
        public PlayerPlotState Plot { get; }

        public static PlotUpgradeResult Ok(PlayerPlotState plot) => new PlotUpgradeResult(true, "ok", plot);
        public static PlotUpgradeResult Fail(string code) => new PlotUpgradeResult(false, code, null);
    }
}
