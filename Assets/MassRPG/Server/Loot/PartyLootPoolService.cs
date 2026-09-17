using System;
using System.Collections.Generic;
using MassRPG.Core.Content;
using MassRPG.Server.Parties;

namespace MassRPG.Server.Loot
{
    public enum NeedGreedChoice
    {
        Pass,
        Greed,
        Need
    }

    public enum SharedLootEntryState
    {
        Open,
        Assigned,
        Claimed,
        ResolvedNoWinner
    }

    public readonly struct LootStack
    {
        public LootStack(ContentId itemId, int quantity)
        {
            if (itemId.IsEmpty) throw new ArgumentException("Loot item id cannot be empty.", nameof(itemId));
            if (quantity < 1) throw new ArgumentOutOfRangeException(nameof(quantity));
            ItemId = itemId;
            Quantity = quantity;
        }

        public ContentId ItemId { get; }
        public int Quantity { get; }
    }

    public sealed class SharedLootEntry
    {
        internal SharedLootEntry(Guid entryId, LootStack stack)
        {
            if (entryId == Guid.Empty) throw new ArgumentException("Loot entry id cannot be empty.", nameof(entryId));
            EntryId = entryId;
            Stack = stack;
        }

        public Guid EntryId { get; }
        public LootStack Stack { get; }
        public Guid? AssignedCharacterId { get; internal set; }
        public Guid? ClaimedByCharacterId { get; internal set; }
        public SharedLootEntryState State { get; internal set; } = SharedLootEntryState.Open;
    }

    public sealed class SharedPartyLootPool
    {
        private readonly HashSet<Guid> _eligibleRecipients;
        private readonly List<SharedLootEntry> _entries;
        private readonly Dictionary<Guid, Dictionary<Guid, NeedGreedChoice>> _needGreedVotes =
            new Dictionary<Guid, Dictionary<Guid, NeedGreedChoice>>();

        internal SharedPartyLootPool(
            Guid poolId,
            Guid targetActorId,
            Guid partyId,
            Guid leaderCharacterId,
            PartyLootMode mode,
            IEnumerable<Guid> eligibleRecipients,
            List<SharedLootEntry> entries)
        {
            if (poolId == Guid.Empty) throw new ArgumentException("Loot pool id cannot be empty.", nameof(poolId));
            if (targetActorId == Guid.Empty) throw new ArgumentException("Target actor id cannot be empty.", nameof(targetActorId));
            if (partyId == Guid.Empty) throw new ArgumentException("Party id cannot be empty.", nameof(partyId));
            PoolId = poolId;
            TargetActorId = targetActorId;
            PartyId = partyId;
            LeaderCharacterId = leaderCharacterId;
            Mode = mode;
            _eligibleRecipients = new HashSet<Guid>(eligibleRecipients ?? throw new ArgumentNullException(nameof(eligibleRecipients)));
            _entries = entries ?? throw new ArgumentNullException(nameof(entries));
            if (_eligibleRecipients.Count == 0) throw new ArgumentException("A party loot pool needs at least one eligible recipient.", nameof(eligibleRecipients));
        }

        public Guid PoolId { get; }
        public Guid TargetActorId { get; }
        public Guid PartyId { get; }
        public Guid LeaderCharacterId { get; }
        public PartyLootMode Mode { get; }
        public IReadOnlyList<SharedLootEntry> Entries => _entries;
        public IReadOnlyCollection<Guid> EligibleRecipients => _eligibleRecipients;

        public bool IsEligible(Guid characterId) => _eligibleRecipients.Contains(characterId);

        internal Dictionary<Guid, NeedGreedChoice> VotesFor(Guid entryId)
        {
            if (!_needGreedVotes.TryGetValue(entryId, out var votes))
            {
                votes = new Dictionary<Guid, NeedGreedChoice>();
                _needGreedVotes.Add(entryId, votes);
            }
            return votes;
        }

        public bool TryGetVotes(Guid entryId, out IReadOnlyDictionary<Guid, NeedGreedChoice> votes)
        {
            if (_needGreedVotes.TryGetValue(entryId, out var mutable))
            {
                votes = mutable;
                return true;
            }
            votes = null;
            return false;
        }

        internal SharedLootEntry Find(Guid entryId)
        {
            for (var i = 0; i < _entries.Count; i++)
                if (_entries[i].EntryId == entryId)
                    return _entries[i];
            return null;
        }
    }

