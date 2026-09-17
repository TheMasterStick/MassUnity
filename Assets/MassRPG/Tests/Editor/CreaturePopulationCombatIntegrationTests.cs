using System;
using System.Linq;
using MassRPG.Core.Authority;
using MassRPG.Core.Characters;
using MassRPG.Core.Combat;
using MassRPG.Core.Content;
using MassRPG.Core.Skills;
using MassRPG.Core.World;
using MassRPG.Data.Creatures;
using MassRPG.Data.Items;
using MassRPG.Data.World;
using MassRPG.Data.World.Semantics;
using MassRPG.Server.Authority;
using MassRPG.Server.Combat;
using MassRPG.Server.Creatures;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class CreaturePopulationCombatIntegrationTests
    {
        private static readonly ContentId Grass = new ContentId("terrain.grass");

        [Test]
        public void KillingOrdinaryPopulationCreature_DecrementsPopulationAndStartsRespawn()
        {
            var map = new AuthoredWorldPageStore(Grass, 32);
            map.GetOrCreatePage(Loc(0, 0));
            var definitions = MigrationSeedCreatureCatalog.Create();
            var creatures = new CreatureRegistry();
            var occupancy = new CreatureOccupancyIndex();
            var placement = new SinglePlacement(Loc(11, 10));
            var populations = new CreaturePopulationService(creatures, definitions, placement, occupancy);
            var region = new CreatureSpawnRegionDefinition(
                new ContentId("spawn.test_chicken"),
                new ContentId("chicken"),
                new CircleAreaShape(new GridCoord(11, 10), 2),
                WorldConstants.SurfacePlane,
                0,
                1,
                5000);
            populations.RegisterRegion(region);
            var population = populations.Activate(region.Id, 0);
            var creatureId = population.MaterializedInstances.Single();

            var items = MigrationSeedItemCatalog.Create();
            var profiles = new FixedProfileSource(new PlayerAttackProfile(CombatStyle.Melee, 1, 2400, 200, 200, 0, 0, 0, 0));
            var approach = new CombatApproachPlanner(map, map);
            var targeting = new CombatTargetingService(creatures, definitions, profiles, approach);
            var combat = new CombatSimulationService(creatures, definitions, profiles, approach, map);
            var authority = new LocalGameAuthority(
                items,
                map,
                combatTargeting: targeting,
                combatSimulation: combat,
                creatures: creatures,
                creaturePopulations: populations);
            var player = new PlayerState(Guid.NewGuid(), "Hunter") { Location = Loc(10, 10) };
            player.Skills.SetXp(SkillId.Attack, SkillProgression.XpForLevel(99));
            player.Skills.SetXp(SkillId.Strength, SkillProgression.XpForLevel(99));
            authority.RegisterPlayer(player);

            Assert.IsTrue(authority.Submit(new AttackCreatureRequest(Guid.NewGuid(), player.CharacterId, creatureId), 1000).Accepted);
            var result = authority.AdvanceCombat(player.CharacterId, 1000, Sequence(0.0, 0.999));

            Assert.AreEqual(CombatAdvanceKind.TargetKilled, result.Kind);
            Assert.AreEqual(0, population.Population);
            Assert.AreEqual(0, population.MaterializedInstances.Count);
            Assert.IsTrue(population.NextRespawnAtUnixMilliseconds.HasValue);
            Assert.AreEqual(6000, population.NextRespawnAtUnixMilliseconds.Value);
            Assert.IsFalse(creatures.TryGet(creatureId, out _));

            populations.Advance(5999);
            Assert.AreEqual(0, population.Population);
            populations.Advance(6000);
            Assert.AreEqual(1, population.Population);
            Assert.AreEqual(1, population.MaterializedInstances.Count);
        }

        private static Func<double> Sequence(params double[] values)
        {
            var index = 0;
            return () => values[Math.Min(index++, values.Length - 1)];
        }

        private static GridLocation Loc(int x, int y)
            => new GridLocation(new GridCoord(x, y), WorldConstants.SurfacePlane, 0);

        private sealed class FixedProfileSource : IPlayerAttackProfileSource
        {
            private readonly PlayerAttackProfile _profile;
            public FixedProfileSource(PlayerAttackProfile profile) { _profile = profile; }
            public PlayerAttackProfile Resolve(PlayerState player) => _profile;
        }

        private sealed class SinglePlacement : ICreatureSpawnPlacementSource
        {
            private readonly GridLocation _location;
            public SinglePlacement(GridLocation location) { _location = location; }
            public bool TryChooseSpawnLocation(CreatureSpawnRegionDefinition region, int materializationOrdinal, out GridLocation location)
            {
                location = _location;
                return true;
            }
        }
    }
}
