using MassRPG.Core.Content;
using MassRPG.Core.World;
using MassRPG.Data.Creatures;
using MassRPG.Data.World.Semantics;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class CreatureSpawnRegionDocumentTests
    {
        [Test]
        public void SpawnRegionDocument_RoundTripsPolygonPopulationAndPatrolRoute()
        {
            var region = new CreatureSpawnRegionDefinition(
                new ContentId("spawn.wolves.north_wood"),
                new ContentId("creature.wolf"),
                new PolygonAreaShape(new[]
                {
                    new GridCoord(100, 100), new GridCoord(140, 100),
                    new GridCoord(145, 135), new GridCoord(105, 140)
                }),
                WorldConstants.SurfacePlane,
                0,
                12,
                45000,
                CreatureRoamingMode.PatrolRoute,
                new[]
                {
                    new GridCoord(110, 110), new GridCoord(130, 112), new GridCoord(128, 130)
                });

            var copy = CreatureSpawnRegionDocumentCodec.Decode(CreatureSpawnRegionDocumentCodec.Encode(region));

            Assert.AreEqual(region.Id, copy.Id);
            Assert.AreEqual(region.CreatureDefinitionId, copy.CreatureDefinitionId);
            Assert.AreEqual(12, copy.PopulationCap);
            Assert.AreEqual(45000, copy.RespawnIntervalMilliseconds);
            Assert.AreEqual(CreatureRoamingMode.PatrolRoute, copy.RoamingMode);
            Assert.AreEqual(3, copy.PatrolRoute.Count);
            Assert.AreEqual(new GridCoord(128, 130), copy.PatrolRoute[2]);
            Assert.IsTrue(copy.Area.Contains(new GridCoord(120, 120)));
        }

        [Test]
        public void PatrolRouteMode_RequiresAtLeastTwoRoutePoints()
        {
            Assert.Throws<System.ArgumentException>(() => new CreatureSpawnRegionDefinition(
                new ContentId("spawn.invalid"),
                new ContentId("creature.wolf"),
                new CircleAreaShape(new GridCoord(20, 20), 5),
                WorldConstants.SurfacePlane,
                0,
                4,
                30000,
                CreatureRoamingMode.PatrolRoute,
                new[] { new GridCoord(20, 20) }));
        }
    }
}
