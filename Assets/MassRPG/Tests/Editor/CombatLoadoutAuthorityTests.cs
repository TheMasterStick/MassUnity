using System;
using MassRPG.Core.Authority;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Data.Items;
using MassRPG.Server.Authority;
using MassRPG.Server.Combat;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class CombatLoadoutAuthorityTests
    {
        private static readonly ContentId ArrowId = new ContentId("bronze_arrow");

        [Test]
        public void CombatStyleSelectionIsAppliedThroughAuthority()
        {
            var items = MigrationSeedItemCatalog.Create();
            var authority = new LocalGameAuthority(items);
            var player = new PlayerState(Guid.NewGuid(), "Style Tester");
            authority.RegisterPlayer(player);

            var result = authority.Submit(new SelectCombatStyleRequest(
                Guid.NewGuid(), player.CharacterId, CombatStyle.Ranged), 1000);

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(CombatStyle.Ranged, player.CombatStyle);
        }

        [Test]
        public void MeleeTrainingSelectionForcesMeleeLikeLegacyCombatTab()
        {
            var items = MigrationSeedItemCatalog.Create();
            var authority = new LocalGameAuthority(items);
            var player = new PlayerState(Guid.NewGuid(), "Training Tester")
            {
                CombatStyle = CombatStyle.Magic
            };
            authority.RegisterPlayer(player);

            var result = authority.Submit(new SelectMeleeTrainingStyleRequest(
                Guid.NewGuid(), player.CharacterId, MeleeTrainingStyle.Defensive), 1000);

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(CombatStyle.Melee, player.CombatStyle);
            Assert.AreEqual(MeleeTrainingStyle.Defensive, player.MeleeTrainingStyle);
        }

        [Test]
        public void RangedAmmunitionCanBeSelectedAndClearedThroughAuthority()
        {
            var items = MigrationSeedItemCatalog.Create();
            var ammunition = new RangedAmmunitionService(items);
            var authority = new LocalGameAuthority(items, rangedAmmunition: ammunition);
            var player = new PlayerState(Guid.NewGuid(), "Ammo Tester");
            authority.RegisterPlayer(player);
            Assert.AreEqual(3, InventoryRules.AddItem(player.Inventory, items, ArrowId, 3));

            var selected = authority.Submit(new SelectRangedAmmunitionRequest(
                Guid.NewGuid(), player.CharacterId, ArrowId), 1000);

            Assert.IsTrue(selected.Accepted);
            Assert.AreEqual(ArrowId, player.SelectedAmmunitionItemId.Value);
            Assert.AreEqual(3, player.Inventory.CountItem(ArrowId));

            var cleared = authority.Submit(new ClearRangedAmmunitionRequest(
                Guid.NewGuid(), player.CharacterId), 1001);

            Assert.IsTrue(cleared.Accepted);
            Assert.IsFalse(player.SelectedAmmunitionItemId.HasValue);
            Assert.AreEqual(3, player.Inventory.CountItem(ArrowId));
        }

        [Test]
        public void AmmunitionSelectionWithoutConfiguredServiceIsRejectedInsteadOfMutatingState()
        {
            var items = MigrationSeedItemCatalog.Create();
            var authority = new LocalGameAuthority(items);
            var player = new PlayerState(Guid.NewGuid(), "No Ammo Service");
            authority.RegisterPlayer(player);
            Assert.AreEqual(1, InventoryRules.AddItem(player.Inventory, items, ArrowId, 1));

            var result = authority.Submit(new SelectRangedAmmunitionRequest(
                Guid.NewGuid(), player.CharacterId, ArrowId), 1000);

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual("ranged_ammunition_unavailable", result.Code);
            Assert.IsFalse(player.SelectedAmmunitionItemId.HasValue);
        }

        [Test]
        public void LoadoutRequestsRejectUndefinedEnumValuesAtConstruction()
        {
            var characterId = Guid.NewGuid();
            Assert.Throws<ArgumentOutOfRangeException>(() => new SelectCombatStyleRequest(
                Guid.NewGuid(), characterId, (CombatStyle)999));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SelectMeleeTrainingStyleRequest(
                Guid.NewGuid(), characterId, (MeleeTrainingStyle)999));
        }
    }
}
