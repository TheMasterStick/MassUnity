using System;
using MassRPG.Core.Authority;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Data.Items;
using MassRPG.Data.Quests;
using MassRPG.Server.Authority;
using MassRPG.Server.Quests;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class QuestAuthorityGatewayTests
    {
        [Test]
        public void QuestStartAndClaimCrossAuthorityBoundary()
        {
            var items = MigrationSeedItemCatalog.Create();
            var questId = new ContentId("quest.gateway_test");
            var quests = new QuestCatalog();
            quests.Register(new QuestDefinition(
                questId,
                "Gateway Test",
                new[]
                {
                    new QuestObjectiveDefinition(
                        "talk",
                        QuestObjectiveKind.TalkToNpc,
                        1,
                        new ContentId("npc.guide"))
                }));

            var questService = new QuestService(quests, items);
            var inner = new LocalGameAuthority(items);
            var player = new PlayerState(Guid.NewGuid(), "Quest Tester");
            inner.RegisterPlayer(player);
            var gateway = new LocalAuthorityGateway(inner, quests: questService);

            var start = gateway.Submit(new StartQuestRequest(Guid.NewGuid(), player.CharacterId, questId), 1000);
            Assert.IsTrue(start.Accepted);

            var state = questService.GetOrCreateState(player.CharacterId);
            Assert.IsTrue(state.TryGetActive(questId, out var progress));
            Assert.AreEqual(QuestRunStatus.Active, progress.Status);

            questService.RecordContentEvent(player, QuestObjectiveKind.TalkToNpc, new ContentId("npc.guide"));
            Assert.AreEqual(QuestRunStatus.ReadyToClaim, progress.Status);

            var claim = gateway.Submit(new ClaimQuestRewardRequest(Guid.NewGuid(), player.CharacterId, questId), 1001);
            Assert.IsTrue(claim.Accepted);
            Assert.IsTrue(state.HasCompleted(questId));
        }

        [Test]
        public void MissingQuestServiceRejectsLifecycleRequests()
        {
            var items = MigrationSeedItemCatalog.Create();
            var inner = new LocalGameAuthority(items);
            var player = new PlayerState(Guid.NewGuid(), "Quest Tester");
            inner.RegisterPlayer(player);
            var gateway = new LocalAuthorityGateway(inner);
            var questId = new ContentId("quest.missing_service");

            var start = gateway.Submit(new StartQuestRequest(Guid.NewGuid(), player.CharacterId, questId), 1000);
            var claim = gateway.Submit(new ClaimQuestRewardRequest(Guid.NewGuid(), player.CharacterId, questId), 1000);

            Assert.IsFalse(start.Accepted);
            Assert.AreEqual("quests_unavailable", start.Code);
            Assert.IsFalse(claim.Accepted);
            Assert.AreEqual("quests_unavailable", claim.Code);
        }
    }
}
