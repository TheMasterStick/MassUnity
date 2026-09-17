using System;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Data.Economy;
using MassRPG.Data.Items;
using MassRPG.Server.Economy;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class EconomyServiceTests
    {
        [Test]
        public void BankDepositAndWithdrawPreserveAuthoritativeQuantity()
        {
            var items = CreateItems();
            var player = new PlayerState(Guid.NewGuid(), "Banker", 4);
            var bank = new CharacterBankState();
            var service = new BankService(items);
            var bread = new ContentId("bread");
            InventoryRules.AddItem(player.Inventory, items, bread, 5);

            var deposit = service.Deposit(player, bank, 0, 3);
            Assert.IsTrue(deposit.Success);
            Assert.AreEqual(3, bank.Count(bread));
            Assert.AreEqual(2, player.Inventory.CountItem(bread));

            var withdraw = service.Withdraw(player, bank, bread, 2);
            Assert.IsTrue(withdraw.Success);
            Assert.AreEqual(1, bank.Count(bread));
            Assert.AreEqual(4, player.Inventory.CountItem(bread));
        }

        [Test]
        public void BankWithdrawOnlyTakesWhatNonStackableInventoryCanFit()
        {
            var items = CreateItems();
            var player = new PlayerState(Guid.NewGuid(), "Banker", 2);
            var bank = new CharacterBankState();
            var service = new BankService(items);
            var sword = new ContentId("test_sword");
            var bread = new ContentId("bread");

            InventoryRules.AddItem(player.Inventory, items, sword, 1);
            InventoryRules.AddItem(player.Inventory, items, bread, 1);
            service.Deposit(player, bank, 0, 1);
            bank.GetType(); // bank now owns one sword and inventory has one free slot.

            // Put two more through normal authoritative transfers, then leave only one slot open.
            InventoryRules.AddItem(player.Inventory, items, sword, 1);
            service.Deposit(player, bank, 0, 1);
            Assert.AreEqual(2, bank.Count(sword));
            Assert.AreEqual(1, player.Inventory.EmptySlotCount);

            var result = service.Withdraw(player, bank, sword, 2);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, result.Quantity);
            Assert.AreEqual(1, bank.Count(sword));
            Assert.AreEqual(0, player.Inventory.EmptySlotCount);
        }

        [Test]
        public void ShopBuyIsAtomicWhenInventoryCannotFitRequestedQuantity()
        {
            var items = CreateItems();
            var shops = CreateShops();
            var service = new ShopService(shops, items, items);
            var player = new PlayerState(Guid.NewGuid(), "Buyer", 2);
            var coins = new ContentId("coins");
            var sword = new ContentId("test_sword");
            InventoryRules.AddItem(player.Inventory, items, coins, 100);

            var failed = service.Buy(player, new ContentId("general_store"), sword, 2);
            Assert.IsFalse(failed.Success);
            Assert.AreEqual("inventory_full", failed.Code);
            Assert.AreEqual(100, player.Inventory.CountItem(coins));
            Assert.AreEqual(0, player.Inventory.CountItem(sword));

            var success = service.Buy(player, new ContentId("general_store"), sword, 1);
            Assert.IsTrue(success.Success);
            Assert.AreEqual(80, player.Inventory.CountItem(coins));
            Assert.AreEqual(1, player.Inventory.CountItem(sword));
        }

        [Test]
        public void GeneralStoreSaleUsesHalfValueFloorAndPaysCoins()
        {
            var items = CreateItems();
            var shops = CreateShops();
            var service = new ShopService(shops, items, items);
            var player = new PlayerState(Guid.NewGuid(), "Seller", 3);
            var bread = new ContentId("bread");
            var coins = new ContentId("coins");
            InventoryRules.AddItem(player.Inventory, items, bread, 4);

            var result = service.Sell(player, new ContentId("general_store"), 0, 3);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(3, result.Quantity);
            Assert.AreEqual(9, result.CoinAmount);
            Assert.AreEqual(1, player.Inventory.CountItem(bread));
            Assert.AreEqual(9, player.Inventory.CountItem(coins));
        }

        private static ItemCatalog CreateItems()
        {
            var catalog = new ItemCatalog();
            catalog.Register(new ItemDefinition(new ContentId("coins"), "Coins", ItemType.Currency, true, 1));
            catalog.Register(new ItemDefinition(new ContentId("bread"), "Bread", ItemType.Food, true, 6));
            catalog.Register(new ItemDefinition(new ContentId("test_sword"), "Test sword", ItemType.Weapon, false, 20,
                allowedEquipmentSlots: new[] { EquipmentSlot.MainHand }));
            return catalog;
        }

        private static ShopCatalog CreateShops()
        {
            var shops = new ShopCatalog();
            shops.Register(new ShopDefinition(
                new ContentId("general_store"),
                "General Store",
                new[] { new ContentId("bread"), new ContentId("test_sword") },
                buysAnyItem: true));
            return shops;
        }
    }
}
