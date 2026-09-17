using System;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Core.World;
using MassRPG.Data.Items;
using MassRPG.Data.Travel;
using MassRPG.Server.Death;
using MassRPG.Server.Economy;
using MassRPG.Server.Persistence;
using MassRPG.Server.Travel;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class CharacterPersistenceSnapshotTests
    {
        private static readonly ContentId OriginId = new ContentId("travel.origin");
        private static readonly ContentId DestinationId = new ContentId("travel.destination");

        [Test]
        public void AggregateRoundTripPreservesBankRespawnAndTravelDiscoveryButNotTransientTravelState()
        {
            var items = MigrationSeedItemCatalog.Create();
            var player = new PlayerState(Guid.NewGuid(), "Persistent");
            player.Location = Loc(10, 10);
            InventoryRules.AddItem(player.Inventory, items, new ContentId("coins"), 100);
            InventoryRules.AddItem(player.Inventory, items, new ContentId("bronze_arrow"), 8);

            var banks = new CharacterBankRegistry();
            var bankService = new BankService(items);
            var bank = banks.GetOrCreate(player.CharacterId);
            var coinSlot = FindSlot(player.Inventory, new ContentId("coins"));
            Assert.IsTrue(bankService.Deposit(player, bank, coinSlot, 40).Success);

            var respawns = new PlayerRespawnRegistry();
            var respawn = respawns.GetOrCreate(player.CharacterId);
            respawn.Preference = RespawnPreference.Home;
            respawn.HomeLocation = Loc(77, 88);

            var nodes = new FastTravelNodeCatalog();
            nodes.Register(new FastTravelNodeDefinition(OriginId, "Origin", Loc(10, 10)));
            nodes.Register(new FastTravelNodeDefinition(DestinationId, "Destination", Loc(12, 10)));
            var travelStates = new FastTravelStateRegistry();
            var travel = new FastTravelService(
                nodes,
                new DistanceFastTravelCostPolicy(0, 0, 100),
                new FastTravelCombatGate(0),
                travelStates,
                items,
                3000);
            Assert.IsTrue(travel.ActivateCurrentNode(player, OriginId).Success);
            player.Location = Loc(12, 10);
            Assert.IsTrue(travel.ActivateCurrentNode(player, DestinationId).Success);
            player.Location = Loc(10, 10);
            Assert.IsTrue(travel.OpenDestinationMap(player, OriginId, 1000).Success);
            Assert.IsTrue(travel.CommitTravel(player, DestinationId, 1001).Success);
            Assert.IsTrue(travelStates.GetOrCreate(player.CharacterId).HasArrivalProtection(1002));

            var snapshot = CharacterPersistenceSnapshotCodec.Capture(player, banks, respawns, travelStates);

            var restoredBanks = new CharacterBankRegistry();
            var restoredRespawns = new PlayerRespawnRegistry();
            var restoredTravel = new FastTravelStateRegistry();
            var restored = CharacterPersistenceSnapshotCodec.Restore(
                snapshot, items, restoredBanks, restoredRespawns, restoredTravel);

            Assert.AreEqual(40, restored.Bank.Count(new ContentId("coins")));
            Assert.AreEqual(60, restored.Player.Inventory.CountItem(new ContentId("coins")));
            Assert.AreEqual(8, restored.Player.Inventory.CountItem(new ContentId("bronze_arrow")));
            Assert.AreEqual(RespawnPreference.Home, restored.Respawn.Preference);
            Assert.AreEqual(Loc(77, 88), restored.Respawn.HomeLocation.Value);
            Assert.IsTrue(restored.FastTravel.IsActivated(OriginId));
            Assert.IsTrue(restored.FastTravel.IsActivated(DestinationId));
            Assert.IsFalse(restored.FastTravel.OpenOriginNodeId.HasValue);
            Assert.IsFalse(restored.FastTravel.HasArrivalProtection(1002));
        }

        [Test]
        public void AggregateRestoreRejectsUnknownBankItem()
        {
            var items = MigrationSeedItemCatalog.Create();
            var player = new PlayerState(Guid.NewGuid(), "Bad Bank");
            var baseSnapshot = MassRPG.Core.Persistence.PlayerStateSnapshotCodec.Capture(player);
            var snapshot = new CharacterPersistenceSnapshot(
                CharacterPersistenceSnapshot.CurrentVersion,
                baseSnapshot,
                new[] { new BankEntrySnapshot(new ContentId("removed_item"), 1) },
                RespawnPreference.NearestSettlement,
                null,
                Array.Empty<ContentId>());

            Assert.Throws<InvalidOperationException>(() => CharacterPersistenceSnapshotCodec.Restore(
                snapshot,
                items,
                new CharacterBankRegistry(),
                new PlayerRespawnRegistry(),
                new FastTravelStateRegistry()));
        }

        private static int FindSlot(InventoryState inventory, ContentId itemId)
        {
            for (var i = 0; i < inventory.Capacity; i++)
            {
                var stack = inventory.GetSlot(i);
                if (stack != null && stack.ItemId == itemId) return i;
            }
            return -1;
        }

        private static GridLocation Loc(int x, int y)
            => new GridLocation(new GridCoord(x, y), WorldConstants.SurfacePlane, 0);
    }
}
