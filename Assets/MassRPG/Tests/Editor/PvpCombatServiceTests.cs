using System;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Core.World;
using MassRPG.Data.Items;
using MassRPG.Data.World;
using MassRPG.Data.World.Semantics;
using MassRPG.Server.Combat;
using MassRPG.Server.Pvp;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class PvpCombatServiceTests
    {
        private static readonly ContentId Grass = new ContentId("terrain.grass");

        [Test]
        public void VoluntaryMeleeAttackEngagesBothPlayersSchedulesCooldownAndSkullsAttacker()
        {
            var setup = CreateSetup();
            setup.Pvp.SetOptIn(setup.Attacker.CharacterId, true);
            setup.Pvp.SetOptIn(setup.Defender.CharacterId, true);
            setup.Attacker.Location = Loc(10, 10);
            setup.Defender.Location = Loc(11, 10);

            var result = setup.Combat.TryAttack(setup.Attacker, setup.Defender, 1000, Sequence(0.0, 0.5));

            Assert.IsTrue(result.DidAttack);
            Assert.IsTrue(setup.Attacker.Combat.IsActive);
            Assert.AreEqual(setup.Defender.CharacterId, setup.Attacker.Combat.TargetActorId.Value);
            Assert.IsTrue(setup.Defender.Combat.IsActive);
            Assert.AreEqual(setup.Attacker.CharacterId, setup.Defender.Combat.TargetActorId.Value);
            Assert.Greater(setup.Attacker.Combat.NextAttackAtUnixMilliseconds, 1000);
            Assert.IsTrue(setup.Pvp.GetOrCreate(setup.Attacker.CharacterId).IsSkulled(1001));

            var cooldown = setup.Combat.TryAttack(setup.Attacker, setup.Defender, 1001, Sequence(0.0, 0.5));
            Assert.AreEqual(PvpAttackKind.WaitingForCooldown, cooldown.Kind);
        }

        [Test]
        public void ProtectedTownRejectsAttackBeforeDamageOrSkull()
        {
            var semantics = new WorldSemanticCatalog();
            semantics.RegisterArea(new WorldAreaDefinition(
                new ContentId("area.safe"), "Town", WorldAreaKind.PvpProtected,
                new CircleAreaShape(new GridCoord(10, 10), 5)));
            var setup = CreateSetup(semantics);
            setup.Pvp.SetOptIn(setup.Attacker.CharacterId, true);
            setup.Pvp.SetOptIn(setup.Defender.CharacterId, true);
            setup.Attacker.Location = Loc(10, 10);
            setup.Defender.Location = Loc(11, 10);
            var hp = setup.Defender.CurrentHitpoints;

            var result = setup.Combat.TryAttack(setup.Attacker, setup.Defender, 1000, Sequence(0.0, 0.5));

            Assert.AreEqual(PvpAttackKind.Failed, result.Kind);
            Assert.AreEqual("pvp_protected", result.Code);
            Assert.AreEqual(hp, setup.Defender.CurrentHitpoints);
            Assert.IsFalse(setup.Pvp.GetOrCreate(setup.Attacker.CharacterId).IsSkulled(1001));
        }

        [Test]
        public void RangedPvpConsumesExactlyOneSelectedArrowOnlyOnActualAttack()
        {
            var setup = CreateSetup();
            setup.Pvp.SetOptIn(setup.Attacker.CharacterId, true);
            setup.Pvp.SetOptIn(setup.Defender.CharacterId, true);
            setup.Attacker.CombatStyle = CombatStyle.Ranged;
            InventoryRules.AddItem(setup.Attacker.Inventory, setup.Items, new ContentId("normal_shortbow"), 1);
            Assert.IsTrue(InventoryRules.EquipFromInventory(
                setup.Attacker.Inventory,
                setup.Attacker.Equipment,
                setup.Items,
                setup.Attacker.Skills,
                FindSlot(setup.Attacker.Inventory, new ContentId("normal_shortbow")),
                EquipmentSlot.MainHand).Success);
            InventoryRules.AddItem(setup.Attacker.Inventory, setup.Items, new ContentId("bronze_arrow"), 3);
            Assert.IsTrue(setup.Ammunition.Select(setup.Attacker, new ContentId("bronze_arrow")).Success);
            setup.Attacker.Location = Loc(10, 10);
            setup.Defender.Location = Loc(20, 10);

            var tooFar = setup.Combat.TryAttack(setup.Attacker, setup.Defender, 1000, Sequence(0.0, 0.5));
            Assert.AreEqual(PvpAttackKind.OutOfRange, tooFar.Kind);
            Assert.AreEqual(3, setup.Attacker.Inventory.CountItem(new ContentId("bronze_arrow")));

            setup.Defender.Location = Loc(15, 10);
            var attack = setup.Combat.TryAttack(setup.Attacker, setup.Defender, 1001, Sequence(0.0, 0.5));
            Assert.IsTrue(attack.DidAttack);
            Assert.AreEqual(2, setup.Attacker.Inventory.CountItem(new ContentId("bronze_arrow")));
        }

        private static Setup CreateSetup(WorldSemanticCatalog semantics = null)
        {
            var world = new AuthoredWorldPageStore(Grass, 32);
            world.GetOrCreatePage(Loc(0, 0));
            var items = MigrationSeedItemCatalog.Create();
            var profiles = new DataDrivenPlayerAttackProfileSource(items);
            var ammunition = new RangedAmmunitionService(items);
            var pvp = new PvpService(semantics ?? new WorldSemanticCatalog(), new PvpPolicy(true, 5000));
            var combat = new PvpCombatService(pvp, profiles, world, world, ammunition);
            var attacker = new PlayerState(Guid.NewGuid(), "Attacker");
            var defender = new PlayerState(Guid.NewGuid(), "Defender");
            return new Setup(world, items, pvp, ammunition, combat, attacker, defender);
        }

        private static int FindSlot(InventoryState inventory, ContentId itemId)
        {
            for (var i = 0; i < inventory.Capacity; i++)
            {
                var stack = inventory.GetSlot(i);
                if (stack != null && stack.ItemId == itemId) return i;
            }
            return -1;
        }

        private static Func<double> Sequence(params double[] values)
        {
            var index = 0;
            return () => values[Math.Min(index++, values.Length - 1)];
        }

        private static GridLocation Loc(int x, int y)
            => new GridLocation(new GridCoord(x, y), WorldConstants.SurfacePlane, 0);

        private sealed class Setup
        {
            public Setup(
                AuthoredWorldPageStore world,
                ItemCatalog items,
                PvpService pvp,
                RangedAmmunitionService ammunition,
                PvpCombatService combat,
                PlayerState attacker,
                PlayerState defender)
            {
                World = world;
                Items = items;
                Pvp = pvp;
                Ammunition = ammunition;
                Combat = combat;
                Attacker = attacker;
                Defender = defender;
            }

            public AuthoredWorldPageStore World { get; }
            public ItemCatalog Items { get; }
            public PvpService Pvp { get; }
            public RangedAmmunitionService Ammunition { get; }
            public PvpCombatService Combat { get; }
            public PlayerState Attacker { get; }
            public PlayerState Defender { get; }
        }
    }
}
