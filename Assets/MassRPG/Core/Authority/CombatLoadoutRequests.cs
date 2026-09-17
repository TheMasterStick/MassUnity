using System;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;

namespace MassRPG.Core.Authority
{
    public sealed class SelectCombatStyleRequest : GameRequest
    {
        public SelectCombatStyleRequest(Guid requestId, Guid characterId, CombatStyle style)
            : base(requestId, characterId)
        {
            if (!Enum.IsDefined(typeof(CombatStyle), style))
                throw new ArgumentOutOfRangeException(nameof(style));
            Style = style;
        }

        public CombatStyle Style { get; }
    }

    public sealed class SelectMeleeTrainingStyleRequest : GameRequest
    {
        public SelectMeleeTrainingStyleRequest(Guid requestId, Guid characterId, MeleeTrainingStyle style)
            : base(requestId, characterId)
        {
            if (!Enum.IsDefined(typeof(MeleeTrainingStyle), style))
                throw new ArgumentOutOfRangeException(nameof(style));
            Style = style;
        }

        public MeleeTrainingStyle Style { get; }
    }

    public sealed class SelectRangedAmmunitionRequest : GameRequest
    {
        public SelectRangedAmmunitionRequest(Guid requestId, Guid characterId, ContentId ammunitionItemId)
            : base(requestId, characterId)
        {
            if (ammunitionItemId.IsEmpty) throw new ArgumentException("Ammunition item id cannot be empty.", nameof(ammunitionItemId));
            AmmunitionItemId = ammunitionItemId;
        }

        public ContentId AmmunitionItemId { get; }
    }

    public sealed class ClearRangedAmmunitionRequest : GameRequest
    {
        public ClearRangedAmmunitionRequest(Guid requestId, Guid characterId)
            : base(requestId, characterId)
        {
        }
    }
}
