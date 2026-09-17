using System;
using System.Collections.Generic;
using MassRPG.Core.Characters;
using MassRPG.Core.Inventory;
using MassRPG.Data.Effects;
using MassRPG.Server.Death;
using MassRPG.Server.Economy;
using MassRPG.Server.Effects;
using MassRPG.Server.Items;
using MassRPG.Server.Pvp;
using MassRPG.Server.Quests;
using MassRPG.Server.Travel;

namespace MassRPG.Server.Persistence
{
    /// <summary>
    /// Complete character persistence envelope layered over the older character/bank/respawn/travel
    /// snapshot. New server-owned systems are added here rather than silently bloating PlayerState or
    /// serializing transient service internals. The envelope is versioned independently so persistence
    /// migrations can remain explicit when gameplay systems evolve.
    /// </summary>
    public sealed class CompleteCharacterPersistenceSnapshot
    {
        public const int CurrentVersion = 2;

        public CompleteCharacterPersistenceSnapshot(
            int version,
            CharacterPersistenceSnapshot character,
            CharacterQuestSnapshot quests,
            IReadOnlyList<EquipmentDurabilityEntry> equipmentDurability,
            PlayerPvpStatusSnapshot pvp,
            CharacterStatusEffectSnapshot statusEffects = null)
        {
            if (version < 1) throw new ArgumentOutOfRangeException(nameof(version));
            Version = version;
            Character = character ?? throw new ArgumentNullException(nameof(character));
            Quests = quests ?? throw new ArgumentNullException(nameof(quests));
            EquipmentDurability = equipmentDurability ?? Array.Empty<EquipmentDurabilityEntry>();
            Pvp = pvp;
            StatusEffects = statusEffects
                ?? new CharacterStatusEffectSnapshot(CharacterStatusEffectSnapshot.CurrentVersion, Array.Empty<ActiveStatusEffectSnapshot>());
        }

        public int Version { get; }
        public CharacterPersistenceSnapshot Character { get; }
        public CharacterQuestSnapshot Quests { get; }
        public IReadOnlyList<EquipmentDurabilityEntry> EquipmentDurability { get; }
        public PlayerPvpStatusSnapshot Pvp { get; }
        public CharacterStatusEffectSnapshot StatusEffects { get; }
    }

    public sealed class CompleteCharacterPersistenceRestoreResult
    {
        internal CompleteCharacterPersistenceRestoreResult(
            CharacterPersistenceRestoreResult character,
            CharacterQuestState quests,
            PlayerPvpStatus pvp,
            IReadOnlyList<EquipmentDurabilityEntry> equipmentDurability,
            CharacterStatusEffectSnapshot statusEffects)
        {
            Character = character ?? throw new ArgumentNullException(nameof(character));
            Quests = quests ?? throw new ArgumentNullException(nameof(quests));
            Pvp = pvp ?? throw new ArgumentNullException(nameof(pvp));
            EquipmentDurability = equipmentDurability ?? Array.Empty<EquipmentDurabilityEntry>();
            StatusEffects = statusEffects
                ?? new CharacterStatusEffectSnapshot(CharacterStatusEffectSnapshot.CurrentVersion, Array.Empty<ActiveStatusEffectSnapshot>());
        }

        public CharacterPersistenceRestoreResult Character { get; }
        public PlayerState Player => Character.Player;
        public CharacterBankState Bank => Character.Bank;
        public PlayerRespawnProfile Respawn => Character.Respawn;
        public CharacterFastTravelState FastTravel => Character.FastTravel;
        public CharacterQuestState Quests { get; }
        public PlayerPvpStatus Pvp { get; }
        public IReadOnlyList<EquipmentDurabilityEntry> EquipmentDurability { get; }
        public CharacterStatusEffectSnapshot StatusEffects { get; }
    }

