namespace GateRush.Platform
{
    /// <summary>How a purchase through <see cref="IPurchaseService"/> ended (Module 36, D72).</summary>
    public enum PurchaseOutcome
    {
        /// <summary>The player paid: the moment to give what was bought and save.</summary>
        Succeeded,

        /// <summary>The player declined. Nothing was charged.</summary>
        Cancelled,

        /// <summary>The purchase could not be made. Nothing was charged.</summary>
        Failed,
    }
}
