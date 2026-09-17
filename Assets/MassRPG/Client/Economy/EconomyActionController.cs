using System;
using MassRPG.Core.Authority;
using MassRPG.Core.Content;
using MassRPG.Core.World;

namespace MassRPG.Client.Economy
{
    /// <summary>
    /// Client bridge for bank and shop intent. Proximity, prices, stock, capacity and inventory
    /// mutation remain authoritative and are never calculated by Unity presentation code.
    /// </summary>
    public sealed class EconomyActionController
    {
        private readonly IGameAuthority _authority;
        private readonly Guid _characterId;

        public EconomyActionController(IGameAuthority authority, Guid characterId)
        {
            _authority = authority ?? throw new ArgumentNullException(nameof(authority));
            if (characterId == Guid.Empty)
                throw new ArgumentException("Character id cannot be empty.", nameof(characterId));
            _characterId = characterId;
        }

        public AuthorityDecision Deposit(GridLocation bankLocation, int inventorySlot, int quantity)
            => _authority.Submit(new DepositBankItemRequest(
                Guid.NewGuid(), _characterId, bankLocation, inventorySlot, quantity));

        public AuthorityDecision Withdraw(GridLocation bankLocation, ContentId itemId, int quantity)
            => _authority.Submit(new WithdrawBankItemRequest(
                Guid.NewGuid(), _characterId, bankLocation, itemId, quantity));

        public AuthorityDecision Buy(ContentId shopId, GridLocation shopLocation, ContentId itemId, int quantity)
            => _authority.Submit(new BuyShopItemRequest(
                Guid.NewGuid(), _characterId, shopId, shopLocation, itemId, quantity));

        public AuthorityDecision Sell(ContentId shopId, GridLocation shopLocation, int inventorySlot, int quantity)
            => _authority.Submit(new SellShopItemRequest(
                Guid.NewGuid(), _characterId, shopId, shopLocation, inventorySlot, quantity));
    }
}
