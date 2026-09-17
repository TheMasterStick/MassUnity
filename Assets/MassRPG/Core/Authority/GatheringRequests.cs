using System;
using MassRPG.Core.Resources;

namespace MassRPG.Core.Authority
{
    public sealed class GatherResourceRequest : GameRequest
    {
        public GatherResourceRequest(Guid requestId, Guid characterId, ResourceNodeKey node)
            : base(requestId, characterId)
        {
            Node = node;
        }

        public ResourceNodeKey Node { get; }
    }
}
