using System;
using System.Collections.Generic;
using GateRush.Meta;
using GateRush.Platform;

namespace GateRush.Runtime
{
    /// <summary>
    /// The one way <see cref="LevelBootstrap"/> shows and hears the front end
    /// (Module 35, D68): Home, its tabs and the overlays over them. Declared
    /// here and implemented in <c>GateRush.UI</c>, so the dependency points
    /// one way — <c>Runtime</c> never references the screens.
    /// </summary>
    /// <remarks>
    /// The front end decides nothing: it shows the <see cref="FrontEndState"/>
    /// it is given and raises an event for every request; the bootstrap
    /// decides what follows. Moving between tabs and overlays is its own
    /// business.
    /// </remarks>
    public interface IFrontEnd
    {
        /// <summary>Raised when the player presses Play: the campaign (D58).</summary>
        event Action PlayRequested;

        /// <summary>Raised when the player presses a level in the level grid, with its file name: free play (D58).</summary>
        event Action<string> LevelRequested;

        /// <summary>Raised when the player presses the sound toggle; the bootstrap flips the sound, not the vibration, and refreshes.</summary>
        event Action SoundToggleRequested;

        /// <summary>Raised when the player presses the haptics toggle (Module 38); the bootstrap flips the setting and refreshes.</summary>
        event Action HapticsToggleRequested;

        /// <summary>
        /// Raised when editing Profile's name field ends — Enter, or the field
        /// losing focus — with text that differs from the name shown, as typed.
        /// The bootstrap applies the name rule: an accepted name is saved and
        /// shown by a refresh; a refused one calls <see cref="ShowNameRefused"/>.
        /// </summary>
        event Action<string> NameChangeRequested;

        /// <summary>Raised when the player presses an avatar in Profile's grid, with its id; the bootstrap saves it at once.</summary>
        event Action<string> AvatarChangeRequested;

        /// <summary>
        /// Raised when the player presses a refill button, on the Store or the
        /// Lives overlay, which is pressable whenever the lives are not full
        /// (<see cref="FrontEndState.IsLivesFull"/>), whatever the balance
        /// (D62, Module 36).
        /// </summary>
        event Action RefillRequested;

        /// <summary>Raised when the player presses a gold pack's price button, with its product id (D72).</summary>
        event Action<string> GoldPackRequested;

        /// <summary>Raised when the player presses a joker's buy button on the Store, with its kind (D62).</summary>
        event Action<JokerKind> JokerPurchaseRequested;

        /// <summary>
        /// Raised when the Leaderboard tab is selected, and when its Retry
        /// button is pressed (Module 39): the page shows Loading until
        /// <see cref="ShowLeaderboard"/> brings a result. The bootstrap fetches
        /// a fresh result each time, so a changed name or avatar shows on the
        /// next selection.
        /// </summary>
        event Action LeaderboardRequested;

        /// <summary>
        /// Raised when the player presses any button of the front end, or Back
        /// changed something, after the press's own event — so sound turned
        /// on by the press is on before the press sounds (Module 25).
        /// </summary>
        event Action ButtonPressed;

        /// <summary>
        /// Raised when a coin of a gold flight lands on the Top Bar's gold, at
        /// most once per the UI Config's tap interval: the moment for the tap
        /// sound (Module 47).
        /// </summary>
        event Action CoinLanded;

        /// <summary>True from <see cref="Show"/> until <see cref="Hide"/>.</summary>
        bool IsOpen { get; }

        /// <summary>
        /// Null when every part the front end needs is assigned and its
        /// configuration has no problem; otherwise one message naming each
        /// missing part and each problem.
        /// </summary>
        string MissingSetup();

        /// <summary>
        /// The fade between the front end and a level (Module 47), from the
        /// interface's configuration, which the bootstrap cannot read. Read
        /// once the setup is known to be complete.
        /// </summary>
        ScreenFadeSettings ScreenFadeSettings { get; }

        /// <summary>
        /// Makes the front end ignore its keys, Back among them, whenever
        /// <paramref name="isHeld"/> answers true: the screen fade's one query,
        /// set once by the bootstrap. Pointer input is held by the fade itself.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="isHeld"/> is null.</exception>
        void HoldInput(Func<bool> isHeld);

