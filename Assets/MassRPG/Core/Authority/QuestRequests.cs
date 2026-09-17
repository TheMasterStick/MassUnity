using System;
using MassRPG.Core.Content;

namespace MassRPG.Core.Authority
{
    public sealed class StartQuestRequest : GameRequest
    {
        public StartQuestRequest(Guid requestId, Guid characterId, ContentId questId)
            : base(requestId, characterId)
        {
            if (questId.IsEmpty) throw new ArgumentException("Quest id cannot be empty.", nameof(questId));
            QuestId = questId;
        }

        public ContentId QuestId { get; }
    }

    public sealed class ClaimQuestRewardRequest : GameRequest
    {
        public ClaimQuestRewardRequest(Guid requestId, Guid characterId, ContentId questId)
            : base(requestId, characterId)
        {
            if (questId.IsEmpty) throw new ArgumentException("Quest id cannot be empty.", nameof(questId));
            QuestId = questId;
        }

        public ContentId QuestId { get; }
    }
}
