using System;
using System.Collections.Generic;
using MassRPG.Core.Authority;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Core.World;
using MassRPG.Data.Items;
using MassRPG.Data.World;
using MassRPG.Server.Authority;
using MassRPG.Server.Construction;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class PlotUpkeepAuthorityGatewayTests
    {
        private const long DayMilliseconds = 24L * 60L * 60L * 1000L;
        private static readonly ContentId Grass = new ContentId("terrain.grass");
        private static readonly ContentId Coins = new ContentId("coins");

        [Test]
        public void PaymentUsesServerPolicyAndOnlySpendsWholeDays()
        {
            var setup = CreateSetup(includeUpkeep: true);
            Assert.AreEqual(100, InventoryRules.AddItem(setup.Player.Inventory, setup.Items, Coins, 100));

            var decision = setup.Gateway.Submit(
                new PayPlotUpkeepRequest(Guid.NewGuid(), setup.Player.CharacterId, setup.Plot.PlotId, 25),
                1000);

            Assert.IsTrue(decision.Accepted);
            Assert.AreEqual(80, setup.Player.Inventory.CountItem(Coins));
            Assert.IsTrue(setup.Upkeep.TryGet(setup.Plot.PlotId, out var state));
            Assert.AreEqual(1000 + 2 * DayMilliseconds, state.PaidThroughUnixMilliseconds);
            Assert.AreEqual(PlotUpkeepStatus.Active, state.Status);
        }

        [Test]
        public void NonOwnerPaymentPreservesServiceRejectionCode()
        {
            var setup = CreateSetup(includeUpkeep: true);
            var visitor = new PlayerState(Guid.NewGuid(), "Visitor") { Location = Loc(10, 10) };
            setup.Inner.RegisterPlayer(visitor);
            Assert.AreEqual(100, InventoryRules.AddItem(visitor.Inventory, setup.Items, Coins, 100));

            var decision = setup.Gateway.Submit(
                new PayPlotUpkeepRequest(Guid.NewGuid(), visitor.CharacterId, setup.Plot.PlotId, 20),
                1000);

            Assert.IsFalse(decision.Accepted);
            Assert.AreEqual("not_owner", decision.Code);
            Assert.AreEqual(100, visitor.Inventory.CountItem(Coins));
        }

        [Test]
        public void MissingUpkeepServiceRejectsWithoutSpendingCoins()
        {
            var setup = CreateSetup(includeUpkeep: false);
            Assert.AreEqual(100, InventoryRules.AddItem(setup.Player.Inventory, setup.Items, Coins, 100));

            var decision = setup.Gateway.Submit(
                new PayPlotUpkeepRequest(Guid.NewGuid(), setup.Player.CharacterId, setup.Plot.PlotId, 20),
                1000);

            Assert.IsFalse(decision.Accepted);
            Assert.AreEqual("plot_upkeep_unavailable", decision.Code);
            Assert.AreEqual(100, setup.Player.Inventory.CountItem(Coins));
        }

        [Test]
        public void UpkeepRequestRejectsInvalidClientAmountsBeforeSubmission()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PayPlotUpkeepRequest(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0));
        }

        private static Setup CreateSetup(bool includeUpkeep)
        {
            var items = MigrationSeedItemCatalog.Create();
            var plots = new PlayerPlotRegistry(new OpenPlacementMap());
            var owner = Guid.NewGuid();
            var placement = plots.TryPlace(
                Guid.NewGuid(),
                owner,
                WorldConstants.SurfacePlane,
                Rect(8, 8, 8, 8),
                Rect(6, 6, 12, 12));
            Assert.IsTrue(placement.Success);

            var player = new PlayerState(owner, "Owner") { Location = Loc(10, 10) };
            var world = new AuthoredWorldPageStore(Grass, 32);
            world.GetOrCreatePage(Loc(0, 0));
            var inner = new LocalGameAuthority(items, world);
            inner.RegisterPlayer(player);

            var upkeep = new PlotUpkeepService(
                plots,
                new PlotUpkeepPolicy(10, 20, 40, TimeSpan.FromDays(30)));
            upkeep.Register(placement.Plot.PlotId, 1000);

            var gateway = new LocalAuthorityGateway(
                inner,
                plotUpkeep: includeUpkeep ? upkeep : null);
            return new Setup(items, placement.Plot, player, inner, upkeep, gateway);
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

        private sealed class Setup
        {
            public Setup(
                ItemCatalog items,
                PlayerPlotState plot,
                PlayerState player,
                LocalGameAuthority inner,
                PlotUpkeepService upkeep,
                LocalAuthorityGateway gateway)
            {
                Items = items;
                Plot = plot;
                Player = player;
                Inner = inner;
                Upkeep = upkeep;
                Gateway = gateway;
            }

            public ItemCatalog Items { get; }
            public PlayerPlotState Plot { get; }
            public PlayerState Player { get; }
            public LocalGameAuthority Inner { get; }
            public PlotUpkeepService Upkeep { get; }
            public LocalAuthorityGateway Gateway { get; }
        }
    }
}
