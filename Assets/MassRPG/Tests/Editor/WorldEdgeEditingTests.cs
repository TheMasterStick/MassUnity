using MassRPG.Core.Content;
using MassRPG.Core.World;
using MassRPG.Data.World;
using MassRPG.EditorCore.World;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class WorldEdgeEditingTests
    {
        private static readonly ContentId Grass = new ContentId("terrain.grass");

        [Test]
        public void Ramp_OnlyConnectsExactlyOneElevationStep_AndIsUndoable()
        {
            var store = new AuthoredWorldPageStore(Grass, 32);
            var session = new WorldEditSession(store);
            var low = Loc(10, 10);
            var high = Loc(11, 10);
            session.SetElevation(low, 1, 0);
            session.SetElevation(high, 1, 1);
            session.MarkAllSaved();

            Assert.IsFalse(store.CanTraverseCardinalEdge(low, high));
            Assert.IsTrue(WorldEdgeEditing.TryPlaceRamp(session, low, high));
            Assert.IsTrue(store.CanTraverseCardinalEdge(low, high));
            Assert.IsTrue(store.CanTraverseCardinalEdge(high, low));
            Assert.IsTrue(session.Undo());
            Assert.IsFalse(store.CanTraverseCardinalEdge(low, high));

            session.SetElevation(high, 1, 2);
            Assert.IsFalse(WorldEdgeEditing.TryPlaceRamp(session, low, high));
        }

        [Test]
        public void FenceStyleBarrier_CanBlockMovementWithoutBlockingRangedLos()
        {
            var store = new AuthoredWorldPageStore(Grass, 32);
            var session = new WorldEditSession(store);
            var west = Loc(10, 10);
            var east = Loc(11, 10);
            store.GetOrCreatePage(west);

            Assert.IsTrue(WorldEdgeEditing.SetBarrier(session, west, east, movementBlocked: true, lineOfSightBlocked: false));

            Assert.IsFalse(store.CanTraverseCardinalEdge(west, east));
            Assert.IsFalse(store.IsRangedLineOfSightBlockedCardinalEdge(west, east));
        }

        [Test]
        public void WallStyleBarrier_BlocksMovementAndRangedLosOnBothSides()
        {
            var store = new AuthoredWorldPageStore(Grass, 32);
            var session = new WorldEditSession(store);
            var west = Loc(10, 10);
            var east = Loc(11, 10);
            store.GetOrCreatePage(west);

            Assert.IsTrue(WorldEdgeEditing.SetBarrier(session, west, east, movementBlocked: true, lineOfSightBlocked: true));

            Assert.IsFalse(store.CanTraverseCardinalEdge(west, east));
            Assert.IsTrue(store.IsRangedLineOfSightBlockedCardinalEdge(west, east));
            Assert.IsTrue(store.IsRangedLineOfSightBlockedCardinalEdge(east, west));
        }

        private static GridLocation Loc(int x, int y)
            => new GridLocation(new GridCoord(x, y), WorldConstants.SurfacePlane, 0);
    }
}
