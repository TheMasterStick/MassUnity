using System;
using System.Collections.Generic;
using MassRPG.Server.Parties;

namespace MassRPG.Server.Combat
{
    public readonly struct CombatRewardPresence
    {
        public CombatRewardPresence(Guid characterId, bool inRewardRange)
        {
            if (characterId == Guid.Empty) throw new ArgumentException("Character id cannot be empty.", nameof(characterId));
            CharacterId = characterId;
            InRewardRange = inRewardRange;
        }

        public Guid CharacterId { get; }
        public bool InRewardRange { get; }
    }

    public readonly struct CombatInitialClaimContext
    {
        public CombatInitialClaimContext(
            Guid firstEngagerCharacterId,
            Guid? partyId,
            Guid leaderCharacterId,
            PartyLootMode lootMode)
        {
            FirstEngagerCharacterId = firstEngagerCharacterId;
            PartyId = partyId;
            LeaderCharacterId = leaderCharacterId;
            LootMode = lootMode;
        }

        public Guid FirstEngagerCharacterId { get; }
        public Guid? PartyId { get; }
        public bool IsParty => PartyId.HasValue;
        public Guid LeaderCharacterId { get; }
        public PartyLootMode LootMode { get; }
    }

    public readonly struct CombatMoneySplit
    {
        public CombatMoneySplit(int totalMoney, int perRecipient, int remainder, IReadOnlyList<Guid> recipients)
        {
            TotalMoney = totalMoney;
            PerRecipient = perRecipient;
            Remainder = remainder;
            Recipients = recipients ?? Array.Empty<Guid>();
        }

        public int TotalMoney { get; }
        public int PerRecipient { get; }
        public int Remainder { get; }
        public IReadOnlyList<Guid> Recipients { get; }
    }

    public sealed class CombatRewardGroup
    {
        internal CombatRewardGroup(
            Guid groupId,
            bool isParty,
            Guid leaderCharacterId,
            PartyLootMode lootMode,
            int eligibleContributionDamage,
            IReadOnlyList<Guid> directContributors,
            IReadOnlyList<Guid> sharedRecipients)
        {
            GroupId = groupId;
            IsParty = isParty;
            LeaderCharacterId = leaderCharacterId;
            LootMode = lootMode;
            EligibleContributionDamage = eligibleContributionDamage;
            DirectContributors = directContributors ?? Array.Empty<Guid>();
            SharedRecipients = sharedRecipients ?? Array.Empty<Guid>();
        }

        /// <summary>
        /// Party id for party groups; contributor character id for a solo group. IsParty keeps the
        /// two namespaces unambiguous even though both use Guid values.
        /// </summary>
        public Guid GroupId { get; }
        public bool IsParty { get; }
        public Guid LeaderCharacterId { get; }
        public PartyLootMode LootMode { get; }
        public int EligibleContributionDamage { get; }
        public IReadOnlyList<Guid> DirectContributors { get; }
        public IReadOnlyList<Guid> SharedRecipients { get; }

        /// <summary>
        /// Money is evenly divided among eligible recipients. Integer currency can leave a small
        /// remainder; the item/loot-mode resolver may assign that remainder without distorting the
        /// equal base share.
        /// </summary>
        public CombatMoneySplit SplitMoney(int totalMoney)
        {
            if (totalMoney < 0) throw new ArgumentOutOfRangeException(nameof(totalMoney));
            if (SharedRecipients.Count == 0) return new CombatMoneySplit(totalMoney, 0, totalMoney, SharedRecipients);
            var perRecipient = totalMoney / SharedRecipients.Count;
            var remainder = totalMoney - perRecipient * SharedRecipients.Count;
            return new CombatMoneySplit(totalMoney, perRecipient, remainder, SharedRecipients);
        }
    }

