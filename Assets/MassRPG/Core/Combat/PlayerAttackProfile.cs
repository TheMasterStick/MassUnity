using MassRPG.Core.Characters;

namespace MassRPG.Core.Combat
{
    /// <summary>
    /// Fully resolved attack-facing values for one player at one moment. The authoritative server
    /// derives this from skills/equipment/spells; the client never supplies its own combat range.
    /// </summary>
    public readonly struct PlayerAttackProfile
    {
        public PlayerAttackProfile(
            CombatStyle style,
            int rangeTiles,
            int attackIntervalMilliseconds,
            int attackBonus,
            int strengthBonus,
            int defenceBonus,
            int rangedAttackBonus,
            int rangedStrengthBonus,
            int magicBonus)
        {
            Style = style;
            RangeTiles = rangeTiles;
            AttackIntervalMilliseconds = attackIntervalMilliseconds;
            AttackBonus = attackBonus;
            StrengthBonus = strengthBonus;
            DefenceBonus = defenceBonus;
            RangedAttackBonus = rangedAttackBonus;
            RangedStrengthBonus = rangedStrengthBonus;
            MagicBonus = magicBonus;
        }

        public CombatStyle Style { get; }
        public int RangeTiles { get; }
        public int AttackIntervalMilliseconds { get; }
        public int AttackBonus { get; }
        public int StrengthBonus { get; }
        public int DefenceBonus { get; }
        public int RangedAttackBonus { get; }
        public int RangedStrengthBonus { get; }
        public int MagicBonus { get; }
    }

    public interface IPlayerAttackProfileSource
    {
        PlayerAttackProfile Resolve(MassRPG.Core.Characters.PlayerState player);
    }
}
