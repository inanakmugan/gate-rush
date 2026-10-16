namespace GateRush.Meta
{
    /// <summary>
    /// What <see cref="MetaGame.TryUseJoker"/> did (D65). The caller saves
    /// after <see cref="Used"/> and <see cref="BoughtAndUsed"/>, and applies
    /// the joker only after one of them.
    /// </summary>
    public enum JokerUse
    {
        /// <summary>One was taken from the inventory.</summary>
        Used,

        /// <summary>None was held, so its store price was spent; the inventory is unchanged.</summary>
        BoughtAndUsed,

        /// <summary>None was held and the balance does not cover the price. Nothing changed.</summary>
        NotEnoughGold,
    }
}
