using System;

namespace GateRush.Runtime
{
    /// <summary>
    /// What one joker button shows and does (Modules 34 and 40): its badge —
    /// a count or a price — and the number on it, whether it can be pressed,
    /// and the gold missing to buy one. Decided by <see cref="JokerButtons"/>;
    /// the level's interface only formats the number and picks the look.
    /// </summary>
    public readonly struct JokerButton : IEquatable<JokerButton>
    {
        /// <summary>
        /// A button with a <paramref name="badge"/> showing
        /// <paramref name="amount"/>, pressable when
        /// <paramref name="isInteractable"/>, with
        /// <paramref name="shortfall"/> gold missing to buy one (0 when none
        /// is).
        /// </summary>
        public JokerButton(JokerBadge badge, int amount, bool isInteractable, int shortfall)
        {
            Badge = badge;
            Amount = amount;
            IsInteractable = isInteractable;
            Shortfall = shortfall;
        }

        /// <summary>Whether the badge shows a count or a price.</summary>
        public JokerBadge Badge { get; }

        /// <summary>The number on the badge: the count held or left, or the price.</summary>
        public int Amount { get; }

        /// <summary>True when the button can be pressed.</summary>
        public bool IsInteractable { get; }

        /// <summary>
        /// The gold missing to buy one: positive only in the campaign with none
        /// held and a price above the balance, when a press opens Not Enough
        /// Gold instead of selecting or using the joker (D74); 0 otherwise.
        /// </summary>
        public int Shortfall { get; }

        /// <summary>True when <see cref="Shortfall"/> is positive.</summary>
        public bool IsShortOfGold => Shortfall > 0;

        /// <inheritdoc />
        public bool Equals(JokerButton other) =>
            Badge == other.Badge && Amount == other.Amount && IsInteractable == other.IsInteractable && Shortfall == other.Shortfall;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is JokerButton other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)Badge;
                hash = hash * 31 + Amount;
                hash = hash * 31 + (IsInteractable ? 1 : 0);
                return hash * 31 + Shortfall;
            }
        }

        /// <summary>True when both buttons show and do the same.</summary>
        public static bool operator ==(JokerButton left, JokerButton right) => left.Equals(right);

        /// <summary>True when the buttons differ in anything they show or do.</summary>
        public static bool operator !=(JokerButton left, JokerButton right) => !left.Equals(right);
    }
}
