namespace MassRPG.Core.World
{
    public static class WorldConstants
    {
        public const int WorldWidthTiles = 180000;
        public const int WorldHeightTiles = 180000;

        /// <summary>
        /// Current render/streaming chunk starting point. Benchmark and change if needed;
        /// this is not part of the authored world's semantic hierarchy.
        /// </summary>
        public const int DefaultRenderChunkSize = 64;

        /// <summary>
        /// Current storage-page starting point. A page is an IO/editing unit, not a semantic region.
        /// 512 divides cleanly into 8x8 of the current 64x64 render chunks.
        /// </summary>
        public const int DefaultStoragePageSize = 512;

        public const int SurfacePlane = 0;
        public const int UndergroundPlane1 = -1;
        public const int UndergroundPlane2 = -2;

        public static bool IsInsideWorld(GridCoord tile)
            => tile.X >= 0 && tile.Y >= 0 && tile.X < WorldWidthTiles && tile.Y < WorldHeightTiles;
    }
}
