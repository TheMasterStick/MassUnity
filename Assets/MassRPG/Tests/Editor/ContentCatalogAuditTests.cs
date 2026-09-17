using System.Linq;
using MassRPG.Core.Content;
using MassRPG.Core.Skills;
using MassRPG.Data.Construction;
using MassRPG.Data.Creatures;
using MassRPG.Data.Items;
using MassRPG.Data.Recipes;
using MassRPG.Data.Skills;
using MassRPG.Data.Validation;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class ContentCatalogAuditTests
    {
        [Test]
        public void MissingRecipeOutput_IsReportedAsError()
        {
            var items = new ItemCatalog();
            items.Register(new ItemDefinition(new ContentId("known_input"), "Known input", ItemType.Material, true));
            var recipes = new RecipeCatalog();
            recipes.Register(new RecipeDefinition(
                new ContentId("test_recipe"),
                "Test recipe",
                SkillId.Crafting,
                1,
                new[] { new RecipeIngredient(new ContentId("known_input"), 1) },
                new ContentId("missing_output"),
                1,
                1,
                1000,
                "Test"));

            var issues = ContentCatalogAudit.Audit(
                items,
                new CreatureCatalog(),
                recipes,
                SkillCatalog.All,
                new BuildPieceCatalog());

            Assert.IsTrue(issues.Any(issue =>
                issue.Severity == ContentAuditSeverity.Error
                && issue.OwnerId == new ContentId("test_recipe")
                && issue.Field == "OutputItemId"
                && issue.Message.Contains("missing_output")));
        }

        [Test]
        public void MissingBuildMaterial_IsReportedAsError()
        {
            var pieces = new BuildPieceCatalog();
            pieces.Register(new BuildPieceDefinition(
                new ContentId("build.test_wall"),
                "Test wall",
                BuildPieceKind.Wall,
                BuildPlacementMode.CardinalEdge,
                BuildOccupancyLayer.Structure,
                1,
                1,
                new[] { new BuildMaterialCost(new ContentId("missing_plank"), 2) }));

            var issues = ContentCatalogAudit.Audit(
                new ItemCatalog(),
                new CreatureCatalog(),
                new RecipeCatalog(),
                SkillCatalog.All,
                pieces);

            Assert.IsTrue(issues.Any(issue =>
                issue.Severity == ContentAuditSeverity.Error
                && issue.OwnerId == new ContentId("build.test_wall")
                && issue.Field == "Costs[0]"));
        }

        [Test]
        public void ValidSmallReferenceGraph_HasNoErrors()
        {
            var items = new ItemCatalog();
            items.Register(new ItemDefinition(new ContentId("material"), "Material", ItemType.Material, true));
            items.Register(new ItemDefinition(new ContentId("output"), "Output", ItemType.Miscellaneous, false));

            var recipes = new RecipeCatalog();
            recipes.Register(new RecipeDefinition(
                new ContentId("make_output"),
                "Make output",
                SkillId.Crafting,
                1,
                new[] { new RecipeIngredient(new ContentId("material"), 1) },
                new ContentId("output"),
                1,
                1,
                1000,
                "Test"));

            var pieces = new BuildPieceCatalog();
            pieces.Register(new BuildPieceDefinition(
                new ContentId("build.test_floor"),
                "Test floor",
                BuildPieceKind.Floor,
                BuildPlacementMode.Tile,
                BuildOccupancyLayer.Surface,
                1,
                1,
                new[] { new BuildMaterialCost(new ContentId("material"), 1) }));

            var issues = ContentCatalogAudit.Audit(
                items,
                new CreatureCatalog(),
                recipes,
                SkillCatalog.All,
                pieces);

            Assert.IsFalse(issues.Any(issue => issue.Severity == ContentAuditSeverity.Error));
        }
    }
}
