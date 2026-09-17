using System;
using MassRPG.Core.Content;
using MassRPG.Core.Creatures;
using MassRPG.Core.World;
using MassRPG.Data.Creatures;
using MassRPG.Server.Creatures;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class CreatureFoundationTests
    {
        [Test]
        public void LargeFootprint_IsMeasuredAgainstAnyOccupiedTile()
        {
            var footprint = new CreatureFootprint(3, 3);
            var anchor = Loc(10, 10);

            Assert.AreEqual(0, footprint.RangeDistanceTo(anchor, Loc(12, 12)));
            Assert.AreEqual(1, footprint.RangeDistanceTo(anchor, Loc(13, 11)));
            Assert.AreEqual(3, footprint.RangeDistanceTo(anchor, Loc(15, 11)));
        }

        [Test]
        public void CreatureOccupancy_PreventsStackingButDoesNotModelPlayersAsBlockers()
        {
            var index = new CreatureOccupancyIndex();
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var one = new CreatureFootprint(1, 1);

            Assert.IsTrue(index.TryPlace(a, Loc(20, 20), one));
            Assert.IsFalse(index.TryPlace(b, Loc(20, 20), one));
            Assert.IsTrue(index.TryPlace(b, Loc(21, 20), one));
        }

        [Test]
        public void SeedCatalog_UsesFinalBehaviorCategoriesAndLargeFootprints()
        {
            var catalog = MigrationSeedCreatureCatalog.Create();
            Assert.IsTrue(catalog.TryGet(new ContentId("chicken"), out var chicken));
            Assert.AreEqual(CreatureDisposition.Passive, chicken.Disposition);
            Assert.IsTrue(catalog.TryGet(new ContentId("cow"), out var cow));
            Assert.AreEqual(CreatureDisposition.Neutral, cow.Disposition);
            Assert.IsTrue(catalog.TryGet(new ContentId("wolf"), out var wolf));
            Assert.AreEqual(CreatureDisposition.Aggressive, wolf.Disposition);
            Assert.IsTrue(catalog.TryGet(new ContentId("hill_giant"), out var giant));
            Assert.AreEqual(new CreatureFootprint(2, 2), giant.Footprint);
        }

        private static GridLocation Loc(int x, int y)
            => new GridLocation(new GridCoord(x, y), WorldConstants.SurfacePlane, 0);
    }
}
