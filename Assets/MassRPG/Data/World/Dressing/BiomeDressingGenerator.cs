using System;
using System.Collections.Generic;
using MassRPG.Core.Content;
using MassRPG.Core.World;
using MassRPG.Data.World.Placements;
using MassRPG.Data.World.Semantics;

namespace MassRPG.Data.World.Dressing
{
    public readonly struct DressingTileBounds
    {
        public DressingTileBounds(int minX, int minY, int maxX, int maxY)
        {
            if (maxX < minX) throw new ArgumentOutOfRangeException(nameof(maxX));
            if (maxY < minY) throw new ArgumentOutOfRangeException(nameof(maxY));
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        public int MinX { get; }
        public int MinY { get; }
        public int MaxX { get; }
        public int MaxY { get; }

        public bool Contains(GridCoord tile)
            => tile.X >= MinX && tile.X <= MaxX && tile.Y >= MinY && tile.Y <= MaxY;

        public DressingTileBounds ExpandAndClamp(int tiles)
        {
            if (tiles < 0) throw new ArgumentOutOfRangeException(nameof(tiles));
            return new DressingTileBounds(
                Math.Max(0, MinX - tiles),
                Math.Max(0, MinY - tiles),
                Math.Min(WorldConstants.WorldWidthTiles - 1, MaxX + tiles),
                Math.Min(WorldConstants.WorldHeightTiles - 1, MaxY + tiles));
        }

        public static DressingTileBounds FromShape(WorldAreaShape shape)
        {
            if (shape == null) throw new ArgumentNullException(nameof(shape));

            var circle = shape as CircleAreaShape;
            if (circle != null)
            {
                return new DressingTileBounds(
                    Math.Max(0, circle.Center.X - circle.RadiusTiles),
                    Math.Max(0, circle.Center.Y - circle.RadiusTiles),
                    Math.Min(WorldConstants.WorldWidthTiles - 1, circle.Center.X + circle.RadiusTiles),
                    Math.Min(WorldConstants.WorldHeightTiles - 1, circle.Center.Y + circle.RadiusTiles));
            }

            var polygon = shape as PolygonAreaShape;
            if (polygon != null)
            {
                var minX = int.MaxValue;
                var minY = int.MaxValue;
                var maxX = int.MinValue;
                var maxY = int.MinValue;
                for (var i = 0; i < polygon.Points.Count; i++)
                {
                    var point = polygon.Points[i];
                    minX = Math.Min(minX, point.X);
                    minY = Math.Min(minY, point.Y);
                    maxX = Math.Max(maxX, point.X);
                    maxY = Math.Max(maxY, point.Y);
                }
                return new DressingTileBounds(
                    Math.Max(0, minX),
                    Math.Max(0, minY),
                    Math.Min(WorldConstants.WorldWidthTiles - 1, maxX),
                    Math.Min(WorldConstants.WorldHeightTiles - 1, maxY));
            }

            throw new NotSupportedException($"Dressing bounds are not implemented for '{shape.GetType().Name}'.");
        }
    }

    public readonly struct BiomeDressingPlacementKey : IEquatable<BiomeDressingPlacementKey>
    {
        public BiomeDressingPlacementKey(ContentId profileId, ContentId entryId, GridLocation anchor)
        {
            if (profileId.IsEmpty) throw new ArgumentException("Profile id cannot be empty.", nameof(profileId));
            if (entryId.IsEmpty) throw new ArgumentException("Entry id cannot be empty.", nameof(entryId));
            ProfileId = profileId;
            EntryId = entryId;
            Anchor = anchor;
        }

        public ContentId ProfileId { get; }
        public ContentId EntryId { get; }
        public GridLocation Anchor { get; }

        public bool Equals(BiomeDressingPlacementKey other)
            => ProfileId == other.ProfileId && EntryId == other.EntryId && Anchor.Equals(other.Anchor);

        public override bool Equals(object obj)
            => obj is BiomeDressingPlacementKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = ProfileId.GetHashCode();
                hash = hash * 397 ^ EntryId.GetHashCode();
                hash = hash * 397 ^ Anchor.GetHashCode();
                return hash;
            }
        }
    }

    /// <summary>
    /// Sparse manual removals of deterministic dressing. The normal generated forest is derived
    /// from seed/profile data; only exceptions need persistence.
    /// </summary>
    public sealed class BiomeDressingExceptionSet
    {
        private readonly HashSet<BiomeDressingPlacementKey> _suppressed = new HashSet<BiomeDressingPlacementKey>();

        public int SuppressedCount => _suppressed.Count;
        public IEnumerable<BiomeDressingPlacementKey> Suppressed => _suppressed;
        public bool Suppress(BiomeDressingPlacementKey key) => _suppressed.Add(key);
        public bool Restore(BiomeDressingPlacementKey key) => _suppressed.Remove(key);
        public bool IsSuppressed(BiomeDressingPlacementKey key) => _suppressed.Contains(key);
    }

    public sealed class BiomeDressingPlacement
    {
        internal BiomeDressingPlacement(
            BiomeDressingPlacementKey key,
            ContentId definitionId,
            WorldPlacementKind kind,
            DressingOccupancyChannel channel,
            float offsetX,
            float offsetY,
            float yawDegrees)
        {
            Key = key;
            DefinitionId = definitionId;
            PlacementKind = kind;
            Channel = channel;
            OffsetX = offsetX;
            OffsetY = offsetY;
            YawDegrees = yawDegrees;
        }

        public BiomeDressingPlacementKey Key { get; }
        public ContentId DefinitionId { get; }
        public WorldPlacementKind PlacementKind { get; }
        public DressingOccupancyChannel Channel { get; }
        public float OffsetX { get; }
        public float OffsetY { get; }
        public float YawDegrees { get; }

        public WorldPlacementRecord ToTransientWorldPlacement()
        {
            var instanceId = new ContentId(
                "dressing/" + Key.ProfileId.Value + "/" + Key.EntryId.Value
                + "/p" + Key.Anchor.Plane + "/s" + Key.Anchor.Storey
                + "/x" + Key.Anchor.Tile.X + "/y" + Key.Anchor.Tile.Y);
            return new WorldPlacementRecord(instanceId, DefinitionId, PlacementKind, Key.Anchor)
            {
                OffsetX = OffsetX,
                OffsetY = OffsetY,
                YawDegrees = YawDegrees
            };
        }
    }

    /// <summary>
    /// Stateless deterministic biome dressing. The requested page/rectangle may be generated alone;
    /// a spacing-sized border is sampled so page boundaries do not alter results.
    /// </summary>
    public static class BiomeDressingGenerator
    {
        private sealed class RawCandidate
        {
            public BiomeDressingEntry Entry;
            public GridCoord Tile;
            public uint Rank;
            public uint VisualHash;
        }

        public static IReadOnlyList<BiomeDressingPlacement> Generate(
            BiomeDressingProfile profile,
            WorldAreaShape biomeShape,
            DressingTileBounds requestedBounds,
            int plane,
            int storey,
            BiomeDressingExceptionSet exceptions = null)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (biomeShape == null) throw new ArgumentNullException(nameof(biomeShape));

            var maxSpacing = 0;
            for (var i = 0; i < profile.Entries.Count; i++)
                maxSpacing = Math.Max(maxSpacing, profile.Entries[i].MinimumSpacingTiles);
            var scanBounds = requestedBounds.ExpandAndClamp(maxSpacing);

            var candidates = new List<RawCandidate>();
            var byEntry = new Dictionary<ContentId, Dictionary<GridCoord, RawCandidate>>();
            var byChannelAtTile = new Dictionary<DressingOccupancyChannel, Dictionary<GridCoord, List<RawCandidate>>>();

            for (var entryIndex = 0; entryIndex < profile.Entries.Count; entryIndex++)
            {
                var entry = profile.Entries[entryIndex];
                Dictionary<GridCoord, RawCandidate> entryCandidates;
                if (!byEntry.TryGetValue(entry.Id, out entryCandidates))
                {
                    entryCandidates = new Dictionary<GridCoord, RawCandidate>();
                    byEntry.Add(entry.Id, entryCandidates);
                }

                for (var y = scanBounds.MinY; y <= scanBounds.MaxY; y++)
                {
                    for (var x = scanBounds.MinX; x <= scanBounds.MaxX; x++)
                    {
                        var tile = new GridCoord(x, y);
                        if (!biomeShape.Contains(tile)) continue;
                        var density = EffectiveDensity(profile, entry, tile);
                        if (density <= 0.0) continue;

                        var chanceHash = StableHash(profile.Seed, entry.SeedSalt, entry.Id.Value, x, y, plane, storey, 0x6a09e667u);
                        var chance = chanceHash / (double)uint.MaxValue;
                        if (chance >= density) continue;

                        var candidate = new RawCandidate
                        {
                            Entry = entry,
                            Tile = tile,
                            Rank = StableHash(profile.Seed, entry.SeedSalt, entry.Id.Value, x, y, plane, storey, 0xbb67ae85u),
                            VisualHash = StableHash(profile.Seed, entry.SeedSalt, entry.DefinitionId.Value, x, y, plane, storey, 0x3c6ef372u)
                        };
                        candidates.Add(candidate);
                        entryCandidates[tile] = candidate;

                        Dictionary<GridCoord, List<RawCandidate>> channel;
                        if (!byChannelAtTile.TryGetValue(entry.Channel, out channel))
                        {
                            channel = new Dictionary<GridCoord, List<RawCandidate>>();
                            byChannelAtTile.Add(entry.Channel, channel);
                        }
                        List<RawCandidate> stack;
                        if (!channel.TryGetValue(tile, out stack))
                        {
                            stack = new List<RawCandidate>();
                            channel.Add(tile, stack);
                        }
                        stack.Add(candidate);
                    }
                }
            }

            var result = new List<BiomeDressingPlacement>();
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (!requestedBounds.Contains(candidate.Tile)) continue;
                if (!WinsOwnEntrySpacing(candidate, byEntry[candidate.Entry.Id])) continue;
                if (!WinsSameTileChannel(candidate, byChannelAtTile[candidate.Entry.Channel])) continue;

                var anchor = new GridLocation(candidate.Tile, plane, storey);
                var key = new BiomeDressingPlacementKey(profile.Id, candidate.Entry.Id, anchor);
                if (exceptions != null && exceptions.IsSuppressed(key)) continue;

                var hx = Mix(candidate.VisualHash ^ 0xa54ff53au);
                var hy = Mix(candidate.VisualHash ^ 0x510e527fu);
                var hr = Mix(candidate.VisualHash ^ 0x9b05688cu);
                result.Add(new BiomeDressingPlacement(
                    key,
                    candidate.Entry.DefinitionId,
                    candidate.Entry.PlacementKind,
                    candidate.Entry.Channel,
                    (float)((hx / (double)uint.MaxValue - 0.5) * 0.70),
                    (float)((hy / (double)uint.MaxValue - 0.5) * 0.70),
                    (float)(hr / (double)uint.MaxValue * 360.0)));
            }

            result.Sort((a, b) =>
            {
                var y = a.Key.Anchor.Tile.Y.CompareTo(b.Key.Anchor.Tile.Y);
                if (y != 0) return y;
                var x = a.Key.Anchor.Tile.X.CompareTo(b.Key.Anchor.Tile.X);
                if (x != 0) return x;
                return a.Key.EntryId.CompareTo(b.Key.EntryId);
            });
            return result;
        }

        private static bool WinsOwnEntrySpacing(RawCandidate candidate, Dictionary<GridCoord, RawCandidate> sameEntry)
        {
            var radius = candidate.Entry.MinimumSpacingTiles;
            if (radius <= 0) return true;

            for (var y = candidate.Tile.Y - radius; y <= candidate.Tile.Y + radius; y++)
            {
                for (var x = candidate.Tile.X - radius; x <= candidate.Tile.X + radius; x++)
                {
                    if (x == candidate.Tile.X && y == candidate.Tile.Y) continue;
                    RawCandidate other;
                    if (!sameEntry.TryGetValue(new GridCoord(x, y), out other)) continue;
                    if (other.Rank < candidate.Rank) return false;
                    if (other.Rank == candidate.Rank && CompareTile(other.Tile, candidate.Tile) < 0) return false;
                }
            }
            return true;
        }

        private static bool WinsSameTileChannel(
            RawCandidate candidate,
            Dictionary<GridCoord, List<RawCandidate>> channel)
        {
            List<RawCandidate> sameTile;
            if (!channel.TryGetValue(candidate.Tile, out sameTile) || sameTile.Count <= 1) return true;
            for (var i = 0; i < sameTile.Count; i++)
            {
                var other = sameTile[i];
                if (ReferenceEquals(other, candidate)) continue;
                if (ComparePrecedence(other, candidate) < 0) return false;
            }
            return true;
        }

        private static int ComparePrecedence(RawCandidate a, RawCandidate b)
        {
            var priority = b.Entry.Priority.CompareTo(a.Entry.Priority);
            if (priority != 0) return priority;
            var rank = a.Rank.CompareTo(b.Rank);
            if (rank != 0) return rank;
            return a.Entry.Id.CompareTo(b.Entry.Id);
        }

        private static int CompareTile(GridCoord a, GridCoord b)
        {
            var y = a.Y.CompareTo(b.Y);
            return y != 0 ? y : a.X.CompareTo(b.X);
        }

        private static double EffectiveDensity(BiomeDressingProfile profile, BiomeDressingEntry entry, GridCoord tile)
        {
            var density = entry.DensityPerTile;
            for (var i = 0; i < profile.DensityOverrides.Count; i++)
            {
                var item = profile.DensityOverrides[i];
                if (!item.Shape.Contains(tile)) continue;
                if (item.TargetEntryId.HasValue && item.TargetEntryId.Value != entry.Id) continue;
                density *= item.DensityMultiplier;
                if (density <= 0.0) return 0.0;
            }
            return Math.Min(1.0, density);
        }

        private static uint StableHash(
            uint seed,
            uint salt,
            string text,
            int x,
            int y,
            int plane,
            int storey,
            uint domain)
        {
            var hash = Mix(seed ^ salt ^ domain);
            for (var i = 0; i < text.Length; i++) hash = Mix(hash ^ text[i]);
            hash = Mix(hash ^ unchecked((uint)x));
            hash = Mix(hash ^ unchecked((uint)y));
            hash = Mix(hash ^ unchecked((uint)plane));
            hash = Mix(hash ^ unchecked((uint)storey));
            return hash;
        }

        private static uint Mix(uint value)
        {
            value ^= value >> 16;
            value *= 0x7feb352du;
            value ^= value >> 15;
            value *= 0x846ca68bu;
            value ^= value >> 16;
            return value;
        }
    }
}
