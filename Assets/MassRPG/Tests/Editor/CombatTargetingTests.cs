using System;
using MassRPG.Core.Authority;
using MassRPG.Core.Characters;
using MassRPG.Core.Combat;
using MassRPG.Core.Content;
using MassRPG.Core.Creatures;
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
    public sealed class CombatTargetingTests
    {
        private static readonly ContentId Grass = new ContentId("terrain.grass");

        [Test]
        public void MeleeClick_FromDistancePlansToOneTileFromTarget()
        {
            var setup = CreateSetup(new FixedProfileSource(new PlayerAttackProfile(CombatStyle.Melee, 1, 2400, 0, 0, 0, 0, 0, 0)));
            setup.Player.Location = Loc(10, 10);
            var creature = Spawn(setup, "goblin", Loc(14, 10));

            var decision = setup.Authority.Submit(new AttackCreatureRequest(Guid.NewGuid(), setup.Player.CharacterId, creature.InstanceId));

            Assert.IsTrue(decision.Accepted);
            Assert.IsTrue(setup.Player.Combat.IsActive);
            Assert.AreEqual(creature.InstanceId, setup.Player.Combat.TargetActorId.Value);
            Assert.Greater(setup.Player.Movement.RemainingSteps, 0);
            var destination = setup.Player.Movement.Destination.Value;
            Assert.AreEqual(1, GridMath.RangeDistance(destination.Tile, creature.Anchor.Tile));
        }

        [Test]
        public void RangedClick_AlreadyInRangeDoesNotCreateMovementPath()
        {
            var setup = CreateSetup(new FixedProfileSource(new PlayerAttackProfile(CombatStyle.Ranged, 6, 3000, 0, 0, 0, 0, 0, 0)));
            setup.Player.Location = Loc(10, 10);
            var creature = Spawn(setup, "goblin", Loc(15, 10));

            var decision = setup.Authority.Submit(new AttackCreatureRequest(Guid.NewGuid(), setup.Player.CharacterId, creature.InstanceId));

            Assert.IsTrue(decision.Accepted);
            Assert.IsTrue(setup.Player.Combat.IsActive);
            Assert.IsFalse(setup.Player.Movement.IsMoving);
        }

        [Test]
        public void LargeCreature_CanBeApproachedFromAnyFootprintSide()
        {
            var setup = CreateSetup(new FixedProfileSource(new PlayerAttackProfile(CombatStyle.Melee, 1, 2400, 0, 0, 0, 0, 0, 0)));
            setup.Player.Location = Loc(19, 21);
            var creature = Spawn(setup, "hill_giant", Loc(20, 20));
            Assert.IsTrue(setup.Definitions.TryGet(creature.DefinitionId, out var definition));

            Assert.IsTrue(CombatGeometry.CanMelee(setup.Map, setup.Player.Location, creature.Anchor, definition.Footprint));
        }

        [Test]
        public void ManualMoveCommandStopsPlayersCurrentAttackAttempt()
        {
            var setup = CreateSetup(new FixedProfileSource(new PlayerAttackProfile(CombatStyle.Melee, 1, 2400, 0, 0, 0, 0, 0, 0)));
            setup.Player.Location = Loc(10, 10);
            var creature = Spawn(setup, "goblin", Loc(12, 10));
            Assert.IsTrue(setup.Authority.Submit(new AttackCreatureRequest(Guid.NewGuid(), setup.Player.CharacterId, creature.InstanceId)).Accepted);

            var move = setup.Authority.Submit(new MoveToRequest(Guid.NewGuid(), setup.Player.CharacterId, Loc(10, 12)));

            Assert.IsTrue(move.Accepted);
            Assert.IsFalse(setup.Player.Combat.IsActive);
        }

        private static Setup CreateSetup(IPlayerAttackProfileSource profiles)
        {
            var map = new AuthoredWorldPageStore(Grass, 32);
            map.GetOrCreatePage(Loc(0, 0));
            var definitions = MigrationSeedCreatureCatalog.Create();
            var creatures = new CreatureRegistry();
            var targeting = new CombatTargetingService(
                creatures,
                definitions,
                profiles,
                new CombatApproachPlanner(map, map));
            var items = new ItemCatalog();
            var authority = new LocalGameAuthority(items, map, null, targeting);
            var player = new PlayerState(Guid.NewGuid(), "Fighter");
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
