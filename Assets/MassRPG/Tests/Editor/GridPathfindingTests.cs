using System.Collections.Generic;
using MassRPG.Core.World;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class GridPathfindingTests
    {
        [Test]
        public void OpenGround_AllowsDiagonalMovement()
        {
            var map = new TestTraversalMap();
            var start = Loc(0, 0);
            var goal = Loc(1, 1);

            var path = GridPathfinder.FindPath(map, start, goal);

            Assert.IsTrue(path.Success);
            Assert.AreEqual(1, path.Steps.Count);
            Assert.AreEqual(goal, path.Steps[0]);
        }

        [Test]
        public void DiagonalCannotSqueezePastBlockedOrthogonalTile()
        {
            var map = new TestTraversalMap();
            map.Block(1, 0);
            var start = Loc(0, 0);
            var diagonal = Loc(1, 1);

            Assert.IsFalse(GridTraversal.CanStep(map, start, diagonal));
        }

        [Test]
        public void CliffEdgeNeedsExplicitRamp()
        {
            var map = new TestTraversalMap();
            map.SetElevation(0, 0, 0);
            map.SetElevation(1, 0, 1);
            var low = Loc(0, 0);
            var high = Loc(1, 0);

            Assert.IsFalse(GridTraversal.CanStep(map, low, high));
            map.AddRamp(low, high);
            Assert.IsTrue(GridTraversal.CanStep(map, low, high));
        }

        [Test]
        public void PathfinderRoutesAroundBlockedCornerInsteadOfCuttingThroughIt()
        {
            var map = new TestTraversalMap();
            map.Block(1, 0);
            var start = Loc(0, 0);
            var goal = Loc(2, 1);

            var path = GridPathfinder.FindPath(map, start, goal);

            Assert.IsTrue(path.Success);
            Assert.Greater(path.Steps.Count, 2);
            Assert.AreNotEqual(new GridCoord(1, 1), path.Steps[0].Tile);
        }

        private static GridLocation Loc(int x, int y) => new GridLocation(new GridCoord(x, y), WorldConstants.SurfacePlane, 0);

        private sealed class TestTraversalMap : IGridTraversalMap
        {
            private readonly HashSet<GridCoord> _blocked = new HashSet<GridCoord>();
            private readonly Dictionary<GridCoord, int> _elevation = new Dictionary<GridCoord, int>();
            private readonly HashSet<string> _ramps = new HashSet<string>();

            public void Block(int x, int y) => _blocked.Add(new GridCoord(x, y));
            public void SetElevation(int x, int y, int elevation) => _elevation[new GridCoord(x, y)] = elevation;

            public void AddRamp(GridLocation a, GridLocation b)
            {
                _ramps.Add(EdgeKey(a.Tile, b.Tile));
                _ramps.Add(EdgeKey(b.Tile, a.Tile));
            }

            public bool IsWalkable(GridLocation location) => !_blocked.Contains(location.Tile);

            public int GetLogicalElevation(GridLocation location)
                => _elevation.TryGetValue(location.Tile, out var value) ? value : 0;

            public bool CanTraverseCardinalEdge(GridLocation from, GridLocation to)
            {
                if (!from.SameLayer(to)) return false;
                var dx = System.Math.Abs(from.Tile.X - to.Tile.X);
                var dy = System.Math.Abs(from.Tile.Y - to.Tile.Y);
                if (dx + dy != 1) return false;
                if (!IsWalkable(to)) return false;
                if (GetLogicalElevation(from) == GetLogicalElevation(to)) return true;
                return _ramps.Contains(EdgeKey(from.Tile, to.Tile));
            }

            private static string EdgeKey(GridCoord from, GridCoord to)
                => from.X + "," + from.Y + ">" + to.X + "," + to.Y;
        }
    }
}
