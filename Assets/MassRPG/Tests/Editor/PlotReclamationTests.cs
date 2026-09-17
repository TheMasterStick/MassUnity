using System;
using System.Collections.Generic;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Core.Skills;
using MassRPG.Core.World;
using MassRPG.Data.Construction;
using MassRPG.Data.Items;
using MassRPG.Server.Construction;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class PlotReclamationTests
    {
        [Test]
        public void ReclamationIsExplicitAndReleasesStructuresAndReservedLandOnlyAfterAbandonment()
        {
            const long day = 24L * 60L * 60L * 1000L;
            var map = new OpenPlacementMap();
            var registry = new PlayerPlotRegistry(map);
            var owner = Guid.NewGuid();
            var plotId = Guid.NewGuid();
            var initial = Rect(10, 10, 2, 2);
            var reserved = Rect(8, 8, 6, 6);
            var placement = registry.TryPlace(plotId, owner, WorldConstants.SurfacePlane, initial, reserved);
            Assert.IsTrue(placement.Success);

            var items = MigrationSeedItemCatalog.Create();
            var player = new PlayerState(owner, "Builder") { Location = Loc(10, 11) };
            player.Skills.SetXp(SkillId.Construction, SkillProgression.XpForLevel(20));
            Give(player, items, "stone", 30);

            var construction = new PlotConstructionService(registry, MigrationSeedBuildPieceCatalog.Create(), items);
            var furnaceLocation = Loc(10, 10);
            Assert.IsTrue(construction.TryPlace(
                player,
                plotId,
                new ContentId("build.furnace"),
                furnaceLocation).Success);
            Assert.IsTrue(construction.IsStationAt(new ContentId("station.furnace"), furnaceLocation));

            var upkeep = new PlotUpkeepService(
                registry,
                new PlotUpkeepPolicy(10, 20, 40, TimeSpan.FromDays(30)));
            upkeep.Register(plotId, 1000);
            var reclamation = new PlotReclamationService(registry, upkeep, construction);

            var premature = reclamation.TryReclaim(plotId, 1000 + day);
            Assert.IsFalse(premature.Success);
            Assert.AreEqual("plot_not_abandoned", premature.Code);
            Assert.IsTrue(registry.TryGet(plotId, out _));
            Assert.IsTrue(construction.IsStationAt(new ContentId("station.furnace"), furnaceLocation));

            // Merely crossing the grace-period threshold marks abandonment; it still does not
            // destroy housing until the explicit reclamation command below is called.
            Assert.AreEqual(PlotUpkeepStatus.Abandoned, upkeep.Advance(plotId, 1000 + 30 * day));
            Assert.IsTrue(registry.TryGet(plotId, out _));

            var result = reclamation.TryReclaim(plotId, 1000 + 30 * day);
            Assert.IsTrue(result.Success);
            Assert.NotNull(result.Snapshot);
            Assert.AreEqual(plotId, result.Snapshot.PlotId);
            Assert.AreEqual(owner, result.Snapshot.OwnerCharacterId);
            Assert.AreEqual(1, result.Snapshot.Pieces.Count);
            Assert.AreEqual(4, result.Snapshot.ClaimedTiles.Count);
            Assert.AreEqual(36, result.Snapshot.ReservedTiles.Count);

            Assert.IsFalse(registry.TryGet(plotId, out _));
            Assert.IsFalse(construction.TryGetState(plotId, out _));
            Assert.IsFalse(construction.IsStationAt(new ContentId("station.furnace"), furnaceLocation));

            // The abandoned upkeep row intentionally survives as an audit tombstone.
            Assert.IsTrue(upkeep.TryGet(plotId, out var tombstone));
            Assert.AreEqual(PlotUpkeepStatus.Abandoned, tombstone.Status);

            // Reclamation really released the maximum future-Large envelope, not only the current
            // Small footprint, so another player may claim the same land afterwards.
            var replacement = registry.TryPlace(
                Guid.NewGuid(),
                Guid.NewGuid(),
                WorldConstants.SurfacePlane,
                Rect(10, 10, 2, 2),
                Rect(8, 8, 6, 6));
            Assert.IsTrue(replacement.Success);
        }

        [Test]
        public void ReclamationRefusesPlotsWithoutRegisteredUpkeep()
        {
            var registry = new PlayerPlotRegistry(new OpenPlacementMap());
            var owner = Guid.NewGuid();
            var plotId = Guid.NewGuid();
            Assert.IsTrue(registry.TryPlace(
                plotId,
                owner,
                WorldConstants.SurfacePlane,
                Rect(1, 1, 1, 1),
                Rect(1, 1, 1, 1)).Success);

            var items = MigrationSeedItemCatalog.Create();
            var construction = new PlotConstructionService(registry, MigrationSeedBuildPieceCatalog.Create(), items);
            var upkeep = new PlotUpkeepService(registry, new PlotUpkeepPolicy(1, 2, 3, TimeSpan.FromDays(30)));
            var reclamation = new PlotReclamationService(registry, upkeep, construction);

            var result = reclamation.TryReclaim(plotId, 1000);
            Assert.IsFalse(result.Success);
            Assert.AreEqual("upkeep_not_registered", result.Code);
            Assert.IsTrue(registry.TryGet(plotId, out _));
        }

        private static void Give(PlayerState player, ItemCatalog items, string id, int quantity)
        {
            Assert.AreEqual(quantity, InventoryRules.AddItem(player.Inventory, items, new ContentId(id), quantity));
        }

        private static GridCoord[] Rect(int x, int y, int width, int height)
        {
            var result = new List<GridCoord>(width * height);
            for (var yy = 0; yy < height; yy++)
                for (var xx = 0; xx < width; xx++)
                    result.Add(new GridCoord(x + xx, y + yy));
            return result.ToArray();
        }

        private static GridLocation Loc(int x, int y)
            => new GridLocation(new GridCoord(x, y), WorldConstants.SurfacePlane, 0);

        private sealed class OpenPlacementMap : IPlotPlacementMap
        {
            public bool CanReserveForPlayerPlot(GridLocation location) => true;
        }
    }
}
