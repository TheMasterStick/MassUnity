using System;
using MassRPG.Core.Authority;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Core.Resources;
using MassRPG.Core.Skills;
using MassRPG.Core.World;
using MassRPG.Data.Items;
using MassRPG.Data.Resources;
using MassRPG.Server.Authority;
using MassRPG.Server.Resources;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class GatheringServiceTests
    {
        [Test]
        public void PersonalTree_GivesLootXpAndOnlyDepletesForGatheringPlayer()
        {
            var items = MakeItems();
            var resources = MakeResources(ResourceAvailabilityMode.Personal);
            var nodes = new InMemoryResourceNodeSource();
            var node = OakAt(101, 100);
            nodes.Register(node);
            var ledger = new ResourceDepletionLedger();
            var service = new GatheringService(nodes, resources, items, items, ledger, new Random(1));
            var a = PlayerAt(100, 100, items);
            var b = PlayerAt(100, 100, items);
            var beforeXp = a.Skills.GetXp(SkillId.Woodcutting);

            var first = service.TryGather(a, node, 1000);
            var secondPlayer = service.TryGather(b, node, 1001);

            Assert.IsTrue(first.Success);
            Assert.IsTrue(secondPlayer.Success);
            Assert.AreEqual(1, a.Inventory.CountItem(new ContentId("oak_logs")));
            Assert.Greater(a.Skills.GetXp(SkillId.Woodcutting), beforeXp);
            Assert.IsFalse(ledger.IsAvailable(a.CharacterId, node, ResourceAvailabilityMode.Personal, 1001));
        }

        [Test]
        public void SharedResource_BlocksOtherPlayersUntilTimestampExpires()
        {
            var items = MakeItems();
            var resources = MakeResources(ResourceAvailabilityMode.Shared);
            var nodes = new InMemoryResourceNodeSource();
            var node = OakAt(101, 100);
            nodes.Register(node);
            var ledger = new ResourceDepletionLedger();
            var service = new GatheringService(nodes, resources, items, items, ledger);
            var a = PlayerAt(100, 100, items);
            var b = PlayerAt(100, 100, items);

            Assert.IsTrue(service.TryGather(a, node, 5000).Success);
            var blocked = service.TryGather(b, node, 5001);
            Assert.IsFalse(blocked.Success);
            Assert.AreEqual("resource_depleted", blocked.Code);
            Assert.IsTrue(service.TryGather(b, node, 5000 + 8000).Success);
        }

        [Test]
        public void GatherRequest_MutatesThroughAuthorityRatherThanClientState()
        {
            var items = MakeItems();
            var resources = MakeResources(ResourceAvailabilityMode.Personal);
            var nodes = new InMemoryResourceNodeSource();
            var node = OakAt(101, 100);
            nodes.Register(node);
            var service = new GatheringService(nodes, resources, items, items, new ResourceDepletionLedger());
            var authority = new LocalGameAuthority(items, null, service);
            var player = PlayerAt(100, 100, items);
            authority.RegisterPlayer(player);

            var decision = authority.Submit(new GatherResourceRequest(Guid.NewGuid(), player.CharacterId, node), 10_000);

            Assert.IsTrue(decision.Accepted);
            Assert.AreEqual(1, player.Inventory.CountItem(new ContentId("oak_logs")));
        }

        private static ItemCatalog MakeItems()
        {
            var items = new ItemCatalog();
            items.Register(new ItemDefinition(new ContentId("oak_logs"), "Oak logs", ItemType.Resource, true));
            items.Register(new ItemDefinition(
                new ContentId("bronze_hatchet"), "Bronze hatchet", ItemType.Tool, false, 1, "",
                new[] { EquipmentSlot.Weapon }, false, null, null, 1, 0, 1, GatheringToolKind.Hatchet));
            return items;
        }

        private static ResourceCatalog MakeResources(ResourceAvailabilityMode mode)
        {
            var resources = new ResourceCatalog();
            resources.Register(new ResourceDefinition(
                new ContentId("resource.oak_tree"), "Oak tree", SkillId.Woodcutting, 1, 25,
                new ContentId("oak_logs"), 8, mode, 1, 1, GatheringToolKind.Hatchet, 1));
            return resources;
        }

        private static PlayerState PlayerAt(int x, int y, ItemCatalog items)
        {
            var player = new PlayerState(Guid.NewGuid(), "Woodcutter");
            player.Location = new GridLocation(new GridCoord(x, y), WorldConstants.SurfacePlane, 0);
            InventoryRules.AddItem(player.Inventory, items, new ContentId("bronze_hatchet"), 1);
            return player;
        }

        private static ResourceNodeKey OakAt(int x, int y)
            => new ResourceNodeKey(
                new ContentId("resource.oak_tree"),
                new GridLocation(new GridCoord(x, y), WorldConstants.SurfacePlane, 0));
    }
}
