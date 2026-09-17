using System;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Data.Items;
using MassRPG.Server.Items;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class FoodConsumptionTests
    {
        [Test]
        public void CookedFoodHealsWithoutExceedingMaximumAndConsumesOne()
        {
            var items = new ItemCatalog();
            var bread = new ContentId("bread");
            items.Register(new ItemDefinition(bread, "Bread", ItemType.Food, true, 6, healAmount: 5));
            var player = new PlayerState(Guid.NewGuid(), "Hungry") { CurrentHitpoints = 8 };
            InventoryRules.AddItem(player.Inventory, items, bread, 2);
            var service = new FoodConsumptionService(items, items);

            var result = service.Eat(player, 0);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(2, result.Healed);
            Assert.AreEqual(player.MaxHitpoints, player.CurrentHitpoints);
            Assert.AreEqual(1, player.Inventory.CountItem(bread));
        }

        [Test]
        public void RawCookingIngredientIsNotConsumedAsFood()
        {
            var items = new ItemCatalog();
            var raw = new ContentId("raw_meat");
            items.Register(new ItemDefinition(raw, "Raw meat", ItemType.Food, true, 4, healAmount: 1));
            var player = new PlayerState(Guid.NewGuid(), "Cook") { CurrentHitpoints = 5 };
            InventoryRules.AddItem(player.Inventory, items, raw, 1);
            var service = new FoodConsumptionService(items, items);

            var result = service.Eat(player, 0);

            Assert.IsFalse(result.Success);
            Assert.AreEqual("not_edible", result.Code);
            Assert.AreEqual(1, player.Inventory.CountItem(raw));
            Assert.AreEqual(5, player.CurrentHitpoints);
        }

        [Test]
        public void ExplicitlyNonConsumableFoodStaysInInventory()
        {
            var items = new ItemCatalog();
            var prop = new ContentId("ceremonial_cake");
            items.Register(new ItemDefinition(prop, "Ceremonial cake", ItemType.Food, true, 10, healAmount: 4, canConsume: false));
            var player = new PlayerState(Guid.NewGuid(), "Guest") { CurrentHitpoints = 5 };
            InventoryRules.AddItem(player.Inventory, items, prop, 1);
            var service = new FoodConsumptionService(items, items);

            var result = service.Eat(player, 0);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(1, player.Inventory.CountItem(prop));
        }
    }
}
