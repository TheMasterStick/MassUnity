using System;
using MassRPG.Core.Authority;
using MassRPG.Core.Interactions;

namespace MassRPG.Client.Interactions
{
    /// <summary>
    /// Submits interaction options through the authoritative request boundary. The logical factory
    /// refuses to parse display IDs, so presentation must provide typed creature/resource/item
    /// identity when it creates an interaction target.
    /// </summary>
    public sealed class InteractionRequestController
    {
        private readonly IGameAuthority _authority;
        private readonly Guid _characterId;

        public InteractionRequestController(IGameAuthority authority, Guid characterId)
        {
            _authority = authority ?? throw new ArgumentNullException(nameof(authority));
            if (characterId == Guid.Empty)
                throw new ArgumentException("Character id cannot be empty.", nameof(characterId));
            _characterId = characterId;
        }

        public bool TrySubmit(
            InteractionOption option,
            out AuthorityDecision decision,
            out string failureCode)
        {
            var requestId = Guid.NewGuid();
            if (!InteractionIntentFactory.TryCreateRequest(
                    option,
                    requestId,
                    _characterId,
                    out var request,
                    out failureCode))
            {
                decision = null;
                return false;
            }

            decision = _authority.Submit(request);
            failureCode = string.Empty;
            return true;
        }
    }
}
