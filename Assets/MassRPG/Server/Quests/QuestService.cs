using System;
using System.Collections.Generic;
using MassRPG.Core.Characters;
using MassRPG.Core.Content;
using MassRPG.Core.Inventory;
using MassRPG.Core.World;
using MassRPG.Data.Quests;

namespace MassRPG.Server.Quests
{
    public enum QuestRunStatus
    {
        Active,
        ReadyToClaim,
        Completed
    }

    public sealed class QuestProgressState
    {
        private readonly Dictionary<string, int> _counts = new Dictionary<string, int>(StringComparer.Ordinal);

        internal QuestProgressState(ContentId questId)
        {
            QuestId = questId;
            Status = QuestRunStatus.Active;
        }

        public ContentId QuestId { get; }
        public QuestRunStatus Status { get; internal set; }
        public IReadOnlyDictionary<string, int> ObjectiveCounts => _counts;

        public int GetCount(string objectiveId) => _counts.TryGetValue(objectiveId, out var count) ? count : 0;

        internal void SetCount(string objectiveId, int count) => _counts[objectiveId] = count;
    }

    public sealed class CharacterQuestState
    {
        private readonly Dictionary<ContentId, QuestProgressState> _active = new Dictionary<ContentId, QuestProgressState>();
        private readonly HashSet<ContentId> _completed = new HashSet<ContentId>();

        public IEnumerable<QuestProgressState> Active => _active.Values;
        public IEnumerable<ContentId> Completed => _completed;

        public bool TryGetActive(ContentId questId, out QuestProgressState progress) => _active.TryGetValue(questId, out progress);
        public bool HasCompleted(ContentId questId) => _completed.Contains(questId);

        internal void Add(QuestProgressState progress) => _active.Add(progress.QuestId, progress);

        internal void Complete(ContentId questId, bool keepRepeatableCompletion)
        {
            _active.Remove(questId);
            if (keepRepeatableCompletion) _completed.Add(questId);
        }
    }

    public readonly struct QuestOperationResult
    {
        public QuestOperationResult(bool success, string code)
        {
            Success = success;
            Code = code;
        }

        public bool Success { get; }
        public string Code { get; }
    }

    /// <summary>
    /// Server-owned quest progression. Clients report only authoritative gameplay events routed by
    /// trusted server systems; they cannot submit arbitrary objective counts or next stages.
    /// </summary>
    public sealed class QuestService
    {
        private readonly IQuestDefinitionSource _definitions;
        private readonly IItemRuleSource _items;
        private readonly Dictionary<Guid, CharacterQuestState> _state = new Dictionary<Guid, CharacterQuestState>();

        public QuestService(IQuestDefinitionSource definitions, IItemRuleSource items)
        {
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            _items = items ?? throw new ArgumentNullException(nameof(items));
        }

        public CharacterQuestState GetOrCreateState(Guid characterId)
        {
            if (characterId == Guid.Empty) throw new ArgumentException("Character id cannot be empty.", nameof(characterId));
            if (!_state.TryGetValue(characterId, out var state))
            {
                state = new CharacterQuestState();
                _state.Add(characterId, state);
            }
            return state;
        }

        /// <summary>
        /// Replaces one character's runtime quest state from a versioned persistence snapshot.
        /// Definition validation happens before the live state is replaced, so malformed or stale
        /// objective ids cannot partially corrupt the active server state.
        /// </summary>
        public CharacterQuestState RestoreState(Guid characterId, CharacterQuestSnapshot snapshot)
        {
            if (characterId == Guid.Empty) throw new ArgumentException("Character id cannot be empty.", nameof(characterId));
            var restored = QuestPersistenceSnapshotCodec.Restore(
                snapshot ?? throw new ArgumentNullException(nameof(snapshot)),
                _definitions);
            _state[characterId] = restored;
            return restored;
        }

        public QuestOperationResult TryStart(PlayerState player, ContentId questId)
        {
            if (player == null) return new QuestOperationResult(false, "player_missing");
            if (!_definitions.TryGet(questId, out var definition)) return new QuestOperationResult(false, "quest_missing");
            var state = GetOrCreateState(player.CharacterId);
            if (state.TryGetActive(questId, out _)) return new QuestOperationResult(false, "already_active");
            if (!definition.Repeatable && state.HasCompleted(questId)) return new QuestOperationResult(false, "already_completed");

            for (var i = 0; i < definition.PrerequisiteQuestIds.Count; i++)
                if (!state.HasCompleted(definition.PrerequisiteQuestIds[i]))
                    return new QuestOperationResult(false, "prerequisite_quest_missing");

            for (var i = 0; i < definition.SkillRequirements.Count; i++)
            {
                var requirement = definition.SkillRequirements[i];
                if (player.Skills.GetLevel(requirement.Skill) < requirement.Level)
                    return new QuestOperationResult(false, "skill_requirement_not_met");
            }

            state.Add(new QuestProgressState(questId));
            return new QuestOperationResult(true, "ok");
        }

