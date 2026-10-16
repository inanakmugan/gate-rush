namespace GateRush.Meta
{
    /// <summary>
    /// One number per <see cref="JokerKind"/>: what <see cref="MetaRules"/>
    /// takes for the joker prices and for the starting jokers. Plain data with
    /// no checks of its own; <see cref="MetaRules"/> checks the values, so a
    /// <c>default</c> instance is harmless.
    /// </summary>
    public readonly struct JokerAmounts
    {
        private readonly int clock;
        private readonly int rocket;
        private readonly int broom;

        /// <summary>The amounts <paramref name="clock"/>, <paramref name="rocket"/> and <paramref name="broom"/>.</summary>
        public JokerAmounts(int clock, int rocket, int broom)
        {
            this.clock = clock;
            this.rocket = rocket;
            this.broom = broom;
        }

        /// <summary>The amount for <paramref name="kind"/>.</summary>
        /// <exception cref="System.ArgumentOutOfRangeException"><paramref name="kind"/> is not a declared kind.</exception>
        public int Of(JokerKind kind)
        {
            switch (kind)
            {
                case JokerKind.Clock:
                    return clock;
                case JokerKind.Rocket:
                    return rocket;
                case JokerKind.Broom:
                    return broom;
                default:
                    throw JokerKinds.Undefined(kind, nameof(kind));
            }
        }
    }
}
