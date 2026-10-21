namespace GateRush.Runtime
{
    /// <summary>
    /// What a joker button's badge shows (Modules 40 and 43): a count, a price
    /// beside the coin, or the level a locked joker opens at.
    /// </summary>
    public enum JokerBadge
    {
        /// <summary>How many are held in the campaign, or left in free play.</summary>
        Count,

        /// <summary>The store price, in the campaign with none held (D65).</summary>
        Price,

        /// <summary>The campaign level the joker unlocks at, while it is locked (D76).</summary>
        Locked
    }
}
