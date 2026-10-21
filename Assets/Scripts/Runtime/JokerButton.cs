using System;

namespace GateRush.Runtime
{
    /// <summary>
    /// What one joker button shows and does (Modules 34, 40 and 43): its
    /// badge — a count, a price or the level a locked joker opens at — and the
    /// number on it, whether it can be pressed, the gold missing to buy one,
    /// and whether it is marked as the joker just introduced. Decided by
    /// <see cref="JokerButtons"/>; the level's interface only formats the
    /// number and picks the look.
    /// </summary>
    public readonly struct JokerButton : IEquatable<JokerButton>
    {
        /// <summary>
        /// A button with a <paramref name="badge"/> showing
        /// <paramref name="amount"/>, pressable when
        /// <paramref name="isInteractable"/>, with
        /// <paramref name="shortfall"/> gold missing to buy one (0 when none
        /// is), pulsing when <paramref name="isMarked"/>.
        /// </summary>
        public JokerButton(JokerBadge badge, int amount, bool isInteractable, int shortfall, bool isMarked)
        {
            Badge = badge;
            Amount = amount;
            IsInteractable = isInteractable;
            Shortfall = shortfall;
            IsMarked = isMarked;
        }

        /// <summary>Whether the badge shows a count, a price or an unlock level.</summary>
        public JokerBadge Badge { get; }

        /// <summary>The number on the badge: the count held or left, the price, or the unlock level.</summary>
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

        /// <summary>
        /// True while the joker its New Joker card just introduced has not been
        /// used on that level (D76): the button pulses. Never in free play,
        /// never while locked.
        /// </summary>
        public bool IsMarked { get; }

        /// <summary>True while the joker is locked in the campaign: the badge shows its unlock level.</summary>
        public bool IsLocked => Badge == JokerBadge.Locked;

        /// <inheritdoc />
        public bool Equals(JokerButton other) =>
            Badge == other.Badge && Amount == other.Amount && IsInteractable == other.IsInteractable && Shortfall == other.Shortfall
            && IsMarked == other.IsMarked;

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
                hash = hash * 31 + Shortfall;
                return hash * 31 + (IsMarked ? 1 : 0);
            }
        }

        /// <summary>True when both buttons show and do the same.</summary>
        public static bool operator ==(JokerButton left, JokerButton right) => left.Equals(right);

        /// <summary>True when the buttons differ in anything they show or do.</summary>
        public static bool operator !=(JokerButton left, JokerButton right) => !left.Equals(right);
    }
}
