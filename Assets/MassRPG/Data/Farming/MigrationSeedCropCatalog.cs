using MassRPG.Core.Content;

namespace MassRPG.Data.Farming
{
    /// <summary>
    /// Farming values migrated from the browser prototype. Growth times preserve prototype parity
    /// for testing and remain balance data rather than hard-coded simulation constants.
    /// </summary>
    public static class MigrationSeedCropCatalog
    {
        private const int PlantXp = 12;

        public static CropCatalog Create()
        {
            var catalog = new CropCatalog();

            Crop(catalog, "potato", "Potato", 1, 8, 24_000);
            Crop(catalog, "onion", "Onion", 5, 10, 30_000);
            Crop(catalog, "cabbage", "Cabbage", 7, 12, 33_000);
            Crop(catalog, "sweetcorn", "Sweetcorn", 20, 17, 42_000);
            Crop(catalog, "strawberry", "Strawberry", 31, 25, 54_000);
            Crop(catalog, "watermelon", "Watermelon", 47, 48, 72_000);

            Herb(catalog, "guam", "Guam leaf", 3, 12, 36_000);
            Herb(catalog, "marrentill", "Marrentill", 5, 14, 39_000);
            Herb(catalog, "harralander", "Harralander", 9, 18, 45_000);
            Herb(catalog, "ranarr", "Ranarr weed", 25, 30, 60_000);
            Herb(catalog, "irit", "Irit leaf", 44, 48, 78_000);
            Herb(catalog, "avantoe", "Avantoe", 50, 55, 87_000);

            return catalog;
        }

        private static void Crop(CropCatalog catalog, string id, string name, int level, int xp, int growthMilliseconds)
        {
            catalog.Register(new CropDefinition(
                new ContentId(id), name, FarmPatchKind.Crop, level, PlantXp, xp, growthMilliseconds,
                new ContentId(id + "_seed"), new ContentId(id)));
        }

        private static void Herb(CropCatalog catalog, string id, string name, int level, int xp, int growthMilliseconds)
        {
            catalog.Register(new CropDefinition(
                new ContentId(id), name, FarmPatchKind.Herb, level, PlantXp, xp, growthMilliseconds,
                new ContentId(id + "_seed"), new ContentId("grimy_" + id)));
        }
    }
}
