using System.Linq;
using MassRPG.Core.Content;
using MassRPG.Core.World;
using MassRPG.Data.World.Dressing;
using MassRPG.Data.World.Placements;
using MassRPG.Data.World.Semantics;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class BiomeDressingTests
    {
        [Test]
        public void SameProfileAndSeed_ProducesIdenticalPlacements()
        {
            var profile = Profile();
            var shape = new CircleAreaShape(new GridCoord(100, 100), 30);
            var bounds = new DressingTileBounds(80, 80, 120, 120);

            var first = BiomeDressingGenerator.Generate(profile, shape, bounds, WorldConstants.SurfacePlane, 0);
            var second = BiomeDressingGenerator.Generate(profile, shape, bounds, WorldConstants.SurfacePlane, 0);

            Assert.AreEqual(first.Count, second.Count);
            for (var i = 0; i < first.Count; i++)
            {
                Assert.AreEqual(first[i].Key, second[i].Key);
                Assert.AreEqual(first[i].DefinitionId, second[i].DefinitionId);
                Assert.AreEqual(first[i].OffsetX, second[i].OffsetX);
                Assert.AreEqual(first[i].OffsetY, second[i].OffsetY);
                Assert.AreEqual(first[i].YawDegrees, second[i].YawDegrees);
            }
        }

        [Test]
        public void ExclusionOverride_RemovesGeneratedDressingInsideShape()
        {
            var profile = Profile();
            var clearing = new CircleAreaShape(new GridCoord(100, 100), 8);
            profile.AddDensityOverride(new BiomeDressingDensityOverride(
                new ContentId("override.clearing"),
                clearing,
                0.0));

            var placements = BiomeDressingGenerator.Generate(
                profile,
                new CircleAreaShape(new GridCoord(100, 100), 30),
                new DressingTileBounds(70, 70, 130, 130),
                WorldConstants.SurfacePlane,
                0);

            Assert.IsFalse(placements.Any(p => clearing.Contains(p.Key.Anchor.Tile)));
        }

        [Test]
        public void SuppressingOneGeneratedPlacement_DoesNotPersistOrRemoveItsNeighbours()
        {
            var profile = Profile();
            var shape = new CircleAreaShape(new GridCoord(100, 100), 30);
            var bounds = new DressingTileBounds(80, 80, 120, 120);
            var original = BiomeDressingGenerator.Generate(profile, shape, bounds, WorldConstants.SurfacePlane, 0);
            Assert.Greater(original.Count, 0);

            var exceptions = new BiomeDressingExceptionSet();
            exceptions.Suppress(original[0].Key);
            var after = BiomeDressingGenerator.Generate(profile, shape, bounds, WorldConstants.SurfacePlane, 0, exceptions);

            Assert.AreEqual(original.Count - 1, after.Count);
            Assert.IsFalse(after.Any(p => p.Key.Equals(original[0].Key)));
        }

        [Test]
        public void AdjacentPageQueries_AgreeWithCombinedQueryAtBoundary()
        {
            var profile = Profile();
            var shape = new CircleAreaShape(new GridCoord(64, 32), 60);
            var left = BiomeDressingGenerator.Generate(
                profile, shape, new DressingTileBounds(0, 0, 63, 63), WorldConstants.SurfacePlane, 0);
            var right = BiomeDressingGenerator.Generate(
                profile, shape, new DressingTileBounds(64, 0, 127, 63), WorldConstants.SurfacePlane, 0);
            var combined = BiomeDressingGenerator.Generate(
                profile, shape, new DressingTileBounds(0, 0, 127, 63), WorldConstants.SurfacePlane, 0);

            var splitKeys = left.Concat(right).Select(p => p.Key)
                .OrderBy(k => k.Anchor.Tile.Y).ThenBy(k => k.Anchor.Tile.X).ThenBy(k => k.EntryId.Value).ToArray();
            var combinedKeys = combined.Select(p => p.Key)
                .OrderBy(k => k.Anchor.Tile.Y).ThenBy(k => k.Anchor.Tile.X).ThenBy(k => k.EntryId.Value).ToArray();
            CollectionAssert.AreEqual(combinedKeys, splitKeys);
        }

        [Test]
        public void ProfileDocument_RoundTripsEntriesAndDensityOverrides()
        {
            var profile = Profile();
            profile.AddDensityOverride(new BiomeDressingDensityOverride(
                new ContentId("override.sparse_edge"),
                new PolygonAreaShape(new[]
                {
                    new GridCoord(10, 10), new GridCoord(20, 10), new GridCoord(20, 20), new GridCoord(10, 20)
                }),
                0.35,
                new ContentId("entry.oak")));

            var copy = BiomeDressingProfileDocumentCodec.Decode(BiomeDressingProfileDocumentCodec.Encode(profile));

            Assert.AreEqual(profile.Id, copy.Id);
            Assert.AreEqual(profile.BiomeAreaId, copy.BiomeAreaId);
            Assert.AreEqual(profile.Seed, copy.Seed);
            Assert.AreEqual(2, copy.Entries.Count);
            Assert.AreEqual(1, copy.DensityOverrides.Count);
            Assert.AreEqual(new ContentId("entry.oak"), copy.DensityOverrides[0].TargetEntryId.Value);
            Assert.AreEqual(0.35, copy.DensityOverrides[0].DensityMultiplier, 0.0001);
            Assert.IsInstanceOf<PolygonAreaShape>(copy.DensityOverrides[0].Shape);
        }

        [Test]
        public void ExceptionDocument_RoundTripsOnlySparseSuppressionKeys()
        {
            var profileId = new ContentId("dressing.forest.test");
            var key = new BiomeDressingPlacementKey(
                profileId,
                new ContentId("entry.oak"),
                new GridLocation(new GridCoord(77, 88), WorldConstants.SurfacePlane, 0));
            var exceptions = new BiomeDressingExceptionSet();
            exceptions.Suppress(key);

            var copy = BiomeDressingExceptionDocumentCodec.Decode(
                BiomeDressingExceptionDocumentCodec.Encode(profileId, exceptions));

            Assert.AreEqual(1, copy.SuppressedCount);
            Assert.IsTrue(copy.IsSuppressed(key));
        }

        private static BiomeDressingProfile Profile()
        {
            var profile = new BiomeDressingProfile(
                new ContentId("dressing.forest.test"),
                new ContentId("biome.forest.test"),
                123456u);
            profile.AddEntry(new BiomeDressingEntry(
                new ContentId("entry.oak"),
                new ContentId("resource.tree.oak"),
                WorldPlacementKind.Resource,
                0.18,
                2,
                DressingOccupancyChannel.Major,
                10,
                7u));
            profile.AddEntry(new BiomeDressingEntry(
                new ContentId("entry.shrub"),
                new ContentId("doodad.shrub"),
                WorldPlacementKind.Doodad,
                0.30,
                0,
                DressingOccupancyChannel.Minor,
                0,
                13u));
            return profile;
        }
    }
}
