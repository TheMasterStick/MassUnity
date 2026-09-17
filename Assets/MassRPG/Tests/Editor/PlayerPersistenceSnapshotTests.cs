using System;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Core.Persistence;
using MassRPG.Core.Skills;
using MassRPG.Core.World;
using MassRPG.Data.Items;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class PlayerPersistenceSnapshotTests
    {
        [Test]
        public void CaptureRestoreRoundTripsAuthoritativeCharacterStateWithoutUnityObjects()
        {
            var items = MigrationSeedItemCatalog.Create();
            var player = new PlayerState(Guid.NewGuid(), "Saved Hero");
            player.Location = new GridLocation(new GridCoord(12345, 67890), -1, 0);
            player.CombatStyle = CombatStyle.Ranged;
            player.MeleeTrainingStyle = MeleeTrainingStyle.Defensive;
            player.Skills.SetXp(SkillId.Ranged, SkillProgression.XpForLevel(20));
            player.Skills.SetXp(SkillId.Hitpoints, SkillProgression.XpForLevel(15));
            player.CurrentHitpoints = 12;

            InventoryRules.AddItem(player.Inventory, items, new ContentId("normal_shortbow"), 1);
            InventoryRules.AddItem(player.Inventory, items, new ContentId("bronze_arrow"), 25);
            InventoryRules.AddItem(player.Inventory, items, new ContentId("coins"), 321);
            var bowSlot = FindSlot(player.Inventory, new ContentId("normal_shortbow"));
            Assert.IsTrue(InventoryRules.EquipFromInventory(
                player.Inventory, player.Equipment, items, player.Skills, bowSlot, EquipmentSlot.MainHand).Success);
            player.SelectedAmmunitionItemId = new ContentId("bronze_arrow");

            var snapshot = PlayerStateSnapshotCodec.Capture(player);
            var restored = PlayerStateSnapshotCodec.Restore(snapshot, items);

            Assert.AreEqual(PlayerStateSnapshot.CurrentVersion, snapshot.Version);
            Assert.AreEqual(player.CharacterId, restored.CharacterId);
            Assert.AreEqual(player.Name, restored.Name);
            Assert.AreEqual(player.Location, restored.Location);
            Assert.AreEqual(player.CombatStyle, restored.CombatStyle);
            Assert.AreEqual(player.MeleeTrainingStyle, restored.MeleeTrainingStyle);
            Assert.AreEqual(player.Skills.GetXp(SkillId.Ranged), restored.Skills.GetXp(SkillId.Ranged));
            Assert.AreEqual(player.Skills.GetXp(SkillId.Hitpoints), restored.Skills.GetXp(SkillId.Hitpoints));
            Assert.AreEqual(12, restored.CurrentHitpoints);
            Assert.AreEqual(25, restored.Inventory.CountItem(new ContentId("bronze_arrow")));
            Assert.AreEqual(321, restored.Inventory.CountItem(new ContentId("coins")));
            Assert.AreEqual(new ContentId("normal_shortbow"), restored.Equipment.GetOrNull(EquipmentSlot.MainHand));
            Assert.AreEqual(new ContentId("bronze_arrow"), restored.SelectedAmmunitionItemId.Value);
            Assert.IsFalse(restored.Combat.IsActive);
            Assert.IsFalse(restored.Movement.IsMoving);
            Assert.IsFalse(restored.Production.IsActive);
        }

        [Test]
        public void RestoreRejectsFutureSnapshotVersionInsteadOfGuessingMigration()
        {
            var items = MigrationSeedItemCatalog.Create();
            var snapshot = new PlayerStateSnapshot(
                PlayerStateSnapshot.CurrentVersion + 1,
                Guid.NewGuid(),
                "Future",
                new GridLocation(new GridCoord(10, 10), 0, 0),
                10,
                CombatStyle.Melee,
                MeleeTrainingStyle.Aggressive,
                28,
                null,
                Array.Empty<SkillXpSnapshot>(),
                Array.Empty<InventorySlotSnapshot>(),
                Array.Empty<EquipmentSlotSnapshot>());

            Assert.Throws<InvalidOperationException>(() => PlayerStateSnapshotCodec.Restore(snapshot, items));
        }

        [Test]
        public void RestoreRejectsStackedNonStackableItem()
        {
            var items = MigrationSeedItemCatalog.Create();
            var snapshot = new PlayerStateSnapshot(
                PlayerStateSnapshot.CurrentVersion,
                Guid.NewGuid(),
                "Corrupt",
                new GridLocation(new GridCoord(10, 10), 0, 0),
                10,
                CombatStyle.Melee,
                MeleeTrainingStyle.Aggressive,
                28,
                null,
                Array.Empty<SkillXpSnapshot>(),
                new[] { new InventorySlotSnapshot(0, new ContentId("bronze_sword"), 2) },
                Array.Empty<EquipmentSlotSnapshot>());

            Assert.Throws<InvalidOperationException>(() => PlayerStateSnapshotCodec.Restore(snapshot, items));
        }

        [Test]
        public void RestoreRejectsSelectedAmmunitionMissingFromInventory()
        {
            var items = MigrationSeedItemCatalog.Create();
            var snapshot = new PlayerStateSnapshot(
                PlayerStateSnapshot.CurrentVersion,
                Guid.NewGuid(),
                "Corrupt Ammo",
                new GridLocation(new GridCoord(10, 10), 0, 0),
                10,
                CombatStyle.Ranged,
                MeleeTrainingStyle.Aggressive,
                28,
                new ContentId("bronze_arrow"),
                Array.Empty<SkillXpSnapshot>(),
                Array.Empty<InventorySlotSnapshot>(),
                Array.Empty<EquipmentSlotSnapshot>());

            Assert.Throws<InvalidOperationException>(() => PlayerStateSnapshotCodec.Restore(snapshot, items));
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
    }
}
