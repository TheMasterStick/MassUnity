using System;
using System.Collections.Generic;
using MassRPG.Core.Construction;
using MassRPG.Core.World;
using MassRPG.Server.Construction;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class PlayerPlotTests
    {
        [Test]
        public void Placement_ReservesFutureLargeEnvelopeAgainstNeighboringPlots()
        {
            var registry = new PlayerPlotRegistry(new OpenPlacementMap());
            var ownerA = Guid.NewGuid();
            var ownerB = Guid.NewGuid();
            var first = registry.TryPlace(
                Guid.NewGuid(), ownerA, WorldConstants.SurfacePlane,
                Rect(10, 10, 2, 2),
                Rect(8, 8, 6, 6));
            Assert.IsTrue(first.Success);

            var second = registry.TryPlace(
                Guid.NewGuid(), ownerB, WorldConstants.SurfacePlane,
                Rect(13, 10, 2, 2),
                Rect(12, 8, 6, 6));

            Assert.IsFalse(second.Success);
            Assert.AreEqual("plot_reservation_overlap", second.Code);
        }

        [Test]
        public void Blocklist_OverridesOpenPlotAndAssignedPermissions()
        {
            var registry = new PlayerPlotRegistry(new OpenPlacementMap());
            var owner = Guid.NewGuid();
            var visitor = Guid.NewGuid();
            var placement = registry.TryPlace(
                Guid.NewGuid(), owner, WorldConstants.SurfacePlane,
                Rect(20, 20, 2, 2),
                Rect(19, 19, 4, 4));
            Assert.IsTrue(placement.Success);
            var plot = placement.Plot;
            var farmhand = new PlotAccessRuleSet(Guid.NewGuid(), "Farmhand", PlotPermission.Enter | PlotPermission.UseOuterGate | PlotPermission.Farm);
            plot.UpsertRuleSet(farmhand);
            Assert.IsTrue(plot.AssignRuleSet(visitor, farmhand.RuleSetId));
            Assert.IsTrue(plot.CanEnter(visitor));
            Assert.IsTrue((plot.PermissionsFor(visitor) & PlotPermission.Farm) != 0);

            plot.Block(visitor);

            Assert.IsFalse(plot.CanEnter(visitor));
            Assert.AreEqual(PlotPermission.None, plot.PermissionsFor(visitor));
            Assert.IsFalse(registry.CanCharacterEnter(visitor, Loc(20, 20)));
            Assert.IsTrue(registry.CanCharacterEnter(visitor, Loc(18, 20)), "Blocklist only makes the actual claimed plot impassable, not nearby public ground.");
        }

        [Test]
        public void ProtectedGround_PreventsReservationEvenWhenSmallClaimWouldFit()
        {
            var blocked = new HashSet<GridLocation> { Loc(32, 32) };
            var registry = new PlayerPlotRegistry(new SelectivePlacementMap(blocked));

            var result = registry.TryPlace(
                Guid.NewGuid(), Guid.NewGuid(), WorldConstants.SurfacePlane,
                Rect(30, 30, 1, 1),
                Rect(29, 29, 4, 4));

            Assert.IsFalse(result.Success);
            Assert.AreEqual("protected_or_invalid_ground", result.Code);
        }

        private static IEnumerable<GridCoord> Rect(int x, int y, int width, int height)
        {
            for (var yy = 0; yy < height; yy++)
                for (var xx = 0; xx < width; xx++)
                    yield return new GridCoord(x + xx, y + yy);
        }

        private static GridLocation Loc(int x, int y)
            => new GridLocation(new GridCoord(x, y), WorldConstants.SurfacePlane, 0);

        private sealed class OpenPlacementMap : IPlotPlacementMap
        {
            public bool CanReserveForPlayerPlot(GridLocation location) => true;
        }

        private sealed class SelectivePlacementMap : IPlotPlacementMap
        {
            private readonly HashSet<GridLocation> _blocked;
            public SelectivePlacementMap(HashSet<GridLocation> blocked) { _blocked = blocked; }
            public bool CanReserveForPlayerPlot(GridLocation location) => !_blocked.Contains(location);
        }
    }
}
