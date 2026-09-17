using MassRPG.Core.Characters;
using MassRPG.Core.Combat;
using MassRPG.Core.Content;
using MassRPG.Core.World;
using MassRPG.Data.World;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class CombatGeometryTests
    {
        private static readonly ContentId Grass = new ContentId("terrain.grass");

        [Test]
        public void MovementOnlyFence_BlocksMeleeButNotRangedLineOfSight()
        {
            var map = MakeMap();
            var a = Loc(5, 5);
            var b = Loc(6, 5);
            map.SetCardinalEdge(a, b, true, false, false);

            Assert.IsFalse(CombatGeometry.CanMelee(map, a, b));
            Assert.IsTrue(CombatGeometry.CanRangedOrMagic(map, a, b, 7));
        }

        [Test]
        public void FullWall_BlocksRangedLineOfSight()
        {
            var map = MakeMap();
            var a = Loc(5, 5);
            var b = Loc(6, 5);
            map.SetCardinalEdge(a, b, true, true, false);

            Assert.IsFalse(CombatGeometry.CanRangedOrMagic(map, a, b, 7));
        }

        [Test]
        public void TwoClosedCornerRoutes_BlockDiagonalShot()
        {
            var map = MakeMap();
            var start = Loc(5, 5);
            var target = Loc(6, 6);
            map.SetCardinalEdge(start, Loc(6, 5), true, true, false);
            map.SetCardinalEdge(start, Loc(5, 6), true, true, false);

            Assert.IsFalse(CombatGeometry.CanRangedOrMagic(map, start, target, 7));
        }

        [Test]
        public void ElevationDoesNotBlockRangedButAppliesStaticFivePercentAccuracyRule()
        {
            var map = MakeMap();
            var high = Loc(5, 5);
            var low = Loc(7, 5);
            map.SetCell(high, new AuthoredTileCell(Grass, 4));
            map.SetCell(low, new AuthoredTileCell(Grass, 1));

            Assert.IsTrue(CombatGeometry.CanRangedOrMagic(map, high, low, 7));
            Assert.AreEqual(0.05, CombatGeometry.ElevationAccuracyModifier(CombatStyle.Ranged, 4, 1), 0.0001);
            Assert.AreEqual(-0.05, CombatGeometry.ElevationAccuracyModifier(CombatStyle.Magic, 1, 4), 0.0001);
            Assert.AreEqual(0.0, CombatGeometry.ElevationAccuracyModifier(CombatStyle.Melee, 4, 1), 0.0001);
        }

        private static AuthoredWorldPageStore MakeMap()
        {
            var map = new AuthoredWorldPageStore(Grass, 16);
            // One loaded page is sufficient for these local tests; default cells are open grass.
            map.GetOrCreatePage(Loc(0, 0));
            return map;
        }

        private static GridLocation Loc(int x, int y)
            => new GridLocation(new GridCoord(x, y), WorldConstants.SurfacePlane, 0);
    }
}
