using System;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Core.Skills;
using MassRPG.Core.World;
using MassRPG.Data.Items;
using MassRPG.Data.Spells;
using MassRPG.Data.World;
using MassRPG.Data.World.Semantics;
using MassRPG.Server.Pvp;
using MassRPG.Server.Spells;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class PvpSpellCastingTests
    {
        private static readonly ContentId Grass = new ContentId("terrain.grass");

        [Test]
        public void PlayerDamageSpellRequiresPvpPermissionBeforeConsumingReagents()
        {
            var setup = CreateSetup();
            InventoryRules.AddItem(setup.Caster.Inventory, setup.Items, new ContentId("coins"), 10);
            setup.Caster.Skills.SetXp(SkillId.Magic, SkillProgression.XpForLevel(10));

            var denied = setup.Spells.TryCastOnPlayer(setup.Caster, setup.SpellId, setup.Target, 1000);
            Assert.IsFalse(denied.Success);
            Assert.AreEqual("attacker_not_opted_in", denied.Code);
            Assert.AreEqual(10, setup.Caster.Inventory.CountItem(new ContentId("coins")));
            Assert.AreEqual(setup.Target.MaxHitpoints, setup.Target.CurrentHitpoints);

            setup.Pvp.SetOptIn(setup.Caster.CharacterId, true);
            setup.Pvp.SetOptIn(setup.Target.CharacterId, true);
            var cast = setup.Spells.TryCastOnPlayer(setup.Caster, setup.SpellId, setup.Target, 1001);
            Assert.IsTrue(cast.Success);
            Assert.AreEqual(4, cast.Magnitude);
            Assert.AreEqual(8, setup.Caster.Inventory.CountItem(new ContentId("coins")));
            Assert.IsTrue(setup.Pvp.GetOrCreate(setup.Caster.CharacterId).IsSkulled(1002));
            Assert.IsTrue(setup.Caster.Combat.IsActive);
            Assert.IsTrue(setup.Target.Combat.IsActive);
        }

        [Test]
        public void ProtectedAreaBlocksPlayerSpellWithoutCostOrCooldown()
        {
            var semantics = new WorldSemanticCatalog();
            semantics.RegisterArea(new WorldAreaDefinition(
                new ContentId("area.safe"), "Town", WorldAreaKind.PvpProtected,
                new CircleAreaShape(new GridCoord(10, 10), 5)));
            var setup = CreateSetup(semantics);
            setup.Pvp.SetOptIn(setup.Caster.CharacterId, true);
            setup.Pvp.SetOptIn(setup.Target.CharacterId, true);
            setup.Caster.Skills.SetXp(SkillId.Magic, SkillProgression.XpForLevel(10));
            InventoryRules.AddItem(setup.Caster.Inventory, setup.Items, new ContentId("coins"), 10);

            var result = setup.Spells.TryCastOnPlayer(setup.Caster, setup.SpellId, setup.Target, 1000);

            Assert.IsFalse(result.Success);
            Assert.AreEqual("pvp_protected", result.Code);
            Assert.AreEqual(10, setup.Caster.Inventory.CountItem(new ContentId("coins")));
            Assert.AreEqual(0, setup.Spells.NextCastAt(setup.Caster.CharacterId));
        }

        [Test]
        public void ForcedPvpAreaAllowsPlayerDamageSpellWithoutVoluntaryFlags()
        {
            var semantics = new WorldSemanticCatalog();
            semantics.RegisterArea(new WorldAreaDefinition(
                new ContentId("area.forced"), "Danger Zone", WorldAreaKind.ForcedPvP,
                new CircleAreaShape(new GridCoord(10, 10), 5)));
            var setup = CreateSetup(semantics);
            setup.Caster.Skills.SetXp(SkillId.Magic, SkillProgression.XpForLevel(10));
            InventoryRules.AddItem(setup.Caster.Inventory, setup.Items, new ContentId("coins"), 10);

            var result = setup.Spells.TryCastOnPlayer(setup.Caster, setup.SpellId, setup.Target, 1000);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(4, result.Magnitude);
            Assert.AreEqual(8, setup.Caster.Inventory.CountItem(new ContentId("coins")));
        }

        private static Setup CreateSetup(WorldSemanticCatalog semantics = null)
        {
            var world = new AuthoredWorldPageStore(Grass, 32);
            world.GetOrCreatePage(Loc(0, 0));
            var items = MigrationSeedItemCatalog.Create();
            var spellId = new ContentId("spell.pvp_test");
            var catalog = new SpellCatalog();
            catalog.Register(new SpellDefinition(
                spellId,
                "PvP Test",
                10,
                SpellTargetKind.Player,
                SpellEffectKind.Damage,
                6,
                2400,
                4,
                15,
                new[] { new SpellReagentCost(new ContentId("coins"), 2) }));
            var pvp = new PvpService(semantics ?? new WorldSemanticCatalog(), new PvpPolicy(true, 5000));
            var spells = new SpellCastingService(catalog, items, world, pvp);
            var caster = new PlayerState(Guid.NewGuid(), "Caster") { Location = Loc(10, 10) };
            var target = new PlayerState(Guid.NewGuid(), "Target") { Location = Loc(12, 10) };
            return new Setup(items, spellId, pvp, spells, caster, target);
        }

        private static GridLocation Loc(int x, int y)
            => new GridLocation(new GridCoord(x, y), WorldConstants.SurfacePlane, 0);

        private sealed class Setup
        {
            public Setup(ItemCatalog items, ContentId spellId, PvpService pvp, SpellCastingService spells, PlayerState caster, PlayerState target)
            {
                Items = items;
                SpellId = spellId;
                Pvp = pvp;
                Spells = spells;
                Caster = caster;
                Target = target;
            }

            public ItemCatalog Items { get; }
            public ContentId SpellId { get; }
            public PvpService Pvp { get; }
            public SpellCastingService Spells { get; }
            public PlayerState Caster { get; }
            public PlayerState Target { get; }
        }
    }
}
