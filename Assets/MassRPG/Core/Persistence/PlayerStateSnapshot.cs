using System;
using System.Collections.Generic;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Core.Skills;
using MassRPG.Core.World;

namespace MassRPG.Core.Persistence
{
    public readonly struct SkillXpSnapshot
    {
        public SkillXpSnapshot(SkillId skill, long xp)
        {
            Skill = skill;
            Xp = xp;
        }
        public SkillId Skill { get; }
        public long Xp { get; }
    }

    public readonly struct InventorySlotSnapshot
    {
        public InventorySlotSnapshot(int slotIndex, ContentId itemId, int quantity)
        {
            if (slotIndex < 0) throw new ArgumentOutOfRangeException(nameof(slotIndex));
            if (itemId.IsEmpty) throw new ArgumentException("Item id cannot be empty.", nameof(itemId));
            if (quantity < 1) throw new ArgumentOutOfRangeException(nameof(quantity));
            SlotIndex = slotIndex;
            ItemId = itemId;
            Quantity = quantity;
        }
        public int SlotIndex { get; }
        public ContentId ItemId { get; }
        public int Quantity { get; }
    }

    public readonly struct EquipmentSlotSnapshot
    {
        public EquipmentSlotSnapshot(EquipmentSlot slot, ContentId itemId)
        {
            if (itemId.IsEmpty) throw new ArgumentException("Item id cannot be empty.", nameof(itemId));
            Slot = slot;
            ItemId = itemId;
        }
        public EquipmentSlot Slot { get; }
        public ContentId ItemId { get; }
    }

    /// <summary>
    /// Versioned authoritative character snapshot replacing the browser localStorage Player save.
    /// It deliberately stores logical ids/state rather than Unity objects, GameObjects or asset paths,
    /// making the same shape suitable for local development files and an eventual MMO database row.
    /// </summary>
    public sealed class PlayerStateSnapshot
    {
        public const int CurrentVersion = 1;

        public PlayerStateSnapshot(
            int version,
            Guid characterId,
            string name,
            GridLocation location,
            int currentHitpoints,
            CombatStyle combatStyle,
            MeleeTrainingStyle meleeTrainingStyle,
            int inventoryCapacity,
            ContentId? selectedAmmunitionItemId,
            IReadOnlyList<SkillXpSnapshot> skills,
            IReadOnlyList<InventorySlotSnapshot> inventory,
            IReadOnlyList<EquipmentSlotSnapshot> equipment)
        {
            if (version < 1) throw new ArgumentOutOfRangeException(nameof(version));
            if (characterId == Guid.Empty) throw new ArgumentException("Character id cannot be empty.", nameof(characterId));
            if (inventoryCapacity < 1) throw new ArgumentOutOfRangeException(nameof(inventoryCapacity));
            if (currentHitpoints < 0) throw new ArgumentOutOfRangeException(nameof(currentHitpoints));
            Version = version;
            CharacterId = characterId;
            Name = name ?? string.Empty;
            Location = location;
            CurrentHitpoints = currentHitpoints;
            CombatStyle = combatStyle;
            MeleeTrainingStyle = meleeTrainingStyle;
            InventoryCapacity = inventoryCapacity;
            SelectedAmmunitionItemId = selectedAmmunitionItemId;
            Skills = skills ?? Array.Empty<SkillXpSnapshot>();
            Inventory = inventory ?? Array.Empty<InventorySlotSnapshot>();
            Equipment = equipment ?? Array.Empty<EquipmentSlotSnapshot>();
        }

        public int Version { get; }
        public Guid CharacterId { get; }
        public string Name { get; }
        public GridLocation Location { get; }
        public int CurrentHitpoints { get; }
        public CombatStyle CombatStyle { get; }
        public MeleeTrainingStyle MeleeTrainingStyle { get; }
        public int InventoryCapacity { get; }
        public ContentId? SelectedAmmunitionItemId { get; }
        public IReadOnlyList<SkillXpSnapshot> Skills { get; }
        public IReadOnlyList<InventorySlotSnapshot> Inventory { get; }
        public IReadOnlyList<EquipmentSlotSnapshot> Equipment { get; }
    }

    public static class PlayerStateSnapshotCodec
    {
        public static PlayerStateSnapshot Capture(PlayerState player)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            var skills = new List<SkillXpSnapshot>();
            foreach (SkillId skill in Enum.GetValues(typeof(SkillId)))
                skills.Add(new SkillXpSnapshot(skill, player.Skills.GetXp(skill)));

            var inventory = new List<InventorySlotSnapshot>();
            for (var i = 0; i < player.Inventory.Capacity; i++)
            {
                var stack = player.Inventory.GetSlot(i);
                if (stack != null) inventory.Add(new InventorySlotSnapshot(i, stack.ItemId, stack.Quantity));
            }

