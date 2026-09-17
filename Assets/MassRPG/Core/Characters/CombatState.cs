using System;

namespace MassRPG.Core.Characters
{
    /// <summary>
    /// Authoritative combat engagement state shared by local development and the future MMO server.
    /// Timing lives here rather than in Unity presentation so changing clients cannot speed attacks up.
    /// </summary>
    public sealed class CombatState
    {
        public bool IsActive { get; private set; }
        public Guid? TargetActorId { get; private set; }
        public long NextAttackAtUnixMilliseconds { get; private set; }

        public void Begin(Guid? targetActorId = null)
        {
            var targetChanged = TargetActorId != targetActorId;
            IsActive = true;
            TargetActorId = targetActorId;
            if (targetChanged) NextAttackAtUnixMilliseconds = 0;
        }

        public bool IsAttackReady(long nowUnixMilliseconds)
            => IsActive && nowUnixMilliseconds >= NextAttackAtUnixMilliseconds;

        public void ScheduleNextAttack(long nowUnixMilliseconds, int intervalMilliseconds)
        {
            if (intervalMilliseconds < 1) throw new ArgumentOutOfRangeException(nameof(intervalMilliseconds));
            NextAttackAtUnixMilliseconds = checked(nowUnixMilliseconds + intervalMilliseconds);
        }

        public void End()
        {
            IsActive = false;
            TargetActorId = null;
            NextAttackAtUnixMilliseconds = 0;
        }
    }
}
