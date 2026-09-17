using MassRPG.Core.Content;
using MassRPG.Core.World;
using MassRPG.Data.World;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class WorldPageImportTests
    {
        [Test]
        public void DecodedPage_CanBeImportedWithoutLoadingWholeWorld()
        {
            var source = new AuthoredWorldPageStore(new ContentId("ground.unpainted"));
            var location = new GridLocation(new GridCoord(620, 700), WorldConstants.SurfacePlane, 0);
            source.SetCell(location, new AuthoredTileCell(new ContentId("ground.grass"), 2, TileFlags.NoBuild));
            Assert.IsTrue(WorldAddressing.TryResolve(location, out var address));
            Assert.IsTrue(source.TryGetPage(address.Key, out var page));

            var decoded = WorldPageCodec.Decode(WorldPageCodec.Encode(page));
            var destination = new AuthoredWorldPageStore(new ContentId("ground.unpainted"));
            destination.ImportPage(decoded);

            Assert.AreEqual(1, destination.LoadedPageCount);
            Assert.IsTrue(destination.TryGetCell(location, out var cell));
            Assert.AreEqual(new ContentId("ground.grass"), cell.GroundId);
            Assert.AreEqual(2, cell.Elevation);
            Assert.IsTrue((cell.Flags & TileFlags.NoBuild) != 0);
        }
    }
}
