using System;
using MassRPG.Core.Content;
using MassRPG.Core.Resources;
using MassRPG.Core.World;
using MassRPG.Server.Resources;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class ResourceDepletionTests
    {
        private static readonly ResourceNodeKey Oak = new ResourceNodeKey(
            new ContentId("resource.oak_tree"),
            new GridLocation(new GridCoord(100, 200), WorldConstants.SurfacePlane, 0));

        [Test]
        public void PersonalNode_DepletionOnlyAffectsTheHarvestingCharacter()
        {
            var ledger = new ResourceDepletionLedger();
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            const long now = 1_000_000;

            ledger.Deplete(a, Oak, ResourceAvailabilityMode.Personal, 8, now);

            Assert.IsFalse(ledger.IsAvailable(a, Oak, ResourceAvailabilityMode.Personal, now + 1000));
            Assert.IsTrue(ledger.IsAvailable(b, Oak, ResourceAvailabilityMode.Personal, now + 1000));
        }

        [Test]
        public void SharedNode_DepletionAffectsEveryone()
        {
            var ledger = new ResourceDepletionLedger();
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            const long now = 2_000_000;

            ledger.Deplete(a, Oak, ResourceAvailabilityMode.Shared, 30, now);

            Assert.IsFalse(ledger.IsAvailable(a, Oak, ResourceAvailabilityMode.Shared, now + 1000));
            Assert.IsFalse(ledger.IsAvailable(b, Oak, ResourceAvailabilityMode.Shared, now + 1000));
        }

        [Test]
        public void ExpiredState_RemovesItselfInsteadOfBecomingPermanentHistory()
        {
            var ledger = new ResourceDepletionLedger();
            var player = Guid.NewGuid();
            const long now = 5_000_000;
            ledger.Deplete(player, Oak, ResourceAvailabilityMode.Personal, 8, now);
            Assert.AreEqual(1, ledger.ActivePersonalExceptions);

            Assert.IsTrue(ledger.IsAvailable(player, Oak, ResourceAvailabilityMode.Personal, now + 8000));
            Assert.AreEqual(0, ledger.ActivePersonalExceptions);
        }
    }
}
