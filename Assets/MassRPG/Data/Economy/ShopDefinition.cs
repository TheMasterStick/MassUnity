using System;
using System.Collections.Generic;
using MassRPG.Core.Content;

namespace MassRPG.Data.Economy
{
    /// <summary>
    /// Published shop definition. Stock membership and price policy are data; runtime ownership,
    /// finite-stock simulation and transaction authority remain server concerns.
    /// </summary>
    public sealed class ShopDefinition
    {
        private readonly HashSet<ContentId> _stock = new HashSet<ContentId>();

        public ShopDefinition(
            ContentId id,
            string displayName,
            IEnumerable<ContentId> stock,
            bool buysAnyItem = true,
            int buyPriceNumerator = 1,
            int buyPriceDenominator = 1,
            int sellPriceNumerator = 1,
            int sellPriceDenominator = 2)
        {
            if (id.IsEmpty) throw new ArgumentException("Shop id cannot be empty.", nameof(id));
            if (buyPriceNumerator < 0) throw new ArgumentOutOfRangeException(nameof(buyPriceNumerator));
            if (buyPriceDenominator <= 0) throw new ArgumentOutOfRangeException(nameof(buyPriceDenominator));
            if (sellPriceNumerator < 0) throw new ArgumentOutOfRangeException(nameof(sellPriceNumerator));
            if (sellPriceDenominator <= 0) throw new ArgumentOutOfRangeException(nameof(sellPriceDenominator));

            Id = id;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? id.Value : displayName;
            BuysAnyItem = buysAnyItem;
            BuyPriceNumerator = buyPriceNumerator;
            BuyPriceDenominator = buyPriceDenominator;
            SellPriceNumerator = sellPriceNumerator;
            SellPriceDenominator = sellPriceDenominator;

            if (stock != null)
            {
                foreach (var itemId in stock)
                    if (!itemId.IsEmpty) _stock.Add(itemId);
            }
        }

        public ContentId Id { get; }
        public string DisplayName { get; set; }
        public bool BuysAnyItem { get; set; }
        public int BuyPriceNumerator { get; set; }
        public int BuyPriceDenominator { get; set; }
        public int SellPriceNumerator { get; set; }
        public int SellPriceDenominator { get; set; }
        public IEnumerable<ContentId> Stock => _stock;

        public bool Sells(ContentId itemId) => _stock.Contains(itemId);
        public bool Buys(ContentId itemId) => BuysAnyItem || _stock.Contains(itemId);

        public int BuyUnitPrice(int baseValue)
            => ScalePrice(baseValue, BuyPriceNumerator, BuyPriceDenominator);

        public int SellUnitPrice(int baseValue)
            => Math.Max(1, ScalePrice(baseValue, SellPriceNumerator, SellPriceDenominator));

        private static int ScalePrice(int baseValue, int numerator, int denominator)
        {
            if (baseValue <= 0 || numerator == 0) return 0;
            var scaled = checked((long)baseValue * numerator / denominator);
            if (scaled > int.MaxValue) throw new OverflowException("Shop price exceeds supported integer currency range.");
            return (int)scaled;
        }
    }

    public interface IShopDefinitionSource
    {
        bool TryGet(ContentId shopId, out ShopDefinition definition);
    }

    public sealed class ShopCatalog : IShopDefinitionSource
    {
        private readonly Dictionary<ContentId, ShopDefinition> _definitions = new Dictionary<ContentId, ShopDefinition>();

        public IEnumerable<ShopDefinition> All => _definitions.Values;

        public void Register(ShopDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (_definitions.ContainsKey(definition.Id))
                throw new InvalidOperationException($"Duplicate shop id '{definition.Id}'.");
            _definitions.Add(definition.Id, definition);
        }

        public bool TryGet(ContentId shopId, out ShopDefinition definition)
            => _definitions.TryGetValue(shopId, out definition);
    }
}
