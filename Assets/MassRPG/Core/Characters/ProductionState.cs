using System;
using MassRPG.Core.Content;
using MassRPG.Core.World;

namespace MassRPG.Core.Characters
{
    /// <summary>
    /// Server-owned in-progress production action. Recipe content is referenced by permanent ID;
    /// the client cannot shorten duration or mint outputs by editing local UI state.
    /// </summary>
    public sealed class ProductionState
    {
        public bool IsActive { get; private set; }
        public ContentId RecipeId { get; private set; }
        public int RemainingQuantity { get; private set; }
        public long NextCompletionAtUnixMilliseconds { get; private set; }
        public GridLocation? StationLocation { get; private set; }

        public void Begin(
            ContentId recipeId,
            int quantity,
            long firstCompletionAtUnixMilliseconds,
            GridLocation? stationLocation)
        {
            if (recipeId.IsEmpty) throw new ArgumentException("Recipe id cannot be empty.", nameof(recipeId));
            if (quantity < 1) throw new ArgumentOutOfRangeException(nameof(quantity));
            RecipeId = recipeId;
            RemainingQuantity = quantity;
            NextCompletionAtUnixMilliseconds = firstCompletionAtUnixMilliseconds;
            StationLocation = stationLocation;
            IsActive = true;
        }

        public void CompleteOne(long nextCompletionAtUnixMilliseconds)
        {
            if (!IsActive) return;
            RemainingQuantity--;
            if (RemainingQuantity <= 0)
            {
                Clear();
                return;
            }
            NextCompletionAtUnixMilliseconds = nextCompletionAtUnixMilliseconds;
        }

        public void Clear()
        {
            IsActive = false;
            RecipeId = default;
            RemainingQuantity = 0;
            NextCompletionAtUnixMilliseconds = 0;
            StationLocation = null;
        }
    }
}