    public readonly struct LootPoolActionResult
    {
        private LootPoolActionResult(bool success, string code, Guid? characterId)
        {
            Success = success;
            Code = code ?? string.Empty;
            CharacterId = characterId;
        }

        public bool Success { get; }
        public string Code { get; }
        public Guid? CharacterId { get; }

        public static LootPoolActionResult Ok(Guid? characterId = null)
            => new LootPoolActionResult(true, "ok", characterId);
        public static LootPoolActionResult Fail(string code)
            => new LootPoolActionResult(false, code, null);
    }

    /// <summary>
    /// Resolves one shared party item pool using the leader-selected loot mode. Currency stays out of
    /// this service because CombatRewardGroup already produces the settled equal party money split.
    /// Item ownership is resolved here first; inventory insertion/drop-on-ground policy is a separate
    /// authoritative settlement step so a full inventory cannot make an item silently disappear.
    /// </summary>
    public sealed class PartyLootPoolService
    {
        private readonly Dictionary<Guid, SharedPartyLootPool> _pools = new Dictionary<Guid, SharedPartyLootPool>();

        public IEnumerable<SharedPartyLootPool> All => _pools.Values;
        public bool TryGet(Guid poolId, out SharedPartyLootPool pool) => _pools.TryGetValue(poolId, out pool);
        public bool Remove(Guid poolId) => _pools.Remove(poolId);

        public SharedPartyLootPool Create(
            Guid poolId,
            Guid targetActorId,
            PartyState party,
            IEnumerable<Guid> eligibleRecipients,
            IEnumerable<LootStack> drops)
        {
            if (party == null) throw new ArgumentNullException(nameof(party));
            if (drops == null) throw new ArgumentNullException(nameof(drops));
            if (_pools.ContainsKey(poolId)) throw new InvalidOperationException("Duplicate loot pool id.");

            var eligible = new HashSet<Guid>(eligibleRecipients ?? throw new ArgumentNullException(nameof(eligibleRecipients)));
            if (eligible.Count == 0) throw new ArgumentException("No eligible party recipients.", nameof(eligibleRecipients));
            foreach (var characterId in eligible)
                if (!party.Contains(characterId))
                    throw new ArgumentException("Eligible loot recipient is not a member of the party.", nameof(eligibleRecipients));

            var entries = new List<SharedLootEntry>();
            foreach (var drop in drops)
                entries.Add(new SharedLootEntry(Guid.NewGuid(), drop));

            var pool = new SharedPartyLootPool(
                poolId,
                targetActorId,
                party.PartyId,
                party.LeaderCharacterId,
                party.LootMode,
                eligible,
                entries);

            if (party.LootMode == PartyLootMode.RoundRobin)
            {
                for (var i = 0; i < entries.Count; i++)
                {
                    if (!party.TryTakeNextRoundRobin(eligible, out var recipient)) continue;
                    entries[i].AssignedCharacterId = recipient;
                    entries[i].State = SharedLootEntryState.Assigned;
                }
            }

            _pools.Add(poolId, pool);
            return pool;
        }

        public LootPoolActionResult AssignByLeader(
            Guid poolId,
            Guid entryId,
            Guid requesterCharacterId,
            Guid recipientCharacterId)
        {
            if (!_pools.TryGetValue(poolId, out var pool)) return LootPoolActionResult.Fail("unknown_pool");
            if (pool.Mode != PartyLootMode.LeaderDistribution) return LootPoolActionResult.Fail("wrong_loot_mode");
            if (requesterCharacterId != pool.LeaderCharacterId) return LootPoolActionResult.Fail("not_party_leader");
            if (!pool.IsEligible(recipientCharacterId)) return LootPoolActionResult.Fail("recipient_not_eligible");
            var entry = pool.Find(entryId);
            if (entry == null) return LootPoolActionResult.Fail("unknown_entry");
            if (entry.State == SharedLootEntryState.Claimed) return LootPoolActionResult.Fail("already_claimed");
            if (entry.State == SharedLootEntryState.ResolvedNoWinner) return LootPoolActionResult.Fail("entry_closed");

            entry.AssignedCharacterId = recipientCharacterId;
            entry.State = SharedLootEntryState.Assigned;
            return LootPoolActionResult.Ok(recipientCharacterId);
        }

