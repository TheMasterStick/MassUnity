using System.Linq;
using MassRPG.Data.Construction;
using MassRPG.Data.Skills;
using MassRPG.Data.Validation;
using UnityEditor;
using UnityEngine;

namespace MassRPG.Editor.Data
{
    /// <summary>
    /// One-click cross-content validation for repository drafts. This is the first piece of the
    /// eventual publish gate: local draft files must be structurally valid and their references must
    /// resolve before a versioned package can be promoted.
    /// </summary>
    public static class RepositoryDraftAuditCommand
    {
        [MenuItem("MassRPG/Validate Repository Drafts", priority = 22)]
        public static void Run()
        {
            var items = RepositoryDraftItemStore.CreateResolvedCatalog(out var itemRecords);
            var creatures = RepositoryDraftGameDataStore.CreateResolvedCreatureCatalog(out var creatureRecords);
            var resources = RepositoryDraftGameDataStore.CreateDraftResourceCatalog(out var resourceRecords);
            var recipes = RepositoryDraftGameDataStore.CreateResolvedRecipeCatalog(out var recipeRecords);
            var buildPieces = MigrationSeedBuildPieceCatalog.Create();

            var structuralErrors = itemRecords.Count(record => !record.IsValid)
                + creatureRecords.Count(record => !record.IsValid)
                + resourceRecords.Count(record => !record.IsValid)
                + recipeRecords.Count(record => !record.IsValid);

            var issues = ContentCatalogAudit.Audit(
                items,
                creatures,
                recipes,
                SkillCatalog.All,
                buildPieces,
                resources);

            var errors = issues.Count(issue => issue.Severity == ContentAuditSeverity.Error) + structuralErrors;
            var warnings = issues.Count(issue => issue.Severity == ContentAuditSeverity.Warning);
            var info = issues.Count(issue => issue.Severity == ContentAuditSeverity.Info);

            foreach (var record in itemRecords.Where(record => !record.IsValid))
                Debug.LogError($"MassRPG draft item '{record.Id}': {record.Error}");
            foreach (var record in creatureRecords.Where(record => !record.IsValid))
                Debug.LogError($"MassRPG draft creature '{record.Id}': {record.Error}");
            foreach (var record in resourceRecords.Where(record => !record.IsValid))
                Debug.LogError($"MassRPG draft resource '{record.Id}': {record.Error}");
            foreach (var record in recipeRecords.Where(record => !record.IsValid))
                Debug.LogError($"MassRPG draft recipe '{record.Id}': {record.Error}");

            for (var i = 0; i < issues.Count; i++)
            {
                var issue = issues[i];
                if (issue.Severity == ContentAuditSeverity.Error) Debug.LogError(issue.ToString());
                else if (issue.Severity == ContentAuditSeverity.Warning) Debug.LogWarning(issue.ToString());
                else Debug.Log(issue.ToString());
            }

            var summary = $"Repository draft audit complete.\n\nErrors: {errors}\nWarnings: {warnings}\nInfo: {info}\n\n"
                + (errors == 0
                    ? "No blocking errors were found. This still does not publish the data."
                    : "Blocking errors were found. See the Unity Console for exact files/references.");
            EditorUtility.DisplayDialog("MassRPG Draft Audit", summary, "OK");
        }
    }
}
