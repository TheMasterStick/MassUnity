using System;
using System.Collections.Generic;
using MassRPG.Core.Content;
using MassRPG.Core.World;

namespace MassRPG.Core.Construction
{
    /// <summary>
    /// Relative modular building arrangement saved by a player. A blueprint stores no plot/world
    /// ownership and can therefore be reused, shared or later represented as a marketable item.
    /// </summary>
    public sealed class BuildingBlueprint
    {
        private readonly List<BlueprintPiece> _pieces;

        public BuildingBlueprint(Guid blueprintId, Guid authorCharacterId, string name, IEnumerable<BlueprintPiece> pieces)
        {
            if (blueprintId == Guid.Empty) throw new ArgumentException("Blueprint id cannot be empty.", nameof(blueprintId));
            if (authorCharacterId == Guid.Empty) throw new ArgumentException("Author id cannot be empty.", nameof(authorCharacterId));
            if (pieces == null) throw new ArgumentNullException(nameof(pieces));
            BlueprintId = blueprintId;
            AuthorCharacterId = authorCharacterId;
            Name = string.IsNullOrWhiteSpace(name) ? "Building blueprint" : name;
            _pieces = new List<BlueprintPiece>(pieces);
            if (_pieces.Count == 0) throw new ArgumentException("A building blueprint must contain at least one piece.", nameof(pieces));
        }

        public Guid BlueprintId { get; }
        public Guid AuthorCharacterId { get; }
        public string Name { get; set; }
        public IReadOnlyList<BlueprintPiece> Pieces => _pieces;
    }

    public readonly struct BlueprintPiece
    {
        public BlueprintPiece(
            ContentId definitionId,
            int offsetX,
            int offsetY,
            int storeyOffset = 0,
            CardinalEdgeMask edge = CardinalEdgeMask.None,
            int rotationQuarterTurns = 0)
        {
            if (definitionId.IsEmpty) throw new ArgumentException("Build piece definition id cannot be empty.", nameof(definitionId));
            if (storeyOffset < 0 || storeyOffset > 2) throw new ArgumentOutOfRangeException(nameof(storeyOffset));
            DefinitionId = definitionId;
            OffsetX = offsetX;
            OffsetY = offsetY;
            StoreyOffset = storeyOffset;
            Edge = edge;
            RotationQuarterTurns = NormalizeRotation(rotationQuarterTurns);
        }

        public ContentId DefinitionId { get; }
        public int OffsetX { get; }
        public int OffsetY { get; }
        public int StoreyOffset { get; }
        public CardinalEdgeMask Edge { get; }
        public int RotationQuarterTurns { get; }

        public GridLocation Resolve(GridLocation origin)
            => Resolve(origin, 0);

        /// <summary>
        /// Resolves the relative piece after rotating the complete blueprint clockwise around its
        /// chosen origin. World coordinates use +X east and +Y south, so a clockwise quarter-turn
        /// transforms (x,y) to (-y,x): north (0,-1) becomes east (1,0).
        /// </summary>
        public GridLocation Resolve(GridLocation origin, int blueprintRotationQuarterTurns)
        {
            var rotation = NormalizeRotation(blueprintRotationQuarterTurns);
            RotateOffset(OffsetX, OffsetY, rotation, out var x, out var y);
            return new GridLocation(
                new GridCoord(origin.Tile.X + x, origin.Tile.Y + y),
                origin.Plane,
                origin.Storey + StoreyOffset);
        }

        public CardinalEdgeMask ResolveEdge(int blueprintRotationQuarterTurns)
            => RotateEdge(Edge, blueprintRotationQuarterTurns);

        public int ResolveRotationQuarterTurns(int blueprintRotationQuarterTurns)
            => NormalizeRotation(RotationQuarterTurns + blueprintRotationQuarterTurns);

        public static void RotateOffset(int x, int y, int quarterTurns, out int rotatedX, out int rotatedY)
        {
            switch (NormalizeRotation(quarterTurns))
            {
                case 0:
                    rotatedX = x;
                    rotatedY = y;
                    return;
                case 1:
                    rotatedX = -y;
                    rotatedY = x;
                    return;
                case 2:
                    rotatedX = -x;
                    rotatedY = -y;
                    return;
                default:
                    rotatedX = y;
                    rotatedY = -x;
                    return;
            }
        }

        public static CardinalEdgeMask RotateEdge(CardinalEdgeMask edge, int quarterTurns)
        {
            var result = edge;
            var turns = NormalizeRotation(quarterTurns);
            for (var i = 0; i < turns; i++)
            {
                var next = CardinalEdgeMask.None;
                if ((result & CardinalEdgeMask.North) != 0) next |= CardinalEdgeMask.East;
                if ((result & CardinalEdgeMask.East) != 0) next |= CardinalEdgeMask.South;
                if ((result & CardinalEdgeMask.South) != 0) next |= CardinalEdgeMask.West;
                if ((result & CardinalEdgeMask.West) != 0) next |= CardinalEdgeMask.North;
                result = next;
            }
            return result;
        }

        private static int NormalizeRotation(int quarterTurns)
        {
            var value = quarterTurns % 4;
            return value < 0 ? value + 4 : value;
        }
    }
}
