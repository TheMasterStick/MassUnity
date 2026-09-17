using MassRPG.Core.Content;
using MassRPG.Core.Economy;
using MassRPG.Server.Economy;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class BankSnapshotTests
    {
        [Test]
        public void CaptureSortsEntriesByPermanentContentId()
        {
            var bank = new CharacterBankState();
            var zItem = new ContentId("z_item");
            var aItem = new ContentId("a_item");
            bank.Add(zItem, 2);
            bank.Add(aItem, 4);

            var snapshot = BankSnapshotCapture.Capture(bank);

            Assert.AreEqual(2, snapshot.Entries.Count);
            Assert.AreEqual(aItem, snapshot.Entries[0].ItemId);
            Assert.AreEqual(4, snapshot.Entries[0].Quantity);
            Assert.AreEqual(zItem, snapshot.Entries[1].ItemId);
            Assert.AreEqual(2, snapshot.Entries[1].Quantity);
        }

        [Test]
        public void CapturedSnapshotDoesNotChangeWhenAuthoritativeBankMutates()
        {
            var bank = new CharacterBankState();
            var item = new ContentId("iron_ore");
            bank.Add(item, 3);
            var before = BankSnapshotCapture.Capture(bank);

            bank.Add(item, 5);
            var after = BankSnapshotCapture.Capture(bank);

            Assert.AreEqual(3, before.Count(item));
            Assert.AreEqual(8, after.Count(item));
        }

        [Test]
        public void CaptureOfEmptyBankProducesEmptySnapshot()
        {
            var snapshot = BankSnapshotCapture.Capture(new CharacterBankState());

            Assert.AreEqual(0, snapshot.Entries.Count);
            Assert.AreEqual(0, snapshot.Count(new ContentId("coins")));
        }

        [Test]
        public void SnapshotRejectsDuplicateItemIds()
        {
            var item = new ContentId("coal");

            Assert.Throws<System.ArgumentException>(() => new BankSnapshot(new[]
            {
                new BankEntrySnapshot(item, 1),
                new BankEntrySnapshot(item, 2)
            }));
        }
    }
}
