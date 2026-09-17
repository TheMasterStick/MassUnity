using MassRPG.Core.Content;
using MassRPG.Core.World;
using MassRPG.Data.World.Placements;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class WorldPlacementTests
    {
        [Test]
        public void PlacementPage_RoundTripsAnchoredResourcePresentationOffsets()
        {
            var placement = new WorldPlacementRecord(
                new ContentId("placement.test.ore_1"),
                new ContentId("resource.iron_rock"),
                WorldPlacementKind.Resource,
                new GridLocation(new GridCoord(1025, 1537), WorldConstants.SurfacePlane, 0))
            {
                OffsetX = 0.2f,
                OffsetY = -0.15f,
                YawDegrees = 35f,
                VisualHeightOffset = 0.05f
            };
            Assert.IsTrue(WorldAddressing.TryResolve(placement.Anchor, out var address));

            var document = WorldPlacementPageCodec.Encode(address.Key, new[] { placement });
            var copy = WorldPlacementPageCodec.Decode(document)[0];

            Assert.AreEqual(placement.InstanceId, copy.InstanceId);
            Assert.AreEqual(placement.DefinitionId, copy.DefinitionId);
            Assert.AreEqual(placement.Anchor, copy.Anchor);
            Assert.AreEqual(0.2f, copy.OffsetX, 0.0001f);
            Assert.AreEqual(-0.15f, copy.OffsetY, 0.0001f);
            Assert.AreEqual(35f, copy.YawDegrees, 0.0001f);
        }

        [Test]
        public void PlacementOffsets_StayInsideAnchorTile()
        {
            var placement = new WorldPlacementRecord(
                new ContentId("placement.test.doodad"),
                new ContentId("doodad.rock"),
                WorldPlacementKind.Doodad,
                new GridLocation(new GridCoord(10, 10), 0, 0))
            {
                OffsetX = 5f,
                OffsetY = -5f
            };

            Assert.AreEqual(0.49f, placement.OffsetX, 0.0001f);
            Assert.AreEqual(-0.49f, placement.OffsetY, 0.0001f);
        }
    }
}
