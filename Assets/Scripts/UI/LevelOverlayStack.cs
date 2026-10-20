using System;
using System.Collections.Generic;

namespace GateRush.UI
{
    /// <summary>
    /// The overlays open over a level (Module 40, D74), newest last: Not
    /// Enough Gold, the gold packs, the test purchase sheet and the message.
    /// Plain code over <see cref="UiNavigation"/>'s stack, with no scene, so
    /// the rules the level's pause depends on are tested on their own.
    /// </summary>
    /// <remarks>
    /// <para><b>The last overlay.</b> <see cref="AllClosed"/> is raised once
    /// each time the player empties the stack — close, OK, Cancel, Buy or
    /// Back — and never by <see cref="Clear"/>, which empties it because the
    /// level is replaced or left. Get Gold replaces Not Enough Gold
    /// (<see cref="ReplaceTop"/>), so the stack is never seen empty on the
    /// way to the gold packs.</para>
    /// <para><b>The test purchase sheet</b> answers exactly once: Buy answers
    /// yes after the sheet is closed; anything else that closes it — Cancel,
    /// its close button, Back, <see cref="Clear"/> — answers no.</para>
    /// <para>Events are raised in this order: the sheet's no, then
    /// <see cref="Changed"/>, then <see cref="AllClosed"/>.</para>
    /// </remarks>
    public sealed class LevelOverlayStack
    {
        private readonly UiNavigation navigation = new UiNavigation();

        // The open sheet's answer; null when none is waiting.
        private Action<bool> pendingAnswer;

        private bool wasOpen;
        private bool isClearing;

        /// <summary>An empty stack.</summary>
        public LevelOverlayStack()
        {
            navigation.Changed += OnNavigationChanged;
        }

        /// <summary>Raised once after any call that changed the open overlays: the view shows them.</summary>
        public event Action Changed;

        /// <summary>Raised when the player closed the last open overlay, after <see cref="Changed"/>.</summary>
        public event Action AllClosed;

        /// <summary>The open overlays, oldest first: the last is on top.</summary>
        public IReadOnlyList<UiOverlay> Overlays => navigation.Overlays;

        /// <summary>True while any overlay is open.</summary>
        public bool IsOpen => navigation.Overlays.Count > 0;

        /// <summary>True while a test purchase sheet waits for its answer.</summary>
        public bool IsAwaitingAnswer => pendingAnswer != null;

        /// <summary>Opens <paramref name="overlay"/> on top; one already open is moved to the top.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="overlay"/> is not a declared overlay.</exception>
        public void Open(UiOverlay overlay)
        {
            navigation.Open(overlay);
        }

        /// <summary>Opens <paramref name="overlay"/> in place of the top one, as one change.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="overlay"/> is not a declared overlay.</exception>
        public void ReplaceTop(UiOverlay overlay)
        {
            navigation.ReplaceTop(overlay);
        }

        /// <summary>Opens the test purchase sheet on top, with <paramref name="answer"/> waiting for Buy or anything else.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="answer"/> is null.</exception>
        /// <exception cref="InvalidOperationException">A sheet is already waiting for its answer.</exception>
        public void OpenTestPurchase(Action<bool> answer)
        {
            if (answer == null)
            {
                throw new ArgumentNullException(nameof(answer));
            }

            if (pendingAnswer != null)
            {
                throw new InvalidOperationException("A test purchase was opened while another is waiting for its answer.");
            }

            pendingAnswer = answer;
            navigation.Open(UiOverlay.TestPurchase);
        }

        /// <summary>
        /// Buy: the answer is taken before the sheet closes, so closing it
        /// does not answer no, and given after, so whatever the answer opens
        /// lands on top. Does nothing when no sheet waits.
        /// </summary>
        public void Buy()
        {
            var answer = pendingAnswer;
            if (answer == null)
            {
                return;
            }

            pendingAnswer = null;
            CloseIfOnTop(UiOverlay.TestPurchase);
            answer(true);
        }

        /// <summary>Closes <paramref name="overlay"/> when it is the top one — the overlays above it cover it. False when it is not on top.</summary>
        public bool CloseIfOnTop(UiOverlay overlay)
        {
            var open = navigation.Overlays;
            if (open.Count == 0 || open[open.Count - 1] != overlay)
            {
                return false;
            }

            navigation.Back();
            return true;
        }

        /// <summary>Back: closes the top overlay. False, with nothing changed, when none is open.</summary>
        public bool Back()
        {
            return IsOpen && navigation.Back();
        }

        /// <summary>
        /// Closes every overlay, answering a waiting sheet no, without
        /// <see cref="AllClosed"/>: the level is being replaced or left.
        /// </summary>
        public void Clear()
        {
            isClearing = true;
            try
            {
                navigation.Reset();
                AnswerPending(false);
            }
            finally
            {
                isClearing = false;
                wasOpen = false;
            }
        }

        private void OnNavigationChanged()
        {
            if (pendingAnswer != null && !Contains(navigation.Overlays, UiOverlay.TestPurchase))
            {
                AnswerPending(false);
            }

            Changed?.Invoke();

            var isOpen = IsOpen;
            var isAllClosed = wasOpen && !isOpen && !isClearing;
            wasOpen = isOpen;
            if (isAllClosed)
            {
                AllClosed?.Invoke();
            }
        }

        private void AnswerPending(bool isBought)
        {
            var answer = pendingAnswer;
            pendingAnswer = null;
            answer?.Invoke(isBought);
        }

        private static bool Contains(IReadOnlyList<UiOverlay> overlays, UiOverlay overlay)
        {
            for (var i = 0; i < overlays.Count; i++)
            {
                if (overlays[i] == overlay)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
