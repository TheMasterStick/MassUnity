using System;
using MassRPG.Core.Authority;
using MassRPG.Core.Content;
using MassRPG.Core.World;

namespace MassRPG.Client.Construction
{
    /// <summary>
    /// Thin Unity-facing bridge for modular construction intent. Placement validation, material
    /// consumption, permissions, support checks, persistence, XP and upkeep remain authoritative.
    /// </summary>
    public sealed class ConstructionActionController
    {
        private readonly IGameAuthority _authority;
        private readonly Guid _characterId;

        public ConstructionActionController(IGameAuthority authority, Guid characterId)
        {
            _authority = authority ?? throw new ArgumentNullException(nameof(authority));
            if (characterId == Guid.Empty)
                throw new ArgumentException("Character id cannot be empty.", nameof(characterId));
            _characterId = characterId;
        }

        public AuthorityDecision Place(
            Guid plotId,
            ContentId definitionId,
            GridLocation anchor,
            CardinalEdgeMask edge = CardinalEdgeMask.None,
            int rotationQuarterTurns = 0)
            => _authority.Submit(new PlaceBuildPieceRequest(
                Guid.NewGuid(),
                _characterId,
                plotId,
                definitionId,
                anchor,
                edge,
                rotationQuarterTurns));

        public AuthorityDecision Demolish(Guid plotId, Guid pieceInstanceId)
            => _authority.Submit(new DemolishBuildPieceRequest(
                Guid.NewGuid(),
                _characterId,
                plotId,
                pieceInstanceId));

        public AuthorityDecision PayUpkeep(Guid plotId, int offeredGold)
            => _authority.Submit(new PayPlotUpkeepRequest(
                Guid.NewGuid(),
                _characterId,
                plotId,
                offeredGold));
    }
}
