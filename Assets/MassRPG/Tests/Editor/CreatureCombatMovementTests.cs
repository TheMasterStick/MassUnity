using System;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.World;
using MassRPG.Data.Creatures;
using MassRPG.Data.World;
using MassRPG.Server.Creatures;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class CreatureCombatMovementTests
    {
        private static readonly ContentId Grass = new ContentId("terrain.grass");

        [Test]
        public void EastApproach_UsesEastArcInsteadOfCirclingBehindPlayer()
        {
            var map = CreateMap();
            var definitions = MigrationSeedCreatureCatalog.Create();
            Assert.IsTrue(definitions.TryGet(new ContentId("goblin"), out var goblin));
            var occupancy = new CreatureOccupancyIndex();
            var planner = new CreatureCombatMovementPlanner(map, map, occupancy);
            var player = new PlayerState(Guid.NewGuid(), "Target") { Location = Loc(10, 10) };
            var creature = CreatureState.Spawn(Guid.NewGuid(), goblin, Loc(14, 10));
            Assert.IsTrue(occupancy.TryPlace(creature.InstanceId, creature.Anchor, goblin.Footprint));

            var path = planner.FindApproach(creature, goblin, player, 1000);

            Assert.IsTrue(path.Success);
            Assert.Greater(path.Steps.Count, 0);
            var destination = path.Steps[path.Steps.Count - 1];
            Assert.AreEqual(11, destination.Tile.X);
            Assert.IsTrue(destination.Tile.Y >= 9 && destination.Tile.Y <= 11);
        }

        [Test]
        public void FullEastMeleeArc_MakesTrailingCreatureWaitRatherThanCircleToWest()
        {
            var map = CreateMap();
            var definitions = MigrationSeedCreatureCatalog.Create();
            Assert.IsTrue(definitions.TryGet(new ContentId("goblin"), out var goblin));
            var occupancy = new CreatureOccupancyIndex();
            var planner = new CreatureCombatMovementPlanner(map, map, occupancy);
            var player = new PlayerState(Guid.NewGuid(), "Target") { Location = Loc(10, 10) };

            Occupy(occupancy, goblin, Loc(11, 9));
            Occupy(occupancy, goblin, Loc(11, 10));
            Occupy(occupancy, goblin, Loc(11, 11));

            var trailing = CreatureState.Spawn(Guid.NewGuid(), goblin, Loc(14, 10));
            Assert.IsTrue(occupancy.TryPlace(trailing.InstanceId, trailing.Anchor, goblin.Footprint));

            var path = planner.FindApproach(trailing, goblin, player, 300);

            Assert.IsFalse(path.Success, "A creature approaching from the east must not circle to the west side merely because the east attack arc is full.");
        }

        [Test]
        public void RangedCreature_StopsOnFreeTileWhenAlreadyInRange()
        {
            var map = CreateMap();
            var definitions = MigrationSeedCreatureCatalog.Create();
            Assert.IsTrue(definitions.TryGet(new ContentId("dark_wizard"), out var wizard));
            var occupancy = new CreatureOccupancyIndex();
            var planner = new CreatureCombatMovementPlanner(map, map, occupancy);
            var player = new PlayerState(Guid.NewGuid(), "Target") { Location = Loc(10, 10) };
            var creature = CreatureState.Spawn(Guid.NewGuid(), wizard, Loc(15, 10));
            Assert.IsTrue(occupancy.TryPlace(creature.InstanceId, creature.Anchor, wizard.Footprint));

            Assert.IsTrue(planner.IsInAttackPosition(creature, wizard, player));
        }

        private static AuthoredWorldPageStore CreateMap()
        {
            var map = new AuthoredWorldPageStore(Grass, 32);
            map.GetOrCreatePage(Loc(0, 0));
            return map;
        }

        private static void Occupy(CreatureOccupancyIndex occupancy, CreatureDefinition definition, GridLocation location)
        {
            Assert.IsTrue(occupancy.TryPlace(Guid.NewGuid(), location, definition.Footprint));
        }

        private static GridLocation Loc(int x, int y)
            => new GridLocation(new GridCoord(x, y), WorldConstants.SurfacePlane, 0);
    }
}
