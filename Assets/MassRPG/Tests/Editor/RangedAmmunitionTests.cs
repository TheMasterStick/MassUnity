using System;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Core.World;
using MassRPG.Data.Creatures;
using MassRPG.Data.Items;
using MassRPG.Data.World;
using MassRPG.Server.Combat;
using MassRPG.Server.Creatures;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class RangedAmmunitionTests
    {
        private static readonly ContentId Grass = new ContentId("terrain.grass");
        private static readonly ContentId BowId = new ContentId("normal_shortbow");
        private static readonly ContentId ArrowId = new ContentId("bronze_arrow");

        [Test]
        public void SelectingArrowsKeepsThemInInventoryAndAddsRangedStrengthToProfile()
        {
            var setup = CreateSetup();
            EquipBowAndAddArrows(setup, 5);
            setup.Player.CombatStyle = CombatStyle.Ranged;
            var before = setup.Profiles.Resolve(setup.Player).RangedStrengthBonus;

            var selected = setup.Ammunition.Select(setup.Player, ArrowId);
            var after = setup.Profiles.Resolve(setup.Player).RangedStrengthBonus;

            Assert.IsTrue(selected.Success);
            Assert.AreEqual(5, setup.Player.Inventory.CountItem(ArrowId));
            Assert.AreEqual(ArrowId, setup.Player.SelectedAmmunitionItemId.Value);
            Assert.Greater(after, before);
            Assert.IsFalse(setup.Player.Equipment.IsOccupied(EquipmentSlot.OffHand));
        }

        [Test]
        public void ActualRangedAttackConsumesExactlyOneArrowAndLastArrowClearsSelection()
        {
            var setup = CreateSetup();
            EquipBowAndAddArrows(setup, 1);
            Assert.IsTrue(setup.Ammunition.Select(setup.Player, ArrowId).Success);
            var target = Spawn(setup, "cow", Loc(15, 10));
            setup.Player.Location = Loc(10, 10);
            setup.Player.CombatStyle = CombatStyle.Ranged;
            setup.Player.Combat.Begin(target.InstanceId);

            var result = setup.Combat.AdvancePlayerAttack(setup.Player, 1000, Sequence(0.0, 0.5));

            Assert.IsTrue(result.DidAttack);
            Assert.AreEqual(0, setup.Player.Inventory.CountItem(ArrowId));
            Assert.IsFalse(setup.Player.SelectedAmmunitionItemId.HasValue);
        }

        [Test]
        public void ApproachAndCooldownDoNotConsumeArrows()
        {
            var setup = CreateSetup(fallbackRange: 2);
            EquipBowAndAddArrows(setup, 3);
            Assert.IsTrue(setup.Ammunition.Select(setup.Player, ArrowId).Success);
            // The migrated shortbow carries its own six-tile range, which correctly overrides the
            // profile fallback. Start outside that authored weapon range so the first tick must
            // approach before any ammunition can be consumed.
            var target = Spawn(setup, "cow", Loc(20, 10));
            setup.Player.Location = Loc(10, 10);
            setup.Player.CombatStyle = CombatStyle.Ranged;
            setup.Player.Combat.Begin(target.InstanceId);

            var approaching = setup.Combat.AdvancePlayerAttack(setup.Player, 1000, Sequence(0.0, 0.5));
            Assert.AreEqual(CombatAdvanceKind.Approaching, approaching.Kind);
            Assert.AreEqual(3, setup.Player.Inventory.CountItem(ArrowId));

            setup.Player.Location = Loc(14, 10);
            var attack = setup.Combat.AdvancePlayerAttack(setup.Player, 1001, Sequence(0.0, 0.0));
            Assert.IsTrue(attack.DidAttack);
            Assert.AreEqual(2, setup.Player.Inventory.CountItem(ArrowId));

            var cooldown = setup.Combat.AdvancePlayerAttack(setup.Player, 1002, Sequence(0.0, 0.0));
            Assert.AreEqual(CombatAdvanceKind.WaitingForCooldown, cooldown.Kind);
            Assert.AreEqual(2, setup.Player.Inventory.CountItem(ArrowId));
        }

        [Test]
        public void RangedAttackWithMissingBowOrAmmoFailsBeforeRollingDamage()
        {
            var setup = CreateSetup();
            var target = Spawn(setup, "cow", Loc(15, 10));
            setup.Player.Location = Loc(10, 10);
            setup.Player.CombatStyle = CombatStyle.Ranged;
            setup.Player.Combat.Begin(target.InstanceId);

            var noBow = setup.Combat.AdvancePlayerAttack(setup.Player, 1000, Sequence(0.0, 0.5));
            Assert.AreEqual(CombatAdvanceKind.Failed, noBow.Kind);
            Assert.AreEqual("ranged_weapon_missing", noBow.Code);

            setup.Player.Combat.Begin(target.InstanceId);
            EquipBowAndAddArrows(setup, 1);
            var noSelection = setup.Combat.AdvancePlayerAttack(setup.Player, 1001, Sequence(0.0, 0.5));
            Assert.AreEqual(CombatAdvanceKind.Failed, noSelection.Kind);
            Assert.AreEqual("ranged_ammunition_missing", noSelection.Code);
            Assert.AreEqual(1, setup.Player.Inventory.CountItem(ArrowId));
        }

        [Test]
        public void SelectingNonAmmunitionIsRejected()
        {
            var setup = CreateSetup();
            InventoryRules.AddItem(setup.Player.Inventory, setup.Items, new ContentId("coins"), 10);

            var result = setup.Ammunition.Select(setup.Player, new ContentId("coins"));

            Assert.IsFalse(result.Success);
            Assert.AreEqual("invalid_ranged_ammunition", result.Code);
            Assert.IsFalse(setup.Player.SelectedAmmunitionItemId.HasValue);
        }

        private static Setup CreateSetup(int fallbackRange = 6)
        {
            var map = new AuthoredWorldPageStore(Grass, 32);
            map.GetOrCreatePage(Loc(0, 0));
            var items = MigrationSeedItemCatalog.Create();
            var definitions = MigrationSeedCreatureCatalog.Create();
            var registry = new CreatureRegistry();
            var occupancy = new CreatureOccupancyIndex();
            var profiles = new DataDrivenPlayerAttackProfileSource(items, fallbackRangedRangeTiles: fallbackRange);
            var ammunition = new RangedAmmunitionService(items);
            var approach = new CombatApproachPlanner(map, map);
            var combat = new CombatSimulationService(registry, definitions, profiles, approach, map, ammunition: ammunition);
            var player = new PlayerState(Guid.NewGuid(), "Archer");
            return new Setup(map, items, definitions, registry, occupancy, profiles, ammunition, combat, player);
        }

        private static void EquipBowAndAddArrows(Setup setup, int arrowQuantity)
        {
            InventoryRules.AddItem(setup.Player.Inventory, setup.Items, BowId, 1);
            var bowSlot = FindSlot(setup.Player.Inventory, BowId);
            Assert.GreaterOrEqual(bowSlot, 0);
            Assert.IsTrue(InventoryRules.EquipFromInventory(
                setup.Player.Inventory,
                setup.Player.Equipment,
                setup.Items,
                setup.Player.Skills,
                bowSlot,
                EquipmentSlot.MainHand).Success);
            Assert.AreEqual(arrowQuantity, InventoryRules.AddItem(setup.Player.Inventory, setup.Items, ArrowId, arrowQuantity));
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

        private static CreatureState Spawn(Setup setup, string definitionId, GridLocation location)
        {
            Assert.IsTrue(setup.Definitions.TryGet(new ContentId(definitionId), out var definition));
            var creature = CreatureState.Spawn(Guid.NewGuid(), definition, location);
            setup.Registry.Register(creature);
            Assert.IsTrue(setup.Occupancy.TryPlace(creature.InstanceId, location, definition.Footprint));
            return creature;
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
                AuthoredWorldPageStore map,
                ItemCatalog items,
                CreatureCatalog definitions,
                CreatureRegistry registry,
                CreatureOccupancyIndex occupancy,
                DataDrivenPlayerAttackProfileSource profiles,
                RangedAmmunitionService ammunition,
                CombatSimulationService combat,
                PlayerState player)
            {
                Map = map;
                Items = items;
                Definitions = definitions;
                Registry = registry;
                Occupancy = occupancy;
                Profiles = profiles;
                Ammunition = ammunition;
                Combat = combat;
                Player = player;
            }

            public AuthoredWorldPageStore Map { get; }
            public ItemCatalog Items { get; }
            public CreatureCatalog Definitions { get; }
            public CreatureRegistry Registry { get; }
            public CreatureOccupancyIndex Occupancy { get; }
            public DataDrivenPlayerAttackProfileSource Profiles { get; }
            public RangedAmmunitionService Ammunition { get; }
            public CombatSimulationService Combat { get; }
            public PlayerState Player { get; }
        }
    }
}
