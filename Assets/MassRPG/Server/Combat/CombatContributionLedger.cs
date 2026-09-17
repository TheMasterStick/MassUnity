using System;
using System.Collections.Generic;
using MassRPG.Core.Combat;

namespace MassRPG.Server.Combat
{
    public readonly struct CombatContributionEntry
    {
        public CombatContributionEntry(Guid characterId, int damage, bool firstEngager, long firstAt, long lastAt)
        {
            CharacterId = characterId;
            Damage = damage;
            FirstEngager = firstEngager;
            FirstAtUnixMilliseconds = firstAt;
            LastAtUnixMilliseconds = lastAt;
        }

        public Guid CharacterId { get; }
        public int Damage { get; }
        public bool FirstEngager { get; }
        public long FirstAtUnixMilliseconds { get; }
        public long LastAtUnixMilliseconds { get; }
    }

    public sealed class CombatContributionSnapshot
    {
        internal CombatContributionSnapshot(Guid targetActorId, int totalDamage, List<CombatContributionEntry> entries)
        {
            TargetActorId = targetActorId;
            TotalDamage = totalDamage;
            Entries = entries;
        }

        public Guid TargetActorId { get; }
        public int TotalDamage { get; }
        public IReadOnlyList<CombatContributionEntry> Entries { get; }
    }

    /// <summary>
    /// Authoritative damage contribution ledger. It intentionally records facts without deciding
    /// final XP/loot percentages. Eligibility is supplied separately so anti-power-level tuning can
    /// change without rewriting combat resolution or historical contribution state.
    /// </summary>
    public sealed class CombatContributionLedger : ICombatContributionSink
    {
        private readonly Dictionary<Guid, TargetLedger> _targets = new Dictionary<Guid, TargetLedger>();

        public void Record(CombatContribution contribution)
        {
            if (contribution.Damage <= 0) return;
            if (!_targets.TryGetValue(contribution.TargetActorId, out var target))
            {
                target = new TargetLedger(contribution.TargetActorId, contribution.ContributorCharacterId);
                _targets.Add(contribution.TargetActorId, target);
            }
            target.Record(contribution);
        }

        public bool TrySnapshot(Guid targetActorId, out CombatContributionSnapshot snapshot)
        {
            if (!_targets.TryGetValue(targetActorId, out var target))
            {
                snapshot = null;
                return false;
            }
            snapshot = target.Snapshot();
            return true;
        }

        public bool Remove(Guid targetActorId) => _targets.Remove(targetActorId);

        private sealed class TargetLedger
        {
            private readonly Guid _targetActorId;
            private readonly Guid _firstEngager;
            private readonly Dictionary<Guid, MutableEntry> _entries = new Dictionary<Guid, MutableEntry>();
            private int _totalDamage;

            public TargetLedger(Guid targetActorId, Guid firstEngager)
            {
                _targetActorId = targetActorId;
                _firstEngager = firstEngager;
            }

            public void Record(CombatContribution contribution)
            {
                if (!_entries.TryGetValue(contribution.ContributorCharacterId, out var entry))
                {
                    entry = new MutableEntry
                    {
                        FirstAt = contribution.AtUnixMilliseconds,
                        LastAt = contribution.AtUnixMilliseconds
                    };
                    _entries.Add(contribution.ContributorCharacterId, entry);
                }
                entry.Damage = checked(entry.Damage + contribution.Damage);
                entry.LastAt = contribution.AtUnixMilliseconds;
                _totalDamage = checked(_totalDamage + contribution.Damage);
            }

            public CombatContributionSnapshot Snapshot()
            {
                var entries = new List<CombatContributionEntry>(_entries.Count);
                foreach (var pair in _entries)
                {
                    entries.Add(new CombatContributionEntry(
                        pair.Key,
                        pair.Value.Damage,
                        pair.Key == _firstEngager,
                        pair.Value.FirstAt,
                        pair.Value.LastAt));
                }
                entries.Sort((a, b) => b.Damage.CompareTo(a.Damage));
                return new CombatContributionSnapshot(_targetActorId, _totalDamage, entries);
            }

            private sealed class MutableEntry
            {
                public int Damage;
                public long FirstAt;
                public long LastAt;
            }
        }
    }

    public sealed class ContributionEligibilityPolicy
    {
        public ContributionEligibilityPolicy(int minimumDamage, double minimumDamageFraction)
        {
            if (minimumDamage < 0) throw new ArgumentOutOfRangeException(nameof(minimumDamage));
            if (minimumDamageFraction < 0.0 || minimumDamageFraction > 1.0)
                throw new ArgumentOutOfRangeException(nameof(minimumDamageFraction));
            MinimumDamage = minimumDamage;
            MinimumDamageFraction = minimumDamageFraction;
        }

        public int MinimumDamage { get; set; }
        public double MinimumDamageFraction { get; set; }

        public bool IsEligible(CombatContributionSnapshot snapshot, Guid characterId)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            for (var i = 0; i < snapshot.Entries.Count; i++)
            {
                var entry = snapshot.Entries[i];
                if (entry.CharacterId != characterId) continue;
                if (entry.Damage < MinimumDamage) return false;
                if (snapshot.TotalDamage <= 0) return false;
                return entry.Damage / (double)snapshot.TotalDamage >= MinimumDamageFraction;
            }
            return false;
        }
    }
}
