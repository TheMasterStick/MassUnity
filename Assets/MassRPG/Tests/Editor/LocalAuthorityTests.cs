using System;
using MassRPG.Core.Authority;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Data.Items;
using MassRPG.Server.Authority;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class LocalAuthorityTests
    {
        [Test]
        public void UnknownCharacter_IsRejected()
        {
            var authority = new LocalGameAuthority(new ItemCatalog());
            var request = new MoveInventorySlotRequest(Guid.NewGuid(), Guid.NewGuid(), 0, 1);
            var decision = authority.Submit(request);

            Assert.IsFalse(decision.Accepted);
            Assert.AreEqual("unknown_character", decision.Code);
        }

        [Test]
        public void RegisteredCharacter_InventoryMutationGoesThroughAuthority()
        {
            var catalog = new ItemCatalog();
            var coins = new ContentId("coins");
            catalog.Register(new ItemDefinition(coins, "Coins", ItemType.Currency, true));
            var player = new PlayerState(Guid.NewGuid(), "Authority Test");
            InventoryRules.AddItem(player.Inventory, catalog, coins, 10);
            var authority = new LocalGameAuthority(catalog);
            authority.RegisterPlayer(player);

            var decision = authority.Submit(new MoveInventorySlotRequest(Guid.NewGuid(), player.CharacterId, 0, 5));

            Assert.IsTrue(decision.Accepted);
            Assert.IsNull(player.Inventory.GetSlot(0));
            Assert.AreEqual(coins, player.Inventory.GetSlot(5).ItemId);
        }
    }
}
