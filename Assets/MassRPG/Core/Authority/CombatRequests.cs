using System;

namespace MassRPG.Core.Authority
{
    public sealed class AttackCreatureRequest : GameRequest
    {
        public AttackCreatureRequest(Guid requestId, Guid characterId, Guid creatureInstanceId)
            : base(requestId, characterId)
        {
            if (creatureInstanceId == Guid.Empty) throw new ArgumentException("Creature instance id cannot be empty.", nameof(creatureInstanceId));
            CreatureInstanceId = creatureInstanceId;
        }

        public Guid CreatureInstanceId { get; }
    }

    public sealed class StopCombatRequest : GameRequest
    {
        public StopCombatRequest(Guid requestId, Guid characterId)
            : base(requestId, characterId)
        {
        }
    }
}
