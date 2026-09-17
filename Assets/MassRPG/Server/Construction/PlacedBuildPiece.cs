using System;
using MassRPG.Core.Content;
using MassRPG.Core.World;

namespace MassRPG.Server.Construction
{
    /// <summary>Persistent authoritative instance of one modular plot building piece.</summary>
    public sealed class PlacedBuildPiece
    {
        public PlacedBuildPiece(
            Guid instanceId,
            ContentId definitionId,
            GridLocation anchor,
            CardinalEdgeMask edge = CardinalEdgeMask.None,
            int rotationQuarterTurns = 0)
        {
            if (instanceId == Guid.Empty) throw new ArgumentException("Instance id cannot be empty.", nameof(instanceId));
            if (definitionId.IsEmpty) throw new ArgumentException("Definition id cannot be empty.", nameof(definitionId));
            InstanceId = instanceId;
            DefinitionId = definitionId;
            Anchor = anchor;
            Edge = edge;
            RotationQuarterTurns = NormalizeRotation(rotationQuarterTurns);
        }

        public Guid InstanceId { get; }
        public ContentId DefinitionId { get; }
        public GridLocation Anchor { get; }
        public CardinalEdgeMask Edge { get; }
        public int RotationQuarterTurns { get; }

        private static int NormalizeRotation(int quarterTurns)
        {
            var value = quarterTurns % 4;
            return value < 0 ? value + 4 : value;
        }
    }
}
