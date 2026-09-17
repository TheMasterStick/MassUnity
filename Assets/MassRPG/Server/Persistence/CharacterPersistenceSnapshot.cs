using System;
using System.Collections.Generic;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Core.Persistence;
using MassRPG.Core.World;
using MassRPG.Server.Death;
using MassRPG.Server.Economy;
using MassRPG.Server.Travel;

namespace MassRPG.Server.Persistence
{
    public readonly struct BankEntrySnapshot
    {
        public BankEntrySnapshot(ContentId itemId, int quantity)
        {
            if (itemId.IsEmpty) throw new ArgumentException("Item id cannot be empty.", nameof(itemId));
            if (quantity < 1) throw new ArgumentOutOfRangeException(nameof(quantity));
            ItemId = itemId;
            Quantity = quantity;
        }
        public ContentId ItemId { get; }
        public int Quantity { get; }
    }

    /// <summary>
    /// Aggregate persistence record for one character. It replaces the browser's monolithic
    /// localStorage save with server-friendly logical state: character, bank, respawn preference and
    /// fast-travel discovery. Static authored world pages are deliberately not duplicated here;
    /// dynamic world services persist their own state independently in the MMO architecture.
    /// </summary>
    public sealed class CharacterPersistenceSnapshot
    {
        public const int CurrentVersion = 1;

        public CharacterPersistenceSnapshot(
            int version,
            PlayerStateSnapshot player,
            IReadOnlyList<BankEntrySnapshot> bank,
            RespawnPreference respawnPreference,
            GridLocation? homeLocation,
            IReadOnlyList<ContentId> activatedFastTravelNodes)
        {
            if (version < 1) throw new ArgumentOutOfRangeException(nameof(version));
            Player = player ?? throw new ArgumentNullException(nameof(player));
            Version = version;
            Bank = bank ?? Array.Empty<BankEntrySnapshot>();
            RespawnPreference = respawnPreference;
            HomeLocation = homeLocation;
            ActivatedFastTravelNodes = activatedFastTravelNodes ?? Array.Empty<ContentId>();
        }

        public int Version { get; }
        public PlayerStateSnapshot Player { get; }
        public IReadOnlyList<BankEntrySnapshot> Bank { get; }
        public RespawnPreference RespawnPreference { get; }
        public GridLocation? HomeLocation { get; }
        public IReadOnlyList<ContentId> ActivatedFastTravelNodes { get; }
    }

    public sealed class CharacterPersistenceRestoreResult
    {
        internal CharacterPersistenceRestoreResult(PlayerState player, CharacterBankState bank, PlayerRespawnProfile respawn, CharacterFastTravelState fastTravel)
        {
            Player = player;
            Bank = bank;
            Respawn = respawn;
            FastTravel = fastTravel;
        }

        public PlayerState Player { get; }
        public CharacterBankState Bank { get; }
        public PlayerRespawnProfile Respawn { get; }
        public CharacterFastTravelState FastTravel { get; }
    }

    public static class CharacterPersistenceSnapshotCodec
    {
        public static CharacterPersistenceSnapshot Capture(
            PlayerState player,
            CharacterBankRegistry banks,
            PlayerRespawnRegistry respawns,
            FastTravelStateRegistry fastTravelStates)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (banks == null) throw new ArgumentNullException(nameof(banks));
            if (respawns == null) throw new ArgumentNullException(nameof(respawns));
            if (fastTravelStates == null) throw new ArgumentNullException(nameof(fastTravelStates));

            var bankState = banks.GetOrCreate(player.CharacterId);
            var bank = new List<BankEntrySnapshot>();
            foreach (var pair in bankState.Entries)
                if (pair.Value > 0) bank.Add(new BankEntrySnapshot(pair.Key, pair.Value));

            var respawn = respawns.GetOrCreate(player.CharacterId);
            var travel = fastTravelStates.GetOrCreate(player.CharacterId);
            var activated = new List<ContentId>();
            foreach (var nodeId in travel.ActivatedNodeIds) activated.Add(nodeId);

            return new CharacterPersistenceSnapshot(
                CharacterPersistenceSnapshot.CurrentVersion,
                PlayerStateSnapshotCodec.Capture(player),
                bank,
                respawn.Preference,
                respawn.HomeLocation,
                activated);
        }

        public static CharacterPersistenceRestoreResult Restore(
            CharacterPersistenceSnapshot snapshot,
            IItemRuleSource itemRules,
            CharacterBankRegistry banks,
            PlayerRespawnRegistry respawns,
            FastTravelStateRegistry fastTravelStates)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (itemRules == null) throw new ArgumentNullException(nameof(itemRules));
            if (banks == null) throw new ArgumentNullException(nameof(banks));
            if (respawns == null) throw new ArgumentNullException(nameof(respawns));
            if (fastTravelStates == null) throw new ArgumentNullException(nameof(fastTravelStates));
            if (snapshot.Version > CharacterPersistenceSnapshot.CurrentVersion)
                throw new InvalidOperationException("Character persistence snapshot was written by a newer server version.");

            var player = PlayerStateSnapshotCodec.Restore(snapshot.Player, itemRules);
            var bank = banks.GetOrCreate(player.CharacterId);
            foreach (var existing in new List<KeyValuePair<ContentId, int>>(bank.Entries))
                if (existing.Value > 0) bank.Remove(existing.Key, existing.Value);

            var bankSeen = new HashSet<ContentId>();
            for (var i = 0; i < snapshot.Bank.Count; i++)
            {
                var entry = snapshot.Bank[i];
                if (!bankSeen.Add(entry.ItemId)) throw new InvalidOperationException("Snapshot contains duplicate bank entries.");
                if (!itemRules.TryGetRule(entry.ItemId, out _))
                    throw new InvalidOperationException("Snapshot bank references unknown item '" + entry.ItemId + "'.");
                bank.Add(entry.ItemId, entry.Quantity);
            }

            var respawn = respawns.GetOrCreate(player.CharacterId);
            respawn.Preference = snapshot.RespawnPreference;
            respawn.HomeLocation = snapshot.HomeLocation;

            var travel = fastTravelStates.GetOrCreate(player.CharacterId);
            travel.CloseMap();
            travel.ClearArrivalProtection();
            var activatedSeen = new HashSet<ContentId>();
            for (var i = 0; i < snapshot.ActivatedFastTravelNodes.Count; i++)
            {
                var nodeId = snapshot.ActivatedFastTravelNodes[i];
                if (nodeId.IsEmpty) throw new InvalidOperationException("Snapshot contains an empty fast-travel node id.");
                if (!activatedSeen.Add(nodeId)) throw new InvalidOperationException("Snapshot contains duplicate fast-travel node ids.");
                travel.Activate(nodeId);
            }

            return new CharacterPersistenceRestoreResult(player, bank, respawn, travel);
        }
    }
}
