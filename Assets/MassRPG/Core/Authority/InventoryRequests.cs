using System;
using MassRPG.Core.Inventory;

namespace MassRPG.Core.Authority
{
    public sealed class MoveInventorySlotRequest : GameRequest
    {
        public MoveInventorySlotRequest(Guid requestId, Guid characterId, int fromIndex, int toIndex)
            : base(requestId, characterId)
        {
            FromIndex = fromIndex;
            ToIndex = toIndex;
        }

        public int FromIndex { get; }
        public int ToIndex { get; }
    }

    public sealed class EquipInventoryItemRequest : GameRequest
    {
        public EquipInventoryItemRequest(Guid requestId, Guid characterId, int inventoryIndex, EquipmentSlot? requestedSlot = null)
            : base(requestId, characterId)
        {
            InventoryIndex = inventoryIndex;
            RequestedSlot = requestedSlot;
        }

        public int InventoryIndex { get; }
        public EquipmentSlot? RequestedSlot { get; }
    }

    public sealed class UnequipItemRequest : GameRequest
    {
        public UnequipItemRequest(Guid requestId, Guid characterId, EquipmentSlot slot)
            : base(requestId, characterId)
        {
            Slot = slot;
        }

        public EquipmentSlot Slot { get; }
    }
}
