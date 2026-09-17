using System;
using System.Collections.Generic;
using MassRPG.Core.Characters;

namespace MassRPG.Server.Combat
{
    public readonly struct CombatExperienceRecipientAward
    {
        public CombatExperienceRecipientAward(Guid characterId, int combatXp, int hitpointsXp)
        {
            CharacterId = characterId;
            CombatXp = combatXp;
            HitpointsXp = hitpointsXp;
        }

        public Guid CharacterId { get; }
        public int CombatXp { get; }
        public int HitpointsXp { get; }
    }

    public sealed class CombatExperienceSettlementResult
    {
        private readonly List<CombatExperienceRecipientAward> _awards;
        private readonly List<Guid> _missingCharacters;

        internal CombatExperienceSettlementResult(
            List<CombatExperienceRecipientAward> awards,
            List<Guid> missingCharacters)
        {
            _awards = awards ?? throw new ArgumentNullException(nameof(awards));
            _missingCharacters = missingCharacters ?? throw new ArgumentNullException(nameof(missingCharacters));
        }

        public IReadOnlyList<CombatExperienceRecipientAward> Awards => _awards;
        public IReadOnlyList<Guid> MissingCharacters => _missingCharacters;
        public bool Complete => _missingCharacters.Count == 0;
    }

    /// <summary>
    /// Applies the settled group-reward model after a creature dies. Eligible contribution damage
    /// is converted to the browser-compatible XP budget once per reward group, then that finite
    /// budget is divided among the group's in-range recipients. A nearby formal party member can
    /// therefore share XP without multiplying the creature's total reward, while non-party players
    /// still receive XP only for their own meaningful contribution group.
    /// </summary>
    public sealed class CombatExperienceSettlementService
    {
        private readonly ICombatExperiencePolicy _policy;

        public CombatExperienceSettlementService(ICombatExperiencePolicy policy = null)
        {
            _policy = policy ?? new BrowserCombatExperiencePolicy();
        }

        public CombatExperienceSettlementResult Settle(
            CombatRewardPlan plan,
            Func<Guid, PlayerState> resolveCharacter)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (resolveCharacter == null) throw new ArgumentNullException(nameof(resolveCharacter));

            var awards = new List<CombatExperienceRecipientAward>();
            var missing = new List<Guid>();

            for (var groupIndex = 0; groupIndex < plan.Groups.Count; groupIndex++)
            {
                var group = plan.Groups[groupIndex];
                if (group.SharedRecipients.Count == 0 || group.EligibleContributionDamage <= 0) continue;

                var budget = _policy.CalculateForDamage(group.EligibleContributionDamage);
                var combatBase = budget.CombatXp / group.SharedRecipients.Count;
                var combatRemainder = budget.CombatXp - combatBase * group.SharedRecipients.Count;
                var hpBase = budget.HitpointsXp / group.SharedRecipients.Count;
                var hpRemainder = budget.HitpointsXp - hpBase * group.SharedRecipients.Count;

                for (var recipientIndex = 0; recipientIndex < group.SharedRecipients.Count; recipientIndex++)
                {
                    var characterId = group.SharedRecipients[recipientIndex];
                    var combatXp = combatBase + (recipientIndex < combatRemainder ? 1 : 0);
                    var hitpointsXp = hpBase + (recipientIndex < hpRemainder ? 1 : 0);
                    var player = resolveCharacter(characterId);
                    if (player == null)
                    {
                        missing.Add(characterId);
                        continue;
                    }

                    _policy.Apply(player, new CombatExperienceAward(combatXp, hitpointsXp));
                    awards.Add(new CombatExperienceRecipientAward(characterId, combatXp, hitpointsXp));
                }
            }

            return new CombatExperienceSettlementResult(awards, missing);
        }
    }
}
