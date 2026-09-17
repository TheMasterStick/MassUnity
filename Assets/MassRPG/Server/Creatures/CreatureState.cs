using System;
using MassRPG.Core.Content;
using MassRPG.Core.World;
using MassRPG.Data.Creatures;

namespace MassRPG.Server.Creatures
{
    /// <summary>
    /// Mutable state for one materialized creature. Ordinary sleeping populations do not need to
    /// preserve every instance; named/persistent creatures can keep this identity across sleeps.
    /// </summary>
    public sealed class CreatureState
    {
        public CreatureState(Guid instanceId, ContentId definitionId, GridLocation anchor, int maxHitpoints)
        {
            if (instanceId == Guid.Empty) throw new ArgumentException("Creature instance id cannot be empty.", nameof(instanceId));
            if (definitionId.IsEmpty) throw new ArgumentException("Creature definition id cannot be empty.", nameof(definitionId));
            if (maxHitpoints < 1) throw new ArgumentOutOfRangeException(nameof(maxHitpoints));
            InstanceId = instanceId;
            DefinitionId = definitionId;
            Anchor = anchor;
            HomeAnchor = anchor;
            CurrentHitpoints = maxHitpoints;
        }

        public Guid InstanceId { get; }
        public ContentId DefinitionId { get; }
        public GridLocation Anchor { get; set; }
        public GridLocation HomeAnchor { get; set; }
        public int CurrentHitpoints { get; set; }
        public Guid? TargetCharacterId { get; set; }
        public long NextAttackAtUnixMilliseconds { get; set; }
        public bool IsAlive => CurrentHitpoints > 0;

        public static CreatureState Spawn(Guid instanceId, CreatureDefinition definition, GridLocation anchor)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            return new CreatureState(instanceId, definition.Id, anchor, definition.MaxHitpoints);
        }
    }
}
