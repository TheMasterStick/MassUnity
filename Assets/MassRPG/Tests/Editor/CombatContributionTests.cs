using System;
using MassRPG.Core.Combat;
using MassRPG.Server.Combat;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class CombatContributionTests
    {
        [Test]
        public void Ledger_TracksFirstEngagerAndDamageWithoutHardcodingEligibility()
        {
            var ledger = new CombatContributionLedger();
            var target = Guid.NewGuid();
            var first = Guid.NewGuid();
            var helper = Guid.NewGuid();

            ledger.Record(new CombatContribution(target, first, 10, 1000));
            ledger.Record(new CombatContribution(target, helper, 30, 1100));
            ledger.Record(new CombatContribution(target, first, 5, 1200));

            Assert.IsTrue(ledger.TrySnapshot(target, out var snapshot));
            Assert.AreEqual(45, snapshot.TotalDamage);
            Assert.AreEqual(2, snapshot.Entries.Count);

            CombatContributionEntry firstEntry = default;
            for (var i = 0; i < snapshot.Entries.Count; i++)
                if (snapshot.Entries[i].CharacterId == first) firstEntry = snapshot.Entries[i];
            Assert.IsTrue(firstEntry.FirstEngager);
            Assert.AreEqual(15, firstEntry.Damage);
        }

        [Test]
        public void EligibilityPolicy_IsConfigurableInsteadOfBakingInFinalAntiPowerLevelNumbers()
        {
            var ledger = new CombatContributionLedger();
            var target = Guid.NewGuid();
            var tagger = Guid.NewGuid();
            var fighter = Guid.NewGuid();
            ledger.Record(new CombatContribution(target, tagger, 1, 1000));
            ledger.Record(new CombatContribution(target, fighter, 99, 1100));
            Assert.IsTrue(ledger.TrySnapshot(target, out var snapshot));

            var policy = new ContributionEligibilityPolicy(minimumDamage: 5, minimumDamageFraction: 0.05);
            Assert.IsFalse(policy.IsEligible(snapshot, tagger));
            Assert.IsTrue(policy.IsEligible(snapshot, fighter));

            policy.MinimumDamage = 1;
            policy.MinimumDamageFraction = 0.0;
            Assert.IsTrue(policy.IsEligible(snapshot, tagger));
        }
    }
}
