using System;
using MassRPG.Core.Authority;
using MassRPG.Core.Inventory;

namespace MassRPG.Client.Inventory
{
    /// <summary>
    /// Client-side bridge for inventory and equipment intent. Unity presentation code should use
    /// this controller instead of changing PlayerState inventory/equipment state directly.
    /// </summary>
    public sealed class InventoryActionController
    {
        private readonly IGameAuthority _authority;
        private readonly Guid _characterId;

        public InventoryActionController(IGameAuthority authority, Guid characterId)
        {
            _authority = authority ?? throw new ArgumentNullException(nameof(authority));
            if (characterId == Guid.Empty)
                throw new ArgumentException("Character id cannot be empty.", nameof(characterId));
            _characterId = characterId;
        }

        public AuthorityDecision MoveSlot(int fromIndex, int toIndex)
            => _authority.Submit(new MoveInventorySlotRequest(Guid.NewGuid(), _characterId, fromIndex, toIndex));

        public AuthorityDecision Equip(int inventoryIndex, EquipmentSlot? requestedSlot = null)
            => _authority.Submit(new EquipInventoryItemRequest(Guid.NewGuid(), _characterId, inventoryIndex, requestedSlot));

        public AuthorityDecision Unequip(EquipmentSlot slot)
            => _authority.Submit(new UnequipItemRequest(Guid.NewGuid(), _characterId, slot));

        public AuthorityDecision Eat(int inventoryIndex)
            => _authority.Submit(new EatFoodRequest(Guid.NewGuid(), _characterId, inventoryIndex));

        public AuthorityDecision DrinkPotion(int inventoryIndex)
            => _authority.Submit(new DrinkPotionRequest(Guid.NewGuid(), _characterId, inventoryIndex));

        public AuthorityDecision Drop(int inventoryIndex, int quantity = 1)
            => _authority.Submit(new DropInventoryItemRequest(Guid.NewGuid(), _characterId, inventoryIndex, quantity));

        public AuthorityDecision TakeGroundItem(Guid groundItemId)
            => _authority.Submit(new TakeGroundItemRequest(Guid.NewGuid(), _characterId, groundItemId));
    }
}
