using System;
using System.Collections.Generic;
using MassRPG.Core.Content;
using MassRPG.Core.World;

namespace MassRPG.Data.World.Semantics
{
    public enum PointOfInterestKind
    {
        Capital,
        Town,
        Village,
        Settlement,
        DungeonEntrance,
        Mine,
        Ruins,
        Shrine,
        Dock,
        Bridge,
        Landmark,
        Transport,
        Custom
    }

    public enum MapMarkerCategory
    {
        Settlement,
        Service,
        Dungeon,
        Transport,
        Landmark,
        Quest,
        Housing,
        Custom
    }

    /// <summary>
    /// Authored location shown on the public map by default. Visible footprint and protected/no-build
    /// footprint are separate so a POI can be visually small but reserve a precise larger corridor.
    /// </summary>
    public sealed class PointOfInterestDefinition
    {
        private readonly List<ContentId> _serviceTags = new List<ContentId>();

        public PointOfInterestDefinition(
            ContentId id,
            string displayName,
            PointOfInterestKind kind,
            GridLocation center,
            MapMarkerCategory markerCategory = MapMarkerCategory.Landmark,
            PlayerMapVisibility mapVisibility = PlayerMapVisibility.Public)
        {
            if (id.IsEmpty) throw new ArgumentException("POI id cannot be empty.", nameof(id));
            Id = id;
            DisplayName = displayName ?? string.Empty;
            Kind = kind;
            Center = center;
            MarkerCategory = markerCategory;
            MapVisibility = mapVisibility;
        }

        public ContentId Id { get; }
        public string DisplayName { get; set; }
        public PointOfInterestKind Kind { get; set; }
        public GridLocation Center { get; set; }
        public MapMarkerCategory MarkerCategory { get; set; }
        public PlayerMapVisibility MapVisibility { get; set; }
        public WorldAreaShape VisibleFootprint { get; set; }
        public WorldAreaShape ProtectionFootprint { get; set; }
        public IReadOnlyList<ContentId> ServiceTags => _serviceTags;

        public void AddServiceTag(ContentId serviceId)
        {
            if (serviceId.IsEmpty) throw new ArgumentException("Service id cannot be empty.", nameof(serviceId));
            if (!_serviceTags.Contains(serviceId)) _serviceTags.Add(serviceId);
        }
    }
}
