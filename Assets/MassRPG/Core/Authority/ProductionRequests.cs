using System;
using MassRPG.Core.Content;
using MassRPG.Core.World;

namespace MassRPG.Core.Authority
{
    public sealed class StartProductionRequest : GameRequest
    {
        public StartProductionRequest(
            Guid requestId,
            Guid characterId,
            ContentId recipeId,
            int quantity,
            GridLocation? stationLocation = null)
            : base(requestId, characterId)
        {
            if (recipeId.IsEmpty) throw new ArgumentException("Recipe id cannot be empty.", nameof(recipeId));
            if (quantity < 1) throw new ArgumentOutOfRangeException(nameof(quantity));
            RecipeId = recipeId;
            Quantity = quantity;
            StationLocation = stationLocation;
        }

        public ContentId RecipeId { get; }
        public int Quantity { get; }
        public GridLocation? StationLocation { get; }
    }

    public sealed class CancelProductionRequest : GameRequest
    {
        public CancelProductionRequest(Guid requestId, Guid characterId)
            : base(requestId, characterId)
        {
        }
    }
}
