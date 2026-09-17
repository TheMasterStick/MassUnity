using System;

namespace MassRPG.Core.Combat
{
    /// <summary>
    /// Initial C# parity port of src/systems/CombatMath.ts.
    /// Keep formulas deterministic and engine-independent so client tests and server authority
    /// can share the same calculations. Balance changes can be made deliberately after parity.
    /// </summary>
    public static class CombatMath
    {
        public static int MaxHitMelee(int strengthLevel, int strengthBonus)
        {
            var effective = strengthLevel + 8;
            return Math.Max(1, (int)Math.Floor(0.5 + (effective * (strengthBonus + 64)) / 640.0));
        }

        public static int MaxHitRanged(int rangedLevel, int rangedStrengthBonus)
        {
            var effective = rangedLevel + 8;
            return Math.Max(1, (int)Math.Floor(0.5 + (effective * (rangedStrengthBonus + 64)) / 640.0));
        }

        public static int MaxHitMagic(int magicLevel)
        {
            return Math.Max(1, (int)Math.Floor(2 + magicLevel * 0.6));
        }

        public static int AttackRoll(int level, int bonus) => (level + 8) * (bonus + 64);
        public static int DefenceRoll(int level, int bonus) => (level + 8) * (bonus + 64);

        public static double HitChance(int attackRoll, int defenceRoll)
        {
            if (attackRoll > defenceRoll)
                return 1.0 - (defenceRoll + 2.0) / (2.0 * (attackRoll + 1.0));

            return attackRoll / (2.0 * (defenceRoll + 1.0));
        }

        public static int RollDamage(int maxHit, double hitChance, Func<double> random01)
        {
            if (random01 == null) throw new ArgumentNullException(nameof(random01));
            if (random01() > hitChance) return 0;
            return (int)Math.Floor(random01() * (maxHit + 1));
        }
    }
}
