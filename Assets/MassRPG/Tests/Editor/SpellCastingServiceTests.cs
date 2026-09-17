using System;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Core.Skills;
using MassRPG.Core.World;
using MassRPG.Data.Items;
using MassRPG.Data.Spells;
using MassRPG.Data.World;
using MassRPG.Server.Creatures;
using MassRPG.Server.Spells;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class SpellCastingServiceTests
    {
        private static readonly ContentId Grass = new ContentId("terrain.grass");

        [Test]
        public void DamageSpellConsumesReagentsAwardsMagicXpAndUsesCooldown()
        {
            var items = MigrationSeedItemCatalog.Create();
            var spells = new SpellCatalog();
            var spellId = new ContentId("spell.test_bolt");
            spells.Register(new SpellDefinition(
                spellId,
                "Test Bolt",
                5,
                SpellTargetKind.Creature,
                SpellEffectKind.Damage,
                6,
                2400,
                4,
                12,
                new[] { new SpellReagentCost(new ContentId("coins"), 2) }));
            var world = new AuthoredWorldPageStore(Grass, 32);
            world.GetOrCreatePage(Loc(0, 0));
            var service = new SpellCastingService(spells, items, world);
            var caster = new PlayerState(Guid.NewGuid(), "Mage") { Location = Loc(10, 10) };
            caster.Skills.SetXp(SkillId.Magic, SkillProgression.XpForLevel(5));
            InventoryRules.AddItem(caster.Inventory, items, new ContentId("coins"), 10);
            var target = new CreatureState(Guid.NewGuid(), new ContentId("goblin"), Loc(14, 10), 10);
            var beforeXp = caster.Skills.GetXp(SkillId.Magic);

            var cast = service.TryCastOnCreature(caster, spellId, target, 1000);
            Assert.IsTrue(cast.Success);
            Assert.AreEqual(4, cast.Magnitude);
            Assert.AreEqual(6, target.CurrentHitpoints);
            Assert.AreEqual(8, caster.Inventory.CountItem(new ContentId("coins")));
            Assert.AreEqual(beforeXp + 12, caster.Skills.GetXp(SkillId.Magic));
            Assert.AreEqual(3400, service.NextCastAt(caster.CharacterId));

            var cooldown = service.TryCastOnCreature(caster, spellId, target, 1001);
            Assert.IsFalse(cooldown.Success);
            Assert.AreEqual("cast_cooldown", cooldown.Code);
            Assert.AreEqual(8, caster.Inventory.CountItem(new ContentId("coins")));
        }

        [Test]
        public void FailedValidationNeverConsumesReagents()
        {
            var items = MigrationSeedItemCatalog.Create();
            var spells = new SpellCatalog();
            var spellId = new ContentId("spell.expensive");
            spells.Register(new SpellDefinition(
                spellId,
                "Expensive Spell",
                10,
                SpellTargetKind.Creature,
                SpellEffectKind.Damage,
                2,
                1000,
                8,
                20,
                new[] { new SpellReagentCost(new ContentId("coins"), 5) }));
            var world = new AuthoredWorldPageStore(Grass, 32);
            world.GetOrCreatePage(Loc(0, 0));
            var service = new SpellCastingService(spells, items, world);
            var caster = new PlayerState(Guid.NewGuid(), "Mage") { Location = Loc(1, 1) };
            caster.Skills.SetXp(SkillId.Magic, SkillProgression.XpForLevel(10));
            InventoryRules.AddItem(caster.Inventory, items, new ContentId("coins"), 5);
            var target = new CreatureState(Guid.NewGuid(), new ContentId("goblin"), Loc(10, 10), 10);

            var result = service.TryCastOnCreature(caster, spellId, target, 1000);
            Assert.IsFalse(result.Success);
            Assert.AreEqual("out_of_range", result.Code);
            Assert.AreEqual(5, caster.Inventory.CountItem(new ContentId("coins")));
            Assert.AreEqual(10, target.CurrentHitpoints);
        }

        [Test]
        public void SelfHealCannotExceedCurrentMaximumHitpoints()
        {
            var items = MigrationSeedItemCatalog.Create();
            var spells = new SpellCatalog();
            var spellId = new ContentId("spell.test_heal");
            spells.Register(new SpellDefinition(
                spellId,
                "Test Heal",
                1,
                SpellTargetKind.Self,
                SpellEffectKind.Heal,
                0,
                1000,
                20,
                5,
                requiresLineOfSight: false));
            var world = new AuthoredWorldPageStore(Grass, 32);
            world.GetOrCreatePage(Loc(0, 0));
            var service = new SpellCastingService(spells, items, world);
            var caster = new PlayerState(Guid.NewGuid(), "Mage") { Location = Loc(3, 3) };
            caster.CurrentHitpoints = 3;

            var result = service.TryCastSelf(caster, spellId, 1000);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(caster.MaxHitpoints, caster.CurrentHitpoints);
            Assert.AreEqual(caster.MaxHitpoints - 3, result.Magnitude);
        }

        private static GridLocation Loc(int x, int y)
            => new GridLocation(new GridCoord(x, y), WorldConstants.SurfacePlane, 0);
    }
}
