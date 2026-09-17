using System;
using MassRPG.Core.Authority;
using MassRPG.Core.Content;

namespace MassRPG.Client.Quests
{
    /// <summary>
    /// Client bridge for quest lifecycle intent. Objective progress is deliberately not exposed as a
    /// client command: kills, gathering, NPC conversations and location checks are recorded by
    /// trusted authoritative systems. The client may only request a valid quest start or reward claim.
    /// </summary>
    public sealed class QuestActionController
    {
        private readonly IGameAuthority _authority;
        private readonly Guid _characterId;

        public QuestActionController(IGameAuthority authority, Guid characterId)
        {
            _authority = authority ?? throw new ArgumentNullException(nameof(authority));
            if (characterId == Guid.Empty)
                throw new ArgumentException("Character id cannot be empty.", nameof(characterId));
            _characterId = characterId;
        }

        public AuthorityDecision Start(ContentId questId)
            => _authority.Submit(new StartQuestRequest(Guid.NewGuid(), _characterId, questId));

        public AuthorityDecision Claim(ContentId questId)
            => _authority.Submit(new ClaimQuestRewardRequest(Guid.NewGuid(), _characterId, questId));
    }
}
