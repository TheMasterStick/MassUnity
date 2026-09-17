using System.Collections.Generic;
using MassRPG.Core.Content;

namespace MassRPG.Core.Inventory
{
    public sealed class EquipmentState
    {
        private readonly Dictionary<EquipmentSlot, ContentId> _items = new Dictionary<EquipmentSlot, ContentId>();

        public IEnumerable<KeyValuePair<EquipmentSlot, ContentId>> EquippedItems => _items;
        public bool TryGet(EquipmentSlot slot, out ContentId itemId) => _items.TryGetValue(slot, out itemId);
        public bool IsOccupied(EquipmentSlot slot) => _items.ContainsKey(slot);
        public ContentId? GetOrNull(EquipmentSlot slot) => _items.TryGetValue(slot, out var itemId) ? itemId : (ContentId?)null;

        internal void Set(EquipmentSlot slot, ContentId itemId)
        {
            _items[slot] = itemId;
        }

        internal bool Clear(EquipmentSlot slot, out ContentId previous)
        {
            if (!_items.TryGetValue(slot, out previous)) return false;
            _items.Remove(slot);
            return true;
        }
    }
}
