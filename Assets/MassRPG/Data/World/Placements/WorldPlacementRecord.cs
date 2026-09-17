using System;
using MassRPG.Core.Content;
using MassRPG.Core.World;

namespace MassRPG.Data.World.Placements
{
    public enum WorldPlacementKind
    {
        Object,
        Doodad,
        Resource,
        NpcAnchor,
        TransportNode,
        Custom
    }

    /// <summary>
    /// Authored placement separate from its content definition. Gameplay things retain an exact
    /// logical anchor while presentation may offset/rotate the model inside that anchor. Decorative
    /// doodads use the same record but are free to ignore gameplay occupancy.
    /// </summary>
    public sealed class WorldPlacementRecord
    {
        public WorldPlacementRecord(
            ContentId instanceId,
            ContentId definitionId,
            WorldPlacementKind kind,
            GridLocation anchor)
        {
            if (instanceId.IsEmpty) throw new ArgumentException("Placement instance id cannot be empty.", nameof(instanceId));
            if (definitionId.IsEmpty) throw new ArgumentException("Placement definition id cannot be empty.", nameof(definitionId));
            if (!WorldConstants.IsInsideWorld(anchor.Tile)) throw new ArgumentOutOfRangeException(nameof(anchor));
            InstanceId = instanceId;
            DefinitionId = definitionId;
            Kind = kind;
            Anchor = anchor;
        }

        public ContentId InstanceId { get; }
        public ContentId DefinitionId { get; set; }
        public WorldPlacementKind Kind { get; set; }
        public GridLocation Anchor { get; set; }

        /// <summary>Visual-only horizontal offset from the anchor centre, in logical tile units.</summary>
        public float OffsetX
        {
            get => _offsetX;
            set => _offsetX = ClampOffset(value);
        }

        /// <summary>Visual-only horizontal offset along logical Y/Z, in logical tile units.</summary>
        public float OffsetY
        {
            get => _offsetY;
            set => _offsetY = ClampOffset(value);
        }

        public float VisualHeightOffset { get; set; }
        public float YawDegrees { get; set; }
        public bool Enabled { get; set; } = true;
        public bool IsGameplayAnchored => Kind != WorldPlacementKind.Doodad;

        private float _offsetX;
        private float _offsetY;

        private static float ClampOffset(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
            return Math.Max(-0.49f, Math.Min(0.49f, value));
        }
    }
}