        public LootPoolActionResult SubmitNeedGreed(
            Guid poolId,
            Guid entryId,
            Guid characterId,
            NeedGreedChoice choice)
        {
            if (!_pools.TryGetValue(poolId, out var pool)) return LootPoolActionResult.Fail("unknown_pool");
            if (pool.Mode != PartyLootMode.NeedGreed) return LootPoolActionResult.Fail("wrong_loot_mode");
            if (!pool.IsEligible(characterId)) return LootPoolActionResult.Fail("recipient_not_eligible");
            var entry = pool.Find(entryId);
            if (entry == null) return LootPoolActionResult.Fail("unknown_entry");
            if (entry.State != SharedLootEntryState.Open) return LootPoolActionResult.Fail("entry_closed");

            pool.VotesFor(entryId)[characterId] = choice;
            return LootPoolActionResult.Ok(characterId);
        }

        /// <summary>
        /// Resolves a Need/Greed entry once every eligible member has answered, or after the caller's
        /// authoritative timeout policy chooses force=true. Need always outranks Greed. The supplied
        /// selector receives the number of tied candidates and must return an index in that range.
        /// </summary>
        public LootPoolActionResult ResolveNeedGreed(
            Guid poolId,
            Guid entryId,
            Func<int, int> selectIndex,
            bool force = false)
        {
            if (selectIndex == null) throw new ArgumentNullException(nameof(selectIndex));
            if (!_pools.TryGetValue(poolId, out var pool)) return LootPoolActionResult.Fail("unknown_pool");
            if (pool.Mode != PartyLootMode.NeedGreed) return LootPoolActionResult.Fail("wrong_loot_mode");
            var entry = pool.Find(entryId);
            if (entry == null) return LootPoolActionResult.Fail("unknown_entry");
            if (entry.State != SharedLootEntryState.Open) return LootPoolActionResult.Fail("entry_closed");

            var votes = pool.VotesFor(entryId);
            if (!force && votes.Count < pool.EligibleRecipients.Count)
                return LootPoolActionResult.Fail("waiting_for_votes");

            var needs = new List<Guid>();
            var greeds = new List<Guid>();
            foreach (var pair in votes)
            {
                if (!pool.IsEligible(pair.Key)) continue;
                if (pair.Value == NeedGreedChoice.Need) needs.Add(pair.Key);
                else if (pair.Value == NeedGreedChoice.Greed) greeds.Add(pair.Key);
            }

            var candidates = needs.Count > 0 ? needs : greeds;
            if (candidates.Count == 0)
            {
                entry.State = SharedLootEntryState.ResolvedNoWinner;
                return LootPoolActionResult.Ok();
            }

            var index = selectIndex(candidates.Count);
            if (index < 0 || index >= candidates.Count)
                throw new InvalidOperationException("Need/Greed selector returned an out-of-range candidate index.");
            var winner = candidates[index];
            entry.AssignedCharacterId = winner;
            entry.State = SharedLootEntryState.Assigned;
            return LootPoolActionResult.Ok(winner);
        }

        public LootPoolActionResult Claim(Guid poolId, Guid entryId, Guid characterId)
        {
            if (!_pools.TryGetValue(poolId, out var pool)) return LootPoolActionResult.Fail("unknown_pool");
            if (!pool.IsEligible(characterId)) return LootPoolActionResult.Fail("recipient_not_eligible");
            var entry = pool.Find(entryId);
            if (entry == null) return LootPoolActionResult.Fail("unknown_entry");
            if (entry.State == SharedLootEntryState.Claimed) return LootPoolActionResult.Fail("already_claimed");
            if (entry.State == SharedLootEntryState.ResolvedNoWinner) return LootPoolActionResult.Fail("entry_closed");

            if (pool.Mode == PartyLootMode.FreeForAll && entry.State == SharedLootEntryState.Open)
            {
                entry.AssignedCharacterId = characterId;
                entry.State = SharedLootEntryState.Assigned;
            }

            if (!entry.AssignedCharacterId.HasValue)
            {
                if (pool.Mode == PartyLootMode.NeedGreed) return LootPoolActionResult.Fail("need_greed_unresolved");
                if (pool.Mode == PartyLootMode.LeaderDistribution) return LootPoolActionResult.Fail("leader_has_not_assigned");
                return LootPoolActionResult.Fail("entry_unassigned");
            }
            if (entry.AssignedCharacterId.Value != characterId)
                return LootPoolActionResult.Fail("assigned_to_other_character");

            entry.ClaimedByCharacterId = characterId;
            entry.State = SharedLootEntryState.Claimed;
            return LootPoolActionResult.Ok(characterId);
        }
    }
}
