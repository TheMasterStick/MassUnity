using System;
using MassRPG.Core.Content;
using MassRPG.Core.World;

namespace MassRPG.Core.Authority
{
    /// <summary>Requests authoritative placement of one modular build piece inside a player plot.</summary>
    public sealed class PlaceBuildPieceRequest : GameRequest
    {
        public PlaceBuildPieceRequest(
            Guid requestId,
            Guid characterId,
            Guid plotId,
            ContentId definitionId,
            GridLocation anchor,
            CardinalEdgeMask edge = CardinalEdgeMask.None,
            int rotationQuarterTurns = 0)
            : base(requestId, characterId)
        {
            if (plotId == Guid.Empty) throw new ArgumentException("Plot id cannot be empty.", nameof(plotId));
            if (definitionId.IsEmpty) throw new ArgumentException("Build piece definition id cannot be empty.", nameof(definitionId));

            PlotId = plotId;
            DefinitionId = definitionId;
            Anchor = anchor;
            Edge = edge;
            RotationQuarterTurns = NormalizeRotation(rotationQuarterTurns);
        }

        public Guid PlotId { get; }
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

    /// <summary>Requests authoritative demolition of one persistent modular build piece.</summary>
    public sealed class DemolishBuildPieceRequest : GameRequest
    {
        public DemolishBuildPieceRequest(Guid requestId, Guid characterId, Guid plotId, Guid pieceInstanceId)
            : base(requestId, characterId)
        {
            if (plotId == Guid.Empty) throw new ArgumentException("Plot id cannot be empty.", nameof(plotId));
            if (pieceInstanceId == Guid.Empty) throw new ArgumentException("Build piece instance id cannot be empty.", nameof(pieceInstanceId));

            PlotId = plotId;
            PieceInstanceId = pieceInstanceId;
        }

        public Guid PlotId { get; }
        public Guid PieceInstanceId { get; }
    }

    /// <summary>
    /// Offers gold toward prepaid upkeep for an owned player plot. The server decides the daily
    /// rate, whole-day conversion, paid-through time and whether the plot is eligible for payment.
    /// </summary>
    public sealed class PayPlotUpkeepRequest : GameRequest
    {
        public PayPlotUpkeepRequest(Guid requestId, Guid characterId, Guid plotId, int offeredGold)
            : base(requestId, characterId)
        {
            if (plotId == Guid.Empty) throw new ArgumentException("Plot id cannot be empty.", nameof(plotId));
            if (offeredGold < 1) throw new ArgumentOutOfRangeException(nameof(offeredGold));

            PlotId = plotId;
            OfferedGold = offeredGold;
        }

        public Guid PlotId { get; }
        public int OfferedGold { get; }
    }
}
