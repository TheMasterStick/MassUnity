using System;

namespace MassRPG.Core.Combat
{
    /// <summary>
    /// Minimal engine-independent contribution event. Server-side ledgers decide eligibility;
    /// combat resolution only reports authoritative damage actually dealt.
    /// </summary>
    public readonly struct CombatContribution
    {
        public CombatContribution(Guid targetActorId, Guid contributorCharacterId, int damage, long atUnixMilliseconds)
        {
            if (targetActorId == Guid.Empty) throw new ArgumentException("Target id cannot be empty.", nameof(targetActorId));
            if (contributorCharacterId == Guid.Empty) throw new ArgumentException("Contributor id cannot be empty.", nameof(contributorCharacterId));
            if (damage < 0) throw new ArgumentOutOfRangeException(nameof(damage));
            TargetActorId = targetActorId;
            ContributorCharacterId = contributorCharacterId;
            Damage = damage;
            AtUnixMilliseconds = atUnixMilliseconds;
        }

        public Guid TargetActorId { get; }
        public Guid ContributorCharacterId { get; }
        public int Damage { get; }
        public long AtUnixMilliseconds { get; }
    }

    public interface ICombatContributionSink
    {
        void Record(CombatContribution contribution);
    }
}
