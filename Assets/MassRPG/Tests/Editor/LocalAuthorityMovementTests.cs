using System;
using System.Collections.Generic;
using MassRPG.Core.Authority;
using MassRPG.Core.Characters;
using MassRPG.Core.World;
using MassRPG.Data.Items;
using MassRPG.Server.Authority;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class LocalAuthorityMovementTests
    {
        [Test]
        public void MoveRequest_PlansPathButAuthorityAdvancesPosition()
        {
            var map = new TestMap();
            var authority = new LocalGameAuthority(new ItemCatalog(), map);
            var player = NewPlayerAt(10, 10);
            authority.RegisterPlayer(player);

            var destination = Loc(12, 10);
            var decision = authority.Submit(new MoveToRequest(Guid.NewGuid(), player.CharacterId, destination));

            Assert.IsTrue(decision.Accepted);
            Assert.AreEqual(2, player.Movement.RemainingSteps);
            Assert.AreEqual(Loc(10, 10), player.Location);

            Assert.IsTrue(authority.AdvanceMovementOneStep(player.CharacterId));
            Assert.AreEqual(Loc(11, 10), player.Location);
            Assert.IsTrue(authority.AdvanceMovementOneStep(player.CharacterId));
            Assert.AreEqual(destination, player.Location);
            Assert.IsFalse(player.Movement.IsMoving);
        }

        [Test]
        public void MoveRequest_DoesNotCutBlockedDiagonalCorner()
        {
            var map = new TestMap();
            map.Block(11, 10);
            var authority = new LocalGameAuthority(new ItemCatalog(), map);
            var player = NewPlayerAt(10, 10);
            authority.RegisterPlayer(player);

            var decision = authority.Submit(new MoveToRequest(Guid.NewGuid(), player.CharacterId, Loc(11, 11)));

            Assert.IsTrue(decision.Accepted);
            Assert.Greater(player.Movement.RemainingSteps, 1);
        }

        [Test]
        public void WorldChange_RevalidatesNextStepAndCancelsStalePath()
        {
            var map = new TestMap();
            var authority = new LocalGameAuthority(new ItemCatalog(), map);
            var player = NewPlayerAt(10, 10);
            authority.RegisterPlayer(player);
            authority.Submit(new MoveToRequest(Guid.NewGuid(), player.CharacterId, Loc(12, 10)));
            map.Block(11, 10);

            Assert.IsFalse(authority.AdvanceMovementOneStep(player.CharacterId));
            Assert.AreEqual(Loc(10, 10), player.Location);
            Assert.IsFalse(player.Movement.IsMoving);
        }

        private static PlayerState NewPlayerAt(int x, int y)
        {
            var player = new PlayerState(Guid.NewGuid(), "Walker");
            player.Location = Loc(x, y);
            return player;
        }

        private static GridLocation Loc(int x, int y)
            => new GridLocation(new GridCoord(x, y), WorldConstants.SurfacePlane, 0);

        private sealed class TestMap : IGridTraversalMap
        {
            private readonly HashSet<GridCoord> _blocked = new HashSet<GridCoord>();
            private readonly Dictionary<GridCoord, int> _elevation = new Dictionary<GridCoord, int>();
            private readonly HashSet<string> _ramps = new HashSet<string>();

            public void Block(int x, int y) => _blocked.Add(new GridCoord(x, y));
            public void SetElevation(int x, int y, int value) => _elevation[new GridCoord(x, y)] = value;
            public void AddRamp(GridCoord a, GridCoord b)
            {
                _ramps.Add(Key(a, b));
                _ramps.Add(Key(b, a));
            }

            public bool IsWalkable(GridLocation location)
                => WorldConstants.IsInsideWorld(location.Tile) && !_blocked.Contains(location.Tile);

            public int GetLogicalElevation(GridLocation location)
                => _elevation.TryGetValue(location.Tile, out var value) ? value : 0;

            public bool CanTraverseCardinalEdge(GridLocation from, GridLocation to)
            {
                if (!from.SameLayer(to)) return false;
                var dx = Math.Abs(from.Tile.X - to.Tile.X);
                var dy = Math.Abs(from.Tile.Y - to.Tile.Y);
                if (dx + dy != 1 || !IsWalkable(to)) return false;
                if (GetLogicalElevation(from) == GetLogicalElevation(to)) return true;
                return _ramps.Contains(Key(from.Tile, to.Tile));
            }

            private static string Key(GridCoord a, GridCoord b)
                => a.X + "," + a.Y + ">" + b.X + "," + b.Y;
        }
    }
}
