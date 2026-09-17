using System;
using System.Collections.Generic;
using MassRPG.Core.Content;

namespace MassRPG.Data.Firemaking
{
    public sealed class FiremakingDefinition
    {
        public FiremakingDefinition(ContentId logItemId, int requiredLevel, int experience, int lifetimeMilliseconds)
        {
            if (logItemId.IsEmpty) throw new ArgumentException("Log item id cannot be empty.", nameof(logItemId));
            if (requiredLevel < 1 || requiredLevel > 300) throw new ArgumentOutOfRangeException(nameof(requiredLevel));
            if (experience < 0) throw new ArgumentOutOfRangeException(nameof(experience));
            if (lifetimeMilliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(lifetimeMilliseconds));
            LogItemId = logItemId;
            RequiredLevel = requiredLevel;
            Experience = experience;
            LifetimeMilliseconds = lifetimeMilliseconds;
        }

        public ContentId LogItemId { get; }
        public int RequiredLevel { get; set; }
        public int Experience { get; set; }
        public int LifetimeMilliseconds { get; set; }
    }

    public interface IFiremakingDefinitionSource
    {
        bool TryGet(ContentId logItemId, out FiremakingDefinition definition);
    }

    public sealed class FiremakingCatalog : IFiremakingDefinitionSource
    {
        private readonly Dictionary<ContentId, FiremakingDefinition> _definitions = new Dictionary<ContentId, FiremakingDefinition>();

        public IEnumerable<FiremakingDefinition> All => _definitions.Values;

        public void Register(FiremakingDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (_definitions.ContainsKey(definition.LogItemId))
                throw new InvalidOperationException($"Duplicate firemaking log id '{definition.LogItemId}'.");
            _definitions.Add(definition.LogItemId, definition);
        }

        public bool TryGet(ContentId logItemId, out FiremakingDefinition definition)
            => _definitions.TryGetValue(logItemId, out definition);
    }
}
