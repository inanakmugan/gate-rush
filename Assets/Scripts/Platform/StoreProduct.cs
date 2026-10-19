namespace GateRush.Platform
{
    /// <summary>
    /// One product a purchase service sells (Module 36, D72): its id, which
    /// the rules know it by, and its price as the platform shows it. The price
    /// is text because a real store formats it in the player's currency; the
    /// game never computes with it. Plain data with no checks of its own.
    /// </summary>
    public readonly struct StoreProduct
    {
        /// <summary>The product <paramref name="id"/>, priced <paramref name="priceText"/>.</summary>
        public StoreProduct(string id, string priceText)
        {
            Id = id;
            PriceText = priceText;
        }

        /// <summary>The product id, as the rules' gold packs name it.</summary>
        public string Id { get; }

        /// <summary>The price as the platform shows it, such as "$2.49".</summary>
        public string PriceText { get; }
    }
}
