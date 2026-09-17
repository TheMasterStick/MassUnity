using System;
using System.Collections.Generic;
using MassRPG.Core.Resources;

namespace MassRPG.Server.Resources
{
    /// <summary>
    /// Sparse authoritative exception state for renewable resources. An untouched/respawned node
    /// has no entry. Personal nodes store an exception only for the affected character; shared
    /// nodes store one world exception visible to everyone.
    /// </summary>
    public sealed class ResourceDepletionLedger
    {
        private readonly Dictionary<ResourceNodeKey, long> _sharedUntil = new Dictionary<ResourceNodeKey, long>();
        private readonly Dictionary<PersonalKey, long> _personalUntil = new Dictionary<PersonalKey, long>();

        public int ActiveSharedExceptions => _sharedUntil.Count;
        public int ActivePersonalExceptions => _personalUntil.Count;

        public bool IsAvailable(
            Guid characterId,
            ResourceNodeKey node,
            ResourceAvailabilityMode mode,
            long nowUnixMilliseconds)
        {
            if (mode == ResourceAvailabilityMode.Shared)
                return IsSharedAvailable(node, nowUnixMilliseconds);

            if (characterId == Guid.Empty) throw new ArgumentException("Personal resource checks require a character id.", nameof(characterId));
            var key = new PersonalKey(characterId, node);
            if (!_personalUntil.TryGetValue(key, out var until)) return true;
            if (nowUnixMilliseconds < until) return false;
            _personalUntil.Remove(key);
            return true;
        }

        public long Deplete(
            Guid characterId,
            ResourceNodeKey node,
            ResourceAvailabilityMode mode,
            int respawnSeconds,
            long nowUnixMilliseconds)
        {
            if (respawnSeconds < 0) throw new ArgumentOutOfRangeException(nameof(respawnSeconds));
            var until = checked(nowUnixMilliseconds + respawnSeconds * 1000L);

            if (mode == ResourceAvailabilityMode.Shared)
            {
                _sharedUntil[node] = until;
                return until;
            }

            if (characterId == Guid.Empty) throw new ArgumentException("Personal resource depletion requires a character id.", nameof(characterId));
            _personalUntil[new PersonalKey(characterId, node)] = until;
            return until;
        }

        public int PruneExpired(long nowUnixMilliseconds)
        {
            var removed = 0;
            var sharedExpired = new List<ResourceNodeKey>();
            foreach (var pair in _sharedUntil)
                if (pair.Value <= nowUnixMilliseconds) sharedExpired.Add(pair.Key);
            for (var i = 0; i < sharedExpired.Count; i++)
                if (_sharedUntil.Remove(sharedExpired[i])) removed++;

            var personalExpired = new List<PersonalKey>();
            foreach (var pair in _personalUntil)
                if (pair.Value <= nowUnixMilliseconds) personalExpired.Add(pair.Key);
            for (var i = 0; i < personalExpired.Count; i++)
                if (_personalUntil.Remove(personalExpired[i])) removed++;
            return removed;
        }

        private bool IsSharedAvailable(ResourceNodeKey node, long nowUnixMilliseconds)
        {
            if (!_sharedUntil.TryGetValue(node, out var until)) return true;
            if (nowUnixMilliseconds < until) return false;
            _sharedUntil.Remove(node);
            return true;
        }

        private readonly struct PersonalKey : IEquatable<PersonalKey>
        {
            public PersonalKey(Guid characterId, ResourceNodeKey node)
            {
                CharacterId = characterId;
                Node = node;
            }

            public Guid CharacterId { get; }
            public ResourceNodeKey Node { get; }

            public bool Equals(PersonalKey other) => CharacterId == other.CharacterId && Node == other.Node;
            public override bool Equals(object obj) => obj is PersonalKey other && Equals(other);
            public override int GetHashCode() => unchecked((CharacterId.GetHashCode() * 397) ^ Node.GetHashCode());
        }
    }
}
