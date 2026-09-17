using System;
using System.Collections.Generic;
using System.Linq;
using MassRPG.Core.Content;
using MassRPG.Core.World;
using MassRPG.Data.Creatures;
using MassRPG.Data.World.Semantics;
using MassRPG.Server.Creatures;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class CreaturePopulationTests
    {
        [Test]
        public void SleepingRegion_DoesNotResetAndRefillsGraduallyFromElapsedTime()
        {
            var definitions = MigrationSeedCreatureCatalog.Create();
            var registry = new CreatureRegistry();
            var placement = new FixedPlacementSource(Loc(10, 10), Loc(11, 10), Loc(12, 10));
            var service = new CreaturePopulationService(registry, definitions, placement);
            var region = new CreatureSpawnRegionDefinition(
                new ContentId("spawn.test_wolves"),
                new ContentId("wolf"),
                new CircleAreaShape(new GridCoord(11, 10), 4),
                WorldConstants.SurfacePlane,
                0,
                3,
                1000);
            service.RegisterRegion(region);

            var state = service.Activate(region.Id, 0);
            Assert.AreEqual(3, state.Population);
            Assert.AreEqual(3, state.MaterializedInstances.Count);

            var killed = state.MaterializedInstances.Take(2).ToArray();
            Assert.IsTrue(service.RecordKill(region.Id, killed[0], 0));
            Assert.IsTrue(service.RecordKill(region.Id, killed[1], 0));
            Assert.AreEqual(1, state.Population);
            Assert.AreEqual(1, state.MaterializedInstances.Count);

            service.Deactivate(region.Id);
            Assert.IsFalse(state.IsActive);
            Assert.AreEqual(1, state.Population);
            Assert.AreEqual(0, state.MaterializedInstances.Count);
            Assert.AreEqual(0, registry.Count);

            state = service.Activate(region.Id, 500);
            Assert.AreEqual(1, state.Population, "Returning quickly must not refill/reset the region.");
            Assert.AreEqual(1, state.MaterializedInstances.Count);

            service.Deactivate(region.Id);
            state = service.Activate(region.Id, 1500);
            Assert.AreEqual(2, state.Population, "Only one scheduled respawn should have matured by 1.5s.");

            service.Deactivate(region.Id);
            state = service.Activate(region.Id, 2500);
            Assert.AreEqual(3, state.Population);
            Assert.IsFalse(state.NextRespawnAtUnixMilliseconds.HasValue);
        }

        [Test]
        public void OrdinarySleep_RematerializesWithNewInstancesInsteadOfPreservingExactActors()
        {
            var definitions = MigrationSeedCreatureCatalog.Create();
            var registry = new CreatureRegistry();
            var service = new CreaturePopulationService(
                registry,
                definitions,
                new FixedPlacementSource(Loc(20, 20), Loc(21, 20)));
            var region = new CreatureSpawnRegionDefinition(
                new ContentId("spawn.test_cows"),
                new ContentId("cow"),
                new CircleAreaShape(new GridCoord(20, 20), 4),
                WorldConstants.SurfacePlane,
                0,
                2,
                5000);
            service.RegisterRegion(region);

            var state = service.Activate(region.Id, 0);
            var before = state.MaterializedInstances.ToArray();
            service.Deactivate(region.Id);
            state = service.Activate(region.Id, 10);
            var after = state.MaterializedInstances.ToArray();

            CollectionAssert.AreNotEquivalent(before, after);
            Assert.AreEqual(2, state.Population);
        }

        private static GridLocation Loc(int x, int y)
            => new GridLocation(new GridCoord(x, y), WorldConstants.SurfacePlane, 0);

        private sealed class FixedPlacementSource : ICreatureSpawnPlacementSource
        {
            private readonly List<GridLocation> _locations;
            public FixedPlacementSource(params GridLocation[] locations)
                => _locations = new List<GridLocation>(locations);

            public bool TryChooseSpawnLocation(CreatureSpawnRegionDefinition region, int materializationOrdinal, out GridLocation location)
            {
                if (_locations.Count == 0)
                {
                    location = default;
                    return false;
                }

                location = _locations[materializationOrdinal % _locations.Count];
                return true;
            }
        }
    }
}
