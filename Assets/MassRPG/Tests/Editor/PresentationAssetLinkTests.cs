using MassRPG.Core.Content;
using MassRPG.EditorCore.Data;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class PresentationAssetLinkTests
    {
        [Test]
        public void StableAssetIds_RoundTripRoleAndContentId()
        {
            var source = new ContentId("item.iron_sword");
            var assetId = PresentationAssetId.For(source, PresentationAssetRole.Model);

            Assert.AreEqual("asset/model/item.iron_sword", assetId.Value);
            Assert.IsTrue(PresentationAssetId.TryParse(assetId, out var role, out var contentId));
            Assert.AreEqual(PresentationAssetRole.Model, role);
            Assert.AreEqual(source, contentId);
        }

        [Test]
        public void JsonPatcher_ReplacesPresentationWithoutTouchingFlexibleData()
        {
            const string original = "{\n  \"id\": \"quest.test\",\n  \"data\": { \"unknownFutureField\": [1, 2, {\"x\":true}] },\n  \"presentation\": { \"assetState\": \"needs-assets\", \"notes\": \"old\" }\n}\n";
            const string replacement = "{\n    \"assetState\": \"linked\",\n    \"modelAssetId\": \"asset/model/quest.test\"\n  }";

            var patched = JsonTopLevelPropertyPatcher.UpsertObjectProperty(original, "presentation", replacement);

            StringAssert.Contains("\"unknownFutureField\": [1, 2, {\"x\":true}]", patched);
            StringAssert.Contains("\"assetState\": \"linked\"", patched);
            StringAssert.Contains("\"modelAssetId\": \"asset/model/quest.test\"", patched);
            StringAssert.DoesNotContain("\"notes\": \"old\"", patched);
        }

        [Test]
        public void JsonPatcher_InsertsMissingPresentationAtTopLevel()
        {
            const string original = "{\n  \"id\": \"npc.test\",\n  \"data\": {\"nested\":{\"presentation\":\"leave me\"}}\n}\n";
            const string replacement = "{\"assetState\":\"needs-assets\"}";

            var patched = JsonTopLevelPropertyPatcher.UpsertObjectProperty(original, "presentation", replacement);

            StringAssert.Contains("\"presentation\": {\"assetState\":\"needs-assets\"}", patched);
            StringAssert.Contains("\"nested\":{\"presentation\":\"leave me\"}", patched);
        }
    }
}
