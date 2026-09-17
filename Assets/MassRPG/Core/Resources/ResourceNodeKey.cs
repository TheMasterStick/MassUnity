using System;
using MassRPG.Core.Content;
using MassRPG.Core.World;

namespace MassRPG.Core.Resources
{
    public enum ResourceAvailabilityMode
    {
        Personal,
        Shared
    }

    /// <summary>
    /// Stable logical identity for a harvestable node. Deterministic biome resources can derive
    /// this from resource id + grid location + local index instead of needing a database row while
    /// untouched. Only exceptional state (for example depletion) needs persistence.
    /// </summary>
    public readonly struct ResourceNodeKey : IEquatable<ResourceNodeKey>
    {
        public ResourceNodeKey(ContentId resourceId, GridLocation location, ushort localIndex = 0)
        {
            if (resourceId.IsEmpty) throw new ArgumentException("Resource id cannot be empty.", nameof(resourceId));
            ResourceId = resourceId;
            Location = location;
            LocalIndex = localIndex;
        }

        public ContentId ResourceId { get; }
        public GridLocation Location { get; }
        public ushort LocalIndex { get; }

        public bool Equals(ResourceNodeKey other)
            => ResourceId == other.ResourceId && Location == other.Location && LocalIndex == other.LocalIndex;
        public override bool Equals(object obj) => obj is ResourceNodeKey other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = ResourceId.GetHashCode();
                hash = hash * 397 ^ Location.GetHashCode();
                hash = hash * 397 ^ LocalIndex;
                return hash;
            }
        }

        public override string ToString() => $"{ResourceId}@{Location}#{LocalIndex}";
        public static bool operator ==(ResourceNodeKey left, ResourceNodeKey right) => left.Equals(right);
        public static bool operator !=(ResourceNodeKey left, ResourceNodeKey right) => !left.Equals(right);
    }
}
