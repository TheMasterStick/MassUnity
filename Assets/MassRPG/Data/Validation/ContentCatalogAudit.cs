using System;
using System.Collections.Generic;
using MassRPG.Core.Content;
using MassRPG.Core.Skills;
using MassRPG.Data.Construction;
using MassRPG.Data.Creatures;
using MassRPG.Data.Items;
using MassRPG.Data.Recipes;
using MassRPG.Data.Resources;
using MassRPG.Data.Skills;

namespace MassRPG.Data.Validation
{
    public enum ContentAuditSeverity
    {
        Info,
        Warning,
        Error
    }

    public sealed class ContentAuditIssue
    {
        public ContentAuditIssue(ContentAuditSeverity severity, string ownerType, ContentId ownerId, string field, string message)
        {
            Severity = severity;
            OwnerType = ownerType ?? string.Empty;
            OwnerId = ownerId;
            Field = field ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public ContentAuditSeverity Severity { get; }
        public string OwnerType { get; }
        public ContentId OwnerId { get; }
        public string Field { get; }
        public string Message { get; }

        public override string ToString()
            => $"{Severity}: {OwnerType} '{OwnerId}' {Field} - {Message}";
    }

    /// <summary>
    /// Cross-reference validation shared by the future Data Editor and publishing pipeline. Model
    /// constructors already validate their own numeric invariants; this catches references that can
    /// only be checked once several catalogs are loaded together.
    /// </summary>
    public static class ContentCatalogAudit
    {
        public static IReadOnlyList<ContentAuditIssue> Audit(
            ItemCatalog items,
            CreatureCatalog creatures,
            RecipeCatalog recipes,
            IReadOnlyList<SkillDefinition> skills,
            BuildPieceCatalog buildPieces,
            ResourceCatalog resources = null)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (creatures == null) throw new ArgumentNullException(nameof(creatures));
            if (recipes == null) throw new ArgumentNullException(nameof(recipes));
            if (skills == null) throw new ArgumentNullException(nameof(skills));
            if (buildPieces == null) throw new ArgumentNullException(nameof(buildPieces));

            var issues = new List<ContentAuditIssue>();
            AuditRecipes(items, recipes, issues);
            AuditBuildPieces(items, buildPieces, issues);
            AuditSkills(skills, issues);
            if (resources != null) AuditResources(items, resources, issues);

            // Creature definitions currently have no external content references. Keeping the
            // argument here makes the audit signature stable when drops/abilities are data-driven.
            foreach (var creature in creatures.All)
            {
                if (string.IsNullOrWhiteSpace(creature.DisplayName))
                    Add(issues, ContentAuditSeverity.Warning, "Creature", creature.Id, "DisplayName", "Visible name is empty.");
            }

            foreach (var item in items.All)
            {
                if (string.IsNullOrWhiteSpace(item.DisplayName))
                    Add(issues, ContentAuditSeverity.Warning, "Item", item.Id, "DisplayName", "Visible name is empty.");
            }

            return issues;
        }

        private static void AuditRecipes(ItemCatalog items, RecipeCatalog recipes, List<ContentAuditIssue> issues)
        {
            foreach (var recipe in recipes.All)
            {
                for (var i = 0; i < recipe.Inputs.Count; i++)
                {
                    var input = recipe.Inputs[i];
                    if (!items.TryGetDefinition(input.ItemId, out _))
                        MissingItem(issues, "Recipe", recipe.Id, $"Inputs[{i}]", input.ItemId);
                }

                if (!items.TryGetDefinition(recipe.OutputItemId, out _))
                    MissingItem(issues, "Recipe", recipe.Id, "OutputItemId", recipe.OutputItemId);

                if (recipe.ToolRequiredId.HasValue && !items.TryGetDefinition(recipe.ToolRequiredId.Value, out _))
                    MissingItem(issues, "Recipe", recipe.Id, "ToolRequiredId", recipe.ToolRequiredId.Value);

                if (recipe.FailureOutputItemId.HasValue && !items.TryGetDefinition(recipe.FailureOutputItemId.Value, out _))
                    MissingItem(issues, "Recipe", recipe.Id, "FailureOutputItemId", recipe.FailureOutputItemId.Value);

                if (string.IsNullOrWhiteSpace(recipe.DisplayName))
                    Add(issues, ContentAuditSeverity.Warning, "Recipe", recipe.Id, "DisplayName", "Visible name is empty.");
                if (string.IsNullOrWhiteSpace(recipe.Category))
                    Add(issues, ContentAuditSeverity.Info, "Recipe", recipe.Id, "Category", "Recipe has no browser/editor category.");
            }
        }

        private static void AuditBuildPieces(ItemCatalog items, BuildPieceCatalog buildPieces, List<ContentAuditIssue> issues)
        {
            foreach (var piece in buildPieces.All)
            {
                for (var i = 0; i < piece.Costs.Count; i++)
                {
                    var cost = piece.Costs[i];
                    if (!items.TryGetDefinition(cost.ItemId, out _))
                        MissingItem(issues, "BuildPiece", piece.Id, $"Costs[{i}]", cost.ItemId);
                }

                if (string.IsNullOrWhiteSpace(piece.DisplayName))
                    Add(issues, ContentAuditSeverity.Warning, "BuildPiece", piece.Id, "DisplayName", "Visible name is empty.");
            }
        }

        private static void AuditResources(ItemCatalog items, ResourceCatalog resources, List<ContentAuditIssue> issues)
        {
            foreach (var resource in resources.All)
            {
                if (!items.TryGetDefinition(resource.YieldItemId, out _))
                    MissingItem(issues, "Resource", resource.Id, "YieldItemId", resource.YieldItemId);
                if (string.IsNullOrWhiteSpace(resource.DisplayName))
                    Add(issues, ContentAuditSeverity.Warning, "Resource", resource.Id, "DisplayName", "Visible name is empty.");
            }
        }

        private static void AuditSkills(IReadOnlyList<SkillDefinition> skills, List<ContentAuditIssue> issues)
        {
            var ids = new HashSet<ContentId>();
            var enumKeys = new HashSet<SkillId>();
            for (var i = 0; i < skills.Count; i++)
            {
                var skill = skills[i];
                if (!ids.Add(skill.Id))
                    Add(issues, ContentAuditSeverity.Error, "Skill", skill.Id, "Id", "Duplicate permanent skill ID.");
                if (!enumKeys.Add(skill.Skill))
                    Add(issues, ContentAuditSeverity.Error, "Skill", skill.Id, "Skill", $"Skill enum key '{skill.Skill}' is registered more than once.");
                if (string.IsNullOrWhiteSpace(skill.DisplayName))
                    Add(issues, ContentAuditSeverity.Warning, "Skill", skill.Id, "DisplayName", "Visible name is empty.");
            }
        }

        private static void MissingItem(
            List<ContentAuditIssue> issues,
            string ownerType,
            ContentId ownerId,
            string field,
            ContentId missingId)
            => Add(
                issues,
                ContentAuditSeverity.Error,
                ownerType,
                ownerId,
                field,
                $"References missing item '{missingId.Value}'.");

        private static void Add(
            List<ContentAuditIssue> issues,
            ContentAuditSeverity severity,
            string ownerType,
            ContentId ownerId,
            string field,
            string message)
            => issues.Add(new ContentAuditIssue(severity, ownerType, ownerId, field, message));
    }
}
