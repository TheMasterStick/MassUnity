using System;

namespace MassRPG.Core.World
{
    [Flags]
    public enum CardinalEdgeMask : byte
    {
        None = 0,
        North = 1 << 0,
        East = 1 << 1,
        South = 1 << 2,
        West = 1 << 3
    }

    public static class CardinalEdges
    {
        public static CardinalEdgeMask Between(GridCoord from, GridCoord to)
        {
            var dx = to.X - from.X;
            var dy = to.Y - from.Y;
            if (dx == 0 && dy == -1) return CardinalEdgeMask.North;
            if (dx == 1 && dy == 0) return CardinalEdgeMask.East;
            if (dx == 0 && dy == 1) return CardinalEdgeMask.South;
            if (dx == -1 && dy == 0) return CardinalEdgeMask.West;
            throw new ArgumentException("Coordinates must be cardinally adjacent.");
        }

        public static CardinalEdgeMask Opposite(CardinalEdgeMask edge)
        {
            switch (edge)
            {
                case CardinalEdgeMask.North: return CardinalEdgeMask.South;
                case CardinalEdgeMask.East: return CardinalEdgeMask.West;
                case CardinalEdgeMask.South: return CardinalEdgeMask.North;
                case CardinalEdgeMask.West: return CardinalEdgeMask.East;
                default: throw new ArgumentException("A single cardinal edge is required.", nameof(edge));
            }
        }
    }
}
