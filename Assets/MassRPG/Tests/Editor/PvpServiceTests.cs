using System;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.World;
using MassRPG.Data.World.Semantics;
using MassRPG.Server.Pvp;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class PvpServiceTests
    {
        [Test]
        public void ProtectedAreaOverridesFlagsAndForcedPvp()
        {
            var semantics = new WorldSemanticCatalog();
            semantics.RegisterArea(new WorldAreaDefinition(
                new ContentId("area.forced"), "Arena", WorldAreaKind.ForcedPvP,
                new CircleAreaShape(new GridCoord(10, 10), 10)));
            semantics.RegisterArea(new WorldAreaDefinition(
                new ContentId("area.safe"), "Town", WorldAreaKind.PvpProtected,
                new CircleAreaShape(new GridCoord(10, 10), 2)));
            var service = new PvpService(semantics);
            var attacker = Player("A", 10, 10);
            var defender = Player("B", 11, 10);
            service.SetOptIn(attacker.CharacterId, true);
            service.SetOptIn(defender.CharacterId, true);

            var decision = service.EvaluateAttack(attacker, defender);
            Assert.IsFalse(decision.Allowed);
            Assert.AreEqual("pvp_protected", decision.Code);
            Assert.IsTrue(decision.ProtectedArea);
        }

        [Test]
        public void ForcedPvpDoesNotRequireVoluntaryFlags()
        {
            var semantics = new WorldSemanticCatalog();
            semantics.RegisterArea(new WorldAreaDefinition(
                new ContentId("area.forced"), "Wilderness", WorldAreaKind.ForcedPvP,
                new CircleAreaShape(new GridCoord(50, 50), 5)));
            var service = new PvpService(semantics);
            var attacker = Player("A", 49, 50);
            var defender = Player("B", 51, 50);

            var decision = service.EvaluateAttack(attacker, defender);
            Assert.IsTrue(decision.Allowed);
            Assert.IsTrue(decision.ForcedPvp);
        }

        [Test]
        public void NormalWorldRequiresBothOptedInAndValidAttackCanSkullAggressor()
        {
            var service = new PvpService(new WorldSemanticCatalog(), new PvpPolicy(true, 5000));
            var attacker = Player("A", 1, 1);
            var defender = Player("B", 2, 1);
            service.SetOptIn(attacker.CharacterId, true);

            Assert.AreEqual("defender_not_opted_in", service.EvaluateAttack(attacker, defender).Code);
            service.SetOptIn(defender.CharacterId, true);
            Assert.AreEqual("voluntary_pvp", service.EvaluateAttack(attacker, defender).Code);
            Assert.IsTrue(service.MarkAggressor(attacker, defender, 1000));
            Assert.IsTrue(service.GetOrCreate(attacker.CharacterId).IsSkulled(5999));
            Assert.IsFalse(service.GetOrCreate(attacker.CharacterId).IsSkulled(6000));
        }

        private static PlayerState Player(string name, int x, int y)
        {
            var player = new PlayerState(Guid.NewGuid(), name);
            player.Location = new GridLocation(new GridCoord(x, y), WorldConstants.SurfacePlane, 0);
            return player;
        }
    }
}
