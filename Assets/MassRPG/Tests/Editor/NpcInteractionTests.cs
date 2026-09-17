using System;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.World;
using MassRPG.Data.Npcs;
using MassRPG.Server.Npcs;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class NpcInteractionTests
    {
        [Test]
        public void ServicesRequireSameLayerAndConfiguredInteractionRange()
        {
            var dialogueId = new ContentId("dialogue.banker");
            var npcId = new ContentId("npc.banker");
            var npcs = new NpcCatalog();
            npcs.Register(new NpcDefinition(npcId, "Banker", new[]
            {
                new NpcServiceDefinition(NpcServiceKind.Dialogue, dialogueId),
                new NpcServiceDefinition(NpcServiceKind.Bank, new ContentId("bank.capital"))
            }, 1));
            var dialogues = new DialogueCatalog();
            dialogues.Register(new DialogueDefinition(dialogueId, "start", new[]
            {
                new DialogueNodeDefinition("start", "Good day.", new[] { new DialogueOptionDefinition("bye", "Goodbye.") })
            }));
            var registry = new NpcRegistry();
            var npc = new NpcState(Guid.NewGuid(), npcId, Loc(10, 10));
            registry.Register(npc);
            var service = new NpcInteractionService(npcs, dialogues, registry);
            var player = new PlayerState(Guid.NewGuid(), "Player") { Location = Loc(11, 10) };

            Assert.AreEqual(2, service.GetAvailableServices(player, npc.InstanceId).Count);
            player.Location = Loc(12, 10);
            Assert.AreEqual(0, service.GetAvailableServices(player, npc.InstanceId).Count);
            player.Location = new GridLocation(new GridCoord(10, 10), -1, 0);
            Assert.AreEqual(0, service.GetAvailableServices(player, npc.InstanceId).Count);
        }

        [Test]
        public void DialogueAdvancesOnlyThroughServerDefinedOptionsAndCanEmitAction()
        {
            var dialogueId = new ContentId("dialogue.shopkeeper");
            var npcId = new ContentId("npc.shopkeeper");
            var openShop = new ContentId("action.open_shop.general");
            var npcs = new NpcCatalog();
            npcs.Register(new NpcDefinition(npcId, "Shopkeeper", new[]
            {
                new NpcServiceDefinition(NpcServiceKind.Dialogue, dialogueId)
            }));
            var dialogues = new DialogueCatalog();
            dialogues.Register(new DialogueDefinition(dialogueId, "start", new[]
            {
                new DialogueNodeDefinition("start", "Need anything?", new[]
                {
                    new DialogueOptionDefinition("trade", "Show me your wares.", "trade_confirm"),
                    new DialogueOptionDefinition("bye", "Nothing today.")
                }),
                new DialogueNodeDefinition("trade_confirm", "Certainly.", new[]
                {
                    new DialogueOptionDefinition("open", "Trade.", null, openShop)
                })
            }));
            var registry = new NpcRegistry();
            var npc = new NpcState(Guid.NewGuid(), npcId, Loc(5, 5));
            registry.Register(npc);
            var service = new NpcInteractionService(npcs, dialogues, registry);
            var player = new PlayerState(Guid.NewGuid(), "Player") { Location = Loc(5, 6) };

            var begin = service.BeginDialogue(player, npc.InstanceId, dialogueId, out var session);
            Assert.IsTrue(begin.Success);
            Assert.AreEqual("start", begin.Node.Id);

            var invalid = service.Choose(player, session, "client_invented_node");
            Assert.IsFalse(invalid.Success);
            Assert.AreEqual("option_invalid", invalid.Code);
            Assert.AreEqual("start", session.NodeId);

            var trade = service.Choose(player, session, "trade");
            Assert.IsTrue(trade.Success);
            Assert.AreEqual("trade_confirm", trade.Node.Id);
            Assert.IsFalse(trade.Ended);

            var open = service.Choose(player, session, "open");
            Assert.IsTrue(open.Success);
            Assert.IsTrue(open.Ended);
            Assert.AreEqual(openShop, open.ActionId.Value);
            Assert.IsTrue(session.IsClosed);
        }

        [Test]
        public void DialogueGraphRejectsMissingNextNodeAtDataConstructionTime()
        {
            Assert.Throws<InvalidOperationException>(() => new DialogueDefinition(
                new ContentId("dialogue.invalid"),
                "start",
                new[]
                {
                    new DialogueNodeDefinition("start", "Broken", new[]
                    {
                        new DialogueOptionDefinition("go", "Continue", "missing")
                    })
                }));
        }

        private static GridLocation Loc(int x, int y)
            => new GridLocation(new GridCoord(x, y), WorldConstants.SurfacePlane, 0);
    }
}
