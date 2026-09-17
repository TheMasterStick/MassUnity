using System;
using MassRPG.Core.Characters;
using MassRPG.Core.Combat;
using MassRPG.Core.Content;
using MassRPG.Core.Creatures;
using MassRPG.Core.World;
using MassRPG.Data.Creatures;
using MassRPG.Server.Combat;
using MassRPG.Server.Creatures;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class CombatRewardPresenceSourceTests
    {
        [Test]
        public void RangeUsesNearestOccupiedCreatureTileAndSameLogicalLayer()
        {
            var catalog = new CreatureCatalog();
            var id = new ContentId("test.large_creature");
            var definition = new CreatureDefinition(
                id,
                "Large Creature",
                combatLevel: 10,
                maxHitpoints: 20,
                attackLevel: 10,
                strengthLevel: 10,
                defenceLevel: 10,
                attackBonus: 0,
                strengthBonus: 0,
                defenceBonus: 0,
                combatStyle: CombatStyle.Melee,
                attackIntervalMilliseconds: 2400,
                attackRangeTiles: 1,
                disposition: CreatureDisposition.Neutral,
                footprint: new CreatureFootprint(2, 2),
                aggroRadiusTiles: 0,
                leashRadiusTiles: 8);
            catalog.Register(definition);
            var creature = CreatureState.Spawn(Guid.NewGuid(), definition, Loc(10, 10));
            var source = new DistanceCombatRewardPresenceSource(catalog, rewardRangeTiles: 2);
            var near = new PlayerState(Guid.NewGuid(), "Near") { Location = Loc(13, 11) };
            var far = new PlayerState(Guid.NewGuid(), "Far") { Location = Loc(14, 11) };
            var otherStorey = new PlayerState(Guid.NewGuid(), "Other storey")
            {
                Location = new GridLocation(new GridCoord(11, 11), WorldConstants.SurfacePlane, 1)
            };

            var result = source.BuildPresences(creature, new[] { near, far, otherStorey });

            Assert.AreEqual(3, result.Count);
            Assert.IsTrue(Find(result, near.CharacterId).InRewardRange);
            Assert.IsFalse(Find(result, far.CharacterId).InRewardRange);
            Assert.IsFalse(Find(result, otherStorey.CharacterId).InRewardRange);
        }

        [Test]
        public void RewardRangeHasNoHiddenDefaultAndMustBeExplicitlyNonNegative()
        {
            var catalog = MigrationSeedCreatureCatalog.Create();
            Assert.Throws<ArgumentOutOfRangeException>(() => new DistanceCombatRewardPresenceSource(catalog, -1));
            Assert.AreEqual(0, new DistanceCombatRewardPresenceSource(catalog, 0).RewardRangeTiles);
        }

        private static CombatRewardPresence Find(System.Collections.Generic.IReadOnlyList<CombatRewardPresence> values, Guid characterId)
        {
            for (var i = 0; i < values.Count; i++)
                if (values[i].CharacterId == characterId) return values[i];
            Assert.Fail("Missing presence for character " + characterId);
            return default;
        }

        private static GridLocation Loc(int x, int y)
            => new GridLocation(new GridCoord(x, y), WorldConstants.SurfacePlane, 0);
    }
}
