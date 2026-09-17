using System;
using MassRPG.Core.Content;
using MassRPG.Core.World;

namespace MassRPG.Data.World
{
    /// <summary>
    /// Expanded in-memory view of one authored logical tile. Pages store ground IDs through a
    /// compact palette so a ContentId string is not repeated for every tile.
    /// </summary>
    public readonly struct AuthoredTileCell : IEquatable<AuthoredTileCell>
    {
        public AuthoredTileCell(
            ContentId groundId,
            short elevation,
            TileFlags flags = TileFlags.None,
            CardinalEdgeMask movementBlockedEdges = CardinalEdgeMask.None,
            CardinalEdgeMask lineOfSightBlockedEdges = CardinalEdgeMask.None,
            CardinalEdgeMask elevationTransitionEdges = CardinalEdgeMask.None)
        {
            GroundId = groundId;
            Elevation = elevation;
            Flags = flags;
            MovementBlockedEdges = movementBlockedEdges;
            LineOfSightBlockedEdges = lineOfSightBlockedEdges;
            ElevationTransitionEdges = elevationTransitionEdges;
        }

        public ContentId GroundId { get; }
        public short Elevation { get; }
        public TileFlags Flags { get; }
        public CardinalEdgeMask MovementBlockedEdges { get; }
        public CardinalEdgeMask LineOfSightBlockedEdges { get; }
        public CardinalEdgeMask ElevationTransitionEdges { get; }

        public bool Equals(AuthoredTileCell other)
            => GroundId == other.GroundId
            && Elevation == other.Elevation
            && Flags == other.Flags
            && MovementBlockedEdges == other.MovementBlockedEdges
            && LineOfSightBlockedEdges == other.LineOfSightBlockedEdges
            && ElevationTransitionEdges == other.ElevationTransitionEdges;

        public override bool Equals(object obj) => obj is AuthoredTileCell other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = GroundId.GetHashCode();
                hash = hash * 397 ^ Elevation;
                hash = hash * 397 ^ (int)Flags;
                hash = hash * 397 ^ (int)MovementBlockedEdges;
                hash = hash * 397 ^ (int)LineOfSightBlockedEdges;
                hash = hash * 397 ^ (int)ElevationTransitionEdges;
                return hash;
            }
        }
    }
}