        public void RecordContentEvent(PlayerState player, QuestObjectiveKind kind, ContentId targetId, int amount = 1)
        {
            if (player == null || amount <= 0) return;
            var state = GetOrCreateState(player.CharacterId);
            var quests = new List<QuestProgressState>(state.Active);
            for (var q = 0; q < quests.Count; q++)
            {
                var progress = quests[q];
                if (progress.Status != QuestRunStatus.Active) continue;
                if (!_definitions.TryGet(progress.QuestId, out var definition)) continue;
                var changed = false;
                for (var i = 0; i < definition.Objectives.Count; i++)
                {
                    var objective = definition.Objectives[i];
                    if (objective.Kind != kind || !objective.TargetContentId.HasValue || objective.TargetContentId.Value != targetId) continue;
                    var current = progress.GetCount(objective.Id);
                    if (current >= objective.RequiredCount) continue;
                    progress.SetCount(objective.Id, Math.Min(objective.RequiredCount, checked(current + amount)));
                    changed = true;
                }
                if (changed) RefreshCompletion(definition, progress);
            }
        }

        public void RecordLocation(PlayerState player)
        {
            if (player == null) return;
            var state = GetOrCreateState(player.CharacterId);
            var quests = new List<QuestProgressState>(state.Active);
            for (var q = 0; q < quests.Count; q++)
            {
                var progress = quests[q];
                if (progress.Status != QuestRunStatus.Active) continue;
                if (!_definitions.TryGet(progress.QuestId, out var definition)) continue;
                var changed = false;
                for (var i = 0; i < definition.Objectives.Count; i++)
                {
                    var objective = definition.Objectives[i];
                    if (objective.Kind != QuestObjectiveKind.ReachLocation || !objective.TargetLocation.HasValue) continue;
                    var target = objective.TargetLocation.Value;
                    if (!player.Location.SameLayer(target)) continue;
                    if (GridMath.RangeDistance(player.Location.Tile, target.Tile) > objective.LocationRadiusTiles) continue;
                    if (progress.GetCount(objective.Id) >= objective.RequiredCount) continue;
                    progress.SetCount(objective.Id, objective.RequiredCount);
                    changed = true;
                }
                if (changed) RefreshCompletion(definition, progress);
            }
        }

        public QuestOperationResult TryClaim(PlayerState player, ContentId questId)
        {
            if (player == null) return new QuestOperationResult(false, "player_missing");
            if (!_definitions.TryGet(questId, out var definition)) return new QuestOperationResult(false, "quest_missing");
            var state = GetOrCreateState(player.CharacterId);
            if (!state.TryGetActive(questId, out var progress)) return new QuestOperationResult(false, "quest_not_active");
            if (progress.Status != QuestRunStatus.ReadyToClaim) return new QuestOperationResult(false, "objectives_incomplete");
            if (!CanFitRewards(player.Inventory, definition.ItemRewards)) return new QuestOperationResult(false, "inventory_full");

            for (var i = 0; i < definition.ItemRewards.Count; i++)
            {
                var reward = definition.ItemRewards[i];
                if (InventoryRules.AddItem(player.Inventory, _items, reward.ItemId, reward.Quantity) != reward.Quantity)
                    throw new InvalidOperationException("Quest reward capacity check failed.");
            }
            for (var i = 0; i < definition.XpRewards.Count; i++)
            {
                var reward = definition.XpRewards[i];
                player.Skills.AddXp(reward.Skill, reward.Xp);
            }

            progress.Status = QuestRunStatus.Completed;
            state.Complete(questId, true);
            return new QuestOperationResult(true, "ok");
        }

        private void RefreshCompletion(QuestDefinition definition, QuestProgressState progress)
        {
            for (var i = 0; i < definition.Objectives.Count; i++)
            {
                var objective = definition.Objectives[i];
                if (progress.GetCount(objective.Id) < objective.RequiredCount) return;
            }
            progress.Status = QuestRunStatus.ReadyToClaim;
        }

        private bool CanFitRewards(InventoryState inventory, IReadOnlyList<QuestItemReward> rewards)
        {
            var emptySlotsNeeded = 0;
            var stackableAlreadyPresent = new HashSet<ContentId>();
            for (var slot = 0; slot < inventory.Capacity; slot++)
            {
                var stack = inventory.GetSlot(slot);
                if (stack != null) stackableAlreadyPresent.Add(stack.ItemId);
            }

            var newStackableIds = new HashSet<ContentId>();
            for (var i = 0; i < rewards.Count; i++)
            {
                var reward = rewards[i];
                if (!_items.TryGetRule(reward.ItemId, out var rule)) return false;
                if (rule.Stackable)
                {
                    if (!stackableAlreadyPresent.Contains(reward.ItemId) && newStackableIds.Add(reward.ItemId)) emptySlotsNeeded++;
                }
                else
                {
                    emptySlotsNeeded = checked(emptySlotsNeeded + reward.Quantity);
                }
            }
            return emptySlotsNeeded <= inventory.EmptySlotCount;
        }
    }
}
