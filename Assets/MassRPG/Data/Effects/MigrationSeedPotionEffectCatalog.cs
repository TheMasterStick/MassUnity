using System;
using MassRPG.Core.Content;
using MassRPG.Core.Skills;

namespace MassRPG.Data.Effects
{
    /// <summary>
    /// Browser-parity potion mappings whose effect magnitudes were explicit in the old content data.
    /// Durations are required inputs because the browser content did not provide trustworthy live
    /// durations and migration should not turn an arbitrary guess into canonical balance.
    /// Prayer potion remains intentionally unmapped until MassRPG has an actual Prayer resource/skill.
    /// </summary>
    public static class MigrationSeedPotionEffectCatalog
    {
        public static readonly ContentId AttackPotionId = new ContentId("attack_potion");
        public static readonly ContentId StrengthPotionId = new ContentId("strength_potion");
        public static readonly ContentId AntipoisonId = new ContentId("antipoison");

        public static readonly ContentId AttackBoostEffectId = new ContentId("effect.potion.attack_boost");
        public static readonly ContentId StrengthBoostEffectId = new ContentId("effect.potion.strength_boost");
        public static readonly ContentId AntipoisonEffectId = new ContentId("effect.potion.antipoison");

        public static PotionEffectCatalog Create(
            long combatBoostDurationMilliseconds,
            long antipoisonDurationMilliseconds)
        {
            if (combatBoostDurationMilliseconds < 1)
                throw new ArgumentOutOfRangeException(nameof(combatBoostDurationMilliseconds));
            if (antipoisonDurationMilliseconds < 1)
                throw new ArgumentOutOfRangeException(nameof(antipoisonDurationMilliseconds));

            var catalog = new PotionEffectCatalog();
            catalog.Register(new PotionEffectDefinition(
                AttackPotionId,
                AttackBoostEffectId,
                combatBoostDurationMilliseconds,
                new[] { new SkillLevelModifier(SkillId.Attack, 3) }));
            catalog.Register(new PotionEffectDefinition(
                StrengthPotionId,
                StrengthBoostEffectId,
                combatBoostDurationMilliseconds,
                new[] { new SkillLevelModifier(SkillId.Strength, 3) }));
            catalog.Register(new PotionEffectDefinition(
                AntipoisonId,
                AntipoisonEffectId,
                antipoisonDurationMilliseconds,
                flags: StatusEffectFlags.PoisonImmunity));
            return catalog;
        }
    }
}
