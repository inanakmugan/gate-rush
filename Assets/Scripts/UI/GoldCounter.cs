using System;

namespace GateRush.UI
{
    /// <summary>
    /// A gold label that can be held at an old value while coins fly to it
    /// (Module 47). Plain code: it decides what number shows and hands it to
    /// the view that owns the label, only when that number changes, and asks
    /// for a pop when a coin lands.
    /// </summary>
    /// <remarks>
    /// <para><b>Not held</b>, it shows the wallet's value, whatever
    /// <see cref="Show"/> last gave: spending shows at once.</para>
    /// <para><b>Held</b> (<see cref="Hold"/>), it shows the held value however
    /// the wallet changes, and each landing coin adds its share
    /// (<see cref="Add"/>). <see cref="Release"/> ends the hold and shows the
    /// wallet's value at once — after the last coin, and whenever a flight
    /// ends early — so the counter never stays on another value.</para>
    /// </remarks>
    public sealed class GoldCounter
    {
        private readonly Action<int> display;
        private readonly Action pop;
        private int wallet;
        private int shown;
        private bool hasShown;

        /// <summary>A counter that shows a number through <paramref name="display"/> and pops through <paramref name="pop"/>.</summary>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public GoldCounter(Action<int> display, Action pop)
        {
            this.display = display ?? throw new ArgumentNullException(nameof(display));
            this.pop = pop ?? throw new ArgumentNullException(nameof(pop));
        }

        /// <summary>True while the counter is held at a value other than the wallet's.</summary>
        public bool IsHeld { get; private set; }

        /// <summary>The number shown now.</summary>
        public int Shown => shown;

        /// <summary>The wallet's value: shown at once unless the counter is held.</summary>
        public void Show(int gold)
        {
            wallet = gold;
            if (!IsHeld)
            {
                Display(wallet);
            }
        }

        /// <summary>Holds the counter at <paramref name="shownGold"/>, the value before the gold arrived, until <see cref="Release"/>.</summary>
        public void Hold(int shownGold)
        {
            IsHeld = true;
            Display(shownGold);
        }

        /// <summary>A coin landed: while held, adds <paramref name="share"/> to what shows and pops. Does nothing otherwise.</summary>
        public void Add(int share)
        {
            if (!IsHeld)
            {
                return;
            }

            Display(shown + share);
            pop();
        }

        /// <summary>Ends the hold, if any, and shows the wallet's value at once.</summary>
        public void Release()
        {
            IsHeld = false;
            Display(wallet);
        }

        private void Display(int value)
        {
            if (hasShown && value == shown)
            {
                return;
            }

            hasShown = true;
            shown = value;
            display(value);
        }
    }
}