    public static class CompleteCharacterPersistenceSnapshotCodec
    {
        public static CompleteCharacterPersistenceSnapshot Capture(
            PlayerState player,
            CharacterBankRegistry banks,
            PlayerRespawnRegistry respawns,
            FastTravelStateRegistry fastTravelStates,
            QuestService quests,
            EquipmentDurabilityService durability,
            PvpService pvp,
            StatusEffectService statusEffects = null,
            long nowUnixMilliseconds = 0)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (banks == null) throw new ArgumentNullException(nameof(banks));
            if (respawns == null) throw new ArgumentNullException(nameof(respawns));
            if (fastTravelStates == null) throw new ArgumentNullException(nameof(fastTravelStates));
            if (quests == null) throw new ArgumentNullException(nameof(quests));
            if (durability == null) throw new ArgumentNullException(nameof(durability));
            if (pvp == null) throw new ArgumentNullException(nameof(pvp));

            var character = CharacterPersistenceSnapshotCodec.Capture(player, banks, respawns, fastTravelStates);
            var questState = QuestPersistenceSnapshotCodec.Capture(quests.GetOrCreateState(player.CharacterId));
            var equipmentDurability = durability.Capture(player);
            var pvpStatus = pvp.CaptureStatus(player.CharacterId);
            var effects = statusEffects == null
                ? new CharacterStatusEffectSnapshot(CharacterStatusEffectSnapshot.CurrentVersion, Array.Empty<ActiveStatusEffectSnapshot>())
                : StatusEffectPersistenceSnapshotCodec.Capture(statusEffects, player, nowUnixMilliseconds);

            return new CompleteCharacterPersistenceSnapshot(
                CompleteCharacterPersistenceSnapshot.CurrentVersion,
                character,
                questState,
                equipmentDurability,
                pvpStatus,
                effects);
        }

        public static CompleteCharacterPersistenceRestoreResult Restore(
            CompleteCharacterPersistenceSnapshot snapshot,
            IItemRuleSource itemRules,
            CharacterBankRegistry banks,
            PlayerRespawnRegistry respawns,
            FastTravelStateRegistry fastTravelStates,
            QuestService quests,
            EquipmentDurabilityService durability,
            PvpService pvp,
            StatusEffectService statusEffects = null,
            IPotionEffectSource potionEffectDefinitions = null,
            long nowUnixMilliseconds = 0)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (itemRules == null) throw new ArgumentNullException(nameof(itemRules));
            if (banks == null) throw new ArgumentNullException(nameof(banks));
            if (respawns == null) throw new ArgumentNullException(nameof(respawns));
            if (fastTravelStates == null) throw new ArgumentNullException(nameof(fastTravelStates));
            if (quests == null) throw new ArgumentNullException(nameof(quests));
            if (durability == null) throw new ArgumentNullException(nameof(durability));
            if (pvp == null) throw new ArgumentNullException(nameof(pvp));
            if (snapshot.Version > CompleteCharacterPersistenceSnapshot.CurrentVersion)
                throw new InvalidOperationException("Complete character snapshot was written by a newer server version.");
            if (snapshot.StatusEffects.Effects.Count > 0 && (statusEffects == null || potionEffectDefinitions == null))
                throw new InvalidOperationException("Complete character snapshot contains timed status effects but no status-effect restore services were provided.");

            // Restore the canonical player/equipment first. Quest, durability, PvP and status state
            // then validate against that restored identity instead of the pre-load runtime object.
            var character = CharacterPersistenceSnapshotCodec.Restore(
                snapshot.Character,
                itemRules,
                banks,
                respawns,
                fastTravelStates);

            var player = character.Player;
            var questState = quests.RestoreState(player.CharacterId, snapshot.Quests);
            durability.Restore(player, snapshot.EquipmentDurability);
            var pvpStatus = pvp.RestoreStatus(player.CharacterId, snapshot.Pvp);

            if (statusEffects != null)
            {
                if (potionEffectDefinitions != null)
                    StatusEffectPersistenceSnapshotCodec.Restore(
                        snapshot.StatusEffects,
                        statusEffects,
                        player,
                        potionEffectDefinitions,
                        nowUnixMilliseconds);
                else
                    statusEffects.Clear(player);
            }

            return new CompleteCharacterPersistenceRestoreResult(
                character,
                questState,
                pvpStatus,
                durability.Capture(player),
                snapshot.StatusEffects);
        }
    }
}
