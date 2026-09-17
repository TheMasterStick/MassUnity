using MassRPG.Core.Content;

namespace MassRPG.Data.Firemaking
{
    /// <summary>
    /// Browser-reference log requirements/XP. Campfire lifetime preserves the old 150 x 600ms
    /// duration as data for parity testing, not as a permanent MMO balance constant.
    /// </summary>
    public static class MigrationSeedFiremakingCatalog
    {
        private const int CampfireLifetimeMilliseconds = 90_000;

        public static FiremakingCatalog Create()
        {
            var catalog = new FiremakingCatalog();
            Register(catalog, "normal_logs", 1, 40);
            Register(catalog, "oak_logs", 15, 60);
            Register(catalog, "willow_logs", 30, 90);
            Register(catalog, "maple_logs", 45, 135);
            Register(catalog, "yew_logs", 60, 202);
            Register(catalog, "magic_logs", 75, 303);
            return catalog;
        }

        private static void Register(FiremakingCatalog catalog, string logItemId, int level, int xp)
            => catalog.Register(new FiremakingDefinition(new ContentId(logItemId), level, xp, CampfireLifetimeMilliseconds));
    }
}
