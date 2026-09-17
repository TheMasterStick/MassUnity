using MassRPG.Core.Inventory;
using UnityEngine;

namespace MassRPG.Client.Characters
{
    /// <summary>
    /// Presentation-only attachment points for modular 3D equipment. Gameplay ownership remains
    /// in EquipmentState; this rig merely tells the Unity character where a visible item belongs.
    /// </summary>
    public sealed class EquipmentSocketRig : MonoBehaviour
    {
        [SerializeField] private Transform mainHandSocket;
        [SerializeField] private Transform offHandSocket;
        [SerializeField] private Transform headSocket;
        [SerializeField] private Transform backSocket;

        public Transform MainHandSocket => mainHandSocket;
        public Transform OffHandSocket => offHandSocket;

        public Transform ResolveAttachmentSocket(EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.MainHand: return mainHandSocket;
                case EquipmentSlot.OffHand: return offHandSocket;
                case EquipmentSlot.Head: return headSocket;
                case EquipmentSlot.Cape: return backSocket;
                default: return null;
            }
        }
    }
}
