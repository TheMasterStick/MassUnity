using MassRPG.Core.Content;
using MassRPG.Core.World;
using MassRPG.Data.World;
using MassRPG.EditorCore.World;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class WorldTileGeometryTests
    {
        [Test]
        public void Line_IsInclusiveAndEightConnected()
        {
            var line = WorldTileGeometry.Line(new GridCoord(2, 3), new GridCoord(8, 6));
            Assert.AreEqual(new GridCoord(2, 3), line[0]);
            Assert.AreEqual(new GridCoord(8, 6), line[line.Count - 1]);
            for (var i = 1; i < line.Count; i++)
            {
                var dx = System.Math.Abs(line[i].X - line[i - 1].X);
                var dy = System.Math.Abs(line[i].Y - line[i - 1].Y);
                Assert.LessOrEqual(dx, 1);
                Assert.LessOrEqual(dy, 1);
                Assert.Greater(dx + dy, 0);
            }
        }

        [Test]
        public void FilledRectangle_WorksInReverseDragDirection()
        {
            var cells = WorldTileGeometry.FilledRectangle(new GridCoord(4, 5), new GridCoord(2, 3));
            Assert.AreEqual(9, cells.Count);
            Assert.AreEqual(new GridCoord(2, 3), cells[0]);
            Assert.AreEqual(new GridCoord(4, 5), cells[cells.Count - 1]);
        }

        [Test]
        public void Stamp_DefaultCopy_DoesNotCopyBorderEdgeMasks()
        {
            var store = new AuthoredWorldPageStore(new ContentId("ground.grass"), 8);
            var source = Loc(2, 2);
            var sourceCell = new AuthoredTileCell(
                new ContentId("ground.stone"), 3, TileFlags.NoBuild,
                CardinalEdgeMask.East, CardinalEdgeMask.South, CardinalEdgeMask.North);
            store.GetOrCreatePage(source);
            store.SetCell(source, sourceCell);

            var stamp = WorldTileStamp.Capture(store, source, source);
            var session = new WorldEditSession(store);
            var destination = Loc(5, 5);
            Assert.AreEqual(1, stamp.Paste(session, destination));
            Assert.IsTrue(store.TryGetCell(destination, out var pasted));
            Assert.AreEqual(new ContentId("ground.stone"), pasted.GroundId);
            Assert.AreEqual(3, pasted.Elevation);
            Assert.AreEqual(TileFlags.NoBuild, pasted.Flags);
            Assert.AreEqual(CardinalEdgeMask.None, pasted.MovementBlockedEdges);
            Assert.AreEqual(CardinalEdgeMask.None, pasted.LineOfSightBlockedEdges);
            Assert.AreEqual(CardinalEdgeMask.None, pasted.ElevationTransitionEdges);
        }

        private static GridLocation Loc(int x, int y)
            => new GridLocation(new GridCoord(x, y), WorldConstants.SurfacePlane, 0);
    }
}