    public sealed class CombatRewardPlan
    {
        internal CombatRewardPlan(
            Guid targetActorId,
            CombatInitialClaimContext initialClaim,
            IReadOnlyList<CombatRewardGroup> groups)
        {
            TargetActorId = targetActorId;
            InitialClaim = initialClaim;
            Groups = groups ?? Array.Empty<CombatRewardGroup>();
        }

        public Guid TargetActorId { get; }
        public CombatInitialClaimContext InitialClaim { get; }
        public IReadOnlyList<CombatRewardGroup> Groups { get; }

        public bool TryGetPartyGroup(Guid partyId, out CombatRewardGroup group)
        {
            for (var i = 0; i < Groups.Count; i++)
            {
                var candidate = Groups[i];
                if (candidate.IsParty && candidate.GroupId == partyId)
                {
                    group = candidate;
                    return true;
                }
            }
            group = null;
            return false;
        }

        public bool TryGetSoloGroup(Guid characterId, out CombatRewardGroup group)
        {
            for (var i = 0; i < Groups.Count; i++)
            {
                var candidate = Groups[i];
                if (!candidate.IsParty && candidate.GroupId == characterId)
                {
                    group = candidate;
                    return true;
                }
            }
            group = null;
            return false;
        }
    }

    /// <summary>
    /// Converts authoritative contribution facts into party-aware reward groups without baking in
    /// final XP multipliers or loot-table balance. Direct contributors must satisfy the configurable
    /// anti-power-level contribution policy and be in reward range. Once a party has a qualifying
    /// contributor, its other in-range members become shared recipients, matching the settled
    /// MassRPG party-XP model. Outsiders remain personal contribution groups.
    ///
    /// Initial loot claim context is captured from the first engager/group, but final claim-steal,
    /// boss/event exceptions and exact XP numbers remain separate policies rather than hidden here.
    /// </summary>
    public sealed class CombatRewardPlanner
    {
        private readonly PartyRegistry _parties;
        private readonly ContributionEligibilityPolicy _eligibility;

        public CombatRewardPlanner(PartyRegistry parties, ContributionEligibilityPolicy eligibility)
        {
            _parties = parties ?? throw new ArgumentNullException(nameof(parties));
            _eligibility = eligibility ?? throw new ArgumentNullException(nameof(eligibility));
        }

        public CombatRewardPlan Build(
            CombatContributionSnapshot snapshot,
            IEnumerable<CombatRewardPresence> presences)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (presences == null) throw new ArgumentNullException(nameof(presences));

            var presenceByCharacter = new Dictionary<Guid, bool>();
            foreach (var presence in presences)
                presenceByCharacter[presence.CharacterId] = presence.InRewardRange;

            var firstEngager = FindFirstEngager(snapshot);
            var initialClaim = ResolveInitialClaim(firstEngager);
            var groups = new Dictionary<RewardGroupKey, MutableRewardGroup>();

            for (var i = 0; i < snapshot.Entries.Count; i++)
            {
                var entry = snapshot.Entries[i];
                if (!presenceByCharacter.TryGetValue(entry.CharacterId, out var inRange) || !inRange) continue;
                if (!_eligibility.IsEligible(snapshot, entry.CharacterId)) continue;

                RewardGroupKey key;
                MutableRewardGroup group;
                if (_parties.TryGetForCharacter(entry.CharacterId, out var party))
                {
                    key = new RewardGroupKey(party.PartyId, true);
                    if (!groups.TryGetValue(key, out group))
                    {
                        group = new MutableRewardGroup(
                            party.PartyId,
                            true,
                            party.LeaderCharacterId,
                            party.LootMode,
                            party);
                        groups.Add(key, group);
                    }
                }
                else
                {
                    key = new RewardGroupKey(entry.CharacterId, false);
                    if (!groups.TryGetValue(key, out group))
                    {
                        group = new MutableRewardGroup(
                            entry.CharacterId,
                            false,
                            entry.CharacterId,
                            PartyLootMode.FreeForAll,
                            null);
                        groups.Add(key, group);
                    }
                }

                group.AddDirectContributor(entry.CharacterId, entry.Damage);
            }

