using System;
using MassRPG.Core.Authority;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Core.World;
using MassRPG.Data.Items;
using MassRPG.Data.Travel;
using MassRPG.Data.World;
using MassRPG.Server.Authority;
using MassRPG.Server.Combat;
using MassRPG.Server.Travel;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class LocalAuthorityGatewayTests
    {
        private static readonly ContentId Grass = new ContentId("terrain.grass");
        private static readonly ContentId OriginId = new ContentId("travel.origin");
        private static readonly ContentId DestinationId = new ContentId("travel.destination");

        [Test]
        public void AmmunitionSelectionTravelsThroughIGameAuthorityBoundary()
        {
            var setup = CreateSetup();
            InventoryRules.AddItem(setup.Player.Inventory, setup.Items, new ContentId("bronze_arrow"), 4);

            IGameAuthority authority = setup.Gateway;
            var request = new SelectRangedAmmunitionRequest(
                Guid.NewGuid(), setup.Player.CharacterId, new ContentId("bronze_arrow"));
            var result = authority.Submit(request);

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(new ContentId("bronze_arrow"), setup.Player.SelectedAmmunitionItemId.Value);
            Assert.AreEqual(4, setup.Player.Inventory.CountItem(new ContentId("bronze_arrow")));
        }

        [Test]
        public void FastTravelRequestsActivateOpenCommitAndMovementEndsArrivalProtection()
        {
            var setup = CreateSetup();
            TravelToDestination(setup, 2000);
            Assert.AreEqual(Loc(12, 10), setup.Player.Location);
            Assert.IsTrue(setup.TravelStates.GetOrCreate(setup.Player.CharacterId).HasArrivalProtection(2002));

            var move = setup.Gateway.Submit(
                new MoveToRequest(Guid.NewGuid(), setup.Player.CharacterId, Loc(13, 10)), 2002);
            Assert.IsTrue(move.Accepted);
            Assert.IsFalse(setup.TravelStates.GetOrCreate(setup.Player.CharacterId).HasArrivalProtection(2002));
        }

        [Test]
        public void FailedMeaningfulRequestDoesNotStripArrivalProtection()
        {
            var setup = CreateSetup();
            TravelToDestination(setup, 2000);
            var state = setup.TravelStates.GetOrCreate(setup.Player.CharacterId);
            Assert.IsTrue(state.HasArrivalProtection(2002));

            var result = setup.Gateway.Submit(
                new MoveToRequest(Guid.NewGuid(), setup.Player.CharacterId, Loc(-1, -1)), 2002);

            Assert.IsFalse(result.Accepted);
            Assert.IsTrue(state.HasArrivalProtection(2002));
        }

        private static void TravelToDestination(Setup setup, long now)
        {
            InventoryRules.AddItem(setup.Player.Inventory, setup.Items, FastTravelService.CoinId, 100);
            setup.Player.Location = Loc(10, 10);
            Assert.IsTrue(setup.Gateway.Submit(
                new ActivateFastTravelNodeRequest(Guid.NewGuid(), setup.Player.CharacterId, OriginId), now - 2).Accepted);
            setup.Player.Location = Loc(12, 10);
            Assert.IsTrue(setup.Gateway.Submit(
                new ActivateFastTravelNodeRequest(Guid.NewGuid(), setup.Player.CharacterId, DestinationId), now - 2).Accepted);
            setup.Player.Location = Loc(10, 10);
            Assert.IsTrue(setup.Gateway.Submit(
                new OpenFastTravelMapRequest(Guid.NewGuid(), setup.Player.CharacterId, OriginId), now - 1).Accepted);
            Assert.IsTrue(setup.Gateway.Submit(
                new CommitFastTravelRequest(Guid.NewGuid(), setup.Player.CharacterId, DestinationId), now).Accepted);
        }

        private static Setup CreateSetup()
        {
            var items = MigrationSeedItemCatalog.Create();
            var map = new AuthoredWorldPageStore(Grass, 32);
            map.GetOrCreatePage(Loc(0, 0));
            var inner = new LocalGameAuthority(items, map);
            var player = new PlayerState(Guid.NewGuid(), "Gateway");
            player.Location = Loc(10, 10);
            inner.RegisterPlayer(player);

            var nodes = new FastTravelNodeCatalog();
            nodes.Register(new FastTravelNodeDefinition(OriginId, "Origin", Loc(10, 10)));
            nodes.Register(new FastTravelNodeDefinition(DestinationId, "Destination", Loc(12, 10)));
            var travelStates = new FastTravelStateRegistry();
            var travel = new FastTravelService(
                nodes,
                new DistanceFastTravelCostPolicy(1, 0, 100),
                new FastTravelCombatGate(10000),
                travelStates,
                items,
                3000);
            var ammunition = new RangedAmmunitionService(items);
            var gateway = new LocalAuthorityGateway(inner, travel, travelStates, ammunition);
            return new Setup(items, player, travelStates, gateway);
        }

        private static GridLocation Loc(int x, int y)
            => new GridLocation(new GridCoord(x, y), WorldConstants.SurfacePlane, 0);

        private sealed class Setup
        {
            public Setup(ItemCatalog items, PlayerState player, FastTravelStateRegistry travelStates, LocalAuthorityGateway gateway)
            {
                Items = items;
                Player = player;
                TravelStates = travelStates;
                Gateway = gateway;
            }

            public ItemCatalog Items { get; }
            public PlayerState Player { get; }
            public FastTravelStateRegistry TravelStates { get; }
            public LocalAuthorityGateway Gateway { get; }
        }
    }
}
