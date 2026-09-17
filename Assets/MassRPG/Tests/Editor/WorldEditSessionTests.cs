using MassRPG.Core.Content;
using MassRPG.Core.World;
using MassRPG.Data.World;
using MassRPG.EditorCore.World;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class WorldEditSessionTests
    {
        private static readonly ContentId Grass = new ContentId("terrain.grass");
        private static readonly ContentId Sand = new ContentId("terrain.sand");

        [Test]
        public void ThreeByThreeBrushChangesExactlyNineCells()
        {
            var store = new AuthoredWorldPageStore(Grass, 16);
            var session = new WorldEditSession(store);

            var changed = session.PaintGround(Loc(8, 8), 3, Sand);

            Assert.AreEqual(9, changed);
            Assert.AreEqual(1, session.DirtyPageCount);
            Assert.AreEqual(Sand, Cell(store, 7, 7).GroundId);
            Assert.AreEqual(Sand, Cell(store, 9, 9).GroundId);
            Assert.AreEqual(Grass, Cell(store, 10, 10).GroundId);
        }

        [Test]
        public void RaiseUndoRedoPreservesExactLogicalElevation()
        {
            var store = new AuthoredWorldPageStore(Grass, 16);
            var session = new WorldEditSession(store);
            session.RaiseElevation(Loc(5, 5), 1, 1);
            Assert.AreEqual(1, Cell(store, 5, 5).Elevation);

            Assert.IsTrue(session.Undo());
            Assert.AreEqual(0, Cell(store, 5, 5).Elevation);
            Assert.IsTrue(session.Redo());
            Assert.AreEqual(1, Cell(store, 5, 5).Elevation);
        }

        [Test]
        public void DirtySaveBatchContainsOnlyTouchedStoragePages()
        {
            var store = new AuthoredWorldPageStore(Grass, 16);
            var session = new WorldEditSession(store);
            session.PaintGround(Loc(2, 2), 1, Sand);
            session.PaintGround(Loc(40, 2), 1, Sand);

            var documents = session.BuildDirtyPageDocuments();

            Assert.AreEqual(2, documents.Count);
            Assert.AreEqual(2, session.DirtyPageCount);
        }

        private static AuthoredTileCell Cell(AuthoredWorldPageStore store, int x, int y)
        {
            Assert.IsTrue(store.TryGetCell(Loc(x, y), out var cell));
            return cell;
        }

        private static GridLocation Loc(int x, int y)
            => new GridLocation(new GridCoord(x, y), WorldConstants.SurfacePlane, 0);
    }
}
