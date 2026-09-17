using System;
using System.Collections.Generic;
using MassRPG.Core.Construction;
using MassRPG.Core.World;

namespace MassRPG.Server.Construction
{
    public sealed class ReclaimedPlotSnapshot
    {
        internal ReclaimedPlotSnapshot(
            Guid plotId,
            Guid ownerCharacterId,
            int plane,
            PlotTier tier,
            IReadOnlyList<GridCoord> claimedTiles,
            IReadOnlyList<GridCoord> reservedTiles,
            IReadOnlyList<PlacedBuildPiece> pieces,
            long reclaimedAtUnixMilliseconds)
        {
            PlotId = plotId;
            OwnerCharacterId = ownerCharacterId;
            Plane = plane;
            Tier = tier;
            ClaimedTiles = claimedTiles ?? Array.Empty<GridCoord>();
            ReservedTiles = reservedTiles ?? Array.Empty<GridCoord>();
            Pieces = pieces ?? Array.Empty<PlacedBuildPiece>();
            ReclaimedAtUnixMilliseconds = reclaimedAtUnixMilliseconds;
        }

        public Guid PlotId { get; }
        public Guid OwnerCharacterId { get; }
        public int Plane { get; }
        public PlotTier Tier { get; }
        public IReadOnlyList<GridCoord> ClaimedTiles { get; }
        public IReadOnlyList<GridCoord> ReservedTiles { get; }
        public IReadOnlyList<PlacedBuildPiece> Pieces { get; }
        public long ReclaimedAtUnixMilliseconds { get; }
    }

    public readonly struct PlotReclamationResult
    {
        private PlotReclamationResult(bool success, string code, ReclaimedPlotSnapshot snapshot)
        {
            Success = success;
            Code = code ?? string.Empty;
            Snapshot = snapshot;
        }

        public bool Success { get; }
        public string Code { get; }
        public ReclaimedPlotSnapshot Snapshot { get; }

        public static PlotReclamationResult Ok(ReclaimedPlotSnapshot snapshot)
            => new PlotReclamationResult(true, "ok", snapshot);

        public static PlotReclamationResult Fail(string code)
            => new PlotReclamationResult(false, code, null);
    }

    /// <summary>
    /// Explicit authoritative land reclamation for abandoned player housing. Advancing upkeep to
    /// Abandoned never destroys a plot by itself; a server operator/policy must call this service.
    /// The service snapshots the plot/building first, removes live structure state, then releases
    /// the future-Large reservation. The abandoned upkeep row intentionally remains as a compact
    /// tombstone/audit record until persistence policy decides when historical records are purged.
    /// </summary>
    public sealed class PlotReclamationService
    {
        private readonly PlayerPlotRegistry _plots;
        private readonly PlotUpkeepService _upkeep;
        private readonly PlotConstructionService _construction;

        public PlotReclamationService(
            PlayerPlotRegistry plots,
            PlotUpkeepService upkeep,
            PlotConstructionService construction)
        {
            _plots = plots ?? throw new ArgumentNullException(nameof(plots));
            _upkeep = upkeep ?? throw new ArgumentNullException(nameof(upkeep));
            _construction = construction ?? throw new ArgumentNullException(nameof(construction));
        }

        public PlotReclamationResult TryReclaim(Guid plotId, long nowUnixMilliseconds)
        {
            if (!_plots.TryGet(plotId, out var plot))
                return PlotReclamationResult.Fail("unknown_plot");
            if (!_upkeep.TryGet(plotId, out var upkeepState))
                return PlotReclamationResult.Fail("upkeep_not_registered");

            var status = _upkeep.Advance(plotId, nowUnixMilliseconds);
            if (status != PlotUpkeepStatus.Abandoned)
                return PlotReclamationResult.Fail("plot_not_abandoned");

            var claimed = CopyTiles(plot.ClaimedTiles);
            var reserved = CopyTiles(plot.ReservedTiles);
            var pieces = CopyPieces(plotId);
            var snapshot = new ReclaimedPlotSnapshot(
                plot.PlotId,
                plot.OwnerCharacterId,
                plot.Plane,
                plot.Tier,
                claimed,
                reserved,
                pieces,
                nowUnixMilliseconds);

            // Remove gameplay-active structures before releasing the land. This prevents a reclaimed
            // furnace/anvil/etc. from continuing to satisfy station queries after the plot is gone.
            _construction.RemoveReclaimedPlotState(plotId);
            if (!_plots.Remove(plotId))
                throw new InvalidOperationException("Plot disappeared during authoritative reclamation.");

            // Keep the upkeep state as an abandoned tombstone for audit/rollback. It can no longer
            // be paid because PlotUpkeepService checks the live registry before accepting payment.
            upkeepState.Status = PlotUpkeepStatus.Abandoned;
            if (!upkeepState.AbandonedAtUnixMilliseconds.HasValue)
                upkeepState.AbandonedAtUnixMilliseconds = nowUnixMilliseconds;

            return PlotReclamationResult.Ok(snapshot);
        }

        private IReadOnlyList<PlacedBuildPiece> CopyPieces(Guid plotId)
        {
            if (!_construction.TryGetState(plotId, out var state))
                return Array.Empty<PlacedBuildPiece>();
            var pieces = new List<PlacedBuildPiece>();
            foreach (var piece in state.Pieces) pieces.Add(piece);
            return pieces.ToArray();
        }

        private static IReadOnlyList<GridCoord> CopyTiles(IEnumerable<GridCoord> source)
        {
            var tiles = new List<GridCoord>();
            foreach (var tile in source) tiles.Add(tile);
            return tiles.ToArray();
        }
    }
}
