using System;
using System.Collections.Generic;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Server.Death;

namespace MassRPG.Server.Items
{
    public readonly struct EquipmentDurabilityEntry
    {
        public EquipmentDurabilityEntry(EquipmentSlot slot, ContentId itemId, int durabilityBasisPoints)
        {
            if (itemId.IsEmpty) throw new ArgumentException("Item id cannot be empty.", nameof(itemId));
            if (durabilityBasisPoints < 0 || durabilityBasisPoints > EquipmentDurabilityService.MaximumDurabilityBasisPoints)
                throw new ArgumentOutOfRangeException(nameof(durabilityBasisPoints));
            Slot = slot;
            ItemId = itemId;
            DurabilityBasisPoints = durabilityBasisPoints;
        }

        public EquipmentSlot Slot { get; }
        public ContentId ItemId { get; }
        public int DurabilityBasisPoints { get; }
        public bool IsBroken => DurabilityBasisPoints <= 0;
    }

    /// <summary>
    /// Authoritative durability state for currently equipped items. Durability is deliberately kept
    /// outside ItemDefinition because it is per equipped instance, not static content. Entries are
    /// keyed by slot plus item id so replacing equipment cannot accidentally inherit the old item's
    /// damage. 10000 basis points represents pristine condition.
    /// </summary>
    public sealed class EquipmentDurabilityService : IEquipmentDurabilityLossSink
    {
        public const int MaximumDurabilityBasisPoints = 10000;

        private readonly Dictionary<Guid, Dictionary<EquipmentSlot, EquipmentDurabilityEntry>> _byCharacter =
            new Dictionary<Guid, Dictionary<EquipmentSlot, EquipmentDurabilityEntry>>();

        public void NormalizeAgainstEquipment(PlayerState player)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            var state = GetOrCreate(player.CharacterId);
            var occupied = new HashSet<EquipmentSlot>();
            foreach (var pair in player.Equipment.EquippedItems)
            {
                occupied.Add(pair.Key);
                if (!state.TryGetValue(pair.Key, out var entry) || entry.ItemId != pair.Value)
                    state[pair.Key] = new EquipmentDurabilityEntry(pair.Key, pair.Value, MaximumDurabilityBasisPoints);
            }

            var stale = new List<EquipmentSlot>();
            foreach (var pair in state)
                if (!occupied.Contains(pair.Key)) stale.Add(pair.Key);
            for (var i = 0; i < stale.Count; i++) state.Remove(stale[i]);
        }

        public bool TryGet(PlayerState player, EquipmentSlot slot, out EquipmentDurabilityEntry entry)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            NormalizeAgainstEquipment(player);
            return GetOrCreate(player.CharacterId).TryGetValue(slot, out entry);
        }

        public void ApplyDeathDurabilityLoss(PlayerState player, int lossBasisPoints)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (lossBasisPoints < 0 || lossBasisPoints > MaximumDurabilityBasisPoints)
                throw new ArgumentOutOfRangeException(nameof(lossBasisPoints));
            if (lossBasisPoints == 0) return;

            NormalizeAgainstEquipment(player);
            var state = GetOrCreate(player.CharacterId);
            var slots = new List<EquipmentSlot>(state.Keys);
            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                var entry = state[slot];
                state[slot] = new EquipmentDurabilityEntry(
                    slot,
                    entry.ItemId,
                    Math.Max(0, entry.DurabilityBasisPoints - lossBasisPoints));
            }
        }

        public bool TryDamage(PlayerState player, EquipmentSlot slot, int lossBasisPoints)
        {
            if (lossBasisPoints < 0) throw new ArgumentOutOfRangeException(nameof(lossBasisPoints));
            if (!TryGet(player, slot, out var entry)) return false;
            GetOrCreate(player.CharacterId)[slot] = new EquipmentDurabilityEntry(
                slot,
                entry.ItemId,
                Math.Max(0, entry.DurabilityBasisPoints - lossBasisPoints));
            return true;
        }

        public bool TryRepair(PlayerState player, EquipmentSlot slot, int repairBasisPoints)
        {
            if (repairBasisPoints < 0) throw new ArgumentOutOfRangeException(nameof(repairBasisPoints));
            if (!TryGet(player, slot, out var entry)) return false;
            GetOrCreate(player.CharacterId)[slot] = new EquipmentDurabilityEntry(
                slot,
                entry.ItemId,
                Math.Min(MaximumDurabilityBasisPoints, entry.DurabilityBasisPoints + repairBasisPoints));
            return true;
        }

        public IReadOnlyList<EquipmentDurabilityEntry> Capture(PlayerState player)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            NormalizeAgainstEquipment(player);
            var result = new List<EquipmentDurabilityEntry>();
            foreach (var pair in GetOrCreate(player.CharacterId)) result.Add(pair.Value);
            result.Sort((a, b) => ((int)a.Slot).CompareTo((int)b.Slot));
            return result;
        }

        public void Restore(PlayerState player, IEnumerable<EquipmentDurabilityEntry> entries)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            var restored = new Dictionary<EquipmentSlot, EquipmentDurabilityEntry>();
            foreach (var entry in entries)
            {
                if (restored.ContainsKey(entry.Slot))
                    throw new InvalidOperationException("Duplicate durability slot '" + entry.Slot + "'.");
                if (!player.Equipment.TryGet(entry.Slot, out var equipped) || equipped != entry.ItemId)
                    throw new InvalidOperationException("Durability snapshot does not match equipped item in slot '" + entry.Slot + "'.");
                restored.Add(entry.Slot, entry);
            }

            _byCharacter[player.CharacterId] = restored;
            NormalizeAgainstEquipment(player);
        }

        private Dictionary<EquipmentSlot, EquipmentDurabilityEntry> GetOrCreate(Guid characterId)
        {
            if (!_byCharacter.TryGetValue(characterId, out var state))
            {
                state = new Dictionary<EquipmentSlot, EquipmentDurabilityEntry>();
                _byCharacter.Add(characterId, state);
            }
            return state;
        }
    }
}
