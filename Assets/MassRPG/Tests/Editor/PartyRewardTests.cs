using System;
using System.Collections.Generic;
using MassRPG.Core.Combat;
using MassRPG.Server.Combat;
using MassRPG.Server.Parties;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class PartyRewardTests
    {
        [Test]
        public void PartyMembershipIsExclusiveAndOnlyLeaderChangesLootMode()
        {
            var registry = new PartyRegistry();
            var leader = Guid.NewGuid();
            var member = Guid.NewGuid();
            var party = registry.Create(Guid.NewGuid(), leader);

            Assert.IsTrue(registry.AddMember(party.PartyId, member));
            Assert.IsFalse(party.SetLootMode(member, PartyLootMode.NeedGreed));
            Assert.AreEqual(PartyLootMode.RoundRobin, party.LootMode);
            Assert.IsTrue(party.SetLootMode(leader, PartyLootMode.NeedGreed));
            Assert.AreEqual(PartyLootMode.NeedGreed, party.LootMode);

            var otherParty = registry.Create(Guid.NewGuid(), Guid.NewGuid());
            Assert.IsFalse(registry.AddMember(otherParty.PartyId, member));
            Assert.IsTrue(registry.TryGetForCharacter(member, out var resolved));
            Assert.AreSame(party, resolved);
        }

        [Test]
        public void RoundRobinSkipsUnavailableMembersWithoutRemovingThem()
        {
            var registry = new PartyRegistry();
            var leader = Guid.NewGuid();
            var second = Guid.NewGuid();
            var third = Guid.NewGuid();
            var party = registry.Create(Guid.NewGuid(), leader);
            registry.AddMember(party.PartyId, second);
            registry.AddMember(party.PartyId, third);

            var eligible = new HashSet<Guid> { leader, third };
            Assert.IsTrue(party.TryTakeNextRoundRobin(eligible, out var first));
            Assert.AreEqual(leader, first);
            Assert.IsTrue(party.TryTakeNextRoundRobin(eligible, out var next));
            Assert.AreEqual(third, next);
            Assert.IsTrue(party.TryTakeNextRoundRobin(eligible, out var wrapped));
            Assert.AreEqual(leader, wrapped);
            Assert.AreEqual(3, party.Count);
        }

        [Test]
        public void RewardPlannerSharesPartyRewardWithNearbyMembersAndKeepsOutsiderPersonal()
        {
            var parties = new PartyRegistry();
            var leader = Guid.NewGuid();
            var contributor = Guid.NewGuid();
            var nearbyNonContributor = Guid.NewGuid();
            var outOfRangeMember = Guid.NewGuid();
            var outsider = Guid.NewGuid();
            var party = parties.Create(Guid.NewGuid(), leader, PartyLootMode.RoundRobin);
            parties.AddMember(party.PartyId, contributor);
            parties.AddMember(party.PartyId, nearbyNonContributor);
            parties.AddMember(party.PartyId, outOfRangeMember);

            var target = Guid.NewGuid();
            var ledger = new CombatContributionLedger();
            ledger.Record(new CombatContribution(target, leader, 40, 100));
            ledger.Record(new CombatContribution(target, contributor, 20, 110));
            ledger.Record(new CombatContribution(target, outsider, 40, 120));
            Assert.IsTrue(ledger.TrySnapshot(target, out var snapshot));

            var planner = new CombatRewardPlanner(
                parties,
                new ContributionEligibilityPolicy(minimumDamage: 10, minimumDamageFraction: 0.10));
            var plan = planner.Build(snapshot, new[]
            {
                new CombatRewardPresence(leader, true),
                new CombatRewardPresence(contributor, true),
                new CombatRewardPresence(nearbyNonContributor, true),
                new CombatRewardPresence(outOfRangeMember, false),
                new CombatRewardPresence(outsider, true)
            });

            Assert.IsTrue(plan.InitialClaim.IsParty);
            Assert.AreEqual(party.PartyId, plan.InitialClaim.PartyId.Value);
            Assert.AreEqual(leader, plan.InitialClaim.FirstEngagerCharacterId);
            Assert.AreEqual(PartyLootMode.RoundRobin, plan.InitialClaim.LootMode);

            Assert.IsTrue(plan.TryGetPartyGroup(party.PartyId, out var partyGroup));
            Assert.AreEqual(60, partyGroup.EligibleContributionDamage);
            CollectionAssert.AreEquivalent(new[] { leader, contributor }, partyGroup.DirectContributors);
            CollectionAssert.AreEquivalent(new[] { leader, contributor, nearbyNonContributor }, partyGroup.SharedRecipients);
            CollectionAssert.DoesNotContain(partyGroup.SharedRecipients, outOfRangeMember);

            var split = partyGroup.SplitMoney(100);
            Assert.AreEqual(33, split.PerRecipient);
            Assert.AreEqual(1, split.Remainder);
            Assert.AreEqual(3, split.Recipients.Count);

            Assert.IsTrue(plan.TryGetSoloGroup(outsider, out var outsiderGroup));
            Assert.AreEqual(40, outsiderGroup.EligibleContributionDamage);
            CollectionAssert.AreEqual(new[] { outsider }, outsiderGroup.SharedRecipients);
        }

        [Test]
        public void FirstEngagerContextSurvivesEvenWhenTokenContributionFailsEligibility()
        {
            var parties = new PartyRegistry();
            var tapper = Guid.NewGuid();
            var partner = Guid.NewGuid();
            var finisher = Guid.NewGuid();
            var party = parties.Create(Guid.NewGuid(), tapper, PartyLootMode.LeaderDistribution);
            parties.AddMember(party.PartyId, partner);

            var target = Guid.NewGuid();
            var ledger = new CombatContributionLedger();
            ledger.Record(new CombatContribution(target, tapper, 5, 100));
            ledger.Record(new CombatContribution(target, finisher, 95, 110));
            Assert.IsTrue(ledger.TrySnapshot(target, out var snapshot));

            var planner = new CombatRewardPlanner(
                parties,
                new ContributionEligibilityPolicy(minimumDamage: 10, minimumDamageFraction: 0.10));
            var plan = planner.Build(snapshot, new[]
            {
                new CombatRewardPresence(tapper, true),
                new CombatRewardPresence(partner, true),
                new CombatRewardPresence(finisher, true)
            });

            Assert.IsTrue(plan.InitialClaim.IsParty);
            Assert.AreEqual(party.PartyId, plan.InitialClaim.PartyId.Value);
            Assert.AreEqual(tapper, plan.InitialClaim.FirstEngagerCharacterId);
            Assert.IsFalse(plan.TryGetPartyGroup(party.PartyId, out _));
            Assert.IsTrue(plan.TryGetSoloGroup(finisher, out var eligibleFinisher));
            Assert.AreEqual(95, eligibleFinisher.EligibleContributionDamage);
        }
    }
}
