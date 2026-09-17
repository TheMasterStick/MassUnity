using System;
using MassRPG.Core.World;

namespace MassRPG.Core.Authority
{
    /// <summary>
    /// Client intent to travel toward a logical destination. The client supplies the destination,
    /// never an authoritative path or resulting position; the authority calculates and validates it.
    /// </summary>
    public sealed class MoveToRequest : GameRequest
    {
        public MoveToRequest(Guid requestId, Guid characterId, GridLocation destination)
            : base(requestId, characterId)
        {
            Destination = destination;
        }

        public GridLocation Destination { get; }
    }

    public sealed class CancelMovementRequest : GameRequest
    {
        public CancelMovementRequest(Guid requestId, Guid characterId)
            : base(requestId, characterId)
        {
        }
    }
}
