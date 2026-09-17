using MassRPG.Core.Content;
using MassRPG.Core.World;
using MassRPG.Data.World;
using MassRPG.Data.World.Semantics;
using MassRPG.Server.Construction;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class AuthoredWorldPlotPlacementTests
    {
        [Test]
        public void Water_IsNonWalkableAndNonBuildableWithoutDuplicatedFlags()
        {
            var world = World();
            var water = Loc(2, 2);
            world.SetCell(water, new AuthoredTileCell(
                new ContentId("ground.sand"),
                0,
                TileFlags.Water));

            Assert.IsFalse(world.IsWalkable(water));
            Assert.IsFalse(world.IsBuildable(water));
            Assert.IsTrue(world.TryGetCell(water, out var cell));
            Assert.AreEqual(TileFlags.Water, cell.Flags);
        }

        [Test]
        public void PlotPlacement_RejectsWaterExplicitNoBuildAndSemanticProtection()
        {
            var world = World();
            var semantics = new WorldSemanticCatalog();
            var normal = Loc(1, 1);
            var water = Loc(2, 1);
            var tileNoBuild = Loc(3, 1);
            var semanticNoBuild = Loc(4, 1);
            var poiProtected = Loc(5, 1);

            world.SetCell(normal, Cell(TileFlags.None));
            world.SetCell(water, Cell(TileFlags.DeepWater));
            world.SetCell(tileNoBuild, Cell(TileFlags.NoBuild));
            world.SetCell(semanticNoBuild, Cell(TileFlags.None));
            world.SetCell(poiProtected, Cell(TileFlags.None));

            semantics.RegisterArea(new WorldAreaDefinition(
                new ContentId("nobuild.test"),
                "Protected strip",
                WorldAreaKind.NoBuild,
                new CircleAreaShape(semanticNoBuild.Tile, 0),
                PlayerMapVisibility.NotPlayerMapData,
                WorldConstants.SurfacePlane));

            var poi = new PointOfInterestDefinition(
                new ContentId("poi.test"),
                "Test POI",
                PointOfInterestKind.Landmark,
                poiProtected,
                MapMarkerCategory.Landmark);
            poi.ProtectionFootprint = new CircleAreaShape(poiProtected.Tile, 0);
            semantics.RegisterPointOfInterest(poi);

            var placement = new AuthoredWorldPlotPlacementMap(world, semantics);

            Assert.IsTrue(placement.CanReserveForPlayerPlot(normal));
            Assert.IsFalse(placement.CanReserveForPlayerPlot(water));
            Assert.IsFalse(placement.CanReserveForPlayerPlot(tileNoBuild));
            Assert.IsFalse(placement.CanReserveForPlayerPlot(semanticNoBuild));
            Assert.IsFalse(placement.CanReserveForPlayerPlot(poiProtected));
        }

        [Test]
        public void NoBuildArea_OnDifferentPlane_DoesNotLeakOntoSurface()
        {
            var world = World();
            var location = Loc(6, 6);
            world.SetCell(location, Cell(TileFlags.None));
            var semantics = new WorldSemanticCatalog();
            semantics.RegisterArea(new WorldAreaDefinition(
                new ContentId("nobuild.underground"),
                "Underground protection",
                WorldAreaKind.NoBuild,
                new CircleAreaShape(location.Tile, 1),
                PlayerMapVisibility.NotPlayerMapData,
                -1));

            var placement = new AuthoredWorldPlotPlacementMap(world, semantics);
            Assert.IsTrue(placement.CanReserveForPlayerPlot(location));
        }

        private static AuthoredWorldPageStore World()
            => new AuthoredWorldPageStore(new ContentId("ground.grass"), 8);

        private static AuthoredTileCell Cell(TileFlags flags)
            => new AuthoredTileCell(new ContentId("ground.grass"), 0, flags);

        private static GridLocation Loc(int x, int y)
            => new GridLocation(new GridCoord(x, y), WorldConstants.SurfacePlane, 0);
    }
}
