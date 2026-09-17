using System.Linq;
using MassRPG.Core.Content;
using MassRPG.Core.World;
using MassRPG.Data.World.Semantics;
using MassRPG.EditorCore.World;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class WorldSemanticTests
    {
        [Test]
        public void AreaLayersOverlapInsteadOfPartitioningTheWorld()
        {
            var catalog = new WorldSemanticCatalog();
            var shape = new CircleAreaShape(new GridCoord(100, 100), 20);
            catalog.RegisterArea(new WorldAreaDefinition(
                new ContentId("region.emberwatch"), "Emberwatch Region", WorldAreaKind.NamedRegion, shape, PlayerMapVisibility.Public));
            catalog.RegisterArea(new WorldAreaDefinition(
                new ContentId("biome.forest.emberwatch"), "Forest", WorldAreaKind.Biome, shape, PlayerMapVisibility.NotPlayerMapData));
            catalog.RegisterArea(new WorldAreaDefinition(
                new ContentId("spawn.wolves.emberwatch"), "Wolf population", WorldAreaKind.CreatureSpawnZone, shape, PlayerMapVisibility.NotPlayerMapData));

            var matches = catalog.AreasContaining(new GridCoord(100, 100));

            Assert.AreEqual(3, matches.Count);
            Assert.IsTrue(matches.Any(a => a.Kind == WorldAreaKind.NamedRegion));
            Assert.IsTrue(matches.Any(a => a.Kind == WorldAreaKind.CreatureSpawnZone));
        }

        [Test]
        public void RuntimeAreaQuery_RespectsPlane()
        {
            var catalog = new WorldSemanticCatalog();
            var shape = new CircleAreaShape(new GridCoord(100, 100), 20);
            catalog.RegisterArea(new WorldAreaDefinition(
                new ContentId("region.surface"), "Surface", WorldAreaKind.NamedRegion, shape,
                PlayerMapVisibility.Public, WorldConstants.SurfacePlane));
            catalog.RegisterArea(new WorldAreaDefinition(
                new ContentId("region.cave"), "Cave", WorldAreaKind.DungeonArea, shape,
                PlayerMapVisibility.NotPlayerMapData, WorldConstants.UndergroundPlane1));

            var surface = catalog.AreasContaining(new GridLocation(new GridCoord(100, 100), WorldConstants.SurfacePlane, 0));
            var cave = catalog.AreasContaining(new GridLocation(new GridCoord(100, 100), WorldConstants.UndergroundPlane1, 0));

            Assert.AreEqual(1, surface.Count);
            Assert.AreEqual(new ContentId("region.surface"), surface[0].Id);
            Assert.AreEqual(1, cave.Count);
            Assert.AreEqual(new ContentId("region.cave"), cave[0].Id);
        }

        [Test]
        public void AreaDocument_RoundTripsPolygonAndPlane()
        {
            var area = new WorldAreaDefinition(
                new ContentId("biome.forest.north"),
                "Northern Forest",
                WorldAreaKind.Biome,
                new PolygonAreaShape(new[]
                {
                    new GridCoord(10, 10), new GridCoord(30, 10), new GridCoord(25, 30), new GridCoord(8, 25)
                }),
                PlayerMapVisibility.NotPlayerMapData,
                WorldConstants.SurfacePlane);

            var copy = WorldAreaDocumentCodec.Decode(WorldAreaDocumentCodec.Encode(area));

            Assert.AreEqual(area.Id, copy.Id);
            Assert.AreEqual(WorldAreaKind.Biome, copy.Kind);
            Assert.AreEqual(WorldConstants.SurfacePlane, copy.Plane);
            Assert.IsTrue(copy.Shape.Contains(new GridCoord(20, 20)));
            Assert.IsFalse(copy.Shape.Contains(new GridCoord(50, 50)));
        }

        [Test]
        public void NormalPoiIsPublicButProtectionFootprintCanDifferFromVisibleFootprint()
        {
            var poi = new PointOfInterestDefinition(
                new ContentId("poi.capital"),
                "Capital",
                PointOfInterestKind.Capital,
                new GridLocation(new GridCoord(90000, 90000), WorldConstants.SurfacePlane, 0),
                MapMarkerCategory.Settlement);
            poi.VisibleFootprint = new CircleAreaShape(new GridCoord(90000, 90000), 40);
            poi.ProtectionFootprint = new CircleAreaShape(new GridCoord(90000, 90000), 80);

            Assert.AreEqual(PlayerMapVisibility.Public, poi.MapVisibility);
            Assert.IsFalse(poi.VisibleFootprint.Contains(new GridCoord(90060, 90000)));
            Assert.IsTrue(poi.ProtectionFootprint.Contains(new GridCoord(90060, 90000)));
        }

        [Test]
        public void CreatureAreasAreSystemDataNotPublicMapMarkersByDefault()
        {
            var area = new WorldAreaDefinition(
                new ContentId("spawn.wolves.test"),
                "Wolves",
                WorldAreaKind.CreatureSpawnZone,
                new CircleAreaShape(new GridCoord(50, 50), 10));

            Assert.AreEqual(PlayerMapVisibility.NotPlayerMapData, area.MapVisibility);
        }

        [Test]
        public void RoadDocument_RoundTripsAuthoringAndRoutingMetadata()
        {
            var road = new RoadDefinition(
                new ContentId("road.capital.north"),
                "North Road",
                new[] { new GridCoord(100, 100), new GridCoord(130, 115), new GridCoord(180, 115) })
            {
                WidthTiles = 5,
                SurfaceGroundId = new ContentId("ground.road.dirt"),
                MovementSpeedMultiplier = 1.05,
                RoutePreferenceWeight = 0.85,
                ReduceAggressiveSpawns = true
            };

            var copy = RoadDocumentCodec.Decode(RoadDocumentCodec.Encode(road));

            Assert.AreEqual(road.Id, copy.Id);
            Assert.AreEqual("North Road", copy.DisplayName);
            Assert.AreEqual(5, copy.WidthTiles);
            Assert.AreEqual(new ContentId("ground.road.dirt"), copy.SurfaceGroundId.Value);
            Assert.AreEqual(1.05, copy.MovementSpeedMultiplier, 0.0001);
            Assert.AreEqual(0.85, copy.RoutePreferenceWeight, 0.0001);
            Assert.AreEqual(3, copy.Points.Count);
            Assert.AreEqual(new GridCoord(180, 115), copy.Points[2]);
        }

        [Test]
        public void RoadDraft_BuildsPolylineAndRasterizesConfiguredWidth()
        {
            var draft = new RoadAuthoringDraft(new ContentId("road.test"), "Test Road");
            draft.WidthTiles = 3;
            draft.AddPoint(new GridCoord(20, 20));
            draft.AddPoint(new GridCoord(24, 20));
            var road = draft.Build();

            var tiles = RoadRasterizer.Rasterize(road);

            Assert.IsTrue(tiles.Contains(new GridCoord(22, 20)));
            Assert.IsTrue(tiles.Contains(new GridCoord(22, 19)));
            Assert.IsTrue(tiles.Contains(new GridCoord(22, 21)));
            Assert.IsFalse(tiles.Contains(new GridCoord(22, 23)));
        }
    }
}
