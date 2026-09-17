using System;
using System.Collections.Generic;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Core.World;
using MassRPG.Data.Items;
using MassRPG.Server.Death;
using MassRPG.Server.Items;
using MassRPG.Server.Pvp;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class PvpDeathServiceTests
    {
        [Test]
        public void SkulledDeathCanDropAllInventoryAndEquipmentProtectedToKiller()
        {
            var items = MigrationSeedItemCatalog.Create();
            var groundRegistry = new GroundItemRegistry();
            var ground = new GroundItemService(items, groundRegistry);
            var respawns = new PlayerRespawnRegistry();
            var deathTile = Loc(100, 100);
            var respawn = Loc(5, 5);
            var defeated = new PlayerState(Guid.NewGuid(), "Skulled") { Location = deathTile };
            InventoryRules.AddItem(defeated.Inventory, items, new ContentId("bronze_sword"), 1);
            Assert.IsTrue(InventoryRules.EquipFromInventory(
                defeated.Inventory,
                defeated.Equipment,
                items,
                defeated.Skills,
                FindSlot(defeated.Inventory, new ContentId("bronze_sword")),
                EquipmentSlot.MainHand).Success);
            InventoryRules.AddItem(defeated.Inventory, items, new ContentId("coins"), 100);
            InventoryRules.AddItem(defeated.Inventory, items, new ContentId("bread"), 2);
            defeated.CurrentHitpoints = 0;
            var killerId = Guid.NewGuid();

            var service = new PvpDeathService(
                respawns,
                new FixedSettlement(respawn),
                new ConfiguredInventoryDropSelector(1, PlayerDeathService.CoinId),
                new PvpDeathPenaltyPolicy(1000, 0, true, true, 5000, 60000),
                ground,
                items);

            var result = service.ResolveDeath(defeated, true, killerId, 1000);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(respawn, defeated.Location);
            Assert.AreEqual(defeated.MaxHitpoints, defeated.CurrentHitpoints);
            Assert.AreEqual(2, result.StacksDropped);
            Assert.AreEqual(1, result.EquipmentDropped);
            Assert.AreEqual(0, defeated.Inventory.CountItem(new ContentId("coins")));
            Assert.AreEqual(0, defeated.Inventory.CountItem(new ContentId("bread")));
            Assert.IsFalse(defeated.Equipment.IsOccupied(EquipmentSlot.MainHand));

            var drops = new List<GroundItemState>(groundRegistry.All);
            Assert.AreEqual(3, drops.Count);
            for (var i = 0; i < drops.Count; i++)
            {
                Assert.AreEqual(deathTile, drops[i].Location);
                Assert.IsTrue(drops[i].CanBeTakenBy(killerId, 1001));
                Assert.IsFalse(drops[i].CanBeTakenBy(Guid.NewGuid(), 1001));
                Assert.IsTrue(drops[i].CanBeTakenBy(Guid.NewGuid(), 6000));
            }
        }

        [Test]
        public void DefenderUsesConfigurableLighterLossAndKeepsEquipment()
        {
            var items = MigrationSeedItemCatalog.Create();
            var groundRegistry = new GroundItemRegistry();
            var ground = new GroundItemService(items, groundRegistry);
            var defeated = new PlayerState(Guid.NewGuid(), "Defender") { Location = Loc(20, 20) };
            InventoryRules.AddItem(defeated.Inventory, items, new ContentId("bronze_sword"), 1);
            Assert.IsTrue(InventoryRules.EquipFromInventory(
                defeated.Inventory,
                defeated.Equipment,
                items,
                defeated.Skills,
                FindSlot(defeated.Inventory, new ContentId("bronze_sword")),
                EquipmentSlot.MainHand).Success);
            InventoryRules.AddItem(defeated.Inventory, items, new ContentId("coins"), 100);
            InventoryRules.AddItem(defeated.Inventory, items, new ContentId("bread"), 3);
            defeated.CurrentHitpoints = 0;

            var service = new PvpDeathService(
                new PlayerRespawnRegistry(),
                new FixedSettlement(Loc(1, 1)),
                new ConfiguredInventoryDropSelector(1, PlayerDeathService.CoinId),
                new PvpDeathPenaltyPolicy(1000, 0, true, true, 0, 60000),
                ground,
                items);

            var result = service.ResolveDeath(defeated, false, Guid.NewGuid(), 1000);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(10, result.CurrencyDestroyed);
            Assert.AreEqual(90, defeated.Inventory.CountItem(new ContentId("coins")));
            Assert.AreEqual(0, defeated.Inventory.CountItem(new ContentId("bread")));
            Assert.AreEqual(1, result.StacksDropped);
            Assert.AreEqual(0, result.EquipmentDropped);
            Assert.AreEqual(new ContentId("bronze_sword"), defeated.Equipment.GetOrNull(EquipmentSlot.MainHand));
            Assert.AreEqual(1, new List<GroundItemState>(groundRegistry.All).Count);
        }

        [Test]
        public void MissingRespawnDoesNotDestroyPvpItems()
        {
            var items = MigrationSeedItemCatalog.Create();
            var defeated = new PlayerState(Guid.NewGuid(), "No Respawn") { CurrentHitpoints = 0 };
            InventoryRules.AddItem(defeated.Inventory, items, new ContentId("coins"), 100);
            var registry = new GroundItemRegistry();
            var service = new PvpDeathService(
                new PlayerRespawnRegistry(),
                new MissingSettlement(),
                new ConfiguredInventoryDropSelector(1, PlayerDeathService.CoinId),
                new PvpDeathPenaltyPolicy(10000, 0, true, true, 0, 0),
                new GroundItemService(items, registry),
                items);

            var result = service.ResolveDeath(defeated, true, Guid.NewGuid(), 1000);

            Assert.IsFalse(result.Success);
            Assert.AreEqual("no_respawn_location", result.Code);
            Assert.AreEqual(100, defeated.Inventory.CountItem(new ContentId("coins")));
            Assert.AreEqual(0, new List<GroundItemState>(registry.All).Count);
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

        private sealed class FixedSettlement : ISettlementRespawnSource
        {
            private readonly GridLocation _location;
            public FixedSettlement(GridLocation location) => _location = location;
            public bool TryFindNearest(GridLocation deathLocation, out GridLocation respawnLocation)
            {
                respawnLocation = _location;
                return true;
            }
        }

        private sealed class MissingSettlement : ISettlementRespawnSource
        {
            public bool TryFindNearest(GridLocation deathLocation, out GridLocation respawnLocation)
            {
                respawnLocation = default;
                return false;
            }
        }
    }
}
