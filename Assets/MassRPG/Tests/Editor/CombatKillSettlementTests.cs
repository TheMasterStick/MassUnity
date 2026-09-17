using System;
using System.Collections.Generic;
using MassRPG.Core.Characters;
using MassRPG.Core.Combat;
using MassRPG.Core.Content;
using MassRPG.Core.World;
using MassRPG.Data.Creatures;
using MassRPG.Data.Items;
using MassRPG.Data.Loot;
using MassRPG.Server.Combat;
using MassRPG.Server.Creatures;
using MassRPG.Server.Items;
using MassRPG.Server.Loot;
using MassRPG.Server.Parties;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class CombatKillSettlementTests
    {
        [Test]
        public void FirstEngagerPartyGetsOneSharedPoolAndMoneySplitsEvenly()
        {
            var setup = CreateSetup();
            var leader = AddPlayer(setup, "Leader");
            var contributor = AddPlayer(setup, "Contributor");
            var nearby = AddPlayer(setup, "Nearby");
            var outsider = AddPlayer(setup, "Outsider");
            var party = setup.Parties.Create(Guid.NewGuid(), leader.CharacterId, PartyLootMode.RoundRobin);
            setup.Parties.AddMember(party.PartyId, contributor.CharacterId);
            setup.Parties.AddMember(party.PartyId, nearby.CharacterId);

            var creature = DeadGoblin(setup);
            setup.Contributions.Record(new CombatContribution(creature.InstanceId, leader.CharacterId, 40, 100));
            setup.Contributions.Record(new CombatContribution(creature.InstanceId, contributor.CharacterId, 20, 110));
            setup.Contributions.Record(new CombatContribution(creature.InstanceId, outsider.CharacterId, 40, 120));

            var result = setup.Service.Settle(
                creature,
                new[]
                {
                    new CombatRewardPresence(leader.CharacterId, true),
                    new CombatRewardPresence(contributor.CharacterId, true),
                    new CombatRewardPresence(nearby.CharacterId, true),
                    new CombatRewardPresence(outsider.CharacterId, true)
                },
                1000,
                Sequence(0.0, 0.9, 0.0, 0.5),
                id => setup.Players.TryGetValue(id, out var player) ? player : null);

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.PartyLootPoolId.HasValue);
            Assert.AreEqual(5, leader.Inventory.CountItem(new ContentId("coins")));
            Assert.AreEqual(5, contributor.Inventory.CountItem(new ContentId("coins")));
            Assert.AreEqual(5, nearby.Inventory.CountItem(new ContentId("coins")));
            Assert.AreEqual(0, outsider.Inventory.CountItem(new ContentId("coins")));
            Assert.AreEqual(0, result.SpawnedGroundItemIds.Count);

            Assert.IsTrue(setup.PartyLoot.TryGet(result.PartyLootPoolId.Value, out var pool));
            Assert.AreEqual(1, pool.Entries.Count);
            Assert.AreEqual(new ContentId("bones"), pool.Entries[0].Stack.ItemId);
            Assert.AreEqual(1, pool.Entries[0].Stack.Quantity);
            Assert.AreEqual(SharedLootEntryState.Assigned, pool.Entries[0].State);
            Assert.AreEqual(leader.CharacterId, pool.Entries[0].AssignedCharacterId.Value);

            Assert.IsFalse(setup.Contributions.TrySnapshot(creature.InstanceId, out _));
            Assert.AreEqual(4, result.Experience.Awards.Count);
        }

        [Test]
        public void SoloFirstClaimDropsProtectedLootAtDeathLocation()
        {
            var setup = CreateSetup();
            var solo = AddPlayer(setup, "Solo");
            var creature = DeadGoblin(setup);
            setup.Contributions.Record(new CombatContribution(creature.InstanceId, solo.CharacterId, 20, 100));

            var result = setup.Service.Settle(
                creature,
                new[] { new CombatRewardPresence(solo.CharacterId, true) },
                5000,
                Sequence(0.0, 0.9, 0.0, 0.5),
                id => setup.Players.TryGetValue(id, out var player) ? player : null);

            Assert.IsTrue(result.Success);
            Assert.IsFalse(result.PartyLootPoolId.HasValue);
            Assert.AreEqual(2, result.SpawnedGroundItemIds.Count);

            foreach (var groundId in result.SpawnedGroundItemIds)
            {
                Assert.IsTrue(setup.GroundRegistry.TryGet(groundId, out var ground));
                Assert.AreEqual(creature.Anchor, ground.Location);
                Assert.IsTrue(ground.CanBeTakenBy(solo.CharacterId, 5001));
                Assert.IsFalse(ground.CanBeTakenBy(Guid.NewGuid(), 5001));
                Assert.IsTrue(ground.CanBeTakenBy(Guid.NewGuid(), 7000));
                Assert.AreEqual(9000, ground.ExpiresAtUnixMilliseconds);
            }
        }

        [Test]
        public void FirstClaimSurvivesTokenContributionButPowerLevelXpDoesNot()
        {
            var setup = CreateSetup();
            var tapper = AddPlayer(setup, "Tapper");
            var nearby = AddPlayer(setup, "PartyMate");
            var finisher = AddPlayer(setup, "Finisher");
            var party = setup.Parties.Create(Guid.NewGuid(), tapper.CharacterId, PartyLootMode.FreeForAll);
            setup.Parties.AddMember(party.PartyId, nearby.CharacterId);
            var creature = DeadGoblin(setup);
            setup.Contributions.Record(new CombatContribution(creature.InstanceId, tapper.CharacterId, 5, 100));
            setup.Contributions.Record(new CombatContribution(creature.InstanceId, finisher.CharacterId, 95, 110));

            var result = setup.Service.Settle(
                creature,
                new[]
                {
                    new CombatRewardPresence(tapper.CharacterId, true),
                    new CombatRewardPresence(nearby.CharacterId, true),
                    new CombatRewardPresence(finisher.CharacterId, true)
                },
                1000,
                Sequence(0.0, 0.9, 0.0, 0.5),
                id => setup.Players.TryGetValue(id, out var player) ? player : null);

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.RewardPlan.InitialClaim.IsParty);
            Assert.AreEqual(party.PartyId, result.RewardPlan.InitialClaim.PartyId.Value);
            Assert.IsFalse(result.RewardPlan.TryGetPartyGroup(party.PartyId, out _));
            Assert.IsTrue(result.RewardPlan.TryGetSoloGroup(finisher.CharacterId, out _));
            Assert.AreEqual(1, result.Experience.Awards.Count);
            Assert.AreEqual(finisher.CharacterId, result.Experience.Awards[0].CharacterId);
            Assert.IsTrue(result.PartyLootPoolId.HasValue);
        }

        private static Setup CreateSetup()
        {
            var creatures = MigrationSeedCreatureCatalog.Create();
            var lootTables = MigrationSeedLootTableCatalog.Create();
            var items = MigrationSeedItemCatalog.Create();
            var contributions = new CombatContributionLedger();
            var parties = new PartyRegistry();
            var rewardPlanner = new CombatRewardPlanner(
                parties,
                new ContributionEligibilityPolicy(minimumDamage: 10, minimumDamageFraction: 0.10));
            var experience = new CombatExperienceSettlementService();
            var partyLoot = new PartyLootPoolService();
            var groundRegistry = new GroundItemRegistry();
            var groundItems = new GroundItemService(items, groundRegistry);
            var service = new CombatKillSettlementService(
                creatures,
                lootTables,
                items,
                contributions,
                rewardPlanner,
                experience,
                parties,
                partyLoot,
                groundItems,
                lootPublicDelayMilliseconds: 2000,
                lootLifetimeMilliseconds: 4000);
            return new Setup(creatures, contributions, parties, partyLoot, groundRegistry, service);
        }

        private static PlayerState AddPlayer(Setup setup, string name)
        {
            var player = new PlayerState(Guid.NewGuid(), name);
            setup.Players.Add(player.CharacterId, player);
            return player;
        }

        private static CreatureState DeadGoblin(Setup setup)
        {
            Assert.IsTrue(setup.Creatures.TryGet(new ContentId("goblin"), out var definition));
            var creature = CreatureState.Spawn(Guid.NewGuid(), definition, Loc(20, 20));
            creature.CurrentHitpoints = 0;
            return creature;
        }

        private static Func<double> Sequence(params double[] values)
        {
            var index = 0;
            return () => values[Math.Min(index++, values.Length - 1)];
        }

        private static GridLocation Loc(int x, int y)
            => new GridLocation(new GridCoord(x, y), WorldConstants.SurfacePlane, 0);

        private sealed class Setup
        {
            public Setup(
                CreatureCatalog creatures,
                CombatContributionLedger contributions,
                PartyRegistry parties,
                PartyLootPoolService partyLoot,
                GroundItemRegistry groundRegistry,
                CombatKillSettlementService service)
            {
                Creatures = creatures;
                Contributions = contributions;
                Parties = parties;
                PartyLoot = partyLoot;
                GroundRegistry = groundRegistry;
                Service = service;
            }

            public CreatureCatalog Creatures { get; }
            public CombatContributionLedger Contributions { get; }
            public PartyRegistry Parties { get; }
            public PartyLootPoolService PartyLoot { get; }
            public GroundItemRegistry GroundRegistry { get; }
            public CombatKillSettlementService Service { get; }
            public Dictionary<Guid, PlayerState> Players { get; } = new Dictionary<Guid, PlayerState>();
        }
    }
}
