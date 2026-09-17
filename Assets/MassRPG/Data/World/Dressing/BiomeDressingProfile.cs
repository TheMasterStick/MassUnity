using System;
using System.Collections.Generic;
using MassRPG.Core.Content;
using MassRPG.Data.World.Placements;
using MassRPG.Data.World.Semantics;

namespace MassRPG.Data.World.Dressing
{
    public enum DressingOccupancyChannel
    {
        Major,
        Minor,
        GroundDetail
    }

    public sealed class BiomeDressingEntry
    {
        private double _densityPerTile;
        private int _minimumSpacingTiles;

        public BiomeDressingEntry(
            ContentId id,
            ContentId definitionId,
            WorldPlacementKind placementKind,
            double densityPerTile,
            int minimumSpacingTiles = 0,
            DressingOccupancyChannel channel = DressingOccupancyChannel.Major,
            int priority = 0,
            uint seedSalt = 0)
        {
            if (id.IsEmpty) throw new ArgumentException("Dressing entry id cannot be empty.", nameof(id));
            if (definitionId.IsEmpty) throw new ArgumentException("Dressing definition id cannot be empty.", nameof(definitionId));
            Id = id;
            DefinitionId = definitionId;
            PlacementKind = placementKind;
            DensityPerTile = densityPerTile;
            MinimumSpacingTiles = minimumSpacingTiles;
            Channel = channel;
            Priority = priority;
            SeedSalt = seedSalt;
        }

        public ContentId Id { get; }
        public ContentId DefinitionId { get; set; }
        public WorldPlacementKind PlacementKind { get; set; }
        public double DensityPerTile
        {
            get => _densityPerTile;
            set
            {
                if (value < 0.0 || value > 1.0) throw new ArgumentOutOfRangeException(nameof(value));
                _densityPerTile = value;
            }
        }

        public int MinimumSpacingTiles
        {
            get => _minimumSpacingTiles;
            set
            {
                if (value < 0 || value > 64) throw new ArgumentOutOfRangeException(nameof(value));
                _minimumSpacingTiles = value;
            }
        }

        public DressingOccupancyChannel Channel { get; set; }
        public int Priority { get; set; }
        public uint SeedSalt { get; set; }
    }

    /// <summary>
    /// Deterministic bulk dressing for one semantic biome area. Same profile + seed + authored area
    /// produces the same untouched scenery after reload without storing one DB row per normal tree.
    /// </summary>
    public sealed class BiomeDressingProfile
    {
        private readonly List<BiomeDressingEntry> _entries = new List<BiomeDressingEntry>();
        private readonly List<BiomeDressingDensityOverride> _densityOverrides = new List<BiomeDressingDensityOverride>();

        public BiomeDressingProfile(ContentId id, ContentId biomeAreaId, uint seed)
        {
            if (id.IsEmpty) throw new ArgumentException("Dressing profile id cannot be empty.", nameof(id));
            if (biomeAreaId.IsEmpty) throw new ArgumentException("Biome area id cannot be empty.", nameof(biomeAreaId));
            Id = id;
            BiomeAreaId = biomeAreaId;
            Seed = seed;
        }

        public ContentId Id { get; }
        public ContentId BiomeAreaId { get; }
        public uint Seed { get; set; }
        public IReadOnlyList<BiomeDressingEntry> Entries => _entries;
        public IReadOnlyList<BiomeDressingDensityOverride> DensityOverrides => _densityOverrides;

        public void AddEntry(BiomeDressingEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            for (var i = 0; i < _entries.Count; i++)
                if (_entries[i].Id == entry.Id) throw new InvalidOperationException($"Duplicate dressing entry id '{entry.Id}'.");
            _entries.Add(entry);
            _entries.Sort(CompareEntries);
        }

        public bool RemoveEntry(ContentId entryId)
        {
            for (var i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Id != entryId) continue;
                _entries.RemoveAt(i);
                for (var j = _densityOverrides.Count - 1; j >= 0; j--)
                    if (_densityOverrides[j].TargetEntryId.HasValue && _densityOverrides[j].TargetEntryId.Value == entryId)
                        _densityOverrides.RemoveAt(j);
                return true;
            }
            return false;
        }

        public void AddDensityOverride(BiomeDressingDensityOverride densityOverride)
        {
            if (densityOverride == null) throw new ArgumentNullException(nameof(densityOverride));
            for (var i = 0; i < _densityOverrides.Count; i++)
                if (_densityOverrides[i].Id == densityOverride.Id)
                    throw new InvalidOperationException($"Duplicate dressing override id '{densityOverride.Id}'.");
            if (densityOverride.TargetEntryId.HasValue && !ContainsEntry(densityOverride.TargetEntryId.Value))
                throw new InvalidOperationException($"Dressing override targets unknown entry '{densityOverride.TargetEntryId.Value}'.");
            _densityOverrides.Add(densityOverride);
        }

        public bool RemoveDensityOverride(ContentId overrideId)
        {
            for (var i = 0; i < _densityOverrides.Count; i++)
            {
                if (_densityOverrides[i].Id != overrideId) continue;
                _densityOverrides.RemoveAt(i);
                return true;
            }
            return false;
        }

        public bool ContainsEntry(ContentId entryId)
        {
            for (var i = 0; i < _entries.Count; i++)
                if (_entries[i].Id == entryId) return true;
            return false;
        }

        private static int CompareEntries(BiomeDressingEntry a, BiomeDressingEntry b)
        {
            var priority = b.Priority.CompareTo(a.Priority);
            return priority != 0 ? priority : a.Id.CompareTo(b.Id);
        }
    }

    public sealed class BiomeDressingDensityOverride
    {
        private double _densityMultiplier;

        public BiomeDressingDensityOverride(
            ContentId id,
            WorldAreaShape shape,
            double densityMultiplier,
            ContentId? targetEntryId = null)
        {
            if (id.IsEmpty) throw new ArgumentException("Override id cannot be empty.", nameof(id));
            if (shape == null) throw new ArgumentNullException(nameof(shape));
            Id = id;
            Shape = shape;
            DensityMultiplier = densityMultiplier;
            TargetEntryId = targetEntryId;
        }

        public ContentId Id { get; }
        public WorldAreaShape Shape { get; set; }
        public double DensityMultiplier
        {
            get => _densityMultiplier;
            set
            {
                if (value < 0.0 || value > 8.0) throw new ArgumentOutOfRangeException(nameof(value));
                _densityMultiplier = value;
            }
        }
        public ContentId? TargetEntryId { get; set; }
    }
}
