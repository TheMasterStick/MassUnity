using System;
using System.Collections.Generic;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.World;

namespace MassRPG.Server.Economy
{
    /// <summary>
    /// Resolves whether a character is actually beside an authored bank/shop. Economy requests carry
    /// a location so the eventual network client cannot bank or trade remotely by sending only an id.
    /// </summary>
    public interface IEconomyAccessSource
    {
        bool CanUseBank(PlayerState player, GridLocation bankLocation);
        bool CanUseShop(PlayerState player, ContentId shopId, GridLocation shopLocation);
    }

    /// <summary>Development/vertical-slice access source; production will resolve authored placements.</summary>
    public sealed class InMemoryEconomyAccessSource : IEconomyAccessSource
    {
        private readonly HashSet<GridLocation> _banks = new HashSet<GridLocation>();
        private readonly Dictionary<GridLocation, ContentId> _shops = new Dictionary<GridLocation, ContentId>();

        public void RegisterBank(GridLocation location) => _banks.Add(location);
        public void RegisterShop(GridLocation location, ContentId shopId)
        {
            if (shopId.IsEmpty) throw new ArgumentException("Shop id cannot be empty.", nameof(shopId));
            _shops[location] = shopId;
        }

        public bool CanUseBank(PlayerState player, GridLocation bankLocation)
            => player != null
               && player.Location.SameLayer(bankLocation)
               && GridMath.RangeDistance(player.Tile, bankLocation.Tile) <= 1
               && _banks.Contains(bankLocation);

        public bool CanUseShop(PlayerState player, ContentId shopId, GridLocation shopLocation)
            => player != null
               && player.Location.SameLayer(shopLocation)
               && GridMath.RangeDistance(player.Tile, shopLocation.Tile) <= 1
               && _shops.TryGetValue(shopLocation, out var actualShop)
               && actualShop == shopId;
    }
}
