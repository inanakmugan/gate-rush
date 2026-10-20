using System;
using GateRush.Meta;
using UnityEngine;

namespace GateRush.Runtime
{
    /// <summary>
    /// The one way <see cref="LevelBootstrap"/> shows and hears the level's
    /// own interface (Module 40, D74): the HUD in the top band, the joker bar
    /// in the bottom band, and the in-level overlays — Not Enough Gold, the
    /// gold packs, the test purchase sheet and the message. Declared here and
    /// implemented in <c>GateRush.UI</c>, as <see cref="IFrontEnd"/> is, so
    /// <c>Runtime</c> never references the screens.
    /// </summary>
    /// <remarks>
    /// <para>It decides nothing: it shows the <see cref="LevelUiState"/> it is
    /// given and raises an event for every request. Moving between its
    /// overlays is its own business; whether the countdown runs is the
    /// bootstrap's, which stops it while <see cref="IsOverlayOpen"/> and
    /// resumes it on <see cref="OverlaysClosed"/>.</para>
    /// <para>Back (Escape) closes the top in-level overlay and does nothing
    /// else; with none open it is not read here.</para>
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

        /// <summary>True from <see cref="Show"/> until <see cref="Hide"/>.</summary>
        bool IsOpen { get; }

        /// <summary>True while any in-level overlay is open: the countdown and the board stop (D74).</summary>
        bool IsOverlayOpen { get; }

        /// <summary>
        /// Null when every part the level's interface needs is assigned and its
        /// configuration has no problem; otherwise one message naming each
        /// missing part and each problem.
        /// </summary>
        string MissingSetup();

        /// <summary>Opens the HUD and the joker bar for a level, with no overlay and no time bonus, showing <paramref name="state"/>.</summary>
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

        /// <summary>Hides the level's interface and closes every overlay. An open test purchase sheet is answered no.</summary>
        void Hide();
    }
}
