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
using MassRPG.Data.Loot;
using MassRPG.Data.World;
using MassRPG.Data.World.Semantics;
using MassRPG.Server.Authority;
using MassRPG.Server.Combat;
using MassRPG.Server.Creatures;
using MassRPG.Server.Items;
using MassRPG.Server.Loot;
using MassRPG.Server.Parties;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class LocalAuthorityKillSettlementIntegrationTests
    {
        private static readonly ContentId Grass = new ContentId("terrain.grass");

        [Test]
        public void LethalAuthorityCombatSettlesRewardsBeforePopulationRemovalAndRaisesObservationEvent()
        {
            var map = new AuthoredWorldPageStore(Grass, 32);
            map.GetOrCreatePage(Loc(0, 0));
            var definitions = MigrationSeedCreatureCatalog.Create();
            var creatures = new CreatureRegistry();
            var occupancy = new CreatureOccupancyIndex();
            var populations = new CreaturePopulationService(
                creatures,
                definitions,
                new SinglePlacement(Loc(11, 10)),
                occupancy);
            var region = new CreatureSpawnRegionDefinition(
                new ContentId("spawn.authority_settlement_chicken"),
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
            var profiles = new FixedProfileSource(new PlayerAttackProfile(
                CombatStyle.Melee,
                1,
                2400,
                200,
                200,
                0,
                0,
                0,
                0));
            var approach = new CombatApproachPlanner(map, map);
            var targeting = new CombatTargetingService(creatures, definitions, profiles, approach);
            var contributions = new CombatContributionLedger();
            var combat = new CombatSimulationService(
                creatures,
                definitions,
                profiles,
                approach,
                map,
                contributions);

            var parties = new PartyRegistry();
            var groundRegistry = new GroundItemRegistry();
            var killService = new CombatKillSettlementService(
                definitions,
                MigrationSeedLootTableCatalog.Create(),
                items,
                contributions,
                new CombatRewardPlanner(parties, new ContributionEligibilityPolicy(1, 0.0)),
                new CombatExperienceSettlementService(),
                parties,
                new PartyLootPoolService(),
                new GroundItemService(items, groundRegistry),
                lootPublicDelayMilliseconds: 2000,
                lootLifetimeMilliseconds: 4000);
            var killCoordinator = new CombatKillSettlementCoordinator(
                creatures,
                killService,
                new DistanceCombatRewardPresenceSource(definitions, rewardRangeTiles: 5),
                populations);

            var authority = new LocalGameAuthority(
                items,
                map,
                combatTargeting: targeting,
                combatSimulation: combat,
                creatures: creatures,
                creaturePopulations: populations,
                killSettlement: killCoordinator);
            var player = new PlayerState(Guid.NewGuid(), "Settler") { Location = Loc(10, 10) };
            player.Skills.SetXp(SkillId.Attack, SkillProgression.XpForLevel(99));
            player.Skills.SetXp(SkillId.Strength, SkillProgression.XpForLevel(99));
            authority.RegisterPlayer(player);

            var eventCount = 0;
            var eventCreatureId = Guid.Empty;
            CombatKillSettlementResult observed = default;
            authority.CombatKillSettled += (id, settlement) =>
            {
                eventCount++;
                eventCreatureId = id;
                observed = settlement;
            };

            Assert.IsTrue(authority.Submit(
                new AttackCreatureRequest(Guid.NewGuid(), player.CharacterId, creatureId),
                1000).Accepted);
            var attack = authority.AdvanceCombat(player.CharacterId, 1000, Sequence(0.0, 0.999, 0.0, 0.5));

            Assert.AreEqual(CombatAdvanceKind.TargetKilled, attack.Kind);
            Assert.AreEqual(1, eventCount);
            Assert.AreEqual(creatureId, eventCreatureId);
            Assert.IsTrue(observed.Success);
            Assert.Greater(observed.Experience.Awards.Count, 0);
            Assert.IsFalse(contributions.TrySnapshot(creatureId, out _));
            Assert.IsFalse(creatures.TryGet(creatureId, out _));
            Assert.AreEqual(0, population.Population);
            Assert.AreEqual(0, population.MaterializedInstances.Count);
            Assert.AreEqual(6000, population.NextRespawnAtUnixMilliseconds.Value);
        }

        [Test]
        public void AuthorityWithoutSettlementCoordinatorKeepsLegacyPopulationCleanupBehavior()
        {
            var map = new AuthoredWorldPageStore(Grass, 32);
            map.GetOrCreatePage(Loc(0, 0));
            var definitions = MigrationSeedCreatureCatalog.Create();
            var creatures = new CreatureRegistry();
            var populations = new CreaturePopulationService(
                creatures,
                definitions,
                new SinglePlacement(Loc(11, 10)));
            var region = new CreatureSpawnRegionDefinition(
                new ContentId("spawn.legacy_cleanup_chicken"),
                new ContentId("chicken"),
                new CircleAreaShape(new GridCoord(11, 10), 2),
                WorldConstants.SurfacePlane,
                0,
                1,
                5000);
            populations.RegisterRegion(region);
            var population = populations.Activate(region.Id, 0);
            var creatureId = population.MaterializedInstances.Single();
            var profiles = new FixedProfileSource(new PlayerAttackProfile(
                CombatStyle.Melee, 1, 2400, 200, 200, 0, 0, 0, 0));
            var approach = new CombatApproachPlanner(map, map);
            var targeting = new CombatTargetingService(creatures, definitions, profiles, approach);
            var combat = new CombatSimulationService(creatures, definitions, profiles, approach, map);
            var authority = new LocalGameAuthority(
                MigrationSeedItemCatalog.Create(),
                map,
                combatTargeting: targeting,
                combatSimulation: combat,
                creatures: creatures,
                creaturePopulations: populations);
            var player = new PlayerState(Guid.NewGuid(), "Legacy") { Location = Loc(10, 10) };
            player.Skills.SetXp(SkillId.Attack, SkillProgression.XpForLevel(99));
            player.Skills.SetXp(SkillId.Strength, SkillProgression.XpForLevel(99));
            authority.RegisterPlayer(player);

            Assert.IsTrue(authority.Submit(
                new AttackCreatureRequest(Guid.NewGuid(), player.CharacterId, creatureId),
                1000).Accepted);
            var result = authority.AdvanceCombat(player.CharacterId, 1000, Sequence(0.0, 0.999));

            Assert.AreEqual(CombatAdvanceKind.TargetKilled, result.Kind);
            Assert.IsFalse(creatures.TryGet(creatureId, out _));
            Assert.AreEqual(0, population.Population);
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
            public bool TryChooseSpawnLocation(
                CreatureSpawnRegionDefinition region,
                int materializationOrdinal,
                out GridLocation location)
            {
                location = _location;
                return true;
            }
        }
    }
}