            var result = new List<CombatRewardGroup>(groups.Count);
            foreach (var pair in groups)
            {
                var group = pair.Value;
                if (group.Party != null)
                {
                    for (var i = 0; i < group.Party.Members.Count; i++)
                    {
                        var member = group.Party.Members[i];
                        if (presenceByCharacter.TryGetValue(member, out var inRange) && inRange)
                            group.AddSharedRecipient(member);
                    }
                }
                else
                {
                    for (var i = 0; i < group.DirectContributors.Count; i++)
                        group.AddSharedRecipient(group.DirectContributors[i]);
                }

                result.Add(group.Freeze());
            }

            result.Sort((a, b) => b.EligibleContributionDamage.CompareTo(a.EligibleContributionDamage));
            return new CombatRewardPlan(snapshot.TargetActorId, initialClaim, result);
        }

        private CombatInitialClaimContext ResolveInitialClaim(Guid firstEngager)
        {
            if (_parties.TryGetForCharacter(firstEngager, out var party))
            {
                return new CombatInitialClaimContext(
                    firstEngager,
                    party.PartyId,
                    party.LeaderCharacterId,
                    party.LootMode);
            }

            return new CombatInitialClaimContext(
                firstEngager,
                null,
                firstEngager,
                PartyLootMode.FreeForAll);
        }

        private static Guid FindFirstEngager(CombatContributionSnapshot snapshot)
        {
            for (var i = 0; i < snapshot.Entries.Count; i++)
                if (snapshot.Entries[i].FirstEngager)
                    return snapshot.Entries[i].CharacterId;
            return snapshot.Entries.Count > 0 ? snapshot.Entries[0].CharacterId : Guid.Empty;
        }

        private readonly struct RewardGroupKey : IEquatable<RewardGroupKey>
        {
            public RewardGroupKey(Guid id, bool isParty)
            {
                Id = id;
                IsParty = isParty;
            }

            public Guid Id { get; }
            public bool IsParty { get; }
            public bool Equals(RewardGroupKey other) => Id == other.Id && IsParty == other.IsParty;
            public override bool Equals(object obj) => obj is RewardGroupKey other && Equals(other);
            public override int GetHashCode() => unchecked((Id.GetHashCode() * 397) ^ IsParty.GetHashCode());
        }

        private sealed class MutableRewardGroup
        {
            private readonly HashSet<Guid> _directLookup = new HashSet<Guid>();
            private readonly HashSet<Guid> _recipientLookup = new HashSet<Guid>();
            private readonly List<Guid> _directContributors = new List<Guid>();
            private readonly List<Guid> _sharedRecipients = new List<Guid>();

            public MutableRewardGroup(
                Guid groupId,
                bool isParty,
                Guid leaderCharacterId,
                PartyLootMode lootMode,
                PartyState party)
            {
                GroupId = groupId;
                IsParty = isParty;
                LeaderCharacterId = leaderCharacterId;
                LootMode = lootMode;
                Party = party;
            }

            public Guid GroupId { get; }
            public bool IsParty { get; }
            public Guid LeaderCharacterId { get; }
            public PartyLootMode LootMode { get; }
            public PartyState Party { get; }
            public int Damage { get; private set; }
            public IReadOnlyList<Guid> DirectContributors => _directContributors;

            public void AddDirectContributor(Guid characterId, int damage)
            {
                if (_directLookup.Add(characterId)) _directContributors.Add(characterId);
                Damage = checked(Damage + damage);
            }

            public void AddSharedRecipient(Guid characterId)
            {
                if (_recipientLookup.Add(characterId)) _sharedRecipients.Add(characterId);
            }

            public CombatRewardGroup Freeze()
                => new CombatRewardGroup(
                    GroupId,
                    IsParty,
                    LeaderCharacterId,
                    LootMode,
                    Damage,
                    _directContributors.ToArray(),
                    _sharedRecipients.ToArray());
        }
    }
}
