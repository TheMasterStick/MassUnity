using System;
using MassRPG.Core.Content;

namespace MassRPG.Core.Authority
{
    public sealed class ActivateFastTravelNodeRequest : GameRequest
    {
        public ActivateFastTravelNodeRequest(Guid requestId, Guid characterId, ContentId nodeId)
            : base(requestId, characterId)
        {
            if (nodeId.IsEmpty) throw new ArgumentException("Node id cannot be empty.", nameof(nodeId));
            NodeId = nodeId;
        }

        public ContentId NodeId { get; }
    }

    public sealed class OpenFastTravelMapRequest : GameRequest
    {
        public OpenFastTravelMapRequest(Guid requestId, Guid characterId, ContentId originNodeId)
            : base(requestId, characterId)
        {
            if (originNodeId.IsEmpty) throw new ArgumentException("Origin node id cannot be empty.", nameof(originNodeId));
            OriginNodeId = originNodeId;
        }

        public ContentId OriginNodeId { get; }
    }

    public sealed class CommitFastTravelRequest : GameRequest
    {
        public CommitFastTravelRequest(Guid requestId, Guid characterId, ContentId destinationNodeId)
            : base(requestId, characterId)
        {
            if (destinationNodeId.IsEmpty) throw new ArgumentException("Destination node id cannot be empty.", nameof(destinationNodeId));
            DestinationNodeId = destinationNodeId;
        }

        public ContentId DestinationNodeId { get; }
    }
}
