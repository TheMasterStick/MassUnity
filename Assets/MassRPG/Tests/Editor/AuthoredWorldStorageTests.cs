using MassRPG.Core.Content;
using MassRPG.Core.World;
using MassRPG.Data.World;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class AuthoredWorldStorageTests
    {
        private static readonly ContentId Grass = new ContentId("terrain.grass");

        [Test]
        public void Addressing_ResolvesAcross512PageBoundary()
        {
            Assert.IsTrue(WorldAddressing.TryResolve(Loc(511, 511), out var a));
            Assert.IsTrue(WorldAddressing.TryResolve(Loc(512, 512), out var b));

            Assert.AreEqual(new WorldPageCoord(0, 0), a.Key.Page);
            Assert.AreEqual(511, a.LocalX);
            Assert.AreEqual(511, a.LocalY);
            Assert.AreEqual(new WorldPageCoord(1, 1), b.Key.Page);
            Assert.AreEqual(0, b.LocalX);
            Assert.AreEqual(0, b.LocalY);
        }

        [Test]
        public void Store_AllocatesOnlyPagesThatAreActuallyLoadedOrEdited()
        {
            var store = new AuthoredWorldPageStore(Grass);
            Assert.AreEqual(0, store.LoadedPageCount);

            store.GetOrCreatePage(Loc(90000, 90000));
            Assert.AreEqual(1, store.LoadedPageCount);
        }

        [Test]
        public void DifferentElevation_BlocksUntilExplicitRampEdgeExists()
        {
            var store = new AuthoredWorldPageStore(Grass, 16);
            var low = Loc(10, 10);
            var high = Loc(11, 10);
            store.SetCell(low, new AuthoredTileCell(Grass, 0));
            store.SetCell(high, new AuthoredTileCell(Grass, 1));

            Assert.IsFalse(GridTraversal.CanStep(store, low, high));
            store.SetCardinalEdge(low, high, false, false, true);
            Assert.IsTrue(GridTraversal.CanStep(store, low, high));
        }

        [Test]
        public void MovementAndRangedLosBlocking_AreIndependent()
        {
            var store = new AuthoredWorldPageStore(Grass, 16);
            var a = Loc(2, 2);
            var b = Loc(3, 2);
            store.SetCell(a, new AuthoredTileCell(Grass, 0));
            store.SetCell(b, new AuthoredTileCell(Grass, 0));
            store.SetCardinalEdge(a, b, true, false, false);

            Assert.IsFalse(store.CanTraverseCardinalEdge(a, b));
            Assert.IsFalse(store.BlocksRangedLineOfSight(a, b));

            store.SetCardinalEdge(a, b, true, true, false);
            Assert.IsTrue(store.BlocksRangedLineOfSight(a, b));
        }

        [Test]
        public void UniformPage_RunLengthEncodesToSingleRunAndRoundTrips()
        {
            var page = new AuthoredWorldPage(
                new WorldPageKey(new WorldPageCoord(4, 9), WorldConstants.SurfacePlane, 0),
                16,
                Grass);

            var document = WorldPageCodec.Encode(page);
            Assert.AreEqual(1, document.Runs.Count);
            Assert.AreEqual(256, document.Runs[0].Length);

            var decoded = WorldPageCodec.Decode(document);
            Assert.AreEqual(page.Key, decoded.Key);
            Assert.AreEqual(page.GetCell(15, 15), decoded.GetCell(15, 15));
        }

        private static GridLocation Loc(int x, int y)
            => new GridLocation(new GridCoord(x, y), WorldConstants.SurfacePlane, 0);
    }
}
