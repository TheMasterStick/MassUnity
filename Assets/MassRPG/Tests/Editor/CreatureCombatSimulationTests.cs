using System;
using MassRPG.Core.Characters;
using MassRPG.Core.Combat;
using MassRPG.Core.Content;
using MassRPG.Core.Skills;
using MassRPG.Core.World;
using MassRPG.Data.Creatures;
using MassRPG.Data.Items;
using MassRPG.Data.World;
using MassRPG.Server.Authority;
using MassRPG.Server.Combat;
using MassRPG.Server.Creatures;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class CreatureCombatSimulationTests
    {
        private static readonly ContentId Grass = new ContentId("terrain.grass");

        [Test]
        public void AggressiveCreature_AcquiresNearbyPlayerThenMovesTowardThem()
        {
            var setup = CreateSetup();
            setup.Player.Location = Loc(10, 10);
            var wolf = Spawn(setup, "wolf", Loc(13, 10));

            var acquired = setup.Authority.AdvanceCreatureCombat(wolf.InstanceId, 1000, Sequence(0.0, 0.5));
            Assert.AreEqual(CreatureAdvanceKind.AcquiredTarget, acquired.Kind);
            Assert.AreEqual(setup.Player.CharacterId, wolf.TargetCharacterId.Value);

            var moved = setup.Authority.AdvanceCreatureCombat(wolf.InstanceId, 1001, Sequence(0.0, 0.5));
            Assert.AreEqual(CreatureAdvanceKind.Moved, moved.Kind);
            Assert.Less(GridMath.RangeDistance(wolf.Anchor.Tile, setup.Player.Location.Tile), 3);
        }

        [Test]
        public void NeutralCreature_DoesNotAutoAggroButRetaliationTargetCanFight()
        {
            var setup = CreateSetup();
            setup.Player.Location = Loc(10, 10);
            var cow = Spawn(setup, "cow", Loc(11, 10));

            var idle = setup.Authority.AdvanceCreatureCombat(cow.InstanceId, 1000, Sequence(0.0, 0.5));
            Assert.AreEqual(CreatureAdvanceKind.Idle, idle.Kind);
            Assert.IsFalse(cow.TargetCharacterId.HasValue);

            cow.TargetCharacterId = setup.Player.CharacterId;
            var attack = setup.Authority.AdvanceCreatureCombat(cow.InstanceId, 1001, Sequence(0.0, 0.999));
            Assert.AreEqual(CreatureAdvanceKind.Attacked, attack.Kind);
        }

        [Test]
        public void PassiveCreature_ClearsAnyCombatTargetAndNeverAttacks()
        {
            var setup = CreateSetup();
            setup.Player.Location = Loc(10, 10);
            var chicken = Spawn(setup, "chicken", Loc(11, 10));
            chicken.TargetCharacterId = setup.Player.CharacterId;

            var result = setup.Authority.AdvanceCreatureCombat(chicken.InstanceId, 1000, Sequence(0.0, 0.999));

            Assert.AreEqual(CreatureAdvanceKind.Idle, result.Kind);
            Assert.IsFalse(chicken.TargetCharacterId.HasValue);
        }

        [Test]
        public void Creature_GivesUpWhenTargetMovesBeyondHomeLeash()
        {
            var setup = CreateSetup();
            setup.Player.Location = Loc(10, 10);
            var wolf = Spawn(setup, "wolf", Loc(11, 10));
            wolf.TargetCharacterId = setup.Player.CharacterId;
            setup.Player.Location = Loc(40, 10);

            var result = setup.Authority.AdvanceCreatureCombat(wolf.InstanceId, 1000, Sequence(0.0, 0.5));

            Assert.AreEqual(CreatureAdvanceKind.GaveUp, result.Kind);
            Assert.IsFalse(wolf.TargetCharacterId.HasValue);
        }

        [Test]
        public void RangedCreature_AttacksFromRangeAndRespectsCooldown()
        {
            var setup = CreateSetup();
            setup.Player.Location = Loc(10, 10);
            setup.Player.Skills.SetXp(SkillId.Hitpoints, SkillProgression.XpForLevel(100));
            setup.Player.CurrentHitpoints = setup.Player.MaxHitpoints;
            var wizard = Spawn(setup, "dark_wizard", Loc(15, 10));
            wizard.TargetCharacterId = setup.Player.CharacterId;

            var attack = setup.Authority.AdvanceCreatureCombat(wizard.InstanceId, 1000, Sequence(0.0, 0.999));
            Assert.AreEqual(CreatureAdvanceKind.Attacked, attack.Kind);
            Assert.IsTrue(attack.Hit);
            Assert.Greater(attack.Damage, 0);

            var cooldown = setup.Authority.AdvanceCreatureCombat(wizard.InstanceId, 1001, Sequence(0.0, 0.999));
            Assert.AreEqual(CreatureAdvanceKind.WaitingForCooldown, cooldown.Kind);
        }

        private static Setup CreateSetup()
        {
            var map = new AuthoredWorldPageStore(Grass, 32);
            map.GetOrCreatePage(Loc(0, 0));
            var definitions = MigrationSeedCreatureCatalog.Create();
            var registry = new CreatureRegistry();
            var occupancy = new CreatureOccupancyIndex();
            var items = MigrationSeedItemCatalog.Create();
            var profiles = new DataDrivenPlayerAttackProfileSource(items);
            var planner = new CreatureCombatMovementPlanner(map, map, occupancy);
            var creatureCombat = new CreatureCombatSimulationService(definitions, profiles, planner, map);
            var authority = new LocalGameAuthority(items, map, creatures: registry, creatureCombat: creatureCombat);
            var player = new PlayerState(Guid.NewGuid(), "Target");
            authority.RegisterPlayer(player);
            return new Setup(definitions, registry, occupancy, authority, player);
        }

        private static CreatureState Spawn(Setup setup, string definitionId, GridLocation location)
        {
            Assert.IsTrue(setup.Definitions.TryGet(new ContentId(definitionId), out var definition));
            var creature = CreatureState.Spawn(Guid.NewGuid(), definition, location);
            setup.Registry.Register(creature);
            Assert.IsTrue(setup.Occupancy.TryPlace(creature.InstanceId, location, definition.Footprint));
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
            public Setup(CreatureCatalog definitions, CreatureRegistry registry, CreatureOccupancyIndex occupancy, LocalGameAuthority authority, PlayerState player)
            {
                Definitions = definitions;
                Registry = registry;
                Occupancy = occupancy;
                Authority = authority;
                Player = player;
            }

            public CreatureCatalog Definitions { get; }
            public CreatureRegistry Registry { get; }
            public CreatureOccupancyIndex Occupancy { get; }
            public LocalGameAuthority Authority { get; }
            public PlayerState Player { get; }
        }
    }
}