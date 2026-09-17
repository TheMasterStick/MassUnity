using System;
using MassRPG.Core.Authority;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;

namespace MassRPG.Client.Combat
{
    /// <summary>
    /// Thin Unity-client bridge for combat-loadout intent. Presentation code should call this
    /// rather than mutating PlayerState directly; the same request contracts can later cross the
    /// real network transport unchanged.
    /// </summary>
    public sealed class CombatLoadoutController
    {
        private readonly IGameAuthority _authority;
        private readonly Guid _characterId;

        public CombatLoadoutController(IGameAuthority authority, Guid characterId)
        {
            _authority = authority ?? throw new ArgumentNullException(nameof(authority));
            if (characterId == Guid.Empty)
                throw new ArgumentException("Character id cannot be empty.", nameof(characterId));
            _characterId = characterId;
        }

        public AuthorityDecision SelectCombatStyle(CombatStyle style)
            => _authority.Submit(new SelectCombatStyleRequest(Guid.NewGuid(), _characterId, style));

        public AuthorityDecision SelectMeleeTrainingStyle(MeleeTrainingStyle style)
            => _authority.Submit(new SelectMeleeTrainingStyleRequest(Guid.NewGuid(), _characterId, style));

        public AuthorityDecision SelectRangedAmmunition(ContentId ammunitionItemId)
            => _authority.Submit(new SelectRangedAmmunitionRequest(Guid.NewGuid(), _characterId, ammunitionItemId));

        public AuthorityDecision ClearRangedAmmunition()
            => _authority.Submit(new ClearRangedAmmunitionRequest(Guid.NewGuid(), _characterId));
    }
}
