using System;
using GateRush.Meta;
using UnityEngine;

namespace GateRush.Runtime
{
    /// <summary>
    /// The one way <see cref="LevelBootstrap"/> shows and hears the level's
    /// own interface (Modules 40 and 41, D74): the HUD in the top band, the
    /// joker bar in the bottom band, the in-level overlays — Not Enough Gold,
    /// the gold packs, the test purchase sheet and the message — and the
    /// level's panels, the result panel and the confirmation before a lost
    /// life. Declared here and implemented in <c>GateRush.UI</c>, as
    /// <see cref="IFrontEnd"/> is, so <c>Runtime</c> never references the
    /// screens.
    /// </summary>
    /// <remarks>
    /// <para>It decides nothing: it shows the <see cref="LevelUiState"/>, the
    /// <see cref="LevelResult"/> and the <see cref="ConfirmKind"/> it is given
    /// and raises an event for every request. Moving between its overlays is
    /// its own business; whether the countdown runs is the bootstrap's, which
    /// stops it while <see cref="IsOverlayOpen"/> or
    /// <see cref="IsConfirmOpen"/>, and resumes it on
    /// <see cref="OverlaysClosed"/> and <see cref="ConfirmKeepPlaying"/>.</para>
    /// <para>Back (Escape) closes the top in-level overlay; with none open, it
    /// answers the confirmation "Keep playing" (<see cref="ConfirmKeepPlaying"/>);
    /// over the result panel, or with nothing open, it does nothing — there
    /// is no back from a result.</para>
    /// </remarks>
    public interface ILevelUi
    {
        /// <summary>Raised when the player presses the HUD's Restart button.</summary>
        event Action RestartRequested;

        /// <summary>Raised when the player presses the HUD's Menu button.</summary>
        event Action MenuRequested;

        /// <summary>Raised when the player presses a joker's button, with its kind. Pressing spends nothing; the bootstrap decides what follows.</summary>
        event Action<JokerKind> JokerPressed;

        /// <summary>Raised when the player presses a gold pack's price button in the in-level gold packs, with its product id (D72).</summary>
        event Action<string> GoldPackRequested;

        /// <summary>
        /// Raised when the player closed the last open in-level overlay — by a
        /// close button, OK, Cancel, Buy or Back — so the level may go on. Not
        /// raised by <see cref="Show"/> or <see cref="Hide"/>, which close the
        /// overlays because the level is replaced or left.
        /// </summary>
        event Action OverlaysClosed;

        /// <summary>
        /// Raised when the player presses any button of the level's interface,
        /// or Back closed an overlay, after the press's own event, for the tap
        /// sound (Module 25).
        /// </summary>
        event Action ButtonPressed;

        /// <summary>
        /// Raised when a coin of a gold flight lands on the HUD's gold pill, at
        /// most once per the UI Config's tap interval: the moment for the tap
        /// sound (Module 47).
        /// </summary>
        event Action CoinLanded;

        /// <summary>Raised when the player presses Next on the result panel.</summary>
        event Action NextRequested;

        /// <summary>Raised when the player presses Restart on the result panel. The bootstrap decides what it costs.</summary>
        event Action ResultRestartRequested;

        /// <summary>Raised when the player presses Menu on the result panel.</summary>
        event Action ResultMenuRequested;

        /// <summary>Raised when the player presses "Lose a life" on the confirmation. It is still open; the listener hides it.</summary>
        event Action ConfirmLoseLife;

        /// <summary>
        /// Raised when the player presses "Keep playing" on the confirmation,
        /// or Back while it is open with no in-level overlay over it. It is
        /// still open; the listener hides it.
        /// </summary>
        event Action ConfirmKeepPlaying;

        /// <summary>True from <see cref="Show"/> until <see cref="Hide"/>.</summary>
        bool IsOpen { get; }

        /// <summary>
        /// True while any in-level overlay is open: the countdown and the board
        /// stop (D74). The result panel and the confirmation are not among them.
        /// </summary>
        bool IsOverlayOpen { get; }

        /// <summary>True from <see cref="ShowConfirm"/> until <see cref="HideConfirm"/>: the countdown and the board stop (D64).</summary>
        bool IsConfirmOpen { get; }

        /// <summary>
        /// Null when every part the level's interface needs is assigned and its
        /// configuration has no problem; otherwise one message naming each
        /// missing part and each problem.
        /// </summary>
        string MissingSetup();

        /// <summary>
        /// Makes the level's interface ignore its keys, Back among them,
        /// whenever <paramref name="isHeld"/> answers true: the screen fade's
        /// one query (Module 47), set once by the bootstrap. Pointer input is
        /// held by the fade itself.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="isHeld"/> is null.</exception>
        void HoldInput(Func<bool> isHeld);

        /// <summary>
        /// Opens the HUD and the joker bar for a level, with no overlay, no
        /// panel and no time bonus, showing <paramref name="state"/>.
        /// </summary>
        void Show(LevelUiState state);

        /// <summary>Shows <paramref name="state"/> in place, whatever overlays are open.</summary>
        void Refresh(LevelUiState state);

