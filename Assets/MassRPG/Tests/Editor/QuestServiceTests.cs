using System;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Skills;
using MassRPG.Core.World;
using MassRPG.Data.Items;
using MassRPG.Data.Quests;
using MassRPG.Server.Quests;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class QuestServiceTests
    {
        [Test]
        public void QuestRequiresPrerequisiteAndSkillThenTracksAuthoritativeEvents()
        {
            var items = MigrationSeedItemCatalog.Create();
            var quests = new QuestCatalog();
            var prerequisite = new ContentId("quest.intro");
            var targetQuest = new ContentId("quest.goblin_patrol");
            quests.Register(new QuestDefinition(
                prerequisite,
                "Introduction",
                new[] { new QuestObjectiveDefinition("talk", QuestObjectiveKind.TalkToNpc, 1, new ContentId("npc.guide")) }));
            quests.Register(new QuestDefinition(
                targetQuest,
                "Goblin Patrol",
                new[]
                {
                    new QuestObjectiveDefinition("kill_goblins", QuestObjectiveKind.KillCreature, 3, new ContentId("goblin")),
                    new QuestObjectiveDefinition("reach_gate", QuestObjectiveKind.ReachLocation, 1, null, Loc(20, 20), 1)
                },
                new[] { prerequisite },
                new[] { new QuestSkillRequirement(SkillId.Attack, 5) },
                new[] { new QuestItemReward(new ContentId("coins"), 100) },
                new[] { new QuestXpReward(SkillId.Attack, 250) }));
            var service = new QuestService(quests, items);
            var player = new PlayerState(Guid.NewGuid(), "Quester");

            Assert.AreEqual("prerequisite_quest_missing", service.TryStart(player, targetQuest).Code);
            Assert.IsTrue(service.TryStart(player, prerequisite).Success);
            service.RecordContentEvent(player, QuestObjectiveKind.TalkToNpc, new ContentId("npc.guide"));
            Assert.IsTrue(service.TryClaim(player, prerequisite).Success);

            Assert.AreEqual("skill_requirement_not_met", service.TryStart(player, targetQuest).Code);
            player.Skills.SetXp(SkillId.Attack, SkillProgression.XpForLevel(5));
            Assert.IsTrue(service.TryStart(player, targetQuest).Success);
            service.RecordContentEvent(player, QuestObjectiveKind.KillCreature, new ContentId("goblin"), 2);
            service.RecordContentEvent(player, QuestObjectiveKind.KillCreature, new ContentId("goblin"), 1);
            player.Location = Loc(21, 20);
            service.RecordLocation(player);

            var state = service.GetOrCreateState(player.CharacterId);
            Assert.IsTrue(state.TryGetActive(targetQuest, out var progress));
            Assert.AreEqual(3, progress.GetCount("kill_goblins"));
            Assert.AreEqual(1, progress.GetCount("reach_gate"));
            Assert.AreEqual(QuestRunStatus.ReadyToClaim, progress.Status);

            var beforeXp = player.Skills.GetXp(SkillId.Attack);
            Assert.IsTrue(service.TryClaim(player, targetQuest).Success);
            Assert.AreEqual(100, player.Inventory.CountItem(new ContentId("coins")));
            Assert.AreEqual(beforeXp + 250, player.Skills.GetXp(SkillId.Attack));
            Assert.IsTrue(state.HasCompleted(targetQuest));
        }

        [Test]
        public void RewardClaimIsAtomicWhenInventoryCannotFitNonStackableRewards()
        {
            var items = MigrationSeedItemCatalog.Create();
            var quests = new QuestCatalog();
            var questId = new ContentId("quest.full_inventory");
            quests.Register(new QuestDefinition(
                questId,
                "Too Much Loot",
                new[] { new QuestObjectiveDefinition("talk", QuestObjectiveKind.TalkToNpc, 1, new ContentId("npc.guide")) },
                itemRewards: new[]
                {
                    new QuestItemReward(new ContentId("bronze_sword"), 2),
                    new QuestItemReward(new ContentId("coins"), 50)
                }));
            var player = new PlayerState(Guid.NewGuid(), "Packed", 1);
            var service = new QuestService(quests, items);
            Assert.IsTrue(service.TryStart(player, questId).Success);
            service.RecordContentEvent(player, QuestObjectiveKind.TalkToNpc, new ContentId("npc.guide"));

            var result = service.TryClaim(player, questId);
            Assert.IsFalse(result.Success);
            Assert.AreEqual("inventory_full", result.Code);
            Assert.AreEqual(0, player.Inventory.CountItem(new ContentId("bronze_sword")));
            Assert.AreEqual(0, player.Inventory.CountItem(new ContentId("coins")));
        }

        [Test]
        public void ClientCannotSkipQuestProgressByReportingUnrelatedContent()
        {
            var items = MigrationSeedItemCatalog.Create();
            var quests = new QuestCatalog();
            var questId = new ContentId("quest.wolves");
            quests.Register(new QuestDefinition(
                questId,
                "Wolf Hunt",
                new[] { new QuestObjectiveDefinition("wolves", QuestObjectiveKind.KillCreature, 2, new ContentId("wolf")) }));
            var player = new PlayerState(Guid.NewGuid(), "Hunter");
            var service = new QuestService(quests, items);
            Assert.IsTrue(service.TryStart(player, questId).Success);

            service.RecordContentEvent(player, QuestObjectiveKind.KillCreature, new ContentId("goblin"), 999);
            var state = service.GetOrCreateState(player.CharacterId);
            Assert.IsTrue(state.TryGetActive(questId, out var progress));
            Assert.AreEqual(0, progress.GetCount("wolves"));
            Assert.AreEqual(QuestRunStatus.Active, progress.Status);
        }

        private static GridLocation Loc(int x, int y)
            => new GridLocation(new GridCoord(x, y), WorldConstants.SurfacePlane, 0);
    }
}
