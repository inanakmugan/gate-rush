using System;

namespace GateRush.UI
{
    /// <summary>
    /// Which front end pages are active while tabs change (Module 47): the
    /// page fully shown, and the page fading in over it, if any. Plain code
    /// with no scene, so fast tab presses are tested on their own.
    /// </summary>
    /// <remarks>
    /// At most two pages are ever active — the shown one and the incoming one
    /// — and exactly one once a fade has finished. A new selection while a
    /// fade runs finishes that fade first, so the page that was coming in is
    /// the one the next fades over and none is ever left half-faded.
    /// </remarks>
    public sealed class TabPages
    {
        /// <summary>Home shown, with no fade.</summary>
        public TabPages()
        {
            Shown = UiTab.Home;
        }

        /// <summary>The page fully shown; under the incoming page while one fades in.</summary>
        public UiTab Shown { get; private set; }

        /// <summary>The page fading in over <see cref="Shown"/>; null when none is.</summary>
        public UiTab? Incoming { get; private set; }

        /// <summary>True while a page fades in.</summary>
        public bool IsFading => Incoming.HasValue;

        /// <summary>True when <paramref name="tab"/>'s page must be active.</summary>
        public bool IsActive(UiTab tab) => tab == Shown || tab == Incoming;

        /// <summary>
        /// Selects <paramref name="tab"/>: a fade in progress is finished
        /// first; then <paramref name="tab"/> fades in over the page shown when
        /// <paramref name="isAnimated"/>, or is shown at once. Selecting the
        /// page shown, after that, changes nothing more. True when a new fade
        /// starts.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="tab"/> is not a declared tab.</exception>
        public bool Select(UiTab tab, bool isAnimated)
        {
            if (!Enum.IsDefined(typeof(UiTab), tab))
            {
                throw new ArgumentOutOfRangeException(nameof(tab), tab, "Not a front end tab.");
            }

            Finish();
            if (tab == Shown)
            {
                return false;
            }

            if (!isAnimated)
            {
                Shown = tab;
                return false;
            }

            Incoming = tab;
            return true;
        }

        /// <summary>The incoming page's fade has ended, or is cut short: it becomes the page shown. Does nothing with no fade.</summary>
        public void Finish()
        {
            if (Incoming.HasValue)
            {
                Shown = Incoming.Value;
                Incoming = null;
            }
        }
    }
}
