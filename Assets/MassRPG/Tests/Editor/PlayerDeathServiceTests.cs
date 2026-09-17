using System;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Core.World;
using MassRPG.Data.Items;
using MassRPG.Server.Death;
using MassRPG.Server.Items;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class PlayerDeathServiceTests
    {
        [Test]
        public void PveDeathUsesConfiguredPenaltyDropsItemsAndRespawnsAtHome()
        {
            var items = CreateItems();
            var groundRegistry = new GroundItemRegistry();
            var ground = new GroundItemService(items, groundRegistry);
            var respawns = new PlayerRespawnRegistry();
            var player = new PlayerState(Guid.NewGuid(), "Fallen")
            {
                CurrentHitpoints = 0,
                Location = new GridLocation(new GridCoord(100, 100), 0)
            };
            var home = new GridLocation(new GridCoord(50, 50), 0);
            var profile = respawns.GetOrCreate(player.CharacterId);
            profile.Preference = RespawnPreference.Home;
            profile.HomeLocation = home;
            InventoryRules.AddItem(player.Inventory, items, new ContentId("coins"), 100);
            InventoryRules.AddItem(player.Inventory, items, new ContentId("bread"), 3);

            var service = new PlayerDeathService(
                respawns,
                new FixedSettlement(new GridLocation(new GridCoord(10, 10), 0)),
                new ConfiguredInventoryDropSelector(1, PlayerDeathService.CoinId),
                new PveDeathPenaltyPolicy(1000, 0, 5_000, 60_000),
                ground,
                items);

            var result = service.ResolvePveDeath(player, 1_000);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(home, player.Location);
            Assert.AreEqual(player.MaxHitpoints, player.CurrentHitpoints);
            Assert.AreEqual(10, result.CurrencyLost);
            Assert.AreEqual(90, player.Inventory.CountItem(new ContentId("coins")));
            Assert.AreEqual(1, result.ItemStacksDropped);
            Assert.AreEqual(0, player.Inventory.CountItem(new ContentId("bread")));

            GroundItemState dropped = null;
            foreach (var entry in groundRegistry.All) dropped = entry;
            Assert.NotNull(dropped);
            Assert.AreEqual(new ContentId("bread"), dropped.ItemId);
            Assert.AreEqual(3, dropped.Quantity);
            Assert.AreEqual(new GridLocation(new GridCoord(100, 100), 0), dropped.Location);
        }

        [Test]
        public void NearestSettlementPreferenceFallsBackToConfiguredSettlement()
        {
            var items = CreateItems();
            var ground = new GroundItemService(items, new GroundItemRegistry());
            var respawns = new PlayerRespawnRegistry();
            var player = new PlayerState(Guid.NewGuid(), "Fallen") { CurrentHitpoints = 0 };
            var settlement = new GridLocation(new GridCoord(25, 40), 0);
            var service = new PlayerDeathService(
                respawns,
                new FixedSettlement(settlement),
                new ConfiguredInventoryDropSelector(0, PlayerDeathService.CoinId),
                new PveDeathPenaltyPolicy(0, 0, 0, 0),
                ground,
                items);

            var result = service.ResolvePveDeath(player, 0);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(settlement, player.Location);
        }

        [Test]
        public void MissingRespawnLeavesDeadCharacterUnchanged()
        {
            var items = CreateItems();
            var respawns = new PlayerRespawnRegistry();
            var player = new PlayerState(Guid.NewGuid(), "Fallen") { CurrentHitpoints = 0 };
            var before = player.Location;
            var service = new PlayerDeathService(
                respawns,
                new MissingSettlement(),
                new ConfiguredInventoryDropSelector(0, PlayerDeathService.CoinId),
                new PveDeathPenaltyPolicy(0, 0, 0, 0),
                new GroundItemService(items, new GroundItemRegistry()),
                items);

            var result = service.ResolvePveDeath(player, 0);

            Assert.IsFalse(result.Success);
            Assert.AreEqual("no_respawn_location", result.Code);
            Assert.AreEqual(before, player.Location);
            Assert.IsFalse(player.IsAlive);
        }

        private static ItemCatalog CreateItems()
        {
            var catalog = new ItemCatalog();
            catalog.Register(new ItemDefinition(new ContentId("coins"), "Coins", ItemType.Currency, true, 1));
            catalog.Register(new ItemDefinition(new ContentId("bread"), "Bread", ItemType.Food, true, 6, healAmount: 5));
            return catalog;
        }

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
