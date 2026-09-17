using MassRPG.Core.Content;
using MassRPG.Core.World;
using MassRPG.Data.World.Semantics;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class PointOfInterestDocumentTests
    {
        [Test]
        public void PointOfInterestDocument_RoundTripsFootprintsAndServices()
        {
            var poi = new PointOfInterestDefinition(
                new ContentId("poi.capital.bank"),
                "Royal Bank",
                PointOfInterestKind.Landmark,
                new GridLocation(new GridCoord(1000, 1200), WorldConstants.SurfacePlane, 0),
                MapMarkerCategory.Service,
                PlayerMapVisibility.Public)
            {
                VisibleFootprint = new CircleAreaShape(new GridCoord(1000, 1200), 4),
                ProtectionFootprint = new PolygonAreaShape(new[]
                {
                    new GridCoord(990, 1190), new GridCoord(1010, 1190),
                    new GridCoord(1010, 1210), new GridCoord(990, 1210)
                })
            };
            poi.AddServiceTag(new ContentId("service.bank"));
            poi.AddServiceTag(new ContentId("service.storage"));

            var copy = PointOfInterestDocumentCodec.Decode(PointOfInterestDocumentCodec.Encode(poi));

            Assert.AreEqual(poi.Id, copy.Id);
            Assert.AreEqual(MapMarkerCategory.Service, copy.MarkerCategory);
            Assert.AreEqual(new GridCoord(1000, 1200), copy.Center.Tile);
            Assert.IsTrue(copy.VisibleFootprint.Contains(new GridCoord(1002, 1200)));
            Assert.IsTrue(copy.ProtectionFootprint.Contains(new GridCoord(1009, 1209)));
            Assert.AreEqual(2, copy.ServiceTags.Count);
        }
    }
}
