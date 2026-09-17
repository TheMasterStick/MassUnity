using System;
using MassRPG.Core.Authority;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Core.Skills;
using MassRPG.Data.Effects;
using MassRPG.Data.Items;
using MassRPG.Server.Authority;
using MassRPG.Server.Effects;
using MassRPG.Server.Items;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class PotionAuthorityGatewayTests
    {
        private static readonly ContentId PotionId = new ContentId("test_gateway_potion");
        private static readonly ContentId EffectId = new ContentId("effect.gateway_attack");

        [Test]
        public void DrinkPotionRequestIsValidatedConsumedAndAppliedByAuthorityGateway()
        {
            var items = CreateItems();
            var status = new StatusEffectService();
            var effects = new PotionEffectCatalog();
            effects.Register(new PotionEffectDefinition(
                PotionId,
                EffectId,
                5000,
                new[] { new SkillLevelModifier(SkillId.Attack, 3) }));
            var potions = new PotionConsumptionService(items, effects, status);
            var inner = new LocalGameAuthority(items);
            var player = new PlayerState(Guid.NewGuid(), "Potion Tester");
            inner.RegisterPlayer(player);
            InventoryRules.AddItem(player.Inventory, items, PotionId, 2);
            var slot = FindSlot(player.Inventory, PotionId);
            var gateway = new LocalAuthorityGateway(inner, potions: potions);
            var requestId = Guid.NewGuid();

            var result = gateway.Submit(
                new DrinkPotionRequest(requestId, player.CharacterId, slot),
                1000);

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(requestId, result.RequestId);
            Assert.AreEqual(1, player.Inventory.CountItem(PotionId));
            Assert.AreEqual(player.Skills.GetLevel(SkillId.Attack) + 3,
                status.GetEffectiveLevel(player, SkillId.Attack, 1001));
        }

        [Test]
        public void MissingPotionServiceRejectsRequestWithoutMutatingInventory()
        {
            var items = CreateItems();
            var inner = new LocalGameAuthority(items);
            var player = new PlayerState(Guid.NewGuid(), "Potion Tester");
            inner.RegisterPlayer(player);
            InventoryRules.AddItem(player.Inventory, items, PotionId, 1);
            var slot = FindSlot(player.Inventory, PotionId);
            var gateway = new LocalAuthorityGateway(inner);

            var result = gateway.Submit(
                new DrinkPotionRequest(Guid.NewGuid(), player.CharacterId, slot),
                1000);

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual("potions_unavailable", result.Code);
            Assert.AreEqual(1, player.Inventory.CountItem(PotionId));
        }

        [Test]
        public void InvalidPotionEffectRejectsRequestWithoutConsumingItem()
        {
            var items = CreateItems();
            var status = new StatusEffectService();
            var potions = new PotionConsumptionService(items, new PotionEffectCatalog(), status);
            var inner = new LocalGameAuthority(items);
            var player = new PlayerState(Guid.NewGuid(), "Potion Tester");
            inner.RegisterPlayer(player);
            InventoryRules.AddItem(player.Inventory, items, PotionId, 1);
            var slot = FindSlot(player.Inventory, PotionId);
            var gateway = new LocalAuthorityGateway(inner, potions: potions);

            var result = gateway.Submit(
                new DrinkPotionRequest(Guid.NewGuid(), player.CharacterId, slot),
                1000);

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual("potion_effect_missing", result.Code);
            Assert.AreEqual(1, player.Inventory.CountItem(PotionId));
        }

        private static ItemCatalog CreateItems()
        {
            var items = new ItemCatalog();
            items.Register(new ItemDefinition(PotionId, "Gateway potion", ItemType.Potion, true, 10));
            return items;
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
    }
}
