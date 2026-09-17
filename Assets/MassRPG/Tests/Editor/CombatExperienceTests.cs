using System;
using System.Collections.Generic;
using MassRPG.Core.Characters;
using MassRPG.Core.Combat;
using MassRPG.Core.Skills;
using MassRPG.Server.Combat;
using MassRPG.Server.Parties;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class CombatExperienceTests
    {
        [Test]
        public void AggressiveMeleePreservesBrowserDamageXpSplit()
        {
            var player = new PlayerState(Guid.NewGuid(), "Fighter");
            player.CombatStyle = CombatStyle.Melee;
            player.MeleeTrainingStyle = MeleeTrainingStyle.Aggressive;
            var strengthBefore = player.Skills.GetXp(SkillId.Strength);
            var hitpointsBefore = player.Skills.GetXp(SkillId.Hitpoints);
            var policy = new BrowserCombatExperiencePolicy();

            policy.Apply(player, policy.CalculateForDamage(10));

            Assert.AreEqual(strengthBefore + 13, player.Skills.GetXp(SkillId.Strength));
            Assert.AreEqual(hitpointsBefore + 3, player.Skills.GetXp(SkillId.Hitpoints));
        }

        [Test]
        public void AccurateDefensiveRangedAndMagicTrainTheirExpectedSkills()
        {
            AssertSingleCombatSkill(CombatStyle.Melee, MeleeTrainingStyle.Accurate, SkillId.Attack);
            AssertSingleCombatSkill(CombatStyle.Melee, MeleeTrainingStyle.Defensive, SkillId.Defence);
            AssertSingleCombatSkill(CombatStyle.Ranged, MeleeTrainingStyle.Aggressive, SkillId.Ranged);
            AssertSingleCombatSkill(CombatStyle.Magic, MeleeTrainingStyle.Aggressive, SkillId.Magic);
        }

        [Test]
        public void ControlledSharesOneRoundedCombatAwardAcrossMeleeSkills()
        {
            var player = new PlayerState(Guid.NewGuid(), "Controlled");
            player.CombatStyle = CombatStyle.Melee;
            player.MeleeTrainingStyle = MeleeTrainingStyle.Controlled;
            var attackBefore = player.Skills.GetXp(SkillId.Attack);
            var strengthBefore = player.Skills.GetXp(SkillId.Strength);
            var defenceBefore = player.Skills.GetXp(SkillId.Defence);
            var policy = new BrowserCombatExperiencePolicy();

            policy.Apply(player, policy.CalculateForDamage(10));

            Assert.AreEqual(attackBefore + 5, player.Skills.GetXp(SkillId.Attack));
            Assert.AreEqual(strengthBefore + 4, player.Skills.GetXp(SkillId.Strength));
            Assert.AreEqual(defenceBefore + 4, player.Skills.GetXp(SkillId.Defence));
        }

        [Test]
        public void HitpointsLevelGainHealsOnlyTheGainedLevelAmount()
        {
            var player = new PlayerState(Guid.NewGuid(), "Tank");
            player.CurrentHitpoints = 5;
            player.Skills.SetXp(SkillId.Hitpoints, SkillProgression.XpForLevel(11) - 1);
            Assert.AreEqual(10, player.Skills.GetLevel(SkillId.Hitpoints));
            var policy = new BrowserCombatExperiencePolicy(0.0, 1.0);

            policy.Apply(player, policy.CalculateForDamage(1));

            Assert.AreEqual(11, player.Skills.GetLevel(SkillId.Hitpoints));
            Assert.AreEqual(6, player.CurrentHitpoints);
        }

        [Test]
        public void MissOrZeroDamageCalculatesNoExperience()
        {
            var award = new BrowserCombatExperiencePolicy().CalculateForDamage(0);
            Assert.AreEqual(0, award.CombatXp);
            Assert.AreEqual(0, award.HitpointsXp);
        }

        [Test]
        public void PartySettlementSharesFiniteXpBudgetWithNearbyMembersAndKeepsOutsiderPersonal()
        {
            var parties = new PartyRegistry();
            var leaderId = Guid.NewGuid();
            var contributorId = Guid.NewGuid();
            var nearbyId = Guid.NewGuid();
            var outsiderId = Guid.NewGuid();
            var party = parties.Create(Guid.NewGuid(), leaderId);
            parties.AddMember(party.PartyId, contributorId);
            parties.AddMember(party.PartyId, nearbyId);

            var target = Guid.NewGuid();
            var ledger = new CombatContributionLedger();
            ledger.Record(new CombatContribution(target, leaderId, 40, 100));
            ledger.Record(new CombatContribution(target, contributorId, 20, 110));
            ledger.Record(new CombatContribution(target, outsiderId, 40, 120));
            Assert.IsTrue(ledger.TrySnapshot(target, out var snapshot));

            var planner = new CombatRewardPlanner(
                parties,
                new ContributionEligibilityPolicy(minimumDamage: 10, minimumDamageFraction: 0.10));
            var plan = planner.Build(snapshot, new[]
            {
                new CombatRewardPresence(leaderId, true),
                new CombatRewardPresence(contributorId, true),
                new CombatRewardPresence(nearbyId, true),
                new CombatRewardPresence(outsiderId, true)
            });

            var players = new Dictionary<Guid, PlayerState>
            {
                [leaderId] = NewAggressiveMelee(leaderId),
                [contributorId] = NewAggressiveMelee(contributorId),
                [nearbyId] = NewAggressiveMelee(nearbyId),
                [outsiderId] = NewRanged(outsiderId)
            };
            var strengthBefore = new Dictionary<Guid, long>
            {
                [leaderId] = players[leaderId].Skills.GetXp(SkillId.Strength),
                [contributorId] = players[contributorId].Skills.GetXp(SkillId.Strength),
                [nearbyId] = players[nearbyId].Skills.GetXp(SkillId.Strength)
            };
            var outsiderRangedBefore = players[outsiderId].Skills.GetXp(SkillId.Ranged);

            var result = new CombatExperienceSettlementService().Settle(
                plan,
                id => players.TryGetValue(id, out var player) ? player : null);

            Assert.IsTrue(result.Complete);
            Assert.AreEqual(4, result.Awards.Count);
            Assert.AreEqual(strengthBefore[leaderId] + 27, players[leaderId].Skills.GetXp(SkillId.Strength));
            Assert.AreEqual(strengthBefore[contributorId] + 27, players[contributorId].Skills.GetXp(SkillId.Strength));
            Assert.AreEqual(strengthBefore[nearbyId] + 26, players[nearbyId].Skills.GetXp(SkillId.Strength));
            Assert.AreEqual(outsiderRangedBefore + 53, players[outsiderId].Skills.GetXp(SkillId.Ranged));

            var totalCombatXp = 0;
            var totalHitpointsXp = 0;
            for (var i = 0; i < result.Awards.Count; i++)
            {
                totalCombatXp += result.Awards[i].CombatXp;
                totalHitpointsXp += result.Awards[i].HitpointsXp;
            }
            Assert.AreEqual(133, totalCombatXp);
            Assert.AreEqual(33, totalHitpointsXp);
        }

        private static void AssertSingleCombatSkill(CombatStyle style, MeleeTrainingStyle meleeStyle, SkillId expected)
        {
            var player = new PlayerState(Guid.NewGuid(), expected.ToString());
            player.CombatStyle = style;
            player.MeleeTrainingStyle = meleeStyle;
            var before = player.Skills.GetXp(expected);
            var policy = new BrowserCombatExperiencePolicy();

            policy.Apply(player, policy.CalculateForDamage(3));

            Assert.AreEqual(before + 4, player.Skills.GetXp(expected));
        }

        private static PlayerState NewAggressiveMelee(Guid id)
        {
            var player = new PlayerState(id, "Party");
            player.CombatStyle = CombatStyle.Melee;
            player.MeleeTrainingStyle = MeleeTrainingStyle.Aggressive;
            return player;
        }

        private static PlayerState NewRanged(Guid id)
        {
            var player = new PlayerState(id, "Outsider");
            player.CombatStyle = CombatStyle.Ranged;
            return player;
        }
    }
}
