using System;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Skills;
using MassRPG.Data.Effects;
using MassRPG.Server.Effects;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class StatusEffectPersistenceSnapshotTests
    {
        private static readonly ContentId PotionId = new ContentId("potion.persist");
        private static readonly ContentId EffectId = new ContentId("effect.persist");

        [Test]
        public void RoundTripPreservesAbsoluteExpiryInsteadOfRestartingDuration()
        {
            var catalog = new PotionEffectCatalog();
            var definition = new PotionEffectDefinition(
                PotionId,
                EffectId,
                10_000,
                new[] { new SkillLevelModifier(SkillId.Attack, 5) });
            catalog.Register(definition);
            var player = new PlayerState(Guid.NewGuid(), "Persistent Effects");
            var source = new StatusEffectService();
            source.Apply(player, definition, 1_000);

            var snapshot = StatusEffectPersistenceSnapshotCodec.Capture(source, player, 4_000);
            Assert.AreEqual(1, snapshot.Effects.Count);
            Assert.AreEqual(11_000, snapshot.Effects[0].ExpiresAtUnixMilliseconds);

            var restored = new StatusEffectService();
            StatusEffectPersistenceSnapshotCodec.Restore(snapshot, restored, player, catalog, 7_000);

            Assert.AreEqual(player.Skills.GetLevel(SkillId.Attack) + 5,
                restored.GetEffectiveLevel(player, SkillId.Attack, 10_999));
            Assert.AreEqual(player.Skills.GetLevel(SkillId.Attack),
                restored.GetEffectiveLevel(player, SkillId.Attack, 11_000));
        }

        [Test]
        public void AlreadyExpiredSavedEffectIsDiscardedEvenIfItsDefinitionWasRemoved()
        {
            var player = new PlayerState(Guid.NewGuid(), "Expired Effects");
            var snapshot = new CharacterStatusEffectSnapshot(
                CharacterStatusEffectSnapshot.CurrentVersion,
                new[] { new ActiveStatusEffectSnapshot(new ContentId("effect.removed"), 5_000) });
            var service = new StatusEffectService();

            Assert.DoesNotThrow(() => StatusEffectPersistenceSnapshotCodec.Restore(
                snapshot,
                service,
                player,
                new PotionEffectCatalog(),
                5_000));
            Assert.AreEqual(0, service.GetActiveEffects(player, 5_000).Count);
        }

        [Test]
        public void UnknownActiveEffectRejectsRestoreBeforeClearingExistingRuntimeState()
        {
            var knownCatalog = new PotionEffectCatalog();
            var known = new PotionEffectDefinition(
                PotionId,
                EffectId,
                10_000,
                new[] { new SkillLevelModifier(SkillId.Strength, 4) });
            knownCatalog.Register(known);
            var player = new PlayerState(Guid.NewGuid(), "Atomic Effects");
            var service = new StatusEffectService();
            service.Apply(player, known, 1_000);
            var baseStrength = player.Skills.GetLevel(SkillId.Strength);
            var badSnapshot = new CharacterStatusEffectSnapshot(
                CharacterStatusEffectSnapshot.CurrentVersion,
                new[] { new ActiveStatusEffectSnapshot(new ContentId("effect.unknown"), 20_000) });

            Assert.Throws<InvalidOperationException>(() => StatusEffectPersistenceSnapshotCodec.Restore(
                badSnapshot,
                service,
                player,
                knownCatalog,
                2_000));

            Assert.AreEqual(baseStrength + 4,
                service.GetEffectiveLevel(player, SkillId.Strength, 2_001));
        }

        [Test]
        public void DuplicateEffectIdsAreRejectedAsCorruptSnapshot()
        {
            var catalog = new PotionEffectCatalog();
            catalog.Register(new PotionEffectDefinition(PotionId, EffectId, 10_000));
            var player = new PlayerState(Guid.NewGuid(), "Duplicates");
            var snapshot = new CharacterStatusEffectSnapshot(
                CharacterStatusEffectSnapshot.CurrentVersion,
                new[]
                {
                    new ActiveStatusEffectSnapshot(EffectId, 10_000),
                    new ActiveStatusEffectSnapshot(EffectId, 12_000)
                });

            Assert.Throws<InvalidOperationException>(() => StatusEffectPersistenceSnapshotCodec.Restore(
                snapshot,
                new StatusEffectService(),
                player,
                catalog,
                1_000));
        }
    }
}
