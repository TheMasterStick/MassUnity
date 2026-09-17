using System;
using MassRPG.Core.Authority;
using MassRPG.Core.Content;

namespace MassRPG.Client.Travel
{
    /// <summary>
    /// Client bridge for fast-travel intent. The active authority gateway owns node activation,
    /// destination availability, costs/timing and arrival protection.
    /// </summary>
    public sealed class FastTravelController
    {
        private readonly IGameAuthority _authority;
        private readonly Guid _characterId;

        public FastTravelController(IGameAuthority authority, Guid characterId)
        {
            _authority = authority ?? throw new ArgumentNullException(nameof(authority));
            if (characterId == Guid.Empty)
                throw new ArgumentException("Character id cannot be empty.", nameof(characterId));
            _characterId = characterId;
        }

        public AuthorityDecision ActivateNode(ContentId nodeId)
            => _authority.Submit(new ActivateFastTravelNodeRequest(Guid.NewGuid(), _characterId, nodeId));

        public AuthorityDecision OpenDestinationMap(ContentId originNodeId)
            => _authority.Submit(new OpenFastTravelMapRequest(Guid.NewGuid(), _characterId, originNodeId));

        public AuthorityDecision TravelTo(ContentId destinationNodeId)
            => _authority.Submit(new CommitFastTravelRequest(Guid.NewGuid(), _characterId, destinationNodeId));
    }
}
