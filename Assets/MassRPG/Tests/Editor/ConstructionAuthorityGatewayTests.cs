using System;
using System.Collections.Generic;
using MassRPG.Core.Authority;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Core.World;
using MassRPG.Data.Construction;
using MassRPG.Data.Items;
using MassRPG.Data.World;
using MassRPG.Server.Authority;
using MassRPG.Server.Construction;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class ConstructionAuthorityGatewayTests
    {
        private static readonly ContentId Grass = new ContentId("terrain.grass");

        [Test]
        public void PlaceAndDemolishTravelThroughAuthorityGateway()
        {
            var setup = CreateSetup(withConstruction: true);
            Give(setup.Player, setup.Items, "plank", 10);
            Give(setup.Player, setup.Items, "nails", 10);

            var place = setup.Gateway.Submit(new PlaceBuildPieceRequest(
                Guid.NewGuid(),
                setup.Player.CharacterId,
                setup.Plot.PlotId,
                new ContentId("build.wall_wood"),
                Loc(10, 10),
                CardinalEdgeMask.North));

            Assert.IsTrue(place.Accepted);
            Assert.IsTrue(setup.Construction.TryGetState(setup.Plot.PlotId, out var state));
            Assert.AreEqual(1, state.Count);

            PlacedBuildPiece placed = null;
            foreach (var piece in state.Pieces)
            {
                placed = piece;
                break;
            }
            Assert.NotNull(placed);

            var demolish = setup.Gateway.Submit(new DemolishBuildPieceRequest(
                Guid.NewGuid(),
                setup.Player.CharacterId,
                setup.Plot.PlotId,
                placed.InstanceId));

            Assert.IsTrue(demolish.Accepted);
            Assert.AreEqual(0, state.Count);
        }

        [Test]
        public void MissingConstructionServiceRejectsWithoutMutation()
        {
            var setup = CreateSetup(withConstruction: false);

            var result = setup.Gateway.Submit(new PlaceBuildPieceRequest(
                Guid.NewGuid(),
                setup.Player.CharacterId,
                setup.Plot.PlotId,
                new ContentId("build.wall_wood"),
                Loc(10, 10),
                CardinalEdgeMask.North));

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual("construction_unavailable", result.Code);
            Assert.IsFalse(setup.Construction.TryGetState(setup.Plot.PlotId, out _));
        }

        [Test]
        public void PlacementFailurePreservesConstructionServiceCode()
        {
            var setup = CreateSetup(withConstruction: true);

            var result = setup.Gateway.Submit(new PlaceBuildPieceRequest(
                Guid.NewGuid(),
                setup.Player.CharacterId,
                setup.Plot.PlotId,
                new ContentId("build.wall_wood"),
                Loc(10, 10),
                CardinalEdgeMask.North));

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual("missing_materials", result.Code);
            Assert.IsTrue(setup.Construction.TryGetState(setup.Plot.PlotId, out var state));
            Assert.AreEqual(0, state.Count);
        }

        [Test]
        public void PlaceRequestNormalizesRotationAndRejectsEmptyIdentifiers()
        {
            var characterId = Guid.NewGuid();
            var plotId = Guid.NewGuid();
            var request = new PlaceBuildPieceRequest(
                Guid.NewGuid(), characterId, plotId, new ContentId("build.wall_wood"), Loc(10, 10),
                CardinalEdgeMask.North, -1);

            Assert.AreEqual(3, request.RotationQuarterTurns);
            Assert.Throws<ArgumentException>(() => new PlaceBuildPieceRequest(
                Guid.NewGuid(), characterId, Guid.Empty, new ContentId("build.wall_wood"), Loc(10, 10)));
            Assert.Throws<ArgumentException>(() => new DemolishBuildPieceRequest(
                Guid.NewGuid(), characterId, plotId, Guid.Empty));
        }

        private static Setup CreateSetup(bool withConstruction)
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

            var player = new PlayerState(owner, "Builder") { Location = Loc(10, 10) };
            var world = new AuthoredWorldPageStore(Grass, 32);
            world.GetOrCreatePage(Loc(0, 0));
            var inner = new LocalGameAuthority(items, world);
            inner.RegisterPlayer(player);

            var construction = new PlotConstructionService(plots, MigrationSeedBuildPieceCatalog.Create(), items);
            var gateway = new LocalAuthorityGateway(
                inner,
                construction: withConstruction ? construction : null);

            return new Setup(items, placement.Plot, player, construction, gateway);
        }

        private static void Give(PlayerState player, ItemCatalog items, string itemId, int quantity)
        {
            Assert.AreEqual(
                quantity,
                InventoryRules.AddItem(player.Inventory, items, new ContentId(itemId), quantity));
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
                PlotConstructionService construction,
                LocalAuthorityGateway gateway)
            {
                Items = items;
                Plot = plot;
                Player = player;
                Construction = construction;
                Gateway = gateway;
            }

            public ItemCatalog Items { get; }
            public PlayerPlotState Plot { get; }
            public PlayerState Player { get; }
            public PlotConstructionService Construction { get; }
            public LocalAuthorityGateway Gateway { get; }
        }
    }
}