            var equipment = new List<EquipmentSlotSnapshot>();
            foreach (var pair in player.Equipment.EquippedItems)
                equipment.Add(new EquipmentSlotSnapshot(pair.Key, pair.Value));

            return new PlayerStateSnapshot(
                PlayerStateSnapshot.CurrentVersion,
                player.CharacterId,
                player.Name,
                player.Location,
                player.CurrentHitpoints,
                player.CombatStyle,
                player.MeleeTrainingStyle,
                player.Inventory.Capacity,
                player.SelectedAmmunitionItemId,
                skills,
                inventory,
                equipment);
        }

        public static PlayerState Restore(PlayerStateSnapshot snapshot, IItemRuleSource itemRules)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (itemRules == null) throw new ArgumentNullException(nameof(itemRules));
            if (snapshot.Version > PlayerStateSnapshot.CurrentVersion)
                throw new InvalidOperationException("Player snapshot was written by a newer persistence version.");

            var player = new PlayerState(snapshot.CharacterId, snapshot.Name, snapshot.InventoryCapacity);
            var seenSkills = new HashSet<SkillId>();
            for (var i = 0; i < snapshot.Skills.Count; i++)
            {
                var skill = snapshot.Skills[i];
                if (!seenSkills.Add(skill.Skill)) throw new InvalidOperationException("Player snapshot contains duplicate skill state.");
                player.Skills.SetXp(skill.Skill, skill.Xp);
            }

            var seenInventorySlots = new HashSet<int>();
            for (var i = 0; i < snapshot.Inventory.Count; i++)
            {
                var slot = snapshot.Inventory[i];
                if (slot.SlotIndex < 0 || slot.SlotIndex >= player.Inventory.Capacity)
                    throw new InvalidOperationException("Player snapshot contains an inventory slot outside its declared capacity.");
                if (!seenInventorySlots.Add(slot.SlotIndex))
                    throw new InvalidOperationException("Player snapshot contains duplicate inventory slots.");
                if (!itemRules.TryGetRule(slot.ItemId, out var rule))
                    throw new InvalidOperationException("Player snapshot references unknown inventory item '" + slot.ItemId + "'.");
                if (!rule.Stackable && slot.Quantity != 1)
                    throw new InvalidOperationException("Player snapshot contains a stacked non-stackable item.");
                player.Inventory.SetSlot(slot.SlotIndex, new InventoryStack(slot.ItemId, slot.Quantity));
            }

            var seenEquipmentSlots = new HashSet<EquipmentSlot>();
            for (var i = 0; i < snapshot.Equipment.Count; i++)
            {
                var equipped = snapshot.Equipment[i];
                if (!seenEquipmentSlots.Add(equipped.Slot))
                    throw new InvalidOperationException("Player snapshot contains duplicate equipment slots.");
                if (!itemRules.TryGetRule(equipped.ItemId, out var rule))
                    throw new InvalidOperationException("Player snapshot references unknown equipped item '" + equipped.ItemId + "'.");
                if (!Allows(rule, equipped.Slot))
                    throw new InvalidOperationException("Player snapshot equips an item into a slot it cannot use.");
                player.Equipment.Set(equipped.Slot, equipped.ItemId);
            }

            if (player.Equipment.TryGet(EquipmentSlot.MainHand, out var mainHand)
                && itemRules.TryGetRule(mainHand, out var mainRule)
                && mainRule.TwoHanded
                && player.Equipment.IsOccupied(EquipmentSlot.OffHand))
                throw new InvalidOperationException("Player snapshot equips an off-hand item alongside a two-handed weapon.");

            if (snapshot.SelectedAmmunitionItemId.HasValue)
            {
                var ammunition = snapshot.SelectedAmmunitionItemId.Value;
                if (!itemRules.TryGetRule(ammunition, out _)
                    || player.Inventory.CountItem(ammunition) <= 0)
                    throw new InvalidOperationException("Player snapshot selects ammunition that is not present in inventory.");
                player.SelectedAmmunitionItemId = ammunition;
            }

            player.Location = snapshot.Location;
            player.CombatStyle = snapshot.CombatStyle;
            player.MeleeTrainingStyle = snapshot.MeleeTrainingStyle;
            player.CurrentHitpoints = Math.Min(snapshot.CurrentHitpoints, player.MaxHitpoints);
            return player;
        }

        private static bool Allows(ItemRule rule, EquipmentSlot slot)
        {
            for (var i = 0; i < rule.AllowedEquipmentSlots.Length; i++)
                if (rule.AllowedEquipmentSlots[i] == slot) return true;
            return false;
        }
    }
}
