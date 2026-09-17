using System;
using System.Collections.Generic;

namespace MassRPG.Server.Parties
{
    public enum PartyLootMode
    {
        RoundRobin,
        NeedGreed,
        LeaderDistribution,
        FreeForAll
    }

    /// <summary>
    /// Authoritative party membership and loot preference. Party size is intentionally not capped
    /// here; any eventual live-server cap is a policy/configuration concern rather than persistence.
    /// </summary>
    public sealed class PartyState
    {
        private readonly List<Guid> _members = new List<Guid>();
        private readonly HashSet<Guid> _memberLookup = new HashSet<Guid>();
        private int _roundRobinCursor;

        public PartyState(Guid partyId, Guid leaderCharacterId, PartyLootMode lootMode = PartyLootMode.RoundRobin)
        {
            if (partyId == Guid.Empty) throw new ArgumentException("Party id cannot be empty.", nameof(partyId));
            if (leaderCharacterId == Guid.Empty) throw new ArgumentException("Leader id cannot be empty.", nameof(leaderCharacterId));
            PartyId = partyId;
            LeaderCharacterId = leaderCharacterId;
            LootMode = lootMode;
            AddMember(leaderCharacterId);
        }

        public Guid PartyId { get; }
        public Guid LeaderCharacterId { get; private set; }
        public PartyLootMode LootMode { get; private set; }
        public IReadOnlyList<Guid> Members => _members;
        public int Count => _members.Count;

        public bool Contains(Guid characterId) => _memberLookup.Contains(characterId);

        internal bool AddMember(Guid characterId)
        {
            if (characterId == Guid.Empty) throw new ArgumentException("Character id cannot be empty.", nameof(characterId));
            if (!_memberLookup.Add(characterId)) return false;
            _members.Add(characterId);
            return true;
        }

        internal bool RemoveMember(Guid characterId)
        {
            if (!_memberLookup.Remove(characterId)) return false;
            _members.Remove(characterId);
            if (_roundRobinCursor >= _members.Count) _roundRobinCursor = 0;
            return true;
        }

        public bool SetLeader(Guid requesterCharacterId, Guid newLeaderCharacterId)
        {
            if (requesterCharacterId != LeaderCharacterId) return false;
            if (!Contains(newLeaderCharacterId)) return false;
            LeaderCharacterId = newLeaderCharacterId;
            return true;
        }

        public bool SetLootMode(Guid requesterCharacterId, PartyLootMode mode)
        {
            if (requesterCharacterId != LeaderCharacterId) return false;
            LootMode = mode;
            return true;
        }

        /// <summary>
        /// Picks the next currently eligible party member for round-robin item assignment. Missing
        /// or out-of-range members are skipped without being removed from the party.
        /// </summary>
        public bool TryTakeNextRoundRobin(IReadOnlyCollection<Guid> eligibleMembers, out Guid characterId)
        {
            if (eligibleMembers == null) throw new ArgumentNullException(nameof(eligibleMembers));
            if (_members.Count == 0 || eligibleMembers.Count == 0)
            {
                characterId = Guid.Empty;
                return false;
            }

            var eligible = eligibleMembers as HashSet<Guid> ?? new HashSet<Guid>(eligibleMembers);
            for (var offset = 0; offset < _members.Count; offset++)
            {
                var index = (_roundRobinCursor + offset) % _members.Count;
                var candidate = _members[index];
                if (!eligible.Contains(candidate)) continue;
                characterId = candidate;
                _roundRobinCursor = (index + 1) % _members.Count;
                return true;
            }

            characterId = Guid.Empty;
            return false;
        }
    }

    /// <summary>Server-owned party lookup with a one-party-per-character invariant.</summary>
    public sealed class PartyRegistry
    {
        private readonly Dictionary<Guid, PartyState> _parties = new Dictionary<Guid, PartyState>();
        private readonly Dictionary<Guid, Guid> _partyByCharacter = new Dictionary<Guid, Guid>();

        public IEnumerable<PartyState> All => _parties.Values;

        public PartyState Create(Guid partyId, Guid leaderCharacterId, PartyLootMode lootMode = PartyLootMode.RoundRobin)
        {
            if (_parties.ContainsKey(partyId)) throw new InvalidOperationException("Duplicate party id.");
            if (_partyByCharacter.ContainsKey(leaderCharacterId)) throw new InvalidOperationException("Character already belongs to a party.");
            var party = new PartyState(partyId, leaderCharacterId, lootMode);
            _parties.Add(partyId, party);
            _partyByCharacter.Add(leaderCharacterId, partyId);
            return party;
        }

        public bool TryGet(Guid partyId, out PartyState party) => _parties.TryGetValue(partyId, out party);

        public bool TryGetForCharacter(Guid characterId, out PartyState party)
        {
            if (_partyByCharacter.TryGetValue(characterId, out var partyId)
                && _parties.TryGetValue(partyId, out party))
                return true;
            party = null;
            return false;
        }

        public bool AddMember(Guid partyId, Guid characterId)
        {
            if (_partyByCharacter.ContainsKey(characterId)) return false;
            if (!_parties.TryGetValue(partyId, out var party)) return false;
            if (!party.AddMember(characterId)) return false;
            _partyByCharacter.Add(characterId, partyId);
            return true;
        }

        public bool RemoveMember(Guid partyId, Guid characterId)
        {
            if (!_parties.TryGetValue(partyId, out var party) || !party.Contains(characterId)) return false;
            if (party.Count == 1)
            {
                _partyByCharacter.Remove(characterId);
                _parties.Remove(partyId);
                return true;
            }
            if (party.LeaderCharacterId == characterId) return false;
            if (!party.RemoveMember(characterId)) return false;
            _partyByCharacter.Remove(characterId);
            return true;
        }

        public bool Disband(Guid partyId, Guid requesterCharacterId)
        {
            if (!_parties.TryGetValue(partyId, out var party)) return false;
            if (party.LeaderCharacterId != requesterCharacterId) return false;
            for (var i = 0; i < party.Members.Count; i++) _partyByCharacter.Remove(party.Members[i]);
            _parties.Remove(partyId);
            return true;
        }
    }
}