        /// <summary>
        /// Puts the HUD in <paramref name="topBand"/> and the joker bar in
        /// <paramref name="bottomBand"/>, anchors normalized to the screen
        /// (<see cref="ScreenBands.HudBandAnchors"/>,
        /// <see cref="ScreenBands.BottomBandAnchors"/>) — the bands the camera
        /// keeps free of the board.
        /// </summary>
        void PlaceBands(Rect topBand, Rect bottomBand);

        /// <summary>Shows the timer with <paramref name="remainingSeconds"/>, rounded up. Cheap to call every frame.</summary>
        void ShowTime(float remainingSeconds);

        /// <summary>Hides the timer, for a level without a countdown.</summary>
        void HideTime();

        /// <summary>Shows "+<paramref name="seconds"/>" beside the timer, rising and fading, and pulses the timer once.</summary>
        void ShowTimeBonus(int seconds);

        /// <summary>Stops a time bonus that is showing, for a restart or a new level.</summary>
        void CancelTimeBonus();

        /// <summary>
        /// Opens Not Enough Gold over the level with <paramref name="shortfall"/>,
        /// the gold missing, as the bootstrap worked it out. Its Get Gold
        /// button opens the in-level gold packs in its place.
        /// </summary>
        /// <exception cref="InvalidOperationException">The level's interface is not open.</exception>
        void ShowNotEnoughGold(int shortfall);

        /// <summary>
        /// Opens the test purchase sheet for <paramref name="offer"/> over
        /// everything and calls <paramref name="answer"/> exactly once: true
        /// for Buy; false for Cancel, Back, the close button, or the level's
        /// interface being shown afresh or hidden first.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="answer"/> is null.</exception>
        /// <exception cref="InvalidOperationException">The level's interface is not open, or a sheet is already waiting for its answer.</exception>
        void ConfirmTestPurchase(GoldOffer offer, Action<bool> answer);

        /// <summary>Opens the message saying that a purchase failed and nothing was charged.</summary>
        /// <exception cref="InvalidOperationException">The level's interface is not open.</exception>
        void ShowPurchaseFailed();

        /// <summary>
        /// Opens the result panel for a won level over everything, with the
        /// buttons, the gold and the closing message <paramref name="result"/>
        /// offers.
        /// </summary>
        /// <exception cref="ArgumentException"><paramref name="result"/> is a loss.</exception>
        /// <exception cref="InvalidOperationException">The level's interface is not open.</exception>
        void ShowWin(LevelResult result);

        /// <summary>Opens the result panel for a lost level, with the buttons <paramref name="result"/> offers.</summary>
        /// <exception cref="ArgumentException"><paramref name="result"/> is a win.</exception>
        /// <exception cref="InvalidOperationException">The level's interface is not open.</exception>
        void ShowLoss(LevelResult result);

        /// <summary>
        /// Closes the result panel with the overlays' short close (Module 47),
        /// stopping its pop; it takes no input from the start of the close.
        /// Does nothing when it is not open. A gold flight from it stops, and
        /// the HUD's gold shows the wallet's value at once.
        /// </summary>
        void HideResult();

        /// <summary>
        /// Holds the HUD's gold at <paramref name="shownGold"/> however the
        /// wallet changes, until a flight lands on it or the level's interface
        /// releases it (Module 47): for a campaign win, from the moment its
        /// gold is credited until its coins fly.
        /// </summary>
        void HoldGold(int shownGold);

        /// <summary>
        /// Flies <paramref name="amount"/> gold from the result panel's gold
        /// coin to the HUD's gold pill, after the panel's pop, the pill held at
        /// <paramref name="shownGold"/> until the coins land and lifted over
        /// the panel's scrim meanwhile (Module 47). Releases the gold at once
        /// when the panel is not open.
        /// </summary>
        void FlyWinGold(int amount, int shownGold, int seed);

        /// <summary>
        /// Flies <paramref name="amount"/> gold from the card of the gold pack
        /// <paramref name="productId"/> to the HUD's gold pill, the pill held
        /// at <paramref name="shownGold"/> until the coins land and lifted over
        /// the overlays' scrim meanwhile (Module 47). With the card not shown,
        /// the gold shows at once.
        /// </summary>
        void FlyPackGold(string productId, int amount, int shownGold, int seed);

        /// <summary>
        /// Opens the confirmation that <paramref name="kind"/> will cost a life,
        /// naming the win streak that goes with it when
        /// <paramref name="streakAtRisk"/> is above 0.
        /// </summary>
        /// <exception cref="InvalidOperationException">The level's interface is not open.</exception>
        void ShowConfirm(ConfirmKind kind, int streakAtRisk);

        /// <summary>
        /// Closes the confirmation with the overlays' short close (Module 47),
        /// stopping its pop; <see cref="IsConfirmOpen"/> is false from the start
        /// of the close. Does nothing when it is not open.
        /// </summary>
        void HideConfirm();

        /// <summary>
        /// Hides the level's interface and closes every overlay and panel. An
        /// open test purchase sheet is answered no.
        /// </summary>
        void Hide();
    }
}
