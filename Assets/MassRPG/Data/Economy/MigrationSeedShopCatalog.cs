using MassRPG.Core.Content;

namespace MassRPG.Data.Economy
{
    /// <summary>
    /// Browser-reference general-store stock. This preserves useful test/progression access while
    /// authored settlements and specialist shops are migrated into normal repository content data.
    /// </summary>
    public static class MigrationSeedShopCatalog
    {
        public static readonly ContentId GeneralStoreId = new ContentId("general_store");

        public static ShopCatalog Create()
        {
            var catalog = new ShopCatalog();
            catalog.Register(new ShopDefinition(
                GeneralStoreId,
                "General Store",
                new[]
                {
                    Id("bronze_hatchet"), Id("bronze_pickaxe"), Id("small_fishing_net"), Id("big_fishing_net"),
                    Id("fishing_rod"), Id("fly_fishing_rod"), Id("fishing_bait"), Id("lobster_pot"), Id("harpoon"),
                    Id("tinderbox"), Id("hammer"), Id("chisel"), Id("needle"), Id("saw"), Id("knife"), Id("shears"), Id("bucket"),
                    Id("pestle_and_mortar"), Id("glassblowing_pipe"),
                    Id("rake"), Id("spade"), Id("seed_dibber"), Id("gardening_trowel"), Id("watering_can"), Id("secateurs"),
                    Id("potato_seed"), Id("onion_seed"), Id("cabbage_seed"),
                    Id("ring_mold"), Id("amulet_mold"), Id("necklace_mold"), Id("bracelet_mold"), Id("tiara_mold"), Id("ammo_mold"),
                    Id("bronze_sword"), Id("bronze_shield"), Id("normal_shortbow"), Id("bronze_arrow"),
                    Id("bread"), Id("vial_of_water")
                },
                buysAnyItem: true,
                buyPriceNumerator: 1,
                buyPriceDenominator: 1,
                sellPriceNumerator: 1,
                sellPriceDenominator: 2));
            return catalog;
        }

        private static ContentId Id(string value) => new ContentId(value);
    }
}
