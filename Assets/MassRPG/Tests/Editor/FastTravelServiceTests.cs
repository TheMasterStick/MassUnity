using System;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Core.World;
using MassRPG.Data.Items;
using MassRPG.Data.Travel;
using MassRPG.Server.Travel;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class FastTravelServiceTests
    {
        private static readonly ContentId OriginId = new ContentId("travel.origin");
        private static readonly ContentId DestinationId = new ContentId("travel.destination");

        [Test]
        public void ActivateRequiresStandingOnNodeAndPersistsDiscovery()
        {
            var setup = CreateSetup();
            setup.Player.Location = Loc(9, 10);

            var rejected = setup.Service.ActivateCurrentNode(setup.Player, OriginId);
            Assert.IsFalse(rejected.Success);
            Assert.AreEqual("must_stand_on_node", rejected.Code);

            setup.Player.Location = Loc(10, 10);
            var activated = setup.Service.ActivateCurrentNode(setup.Player, OriginId);
            Assert.IsTrue(activated.Success);
            Assert.IsTrue(setup.States.GetOrCreate(setup.Player.CharacterId).IsActivated(OriginId));
        }

        [Test]
        public void TravelChargesOnlyOnCommitAndMovesToActivatedDestination()
        {
            var setup = CreateSetup();
            setup.Player.Location = Loc(10, 10);
            InventoryRules.AddItem(setup.Player.Inventory, setup.Items, FastTravelService.CoinId, 100);
            Assert.IsTrue(setup.Service.ActivateCurrentNode(setup.Player, OriginId).Success);

            setup.Player.Location = Loc(40, 10);
            Assert.IsTrue(setup.Service.ActivateCurrentNode(setup.Player, DestinationId).Success);
            setup.Player.Location = Loc(10, 10);

            var opened = setup.Service.OpenDestinationMap(setup.Player, OriginId, 1000);
            Assert.IsTrue(opened.Success);
            Assert.AreEqual(100, setup.Player.Inventory.CountItem(FastTravelService.CoinId));

            var committed = setup.Service.CommitTravel(setup.Player, DestinationId, 1100);
            Assert.IsTrue(committed.Success);
            Assert.AreEqual(8, committed.CostCoins); // 5 base + one 100-tile distance unit at 3 coins.
            Assert.AreEqual(92, setup.Player.Inventory.CountItem(FastTravelService.CoinId));
            Assert.AreEqual(Loc(40, 10), setup.Player.Location);
            Assert.IsFalse(setup.States.GetOrCreate(setup.Player.CharacterId).OpenOriginNodeId.HasValue);
        }

        [Test]
        public void CombatDuringOpenMapCancelsWithoutCharging()
        {
            var setup = CreateSetup();
            ActivateBoth(setup);
            InventoryRules.AddItem(setup.Player.Inventory, setup.Items, FastTravelService.CoinId, 100);
            setup.Player.Location = Loc(10, 10);
            Assert.IsTrue(setup.Service.OpenDestinationMap(setup.Player, OriginId, 1000).Success);

            setup.Gate.MarkCombatActivity(setup.Player.CharacterId, 1001);
            var result = setup.Service.CommitTravel(setup.Player, DestinationId, 1002);

            Assert.IsFalse(result.Success);
            Assert.AreEqual("post_combat_delay", result.Code);
            Assert.AreEqual(100, setup.Player.Inventory.CountItem(FastTravelService.CoinId));
            Assert.AreEqual(Loc(10, 10), setup.Player.Location);
            Assert.IsFalse(setup.States.GetOrCreate(setup.Player.CharacterId).OpenOriginNodeId.HasValue);
        }

        [Test]
        public void PostCombatDelayExpiresAndArrivalProtectionIsTimestamped()
        {
            var setup = CreateSetup();
            ActivateBoth(setup);
            InventoryRules.AddItem(setup.Player.Inventory, setup.Items, FastTravelService.CoinId, 100);
            setup.Player.Location = Loc(10, 10);
            setup.Gate.MarkCombatActivity(setup.Player.CharacterId, 1000);

            var blocked = setup.Service.OpenDestinationMap(setup.Player, OriginId, 10999);
            Assert.IsFalse(blocked.Success);
            Assert.AreEqual("post_combat_delay", blocked.Code);

            Assert.IsTrue(setup.Service.OpenDestinationMap(setup.Player, OriginId, 11000).Success);
            var travelled = setup.Service.CommitTravel(setup.Player, DestinationId, 11000);
            Assert.IsTrue(travelled.Success);
            Assert.AreEqual(14000, travelled.ProtectionUntilUnixMilliseconds);

            var state = setup.States.GetOrCreate(setup.Player.CharacterId);
            Assert.IsTrue(state.HasArrivalProtection(13999));
            Assert.IsFalse(state.HasArrivalProtection(14000));
            state.ClearArrivalProtection();
            Assert.IsFalse(state.HasArrivalProtection(11001));
        }

        [Test]
        public void LeavingOriginAfterOpeningMapCancelsCommitWithoutCharge()
        {
            var setup = CreateSetup();
            ActivateBoth(setup);
            InventoryRules.AddItem(setup.Player.Inventory, setup.Items, FastTravelService.CoinId, 100);
            setup.Player.Location = Loc(10, 10);
            Assert.IsTrue(setup.Service.OpenDestinationMap(setup.Player, OriginId, 1000).Success);
            setup.Player.Location = Loc(11, 10);

            var result = setup.Service.CommitTravel(setup.Player, DestinationId, 1001);

            Assert.IsFalse(result.Success);
            Assert.AreEqual("left_origin", result.Code);
            Assert.AreEqual(100, setup.Player.Inventory.CountItem(FastTravelService.CoinId));
        }

        [Test]
        public void DestinationMustBeActivatedAndAffordable()
        {
            var setup = CreateSetup();
            setup.Player.Location = Loc(10, 10);
            Assert.IsTrue(setup.Service.ActivateCurrentNode(setup.Player, OriginId).Success);
            Assert.IsTrue(setup.Service.OpenDestinationMap(setup.Player, OriginId, 1000).Success);

            var locked = setup.Service.CommitTravel(setup.Player, DestinationId, 1001);
            Assert.IsFalse(locked.Success);
            Assert.AreEqual("destination_not_activated", locked.Code);

            setup.Player.Location = Loc(40, 10);
            Assert.IsTrue(setup.Service.ActivateCurrentNode(setup.Player, DestinationId).Success);
            setup.Player.Location = Loc(10, 10);
            Assert.IsTrue(setup.Service.OpenDestinationMap(setup.Player, OriginId, 1002).Success);

            var poor = setup.Service.CommitTravel(setup.Player, DestinationId, 1003);
            Assert.IsFalse(poor.Success);
            Assert.AreEqual("insufficient_coins", poor.Code);
            Assert.AreEqual(Loc(10, 10), setup.Player.Location);
        }

        private static void ActivateBoth(Setup setup)
        {
            setup.Player.Location = Loc(10, 10);
            Assert.IsTrue(setup.Service.ActivateCurrentNode(setup.Player, OriginId).Success);
            setup.Player.Location = Loc(40, 10);
            Assert.IsTrue(setup.Service.ActivateCurrentNode(setup.Player, DestinationId).Success);
        }

        private static Setup CreateSetup()
        {
            var items = MigrationSeedItemCatalog.Create();
            var nodes = new FastTravelNodeCatalog();
            nodes.Register(new FastTravelNodeDefinition(OriginId, "Origin", Loc(10, 10)));
            nodes.Register(new FastTravelNodeDefinition(DestinationId, "Destination", Loc(40, 10)));
            var gate = new FastTravelCombatGate(10000);
            var states = new FastTravelStateRegistry();
            var service = new FastTravelService(
                nodes,
                new DistanceFastTravelCostPolicy(5, 3, 100),
                gate,
                states,
                items,
                3000);
            var player = new PlayerState(Guid.NewGuid(), "Traveller");
            return new Setup(items, gate, states, service, player);
        }

        private static GridLocation Loc(int x, int y)
            => new GridLocation(new GridCoord(x, y), WorldConstants.SurfacePlane, 0);

        private sealed class Setup
        {
            public Setup(
                ItemCatalog items,
                FastTravelCombatGate gate,
                FastTravelStateRegistry states,
                FastTravelService service,
                PlayerState player)
            {
                Items = items;
                Gate = gate;
                States = states;
                Service = service;
                Player = player;
            }

            public ItemCatalog Items { get; }
            public FastTravelCombatGate Gate { get; }
            public FastTravelStateRegistry States { get; }
            public FastTravelService Service { get; }
            public PlayerState Player { get; }
        }
    }
}
