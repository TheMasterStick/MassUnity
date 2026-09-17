using System;
using System.Collections.Generic;
using MassRPG.Client.Presentation;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using UnityEngine;

namespace MassRPG.Client.Characters
{
    /// <summary>
    /// Presentation-only binding from authoritative EquipmentState to linked Unity model assets.
    /// The first pass intentionally handles socket-mounted equipment (hands/head/back). Body-fitted
    /// skinned armour remains a separate layer once the canonical male/female rigs are imported.
    /// </summary>
    public sealed class EquipmentVisualBinder : MonoBehaviour
    {
        [SerializeField] private EquipmentSocketRig socketRig;
        [SerializeField] private PresentationAssetCatalog presentationAssets;
        [SerializeField] private bool logMissingModels;

        private readonly Dictionary<EquipmentSlot, VisualInstance> _instances = new Dictionary<EquipmentSlot, VisualInstance>();
        private readonly HashSet<string> _loggedMissing = new HashSet<string>(StringComparer.Ordinal);

        public EquipmentSocketRig SocketRig => socketRig;
        public PresentationAssetCatalog PresentationAssets => presentationAssets;

        public void Configure(EquipmentSocketRig rig, PresentationAssetCatalog catalog)
        {
            socketRig = rig;
            presentationAssets = catalog;
            ClearAll();
        }

        public void Apply(EquipmentState equipment)
        {
            if (equipment == null) throw new ArgumentNullException(nameof(equipment));
            if (socketRig == null || presentationAssets == null) return;

            ApplySlot(equipment, EquipmentSlot.MainHand);
            ApplySlot(equipment, EquipmentSlot.OffHand);
            ApplySlot(equipment, EquipmentSlot.Head);
            ApplySlot(equipment, EquipmentSlot.Cape);
        }

        public void ClearAll()
        {
            foreach (var pair in _instances) DestroyVisual(pair.Value.GameObject);
            _instances.Clear();
        }

        private void ApplySlot(EquipmentState equipment, EquipmentSlot slot)
        {
            var socket = socketRig.ResolveAttachmentSocket(slot);
            if (socket == null)
            {
                RemoveSlot(slot);
                return;
            }

            if (!equipment.TryGet(slot, out var itemId))
            {
                RemoveSlot(slot);
                return;
            }

            if (_instances.TryGetValue(slot, out var existing) && existing.ItemId == itemId && existing.GameObject != null)
                return;

            RemoveSlot(slot);
            if (!presentationAssets.TryResolve<GameObject>(itemId, PresentationAssetRole.Model, out var prefab))
            {
                LogMissing(itemId, slot);
                return;
            }

            var instance = Instantiate(prefab, socket, false);
            instance.name = itemId.Value + " (" + EquipmentSlotLabels.DisplayName(slot) + ")";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            _instances[slot] = new VisualInstance(itemId, instance);
        }

        private void RemoveSlot(EquipmentSlot slot)
        {
            if (!_instances.TryGetValue(slot, out var existing)) return;
            _instances.Remove(slot);
            DestroyVisual(existing.GameObject);
        }

        private void LogMissing(ContentId itemId, EquipmentSlot slot)
        {
            if (!logMissingModels) return;
            var key = itemId.Value + "|" + slot;
            if (!_loggedMissing.Add(key)) return;
            Debug.LogWarning(
                "MassRPG presentation catalog has no linked model for equipped item '" + itemId.Value
                + "' in " + EquipmentSlotLabels.DisplayName(slot) + ". Gameplay equipment remains valid; presentation can be linked later.",
                this);
        }

        private static void DestroyVisual(GameObject visual)
        {
            if (visual == null) return;
            if (Application.isPlaying) Destroy(visual);
            else DestroyImmediate(visual);
        }

        private readonly struct VisualInstance
        {
            public VisualInstance(ContentId itemId, GameObject gameObject)
            {
                ItemId = itemId;
                GameObject = gameObject;
            }

            public ContentId ItemId { get; }
            public GameObject GameObject { get; }
        }

        private void OnDestroy() => ClearAll();
    }
}
