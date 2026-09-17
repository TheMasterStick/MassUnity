using System;

namespace MassRPG.Core.Authority
{
    /// <summary>
    /// Serializable-intent boundary between presentation and authoritative simulation.
    /// Early Unity builds can submit these in-process; a network transport can carry the same
    /// intent later without moving gameplay truth into the client.
    /// </summary>
    public abstract class GameRequest
    {
        protected GameRequest(Guid requestId, Guid characterId)
        {
            if (requestId == Guid.Empty) throw new ArgumentException("Request id cannot be empty.", nameof(requestId));
            if (characterId == Guid.Empty) throw new ArgumentException("Character id cannot be empty.", nameof(characterId));
            RequestId = requestId;
            CharacterId = characterId;
        }

        public Guid RequestId { get; }
        public Guid CharacterId { get; }
    }

    public interface IGameAuthority
    {
        AuthorityDecision Submit(GameRequest request);
    }
}