        /// <summary>Opens the front end on the Home tab with no overlay and no notice, showing <paramref name="state"/>.</summary>
        void Show(FrontEndState state);

        /// <summary>Shows <paramref name="state"/> in place, on whatever tab and overlays are showing.</summary>
        void Refresh(FrontEndState state);

        /// <summary>Opens the Lives overlay over the current tab.</summary>
        /// <exception cref="InvalidOperationException">The front end is not open.</exception>
        void ShowLives();

        /// <summary>
        /// Shows <paramref name="text"/> in the line above Home's play button,
        /// selecting Home first. It stays until the tab changes or the front
        /// end is shown again.
        /// </summary>
        /// <exception cref="InvalidOperationException">The front end is not open.</exception>
        void ShowNotice(string text);

        /// <summary>
        /// Opens the Not Enough Gold prompt over whatever is showing, with
        /// <paramref name="shortfall"/>, the gold missing, as the bootstrap
        /// worked it out. Its Get Gold button selects the Store.
        /// </summary>
        /// <exception cref="InvalidOperationException">The front end is not open.</exception>
        void ShowNotEnoughGold(int shortfall);

        /// <summary>Opens the message prompt saying that a purchase failed and nothing was charged.</summary>
        /// <exception cref="InvalidOperationException">The front end is not open.</exception>
        void ShowPurchaseFailed();

        /// <summary>
        /// Flies <paramref name="amount"/> gold from the Store's card of the
        /// gold pack <paramref name="productId"/> to the Top Bar's gold, the
        /// gold held at <paramref name="shownGold"/> until the coins land and
        /// lifted over any scrim meanwhile (Module 47). With the card not
        /// shown, the gold shows at once. A tab change, <see cref="Show"/>,
        /// <see cref="Hide"/> and a disable end the flight and show the
        /// wallet's value.
        /// </summary>
        void FlyPackGold(string productId, int amount, int shownGold, int seed);

        /// <summary>
        /// Opens the test purchase sheet for <paramref name="offer"/> over
        /// everything, saying that the store is simulated and no money is
        /// charged, and calls <paramref name="answer"/> exactly once: true for
        /// Buy; false for Cancel, Back, the close button, or the front end
        /// being hidden first.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="answer"/> is null.</exception>
        /// <exception cref="InvalidOperationException">The front end is not open, or a sheet is already waiting for its answer.</exception>
        void ConfirmTestPurchase(GoldOffer offer, Action<bool> answer);

        /// <summary>
        /// Opens the Streak Reward overlay over whatever is showing (Module
        /// 37): the streak rewards campaign wins gave since the last
        /// announcement, already in the inventory, merged by joker kind, under
        /// a headline with <paramref name="latestStreak"/>, the streak of the
        /// latest of those wins. Collect only closes it. A joker still locked
        /// in the state last given to <see cref="Show"/> or
        /// <see cref="Refresh"/> has its line say the level it opens at (D76).
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="rewards"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="rewards"/> is empty: there is nothing to announce.</exception>
        /// <exception cref="InvalidOperationException">The front end is not open, or no state was given to it yet.</exception>
        void ShowStreakReward(int latestStreak, IReadOnlyList<StreakReward> rewards);

        /// <summary>
        /// Puts the name last shown back in Profile's name field and shows the
        /// refusal line under it (Module 38). The line stays until Profile is
        /// opened again or editing starts again.
        /// </summary>
        /// <exception cref="InvalidOperationException">The front end is not open.</exception>
        void ShowNameRefused();

        /// <summary>
        /// Shows <paramref name="result"/> on the Leaderboard page (Module 39):
        /// the rows with the player's marked and scrolled into view and, while
        /// the result is simulated, the line saying so; or, for a failed
        /// fetch, the failure line and Retry. Ignored while another tab is
        /// selected: the next selection asks again.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="result"/> is null.</exception>
        /// <exception cref="InvalidOperationException">The front end is not open.</exception>
        void ShowLeaderboard(LeaderboardResult result);

        /// <summary>Hides the front end. An open test purchase sheet is answered no.</summary>
        void Hide();
    }
}
