using System;

namespace GateRush.Meta
{
    /// <summary>
    /// The player's gold (Module 30, D22): the balance that can be spent, and
    /// the lifetime gold earned, which the leaderboard ranks on and which
    /// spending never lowers. A section of <see cref="SaveData"/>.
    /// </summary>
    /// <remarks>
    /// <para>Amounts are never negative: a negative amount is a bug in the
    /// caller and throws. Zero is allowed and changes nothing. Sums stop at
    /// <see cref="int.MaxValue"/> rather than overflowing.</para>
    /// <para>When gold is paid and what it buys is <c>MetaGame</c>'s question
    /// (Module 31); this class only keeps the balances.</para>
    /// </remarks>
    public sealed class Wallet
    {
        /// <summary>A wallet holding <paramref name="balance"/> and <paramref name="lifetimeEarned"/>, as a save restores it.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Either value is negative.</exception>
        public Wallet(int balance, int lifetimeEarned)
        {
            if (balance < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(balance), balance, "The balance may not be negative.");
            }

            if (lifetimeEarned < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(lifetimeEarned), lifetimeEarned, "The lifetime gold may not be negative.");
            }

            Balance = balance;
            LifetimeEarned = lifetimeEarned;
        }

        /// <summary>The gold that can be spent.</summary>
        public int Balance { get; private set; }

        /// <summary>All the gold ever earned. Spending never lowers it; the starting gold is not in it.</summary>
        public int LifetimeEarned { get; private set; }

        /// <summary>
        /// The wallet of a new player: <see cref="MetaRules.StartingGold"/> in
        /// the balance and no lifetime gold, since the starting gold was not
        /// earned (D22).
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="rules"/> is null.</exception>
        public static Wallet Starting(MetaRules rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            return new Wallet(rules.StartingGold, 0);
        }

        /// <summary>Adds <paramref name="amount"/> to the balance and to the lifetime gold, each stopping at <see cref="int.MaxValue"/>.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="amount"/> is negative.</exception>
        public void Earn(int amount)
        {
            ThrowIfNegative(amount);
            Balance = AddCapped(Balance, amount);
            LifetimeEarned = AddCapped(LifetimeEarned, amount);
        }

        /// <summary>
        /// Adds bought gold (Module 36, D72) to the balance only, stopping at
        /// <see cref="int.MaxValue"/>. The lifetime gold counts gold earned by
        /// playing, which the leaderboard ranks on (D22, D70), so bought gold
        /// never enters it: a rank cannot be bought.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="amount"/> is negative.</exception>
        public void AddPurchased(int amount)
        {
            ThrowIfNegative(amount);
            Balance = AddCapped(Balance, amount);
        }

        /// <summary>
        /// Takes <paramref name="amount"/> from the balance when it holds that
        /// much. False, with nothing changed, when it does not. The lifetime
        /// gold is never touched.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="amount"/> is negative.</exception>
        public bool TrySpend(int amount)
        {
            ThrowIfNegative(amount);
            if (amount > Balance)
            {
                return false;
            }

            Balance -= amount;
            return true;
        }

        private static int AddCapped(int value, int amount) => (int)Math.Min((long)value + amount, int.MaxValue);

        private static void ThrowIfNegative(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "An amount of gold may not be negative.");
            }
        }
    }
}
