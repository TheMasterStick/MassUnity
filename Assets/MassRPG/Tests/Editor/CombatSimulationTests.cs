using System;
using MassRPG.Core.Authority;
using MassRPG.Core.Characters;
using MassRPG.Core.Combat;
using MassRPG.Core.Content;
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
    public sealed class CombatSimulationTests
    {
        private static readonly ContentId Grass = new ContentId("terrain.grass");

        [Test]
        public void AutoAttack_IsResolvedByAuthorityAndSchedulesCooldown()
        {
            var setup = CreateSetup(CombatStyle.Melee, 1, 2400);
            setup.Player.Location = Loc(10, 10);
            var target = Spawn(setup, "cow", Loc(11, 10));
            Assert.IsTrue(setup.Authority.Submit(new AttackCreatureRequest(
                Guid.NewGuid(), setup.Player.CharacterId, target.InstanceId)).Accepted);

            var rng = Sequence(0.0, 0.999);
            var result = setup.Authority.AdvanceCombat(setup.Player.CharacterId, 1000, rng);

            Assert.AreEqual(CombatAdvanceKind.Attacked, result.Kind);
            Assert.IsTrue(result.Hit);
            Assert.Greater(result.Damage, 0);
            Assert.Less(target.CurrentHitpoints, 8);
            Assert.IsTrue(setup.Player.Combat.IsActive);
            Assert.IsTrue(setup.Player.Combat.NextAttackAtUnixMilliseconds > 1000);
            Assert.AreEqual(setup.Player.CharacterId, target.TargetCharacterId.Value);

            var cooldown = setup.Authority.AdvanceCombat(setup.Player.CharacterId, 1001, Sequence(0.0, 0.999));
            Assert.AreEqual(CombatAdvanceKind.WaitingForCooldown, cooldown.Kind);
        }

        [Test]
        public void PassiveCreature_DoesNotRetaliateWhenHit()
        {
            var setup = CreateSetup(CombatStyle.Melee, 1, 2400);
            setup.Player.Location = Loc(10, 10);
            var target = Spawn(setup, "chicken", Loc(11, 10));
            Assert.IsTrue(setup.Authority.Submit(new AttackCreatureRequest(
                Guid.NewGuid(), setup.Player.CharacterId, target.InstanceId)).Accepted);

            var result = setup.Authority.AdvanceCombat(setup.Player.CharacterId, 1000, Sequence(0.0, 0.999));

            Assert.IsTrue(result.DidAttack);
            Assert.IsFalse(target.TargetCharacterId.HasValue);
        }

        [Test]
        public void MovingTarget_OutOfRangeCreatesNewAuthoritativeApproachPath()
        {
            var setup = CreateSetup(CombatStyle.Melee, 1, 2400);
            setup.Player.Location = Loc(10, 10);
            var target = Spawn(setup, "goblin", Loc(11, 10));
            Assert.IsTrue(setup.Authority.Submit(new AttackCreatureRequest(
                Guid.NewGuid(), setup.Player.CharacterId, target.InstanceId)).Accepted);
            Assert.IsFalse(setup.Player.Movement.IsMoving);

            target.Anchor = Loc(15, 10);
            var result = setup.Authority.AdvanceCombat(setup.Player.CharacterId, 1000, Sequence(0.0, 0.999));

            Assert.AreEqual(CombatAdvanceKind.Approaching, result.Kind);
            Assert.IsTrue(setup.Player.Movement.IsMoving);
            Assert.AreEqual(1, GridMath.RangeDistance(setup.Player.Movement.Destination.Value.Tile, target.Anchor.Tile));
        }

        private static Setup CreateSetup(CombatStyle style, int range, int interval)
        {
            var map = new AuthoredWorldPageStore(Grass, 32);
            map.GetOrCreatePage(Loc(0, 0));
            var definitions = MigrationSeedCreatureCatalog.Create();
            var creatures = new CreatureRegistry();
            var profiles = new FixedProfileSource(new PlayerAttackProfile(style, range, interval, 100, 100, 0, 100, 100, 100));
            var approach = new CombatApproachPlanner(map, map);
            var targeting = new CombatTargetingService(creatures, definitions, profiles, approach);
            var simulation = new CombatSimulationService(creatures, definitions, profiles, approach, map);
            var items = new ItemCatalog();
            var authority = new LocalGameAuthority(items, map, null, targeting, simulation);
            var player = new PlayerState(Guid.NewGuid(), "Fighter") { CombatStyle = style };
            authority.RegisterPlayer(player);
            return new Setup(map, definitions, creatures, authority, player);
        }

        private static CreatureState Spawn(Setup setup, string id, GridLocation anchor)
        {
            Assert.IsTrue(setup.Definitions.TryGet(new ContentId(id), out var definition));
            var creature = CreatureState.Spawn(Guid.NewGuid(), definition, anchor);
            setup.Creatures.Register(creature);
            return creature;
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

        private sealed class Setup
        {
            public Setup(AuthoredWorldPageStore map, CreatureCatalog definitions, CreatureRegistry creatures, LocalGameAuthority authority, PlayerState player)
            {
                Map = map;
                Definitions = definitions;
                Creatures = creatures;
                Authority = authority;
                Player = player;
            }

            public AuthoredWorldPageStore Map { get; }
            public CreatureCatalog Definitions { get; }
            public CreatureRegistry Creatures { get; }
            public LocalGameAuthority Authority { get; }
            public PlayerState Player { get; }
        }
    }
}
