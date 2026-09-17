using System;
using MassRPG.Core.Content;
using MassRPG.Server.Loot;
using MassRPG.Server.Parties;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class PartyLootPoolTests
    {
        [Test]
        public void RoundRobinAssignsAcrossEligibleMembersAndSkipsOutOfRangePartyMember()
        {
            var leader = Guid.NewGuid();
            var second = Guid.NewGuid();
            var outOfRange = Guid.NewGuid();
            var party = CreateParty(PartyLootMode.RoundRobin, leader, second, outOfRange);
            var service = new PartyLootPoolService();

            var pool = service.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                party,
                new[] { leader, second },
                new[]
                {
                    new LootStack(new ContentId("item.a"), 1),
                    new LootStack(new ContentId("item.b"), 1),
                    new LootStack(new ContentId("item.c"), 1)
                });

            Assert.AreEqual(leader, pool.Entries[0].AssignedCharacterId);
            Assert.AreEqual(second, pool.Entries[1].AssignedCharacterId);
            Assert.AreEqual(leader, pool.Entries[2].AssignedCharacterId);
            Assert.AreNotEqual(outOfRange, pool.Entries[0].AssignedCharacterId);
            Assert.IsTrue(service.Claim(pool.PoolId, pool.Entries[0].EntryId, leader).Success);
            Assert.IsFalse(service.Claim(pool.PoolId, pool.Entries[1].EntryId, leader).Success);
        }

        [Test]
        public void NeedAlwaysBeatsGreedAndOnlyEligibleMembersCanVote()
        {
            var leader = Guid.NewGuid();
            var needer = Guid.NewGuid();
            var greeder = Guid.NewGuid();
            var stranger = Guid.NewGuid();
            var party = CreateParty(PartyLootMode.NeedGreed, leader, needer, greeder);
            var service = new PartyLootPoolService();
            var pool = service.Create(
                Guid.NewGuid(), Guid.NewGuid(), party,
                new[] { leader, needer, greeder },
                new[] { new LootStack(new ContentId("rare_sword"), 1) });
            var entry = pool.Entries[0];

            Assert.IsFalse(service.SubmitNeedGreed(pool.PoolId, entry.EntryId, stranger, NeedGreedChoice.Need).Success);
            Assert.IsTrue(service.SubmitNeedGreed(pool.PoolId, entry.EntryId, leader, NeedGreedChoice.Pass).Success);
            Assert.IsTrue(service.SubmitNeedGreed(pool.PoolId, entry.EntryId, greeder, NeedGreedChoice.Greed).Success);
            Assert.IsFalse(service.ResolveNeedGreed(pool.PoolId, entry.EntryId, _ => 0).Success);
            Assert.IsTrue(service.SubmitNeedGreed(pool.PoolId, entry.EntryId, needer, NeedGreedChoice.Need).Success);

            var result = service.ResolveNeedGreed(pool.PoolId, entry.EntryId, _ => 0);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(needer, result.CharacterId);
            Assert.AreEqual(needer, entry.AssignedCharacterId);
            Assert.IsTrue(service.Claim(pool.PoolId, entry.EntryId, needer).Success);
        }

        [Test]
        public void NeedGreedTieUsesInjectedSelectorAndForcedTimeoutCanIgnoreMissingVotes()
        {
            var leader = Guid.NewGuid();
            var second = Guid.NewGuid();
            var silent = Guid.NewGuid();
            var party = CreateParty(PartyLootMode.NeedGreed, leader, second, silent);
            var service = new PartyLootPoolService();
            var pool = service.Create(
                Guid.NewGuid(), Guid.NewGuid(), party,
                new[] { leader, second, silent },
                new[] { new LootStack(new ContentId("gem"), 1) });
            var entry = pool.Entries[0];

            service.SubmitNeedGreed(pool.PoolId, entry.EntryId, leader, NeedGreedChoice.Greed);
            service.SubmitNeedGreed(pool.PoolId, entry.EntryId, second, NeedGreedChoice.Greed);
            var result = service.ResolveNeedGreed(pool.PoolId, entry.EntryId, count => count - 1, force: true);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(second, result.CharacterId);
        }

        [Test]
        public void LeaderDistributionRequiresLeaderAndEligibleRecipient()
        {
            var leader = Guid.NewGuid();
            var member = Guid.NewGuid();
            var stranger = Guid.NewGuid();
            var party = CreateParty(PartyLootMode.LeaderDistribution, leader, member);
            var service = new PartyLootPoolService();
            var pool = service.Create(
                Guid.NewGuid(), Guid.NewGuid(), party,
                new[] { leader, member },
                new[] { new LootStack(new ContentId("drop"), 2) });
            var entry = pool.Entries[0];

            Assert.AreEqual("leader_has_not_assigned", service.Claim(pool.PoolId, entry.EntryId, member).Code);
            Assert.AreEqual("not_party_leader", service.AssignByLeader(pool.PoolId, entry.EntryId, member, member).Code);
            Assert.AreEqual("recipient_not_eligible", service.AssignByLeader(pool.PoolId, entry.EntryId, leader, stranger).Code);
            Assert.IsTrue(service.AssignByLeader(pool.PoolId, entry.EntryId, leader, member).Success);
            Assert.IsTrue(service.Claim(pool.PoolId, entry.EntryId, member).Success);
        }

        [Test]
        public void FreeForAllFirstEligibleClaimWins()
        {
            var leader = Guid.NewGuid();
            var member = Guid.NewGuid();
            var party = CreateParty(PartyLootMode.FreeForAll, leader, member);
            var service = new PartyLootPoolService();
            var pool = service.Create(
                Guid.NewGuid(), Guid.NewGuid(), party,
                new[] { leader, member },
                new[] { new LootStack(new ContentId("drop"), 1) });
            var entry = pool.Entries[0];

            Assert.IsTrue(service.Claim(pool.PoolId, entry.EntryId, member).Success);
            Assert.AreEqual(member, entry.ClaimedByCharacterId);
            Assert.AreEqual("already_claimed", service.Claim(pool.PoolId, entry.EntryId, leader).Code);
        }

        [Test]
        public void AllPassNeedGreedClosesEntryWithoutInventingWinner()
        {
            var leader = Guid.NewGuid();
            var member = Guid.NewGuid();
            var party = CreateParty(PartyLootMode.NeedGreed, leader, member);
            var service = new PartyLootPoolService();
            var pool = service.Create(
                Guid.NewGuid(), Guid.NewGuid(), party,
                new[] { leader, member },
                new[] { new LootStack(new ContentId("drop"), 1) });
            var entry = pool.Entries[0];

            service.SubmitNeedGreed(pool.PoolId, entry.EntryId, leader, NeedGreedChoice.Pass);
            service.SubmitNeedGreed(pool.PoolId, entry.EntryId, member, NeedGreedChoice.Pass);
            var result = service.ResolveNeedGreed(pool.PoolId, entry.EntryId, _ => 0);

            Assert.IsTrue(result.Success);
            Assert.IsFalse(result.CharacterId.HasValue);
            Assert.AreEqual(SharedLootEntryState.ResolvedNoWinner, entry.State);
            Assert.AreEqual("entry_closed", service.Claim(pool.PoolId, entry.EntryId, leader).Code);
        }

        private static PartyState CreateParty(PartyLootMode mode, Guid leader, params Guid[] members)
        {
            var registry = new PartyRegistry();
            var party = registry.Create(Guid.NewGuid(), leader, mode);
            for (var i = 0; i < members.Length; i++)
                Assert.IsTrue(registry.AddMember(party.PartyId, members[i]));
            return party;
        }
    }
}
