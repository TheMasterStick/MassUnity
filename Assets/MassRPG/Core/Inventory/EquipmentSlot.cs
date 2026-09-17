namespace MassRPG.Core.Inventory
{
    /// <summary>
    /// Settled MassRPG equipment layout. MainHand and OffHand are the canonical hand semantics.
    /// The Weapon/Shield aliases preserve migrated code/data while the old names are retired.
    /// One-handed weapons may occupy either hand; shields are off-hand only; two-handed weapons
    /// occupy MainHand and exclude OffHand.
    /// </summary>
    public enum EquipmentSlot
    {
        Head = 0,
        Amulet = 1,
        Cape = 2,
        Chest = 3,
        Legs = 4,
        Hands = 5,
        Boots = 6,
        MainHand = 7,
        OffHand = 8,
        Ring1 = 9,
        Ring2 = 10,

        // Migration aliases. Do not present these legacy labels in the Unity UI.
        Weapon = MainHand,
        Shield = OffHand
    }

    public static class EquipmentSlotLabels
    {
        public static string DisplayName(EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.MainHand: return "Main Hand";
                case EquipmentSlot.OffHand: return "Off Hand";
                case EquipmentSlot.Ring1: return "Ring 1";
                case EquipmentSlot.Ring2: return "Ring 2";
                default: return slot.ToString();
            }
        }
    }
}
