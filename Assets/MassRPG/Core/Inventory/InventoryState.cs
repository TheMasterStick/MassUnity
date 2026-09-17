using System;
using MassRPG.Core.Content;

namespace MassRPG.Core.Inventory
{
    public sealed class InventoryStack
    {
        public InventoryStack(ContentId itemId, int quantity)
        {
            if (itemId.IsEmpty) throw new ArgumentException("Item id cannot be empty.", nameof(itemId));
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            ItemId = itemId;
            Quantity = quantity;
        }

        public ContentId ItemId { get; }
        public int Quantity { get; internal set; }
    }

    public sealed class InventoryState
    {
        public const int DefaultCapacity = 28;
        private readonly InventoryStack[] _slots;

        public InventoryState(int capacity = DefaultCapacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _slots = new InventoryStack[capacity];
        }

        public int Capacity => _slots.Length;
        public InventoryStack GetSlot(int index) => _slots[index];

        internal void SetSlot(int index, InventoryStack stack)
        {
            _slots[index] = stack;
        }

        public int FindEmptySlot()
        {
            for (var i = 0; i < _slots.Length; i++)
                if (_slots[i] == null) return i;
            return -1;
        }

        public int EmptySlotCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < _slots.Length; i++)
                    if (_slots[i] == null) count++;
                return count;
            }
        }

        public int CountItem(ContentId itemId)
        {
            var total = 0;
            for (var i = 0; i < _slots.Length; i++)
            {
                var stack = _slots[i];
                if (stack != null && stack.ItemId == itemId)
                    total = checked(total + stack.Quantity);
            }
            return total;
        }
    }
}
