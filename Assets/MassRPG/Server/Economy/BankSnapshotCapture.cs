using System;
using System.Collections.Generic;
using MassRPG.Core.Economy;

namespace MassRPG.Server.Economy
{
    /// <summary>
    /// Copies authoritative character-bank state into the immutable Core read model consumed by
    /// presentation/network layers. The server retains ownership of the mutable bank dictionary.
    /// </summary>
    public static class BankSnapshotCapture
    {
        public static BankSnapshot Capture(CharacterBankState bank)
        {
            if (bank == null) throw new ArgumentNullException(nameof(bank));

            var entries = new List<BankEntrySnapshot>();
            foreach (var entry in bank.Entries)
                entries.Add(new BankEntrySnapshot(entry.Key, entry.Value));

            return new BankSnapshot(entries);
        }
    }
}
