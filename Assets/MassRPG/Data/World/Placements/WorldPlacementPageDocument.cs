using System;
using System.Collections.Generic;
using MassRPG.Core.Content;
using MassRPG.Core.World;

namespace MassRPG.Data.World.Placements
{
    /// <summary>
    /// Placement IO is page-scoped just like terrain, but stored separately so moving a tree/chest
    /// does not rewrite terrain data. Runtime streaming can request both documents for the same page.
    /// </summary>
    public sealed class WorldPlacementPageDocument
    {
        public const int CurrentFormatVersion = 1;
        public int FormatVersion { get; set; } = CurrentFormatVersion;
        public int PageX { get; set; }
        public int PageY { get; set; }
        public int Plane { get; set; }
        public int Storey { get; set; }
        public List<WorldPlacementDocument> Placements { get; set; } = new List<WorldPlacementDocument>();
    }

    public sealed class WorldPlacementDocument
    {
        public string InstanceId { get; set; } = string.Empty;
        public string DefinitionId { get; set; } = string.Empty;
        public WorldPlacementKind Kind { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public float OffsetX { get; set; }
        public float OffsetY { get; set; }
        public float VisualHeightOffset { get; set; }
        public float YawDegrees { get; set; }
        public bool Enabled { get; set; } = true;
    }

    public static class WorldPlacementPageCodec
    {
        public static WorldPlacementPageDocument Encode(WorldPageKey key, IEnumerable<WorldPlacementRecord> placements)
        {
            if (placements == null) throw new ArgumentNullException(nameof(placements));
            var document = new WorldPlacementPageDocument
            {
                PageX = key.Page.X,
                PageY = key.Page.Y,
                Plane = key.Plane,
                Storey = key.Storey
            };
            foreach (var placement in placements)
            {
                if (placement == null) continue;
                if (!WorldAddressing.TryResolve(placement.Anchor, out var address) || address.Key != key)
                    throw new InvalidOperationException($"Placement '{placement.InstanceId}' does not belong to page {key}.");
                document.Placements.Add(new WorldPlacementDocument
                {
                    InstanceId = placement.InstanceId.Value,
                    DefinitionId = placement.DefinitionId.Value,
                    Kind = placement.Kind,
                    X = placement.Anchor.Tile.X,
                    Y = placement.Anchor.Tile.Y,
                    OffsetX = placement.OffsetX,
                    OffsetY = placement.OffsetY,
                    VisualHeightOffset = placement.VisualHeightOffset,
                    YawDegrees = placement.YawDegrees,
                    Enabled = placement.Enabled
                });
            }
            return document;
        }

        public static List<WorldPlacementRecord> Decode(WorldPlacementPageDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (document.FormatVersion != WorldPlacementPageDocument.CurrentFormatVersion)
                throw new InvalidOperationException($"Unsupported placement page format version {document.FormatVersion}.");
            var key = new WorldPageKey(new WorldPageCoord(document.PageX, document.PageY), document.Plane, document.Storey);
            var result = new List<WorldPlacementRecord>();
            if (document.Placements == null) return result;

            for (var i = 0; i < document.Placements.Count; i++)
            {
                var data = document.Placements[i];
                if (!ContentId.TryCreate(data.InstanceId, out var instanceId)) throw new InvalidOperationException("Placement has invalid instance ID.");
                if (!ContentId.TryCreate(data.DefinitionId, out var definitionId)) throw new InvalidOperationException("Placement has invalid definition ID.");
                var anchor = new GridLocation(new GridCoord(data.X, data.Y), document.Plane, document.Storey);
                if (!WorldAddressing.TryResolve(anchor, out var address) || address.Key != key)
                    throw new InvalidOperationException($"Placement '{data.InstanceId}' is stored in the wrong page document.");
                var placement = new WorldPlacementRecord(instanceId, definitionId, data.Kind, anchor)
                {
                    OffsetX = data.OffsetX,
                    OffsetY = data.OffsetY,
                    VisualHeightOffset = data.VisualHeightOffset,
                    YawDegrees = data.YawDegrees,
                    Enabled = data.Enabled
                };
                result.Add(placement);
            }
            return result;
        }
    }
}
