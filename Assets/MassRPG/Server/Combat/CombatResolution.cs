namespace MassRPG.Server.Combat
{
    public enum CombatAdvanceKind
    {
        Idle,
        Approaching,
        WaitingForCooldown,
        Attacked,
        TargetKilled,
        TargetLost,
        Failed
    }

    public readonly struct CombatAdvanceResult
    {
        public CombatAdvanceResult(
            CombatAdvanceKind kind,
            string code,
            int damage,
            double hitChance,
            bool hit)
        {
            Kind = kind;
            Code = code ?? string.Empty;
            Damage = damage;
            HitChance = hitChance;
            Hit = hit;
        }

        public CombatAdvanceKind Kind { get; }
        public string Code { get; }
        public int Damage { get; }
        public double HitChance { get; }
        public bool Hit { get; }

        public bool DidAttack => Kind == CombatAdvanceKind.Attacked || Kind == CombatAdvanceKind.TargetKilled;

        public static CombatAdvanceResult State(CombatAdvanceKind kind, string code = "")
            => new CombatAdvanceResult(kind, code, 0, 0.0, false);

        public static CombatAdvanceResult Attack(int damage, double hitChance, bool hit, bool killed)
            => new CombatAdvanceResult(
                killed ? CombatAdvanceKind.TargetKilled : CombatAdvanceKind.Attacked,
                killed ? "target_killed" : "attack_resolved",
                damage,
                hitChance,
                hit);
    }
}
