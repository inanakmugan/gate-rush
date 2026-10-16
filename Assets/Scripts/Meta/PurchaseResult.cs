namespace GateRush.Meta
{
    /// <summary>What a purchase in the store gave (D62).</summary>
    public enum PurchaseResult
    {
        /// <summary>The price was spent and the item given: the moment to save.</summary>
        Bought,

        /// <summary>The balance does not cover the price. Nothing changed.</summary>
        NotEnoughGold,

        /// <summary>A refill was asked for with the lives already full. Nothing was spent.</summary>
        AlreadyFull,
    }
}
