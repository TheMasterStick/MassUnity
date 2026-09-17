using System;
using System.Collections.Generic;
using MassRPG.Core.Content;
using MassRPG.Data.Quests;

namespace MassRPG.Server.Quests
{
    public readonly struct QuestObjectiveProgressSnapshot
    {
        public QuestObjectiveProgressSnapshot(string objectiveId, int count)
        {
            if (string.IsNullOrWhiteSpace(objectiveId)) throw new ArgumentException("Objective id cannot be empty.", nameof(objectiveId));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            ObjectiveId = objectiveId;
            Count = count;
        }

        public string ObjectiveId { get; }
        public int Count { get; }
    }

    public sealed class ActiveQuestSnapshot
    {
        public ActiveQuestSnapshot(ContentId questId, QuestRunStatus status, IReadOnlyList<QuestObjectiveProgressSnapshot> objectives)
        {
            if (questId.IsEmpty) throw new ArgumentException("Quest id cannot be empty.", nameof(questId));
            if (status == QuestRunStatus.Completed) throw new ArgumentException("Completed quests belong in the completed-id set, not active quest state.", nameof(status));
            QuestId = questId;
            Status = status;
            Objectives = objectives ?? Array.Empty<QuestObjectiveProgressSnapshot>();
        }

        public ContentId QuestId { get; }
        public QuestRunStatus Status { get; }
        public IReadOnlyList<QuestObjectiveProgressSnapshot> Objectives { get; }
    }

    public sealed class CharacterQuestSnapshot
    {
        public const int CurrentVersion = 1;

        public CharacterQuestSnapshot(
            int version,
            IReadOnlyList<ActiveQuestSnapshot> activeQuests,
            IReadOnlyList<ContentId> completedQuestIds)
        {
            if (version < 1) throw new ArgumentOutOfRangeException(nameof(version));
            Version = version;
            ActiveQuests = activeQuests ?? Array.Empty<ActiveQuestSnapshot>();
            CompletedQuestIds = completedQuestIds ?? Array.Empty<ContentId>();
        }

        public int Version { get; }
        public IReadOnlyList<ActiveQuestSnapshot> ActiveQuests { get; }
        public IReadOnlyList<ContentId> CompletedQuestIds { get; }
    }

    /// <summary>
    /// Versioned engine-independent quest persistence. Objective ids, not list positions, are saved
    /// so content can be reviewed/migrated explicitly instead of silently attaching old progress to
    /// a different objective after a quest definition is edited.
    /// </summary>
    public static class QuestPersistenceSnapshotCodec
    {
        public static CharacterQuestSnapshot Capture(CharacterQuestState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var active = new List<ActiveQuestSnapshot>();
            foreach (var progress in state.Active)
            {
                var objectives = new List<QuestObjectiveProgressSnapshot>();
                foreach (var pair in progress.ObjectiveCounts)
                    objectives.Add(new QuestObjectiveProgressSnapshot(pair.Key, pair.Value));
                active.Add(new ActiveQuestSnapshot(progress.QuestId, progress.Status, objectives));
            }

            var completed = new List<ContentId>();
            foreach (var questId in state.Completed) completed.Add(questId);
            return new CharacterQuestSnapshot(CharacterQuestSnapshot.CurrentVersion, active, completed);
        }

        public static CharacterQuestState Restore(CharacterQuestSnapshot snapshot, IQuestDefinitionSource definitions)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            if (snapshot.Version > CharacterQuestSnapshot.CurrentVersion)
                throw new InvalidOperationException("Quest snapshot was written by a newer server version.");

            var state = new CharacterQuestState();
            var seenQuestIds = new HashSet<ContentId>();
            for (var i = 0; i < snapshot.ActiveQuests.Count; i++)
            {
                var saved = snapshot.ActiveQuests[i] ?? throw new InvalidOperationException("Quest snapshot contains a null active quest.");
                if (!seenQuestIds.Add(saved.QuestId)) throw new InvalidOperationException("Quest snapshot contains duplicate active quest ids.");
                if (!definitions.TryGet(saved.QuestId, out var definition))
                    throw new InvalidOperationException("Quest snapshot references unknown quest '" + saved.QuestId + "'.");

                var objectiveDefinitions = new Dictionary<string, QuestObjectiveDefinition>(StringComparer.Ordinal);
                for (var o = 0; o < definition.Objectives.Count; o++)
                    objectiveDefinitions.Add(definition.Objectives[o].Id, definition.Objectives[o]);

                var progress = new QuestProgressState(saved.QuestId);
                var seenObjectives = new HashSet<string>(StringComparer.Ordinal);
                for (var o = 0; o < saved.Objectives.Count; o++)
                {
                    var savedObjective = saved.Objectives[o];
                    if (!seenObjectives.Add(savedObjective.ObjectiveId))
                        throw new InvalidOperationException("Quest snapshot contains duplicate objective progress for '" + savedObjective.ObjectiveId + "'.");
                    if (!objectiveDefinitions.TryGetValue(savedObjective.ObjectiveId, out var objective))
                        throw new InvalidOperationException("Quest snapshot references unknown objective '" + savedObjective.ObjectiveId + "'.");
                    if (savedObjective.Count > objective.RequiredCount)
                        throw new InvalidOperationException("Quest snapshot objective progress exceeds its required count.");
                    progress.SetCount(savedObjective.ObjectiveId, savedObjective.Count);
                }

                progress.Status = saved.Status;
                if (saved.Status == QuestRunStatus.ReadyToClaim)
                {
                    for (var o = 0; o < definition.Objectives.Count; o++)
                    {
                        var objective = definition.Objectives[o];
                        if (progress.GetCount(objective.Id) < objective.RequiredCount)
                            throw new InvalidOperationException("Quest marked ready to claim while objectives are incomplete.");
                    }
                }
                state.Add(progress);
            }

            var completedSeen = new HashSet<ContentId>();
            for (var i = 0; i < snapshot.CompletedQuestIds.Count; i++)
            {
                var questId = snapshot.CompletedQuestIds[i];
                if (questId.IsEmpty) throw new InvalidOperationException("Quest snapshot contains an empty completed quest id.");
                if (!completedSeen.Add(questId)) throw new InvalidOperationException("Quest snapshot contains duplicate completed quest ids.");
                if (seenQuestIds.Contains(questId)) throw new InvalidOperationException("Quest cannot be active and completed in the same snapshot.");
                if (!definitions.TryGet(questId, out _)) throw new InvalidOperationException("Quest snapshot references unknown completed quest '" + questId + "'.");
                state.Complete(questId, true);
            }

            return state;
        }
    }
}
