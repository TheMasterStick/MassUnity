using System;
using System.Collections.Generic;
using System.Linq;
using MassRPG.Core.Characters;
using MassRPG.Core.Combat;
using MassRPG.Core.Content;
using MassRPG.Core.World;
using MassRPG.Data.Creatures;
using MassRPG.Data.Items;
using MassRPG.Data.Loot;
using MassRPG.Data.World.Semantics;
using MassRPG.Server.Combat;
using MassRPG.Server.Creatures;
using MassRPG.Server.Items;
using MassRPG.Server.Loot;
using MassRPG.Server.Parties;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class CombatKillSettlementCoordinatorTests
    {
        [Test]
        public void SettlementReadsDeadCreatureThenPopulationRemovesIt()
        {
            var definitions = MigrationSeedCreatureCatalog.Create();
            var registry = new CreatureRegistry();
            var population = new CreaturePopulationService(
                registry,
                definitions,
                new FixedPlacementSource(Loc(20, 20)));
            var region = new CreatureSpawnRegionDefinition(
                new ContentId("spawn.coordinator_goblin"),
                new ContentId("goblin"),
                new CircleAreaShape(new GridCoord(20, 20), 2),
                WorldConstants.SurfacePlane,
                0,
                1,
                5000);
            population.RegisterRegion(region);
            var populationState = population.Activate(region.Id, 0);
            var creatureId = populationState.MaterializedInstances.Single();
            Assert.IsTrue(registry.TryGet(creatureId, out var creature));

            var items = MigrationSeedItemCatalog.Create();
            var contributions = new CombatContributionLedger();
            var parties = new PartyRegistry();
            var planner = new CombatRewardPlanner(
                parties,
                new ContributionEligibilityPolicy(minimumDamage: 1, minimumDamageFraction: 0.0));
            var partyLoot = new PartyLootPoolService();
            var groundRegistry = new GroundItemRegistry();
            var settlement = new CombatKillSettlementService(
                definitions,
                MigrationSeedLootTableCatalog.Create(),
                items,
                contributions,
                planner,
                new CombatExperienceSettlementService(),
                parties,
                partyLoot,
                new GroundItemService(items, groundRegistry),
                lootPublicDelayMilliseconds: 2000,
                lootLifetimeMilliseconds: 4000);
            var coordinator = new CombatKillSettlementCoordinator(
                registry,
                settlement,
                new DistanceCombatRewardPresenceSource(definitions, rewardRangeTiles: 5),
                population);
            var player = new PlayerState(Guid.NewGuid(), "Finisher") { Location = Loc(20, 20) };
            contributions.Record(new CombatContribution(creatureId, player.CharacterId, 20, 100));
            creature.CurrentHitpoints = 0;

            var result = coordinator.SettleKilledCreature(
                creatureId,
                new[] { player },
                1000,
                Sequence(0.0, 0.9, 0.0, 0.5));

            Assert.IsTrue(result.Success);
            Assert.Greater(result.Experience.Awards.Count, 0);
            Assert.Greater(result.SpawnedGroundItemIds.Count, 0,
                "Loot must be spawned while the dead creature location is still available.");
            Assert.IsFalse(registry.TryGet(creatureId, out _),
                "Ordinary population removal should happen after settlement.");
            Assert.AreEqual(0, populationState.Population);
            Assert.AreEqual(0, populationState.MaterializedInstances.Count);
            Assert.IsFalse(contributions.TrySnapshot(creatureId, out _));
        }

        [Test]
        public void MissingCreatureReturnsFailureWithoutThrowing()
        {
            var definitions = MigrationSeedCreatureCatalog.Create();
            var registry = new CreatureRegistry();
            var items = MigrationSeedItemCatalog.Create();
            var contributions = new CombatContributionLedger();
            var parties = new PartyRegistry();
            var settlement = new CombatKillSettlementService(
                definitions,
                MigrationSeedLootTableCatalog.Create(),
                items,
                contributions,
                new CombatRewardPlanner(parties, new ContributionEligibilityPolicy(1, 0.0)),
                new CombatExperienceSettlementService(),
                parties,
                new PartyLootPoolService(),
                new GroundItemService(items, new GroundItemRegistry()),
                0,
                0);
            var coordinator = new CombatKillSettlementCoordinator(
                registry,
                settlement,
                new DistanceCombatRewardPresenceSource(definitions, 5));

            var result = coordinator.SettleKilledCreature(Guid.NewGuid(), Array.Empty<PlayerState>(), 0, () => 0.0);

            Assert.IsFalse(result.Success);
            Assert.AreEqual("creature_missing_before_settlement", result.Code);
        }

        private static Func<double> Sequence(params double[] values)
        {
            var index = 0;
            return () => values[Math.Min(index++, values.Length - 1)];
        }

        private static GridLocation Loc(int x, int y)
            => new GridLocation(new GridCoord(x, y), WorldConstants.SurfacePlane, 0);

        private sealed class FixedPlacementSource : ICreatureSpawnPlacementSource
        {
            private readonly List<GridLocation> _locations;

            public FixedPlacementSource(params GridLocation[] locations)
                => _locations = new List<GridLocation>(locations);

            public bool TryChooseSpawnLocation(
                CreatureSpawnRegionDefinition region,
                int materializationOrdinal,
                out GridLocation location)
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
