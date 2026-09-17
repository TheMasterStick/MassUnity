using System;
using MassRPG.Core.Authority;
using MassRPG.Core.World;

namespace MassRPG.Client.World
{
    /// <summary>
    /// Thin client bridge for movement and combat-target intent. Presentation selects a logical
    /// destination or target; authoritative pathing, range checks and simulation stay server-owned.
    /// </summary>
    public sealed class WorldActionController
    {
        private readonly IGameAuthority _authority;
        private readonly Guid _characterId;

        public WorldActionController(IGameAuthority authority, Guid characterId)
        {
            _authority = authority ?? throw new ArgumentNullException(nameof(authority));
            if (characterId == Guid.Empty)
                throw new ArgumentException("Character id cannot be empty.", nameof(characterId));
            _characterId = characterId;
        }

        public AuthorityDecision MoveTo(GridLocation destination)
            => _authority.Submit(new MoveToRequest(Guid.NewGuid(), _characterId, destination));

        public AuthorityDecision CancelMovement()
            => _authority.Submit(new CancelMovementRequest(Guid.NewGuid(), _characterId));

        public AuthorityDecision AttackCreature(Guid creatureInstanceId)
            => _authority.Submit(new AttackCreatureRequest(Guid.NewGuid(), _characterId, creatureInstanceId));

        public AuthorityDecision StopCombat()
            => _authority.Submit(new StopCombatRequest(Guid.NewGuid(), _characterId));
    }
}
