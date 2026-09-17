using System;
using System.Collections.Generic;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Data.Effects;

namespace MassRPG.Server.Effects
{
    public readonly struct ActiveStatusEffectSnapshot
    {
        public ActiveStatusEffectSnapshot(ContentId effectId, long expiresAtUnixMilliseconds)
        {
            if (effectId.IsEmpty) throw new ArgumentException("Effect id cannot be empty.", nameof(effectId));
            if (expiresAtUnixMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(expiresAtUnixMilliseconds));
            EffectId = effectId;
            ExpiresAtUnixMilliseconds = expiresAtUnixMilliseconds;
        }

        public ContentId EffectId { get; }
        public long ExpiresAtUnixMilliseconds { get; }
    }

    public sealed class CharacterStatusEffectSnapshot
    {
        public const int CurrentVersion = 1;

        public CharacterStatusEffectSnapshot(int version, IReadOnlyList<ActiveStatusEffectSnapshot> effects)
        {
            if (version < 1) throw new ArgumentOutOfRangeException(nameof(version));
            Version = version;
            Effects = effects ?? Array.Empty<ActiveStatusEffectSnapshot>();
        }

        public int Version { get; }
        public IReadOnlyList<ActiveStatusEffectSnapshot> Effects { get; }
    }

    /// <summary>
    /// Versioned persistence for temporary server effects. Absolute expiry timestamps are stored so
    /// logging out does not restart a potion's full duration. Expired entries are intentionally
    /// discarded on restore. Active effects whose definitions no longer exist are rejected so a
    /// removed/renamed gameplay effect cannot silently become some different buff.
    /// </summary>
    public static class StatusEffectPersistenceSnapshotCodec
    {
        public static CharacterStatusEffectSnapshot Capture(
            StatusEffectService service,
            PlayerState player,
            long nowUnixMilliseconds)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            if (player == null) throw new ArgumentNullException(nameof(player));

            var active = service.GetActiveEffects(player, nowUnixMilliseconds);
            var snapshots = new ActiveStatusEffectSnapshot[active.Count];
            for (var i = 0; i < active.Count; i++)
                snapshots[i] = new ActiveStatusEffectSnapshot(active[i].EffectId, active[i].ExpiresAtUnixMilliseconds);
            return new CharacterStatusEffectSnapshot(CharacterStatusEffectSnapshot.CurrentVersion, snapshots);
        }

        public static void Restore(
            CharacterStatusEffectSnapshot snapshot,
            StatusEffectService service,
            PlayerState player,
            IPotionEffectSource definitions,
            long nowUnixMilliseconds)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (service == null) throw new ArgumentNullException(nameof(service));
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            if (snapshot.Version > CharacterStatusEffectSnapshot.CurrentVersion)
                throw new InvalidOperationException("Status-effect snapshot was written by a newer server version.");

            // Validate the complete still-active snapshot before touching live runtime state.
            var seen = new HashSet<ContentId>();
            var restore = new List<RestoreEntry>();
            for (var i = 0; i < snapshot.Effects.Count; i++)
            {
                var saved = snapshot.Effects[i];
                if (!seen.Add(saved.EffectId))
                    throw new InvalidOperationException("Status-effect snapshot contains duplicate effect id '" + saved.EffectId + "'.");
                if (saved.ExpiresAtUnixMilliseconds <= nowUnixMilliseconds) continue;
                if (!definitions.TryGetByEffect(saved.EffectId, out var definition))
                    throw new InvalidOperationException("Status-effect snapshot references unknown active effect '" + saved.EffectId + "'.");
                restore.Add(new RestoreEntry(definition, saved.ExpiresAtUnixMilliseconds));
            }

            service.Clear(player);
            for (var i = 0; i < restore.Count; i++)
            {
                var entry = restore[i];
                if (!service.RestoreAbsolute(player, entry.Definition, entry.ExpiresAtUnixMilliseconds, nowUnixMilliseconds))
                    throw new InvalidOperationException("Validated status effect unexpectedly expired during restore.");
            }
        }

        private readonly struct RestoreEntry
        {
            public RestoreEntry(PotionEffectDefinition definition, long expiresAtUnixMilliseconds)
            {
                Definition = definition;
                ExpiresAtUnixMilliseconds = expiresAtUnixMilliseconds;
            }

            public PotionEffectDefinition Definition { get; }
            public long ExpiresAtUnixMilliseconds { get; }
        }
    }
}
