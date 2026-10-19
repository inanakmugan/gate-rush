namespace GateRush.Runtime
{
    /// <summary>
    /// A gold pack as the Store shows it (Module 36, D72): the product id, the
    /// gold from the rules, and the price text from the purchase service.
    /// Built by <see cref="GoldOffers.Match"/> only for packs both sides know.
    /// </summary>
    public readonly struct GoldOffer
    {
        /// <summary>The offer of <paramref name="productId"/>: <paramref name="gold"/> for <paramref name="priceText"/>.</summary>
        public GoldOffer(string productId, int gold, string priceText)
        {
            ProductId = productId;
            Gold = gold;
            PriceText = priceText;
        }

        /// <summary>The product id, as the rules and the purchase service both know it.</summary>
        public string ProductId { get; }

        /// <summary>The gold the pack adds to the balance.</summary>
        public int Gold { get; }

        /// <summary>The price as the purchase service shows it.</summary>
        public string PriceText { get; }
    }
}
