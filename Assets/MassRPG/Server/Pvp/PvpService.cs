using System;
using System.Collections.Generic;
using MassRPG.Core.Characters;
using MassRPG.Data.World.Semantics;

namespace MassRPG.Server.Pvp
{
    public sealed class PlayerPvpStatus
    {
        public bool OptedIn { get; internal set; }
        public long SkulledUntilUnixMilliseconds { get; internal set; }
        public bool IsSkulled(long nowUnixMilliseconds) => SkulledUntilUnixMilliseconds > nowUnixMilliseconds;
    }

    public readonly struct PlayerPvpStatusSnapshot
    {
        public PlayerPvpStatusSnapshot(bool optedIn, long skulledUntilUnixMilliseconds)
        {
            if (skulledUntilUnixMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(skulledUntilUnixMilliseconds));
            OptedIn = optedIn;
            SkulledUntilUnixMilliseconds = skulledUntilUnixMilliseconds;
        }

        public bool OptedIn { get; }
        public long SkulledUntilUnixMilliseconds { get; }
    }

    public sealed class PvpPolicy
    {
        public PvpPolicy(bool requireBothOptedIn = true, long skullDurationMilliseconds = 20 * 60 * 1000L)
        {
            if (skullDurationMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(skullDurationMilliseconds));
            RequireBothOptedIn = requireBothOptedIn;
            SkullDurationMilliseconds = skullDurationMilliseconds;
        }

        public bool RequireBothOptedIn { get; }
        public long SkullDurationMilliseconds { get; }
    }

    public readonly struct PvpAttackDecision
    {
        public PvpAttackDecision(bool allowed, string code, bool forcedPvp, bool protectedArea)
        {
            Allowed = allowed;
            Code = code;
            ForcedPvp = forcedPvp;
            ProtectedArea = protectedArea;
        }

        public bool Allowed { get; }
        public string Code { get; }
        public bool ForcedPvp { get; }
        public bool ProtectedArea { get; }
    }

    /// <summary>
    /// Server-owned PvP opt-in and area policy. Protected areas override everything. Forced-PvP
    /// areas bypass voluntary flags. Normal-world attacks use configurable opt-in policy; a valid
    /// initiating attack can then mark the attacker skulled for death-penalty handling elsewhere.
    /// </summary>
    public sealed class PvpService
    {
        private readonly WorldSemanticCatalog _semantics;
        private readonly PvpPolicy _policy;
        private readonly Dictionary<Guid, PlayerPvpStatus> _statuses = new Dictionary<Guid, PlayerPvpStatus>();

        public PvpService(WorldSemanticCatalog semantics, PvpPolicy policy = null)
        {
            _semantics = semantics ?? throw new ArgumentNullException(nameof(semantics));
            _policy = policy ?? new PvpPolicy();
        }

        public PlayerPvpStatus GetOrCreate(Guid characterId)
        {
            if (characterId == Guid.Empty) throw new ArgumentException("Character id cannot be empty.", nameof(characterId));
            if (!_statuses.TryGetValue(characterId, out var status))
            {
                status = new PlayerPvpStatus();
                _statuses.Add(characterId, status);
            }
            return status;
        }

        public PlayerPvpStatusSnapshot CaptureStatus(Guid characterId)
        {
            var status = GetOrCreate(characterId);
            return new PlayerPvpStatusSnapshot(status.OptedIn, Math.Max(0L, status.SkulledUntilUnixMilliseconds));
        }

        public PlayerPvpStatus RestoreStatus(Guid characterId, PlayerPvpStatusSnapshot snapshot)
        {
            var status = GetOrCreate(characterId);
            status.OptedIn = snapshot.OptedIn;
            status.SkulledUntilUnixMilliseconds = snapshot.SkulledUntilUnixMilliseconds;
            return status;
        }

        public void SetOptIn(Guid characterId, bool enabled) => GetOrCreate(characterId).OptedIn = enabled;

        public PvpAttackDecision EvaluateAttack(PlayerState attacker, PlayerState defender)
        {
            if (attacker == null || defender == null) return new PvpAttackDecision(false, "player_missing", false, false);
            if (attacker.CharacterId == defender.CharacterId) return new PvpAttackDecision(false, "self_target", false, false);
            if (!attacker.IsAlive || !defender.IsAlive) return new PvpAttackDecision(false, "player_dead", false, false);
            if (!attacker.Location.SameLayer(defender.Location)) return new PvpAttackDecision(false, "different_layer", false, false);

            var attackerAreas = _semantics.AreasContaining(attacker.Location);
            var defenderAreas = _semantics.AreasContaining(defender.Location);
            var protectedArea = ContainsKind(attackerAreas, WorldAreaKind.PvpProtected)
                || ContainsKind(defenderAreas, WorldAreaKind.PvpProtected);
            if (protectedArea) return new PvpAttackDecision(false, "pvp_protected", false, true);

            var forced = ContainsKind(attackerAreas, WorldAreaKind.ForcedPvP)
                && ContainsKind(defenderAreas, WorldAreaKind.ForcedPvP);
            if (forced) return new PvpAttackDecision(true, "forced_pvp", true, false);

            var attackerStatus = GetOrCreate(attacker.CharacterId);
            var defenderStatus = GetOrCreate(defender.CharacterId);
            if (!attackerStatus.OptedIn) return new PvpAttackDecision(false, "attacker_not_opted_in", false, false);
            if (_policy.RequireBothOptedIn && !defenderStatus.OptedIn)
                return new PvpAttackDecision(false, "defender_not_opted_in", false, false);
            return new PvpAttackDecision(true, "voluntary_pvp", false, false);
        }

        public bool MarkAggressor(PlayerState attacker, PlayerState defender, long nowUnixMilliseconds)
        {
            var decision = EvaluateAttack(attacker, defender);
            if (!decision.Allowed) return false;
            var status = GetOrCreate(attacker.CharacterId);
            status.SkulledUntilUnixMilliseconds = Math.Max(
                status.SkulledUntilUnixMilliseconds,
                checked(nowUnixMilliseconds + _policy.SkullDurationMilliseconds));
            return true;
        }

        private static bool ContainsKind(IReadOnlyList<WorldAreaDefinition> areas, WorldAreaKind kind)
        {
            for (var i = 0; i < areas.Count; i++)
                if (areas[i].Kind == kind) return true;
            return false;
        }
    }
}
