using System;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Core.World;
using MassRPG.Data.Items;
using MassRPG.Data.World;
using MassRPG.Data.World.Semantics;
using MassRPG.Server.Combat;
using MassRPG.Server.Death;
using MassRPG.Server.Items;
using MassRPG.Server.Pvp;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class PvpCombatDeathIntegrationTests
    {
        private static readonly ContentId Grass = new ContentId("terrain.grass");

        [Test]
        public void LethalAttackSettlesAndRespawnsDefenderWhenDeathServiceIsConfigured()
        {
            var setup = CreateSetup();
            setup.Defender.CurrentHitpoints = 1;

            var result = setup.Combat.TryAttack(
                setup.Attacker,
                setup.Defender,
                1000,
                Sequence(0.0, 0.99));

            Assert.AreEqual(PvpAttackKind.TargetKilled, result.Kind);
            Assert.IsTrue(result.DeathResolved);
            Assert.AreEqual("target_killed_respawned", result.Code);
            Assert.AreEqual(setup.Settlement, setup.Defender.Location);
            Assert.AreEqual(setup.Defender.MaxHitpoints, setup.Defender.CurrentHitpoints);
            Assert.IsFalse(setup.Defender.Combat.IsActive);
            Assert.IsTrue(setup.Pvp.GetOrCreate(setup.Attacker.CharacterId).IsSkulled(1001));
        }

        [Test]
        public void LethalAttackUsesExistingDefenderSkullForFullInventoryAndEquipmentLoss()
        {
            var setup = CreateSetup();
            setup.Pvp.RestoreStatus(setup.Defender.CharacterId, new PlayerPvpStatusSnapshot(true, 5000));
            InventoryRules.AddItem(setup.Defender.Inventory, setup.Items, new ContentId("bread"), 2);
            InventoryRules.AddItem(setup.Defender.Inventory, setup.Items, new ContentId("bronze_sword"), 1);
            var swordSlot = FindSlot(setup.Defender.Inventory, new ContentId("bronze_sword"));
            Assert.IsTrue(InventoryRules.EquipFromInventory(
                setup.Defender.Inventory,
                setup.Defender.Equipment,
                setup.Items,
                setup.Defender.Skills,
                swordSlot,
                EquipmentSlot.MainHand).Success);
            setup.Defender.CurrentHitpoints = 1;

            var result = setup.Combat.TryAttack(
                setup.Attacker,
                setup.Defender,
                1000,
                Sequence(0.0, 0.99));

            Assert.IsTrue(result.DeathResolved);
            Assert.AreEqual(1, result.DeathResult.Value.StacksDropped);
            Assert.AreEqual(1, result.DeathResult.Value.EquipmentDropped);
            Assert.AreEqual(0, setup.Defender.Inventory.CountItem(new ContentId("bread")));
            Assert.IsFalse(setup.Defender.Equipment.IsOccupied(EquipmentSlot.MainHand));

            var groundCount = 0;
            var protectedForKiller = 0;
            foreach (var item in setup.GroundItems.All)
            {
                groundCount++;
                foreach (var recipient in item.ProtectedRecipients)
                    if (recipient == setup.Attacker.CharacterId) protectedForKiller++;
            }
            Assert.AreEqual(2, groundCount);
            Assert.AreEqual(2, protectedForKiller);
        }

        private static Setup CreateSetup()
        {
            var world = new AuthoredWorldPageStore(Grass, 32);
            world.GetOrCreatePage(Loc(0, 0));
            var items = MigrationSeedItemCatalog.Create();
            var pvp = new PvpService(new WorldSemanticCatalog(), new PvpPolicy(true, 5000));
            var groundRegistry = new GroundItemRegistry();
            var groundItems = new GroundItemService(items, groundRegistry);
            var respawns = new PlayerRespawnRegistry();
            var settlement = Loc(4, 4);
            var death = new PvpDeathService(
                respawns,
                new FixedSettlement(settlement),
                new ConfiguredInventoryDropSelector(0, PlayerDeathService.CoinId),
                new PvpDeathPenaltyPolicy(0, 0, true, true, 5000, 60000),
                groundItems,
                items);
            var profiles = new DataDrivenPlayerAttackProfileSource(items);
            var ammunition = new RangedAmmunitionService(items);
            var combat = new PvpCombatService(pvp, profiles, world, world, ammunition, death);
            var attacker = new PlayerState(Guid.NewGuid(), "Attacker") { Location = Loc(10, 10) };
            var defender = new PlayerState(Guid.NewGuid(), "Defender") { Location = Loc(11, 10) };
            pvp.SetOptIn(attacker.CharacterId, true);
            pvp.SetOptIn(defender.CharacterId, true);
            return new Setup(items, pvp, groundRegistry, combat, attacker, defender, settlement);
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

        private sealed class FixedSettlement : ISettlementRespawnSource
        {
            private readonly GridLocation _location;
            public FixedSettlement(GridLocation location) => _location = location;
            public bool TryFindNearest(GridLocation deathLocation, out GridLocation respawnLocation)
            {
                respawnLocation = _location;
                return true;
            }
        }

        private sealed class Setup
        {
            public Setup(
                ItemCatalog items,
                PvpService pvp,
                GroundItemRegistry groundItems,
                PvpCombatService combat,
                PlayerState attacker,
                PlayerState defender,
                GridLocation settlement)
            {
                Items = items;
                Pvp = pvp;
                GroundItems = groundItems;
                Combat = combat;
                Attacker = attacker;
                Defender = defender;
                Settlement = settlement;
            }

            public ItemCatalog Items { get; }
            public PvpService Pvp { get; }
            public GroundItemRegistry GroundItems { get; }
            public PvpCombatService Combat { get; }
            public PlayerState Attacker { get; }
            public PlayerState Defender { get; }
            public GridLocation Settlement { get; }
        }
    }
}
