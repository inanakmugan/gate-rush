using System;
using System.Collections.Generic;
using GateRush.Meta;
using GateRush.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GateRush.UI
{
    /// <summary>
    /// The level's own interface (Modules 40 and 41, D74): the HUD in the top
    /// band, the joker bar in the bottom band, a stack of overlays over the
    /// level — Not Enough Gold, the gold packs, the test purchase sheet and
    /// the message, the same prefabs as the front end's — and the result
    /// panel and the confirmation before a lost life. Implements
    /// <see cref="ILevelUi"/>, the one way the bootstrap shows and hears it.
    /// </summary>
    /// <remarks>
    /// <para><b>No rules.</b> It shows the <see cref="LevelUiState"/> it is
    /// given and raises an event for every request; moving between its
    /// overlays (<see cref="LevelOverlayStack"/>) is all it decides itself.
    /// The front end's code is not used: the overlay views are driven from
    /// here.</para>
    /// <para><b>Open and closed.</b> <see cref="Show"/> and
    /// <see cref="Hide"/> switch this component's own GameObject, as the front
    /// end's do; the prefab is saved inactive. Both close every overlay
    /// without <see cref="OverlaysClosed"/>.</para>
    /// <para><b>Overlays.</b> They share one parent and stack by sibling
    /// order, newest last; their scrims cover the HUD, the bar and the board,
    /// so nothing under them can be pressed. Not Enough Gold's Get Gold opens
    /// the gold packs in its place. The test purchase sheet answers exactly
    /// once (<see cref="LevelOverlayStack"/>). Every overlay and both panels
    /// open with the shared pop and close with its short reverse
    /// (<see cref="OverlayMotion"/>, Module 47): what is open changes at once,
    /// and a closing one takes no input. <see cref="Show"/> and
    /// <see cref="Hide"/> close everything at once.</para>
    /// <para><b>The level's panels (Module 41).</b> The result panel and the
    /// confirmation sit beside the overlay stack, under the same parent, but
    /// are not in it: they are opened and closed by the bootstrap alone, and
    /// closing them never raises <see cref="OverlaysClosed"/>.
    /// <see cref="Show"/> and <see cref="Hide"/> close them too.</para>
    /// <para><b>Back</b> is the Escape key, which is also Android's back
    /// button under the Input System. With an overlay open it closes the top
    /// one and nothing else; with none open and the confirmation open it
    /// answers Keep playing; over the result panel, or with nothing open, it
    /// does nothing, and nothing else in a level reads it. While the screen
    /// fade holds input (<see cref="HoldInput"/>) it is ignored.</para>
    /// <para><b>Gold flights (Module 47).</b> Gold a campaign win pays flies
    /// from the result panel's coin, and gold a pack adds from its card, to
    /// the HUD's gold pill (<see cref="CoinFlightView"/>), which shows the
    /// held value until the coins land. <see cref="Show"/>, <see cref="Hide"/>
    /// and <see cref="HideResult"/> — Next, Restart, Menu and every way out —
    /// end a flight and show the wallet's value at once.</para>
    /// <para><b>Bands.</b> The HUD and the bar are anchored to the bands the
    /// bootstrap gives (<see cref="PlaceBands"/>); each one's row fits its
    /// band (<see cref="BandFit"/>).</para>
    /// </remarks>
    public sealed class LevelUi : MonoBehaviour, ILevelUi
    {
        // The key Back is read from: Escape, which the Input System also
        // reports for Android's back button. Not a tunable.
        private const Key BackKey = Key.Escape;

        [Tooltip("Assets/Config/UiConfig.asset: every text, format, colour and timing set from code.")]
        [SerializeField] private UiConfig config;

        [Tooltip("The HUD: anchored to the top band.")]
        [SerializeField] private LevelHud hud;

        [Tooltip("The joker bar: anchored to the bottom band.")]
        [SerializeField] private JokerBarView jokerBar;

        [Tooltip("Every overlay must share one parent: they are stacked by sibling order.")]
        [SerializeField] private NotEnoughGoldOverlay notEnoughGold;

        [SerializeField] private GoldPacksOverlay goldPacks;
        [SerializeField] private TestPurchaseOverlay testPurchase;
        [SerializeField] private MessageOverlay message;

        [Tooltip("The result panel (Module 41), under the overlays' parent; not in the overlay stack.")]
        [SerializeField] private ResultOverlay resultOverlay;

        [Tooltip("The confirmation before a lost life (Module 41), under the overlays' parent; not in the overlay stack.")]
        [SerializeField] private ConfirmOverlay confirmOverlay;

        [Tooltip("The coins gold flies to the HUD's gold pill in (Module 47), on their own canvas above every overlay.")]
        [SerializeField] private CoinFlightView coinFlight;

        private readonly LevelOverlayStack overlays = new LevelOverlayStack();
        private bool isSubscribed;

        // The screen fade's query (Module 47): while it answers true, Back is
        // ignored. Null until the bootstrap sets it.
        private Func<bool> inputHold;

        // Each stack overlay's shared open and close, read once.
        private OverlayMotion notEnoughGoldMotion;
        private OverlayMotion goldPacksMotion;
        private OverlayMotion testPurchaseMotion;
        private OverlayMotion messageMotion;

        /// <inheritdoc />
        public event Action RestartRequested;

        /// <inheritdoc />
        public event Action MenuRequested;

        /// <inheritdoc />
        public event Action<JokerKind> JokerPressed;

        /// <inheritdoc />
        public event Action<string> GoldPackRequested;

        /// <inheritdoc />
        public event Action OverlaysClosed;

        /// <inheritdoc />
        public event Action ButtonPressed;

        /// <inheritdoc />
        public event Action CoinLanded;

        /// <inheritdoc />
        public event Action NextRequested;

        /// <inheritdoc />
        public event Action ResultRestartRequested;

        /// <inheritdoc />
        public event Action ResultMenuRequested;

        /// <inheritdoc />
        public event Action ConfirmLoseLife;

        /// <inheritdoc />
        public event Action ConfirmKeepPlaying;

        /// <inheritdoc />
        public bool IsOpen => gameObject.activeSelf;

        /// <inheritdoc />
        public bool IsOverlayOpen => overlays.IsOpen;

        /// <inheritdoc />
        public bool IsConfirmOpen => confirmOverlay != null && confirmOverlay.IsOpen;

        /// <inheritdoc />
        public string MissingSetup()
        {
            var missing = new List<string>();
            UiParts.AddIfMissing(missing, config, nameof(LevelUi), nameof(config));
            UiParts.AddIfMissing(missing, hud, nameof(LevelUi), nameof(hud));
            UiParts.AddIfMissing(missing, jokerBar, nameof(LevelUi), nameof(jokerBar));
            UiParts.AddIfMissing(missing, notEnoughGold, nameof(LevelUi), nameof(notEnoughGold));
            UiParts.AddIfMissing(missing, goldPacks, nameof(LevelUi), nameof(goldPacks));
            UiParts.AddIfMissing(missing, testPurchase, nameof(LevelUi), nameof(testPurchase));
            UiParts.AddIfMissing(missing, message, nameof(LevelUi), nameof(message));
            UiParts.AddIfMissing(missing, resultOverlay, nameof(LevelUi), nameof(resultOverlay));
            UiParts.AddIfMissing(missing, confirmOverlay, nameof(LevelUi), nameof(confirmOverlay));
            UiParts.AddIfMissing(missing, coinFlight, nameof(LevelUi), nameof(coinFlight));

            if (coinFlight != null)
            {
                coinFlight.AddMissing(missing);
            }

            if (resultOverlay != null)
            {
                resultOverlay.AddMissing(missing);
            }

            if (confirmOverlay != null)
            {
                confirmOverlay.AddMissing(missing);
            }

            if (hud != null)
            {
                hud.AddMissing(missing);
            }

            if (jokerBar != null)
            {
                jokerBar.AddMissing(missing);
            }

            if (notEnoughGold != null)
            {
                notEnoughGold.AddMissing(missing);
            }

            if (goldPacks != null)
            {
                goldPacks.AddMissing(missing);
            }

            if (testPurchase != null)
            {
                testPurchase.AddMissing(missing);
            }

            if (message != null)
            {
                message.AddMissing(missing);
            }

            OverlayMotion.AddMissingOn(missing, notEnoughGold, nameof(notEnoughGold));
            OverlayMotion.AddMissingOn(missing, goldPacks, nameof(goldPacks));
            OverlayMotion.AddMissingOn(missing, testPurchase, nameof(testPurchase));
            OverlayMotion.AddMissingOn(missing, message, nameof(message));

            UiParts.AddMissingTints(missing, this);

            // The instance is saved inactive, so the search includes inactive parents.
            if (GetComponentInParent<Canvas>(true) == null)
            {
                missing.Add("LevelUi must sit on a Canvas or under one");
            }

            if (!OverlaysShareOneParent())
            {
                missing.Add("the Not Enough Gold, Gold Packs, Test Purchase, Message, Result and Confirm overlays must share one parent, which stacks them");
            }

            if (config != null)
            {
                missing.AddRange(config.Problems());
            }

            return missing.Count == 0
                ? null
                : $"{name}: the level's interface is not set up: {string.Join("; ", missing)}.";
        }

        /// <inheritdoc />
        public void HoldInput(Func<bool> isHeld)
        {
            inputHold = isHeld ?? throw new ArgumentNullException(nameof(isHeld));
        }

        /// <inheritdoc />
        public void Show(LevelUiState state)
        {
            gameObject.SetActive(true);
            UiTint.ApplyAll(this, config);
            EndGoldFlight();
            overlays.Clear();
            ApplyOverlays(false);
            HidePanelsNow();
            hud.CancelTimeBonus();
            Refresh(state);
        }

        /// <inheritdoc />
        public void Refresh(LevelUiState state)
        {
            hud.Show(state, config);
            jokerBar.Show(state, config);
            goldPacks.Show(state.GoldOffers, config);
        }

        /// <inheritdoc />
        public void PlaceBands(Rect topBand, Rect bottomBand)
        {
            UiBuilder.SetAnchors((RectTransform)hud.transform, topBand.min, topBand.max);
            UiBuilder.SetAnchors((RectTransform)jokerBar.transform, bottomBand.min, bottomBand.max);
        }

        /// <inheritdoc />
        public void ShowTime(float remainingSeconds)
        {
            hud.ShowTime(remainingSeconds, config);
        }

        /// <inheritdoc />
        public void HideTime()
        {
            hud.HideTime();
        }

        /// <inheritdoc />
        public void ShowTimeBonus(int seconds)
        {
            hud.ShowTimeBonus(seconds, config);
        }

        /// <inheritdoc />
        public void CancelTimeBonus()
        {
            hud.CancelTimeBonus();
        }

        /// <inheritdoc />
        public void ShowNotEnoughGold(int shortfall)
        {
            RequireOpen(nameof(ShowNotEnoughGold));
            notEnoughGold.Show(shortfall, config);
            overlays.Open(UiOverlay.NotEnoughGold);
        }

        /// <inheritdoc />
        public void ConfirmTestPurchase(GoldOffer offer, Action<bool> answer)
        {
            if (answer == null)
            {
                throw new ArgumentNullException(nameof(answer));
            }

            RequireOpen(nameof(ConfirmTestPurchase));
            if (overlays.IsAwaitingAnswer)
            {
                throw new InvalidOperationException(
                    $"{name}: LevelUi.{nameof(ConfirmTestPurchase)} was called while a test purchase is waiting for its answer.");
            }

            testPurchase.Show(offer, config);
            overlays.OpenTestPurchase(answer);
        }

        /// <inheritdoc />
        public void ShowPurchaseFailed()
        {
            RequireOpen(nameof(ShowPurchaseFailed));
            message.Show(config.PurchaseFailedTitle, config.PurchaseFailedText);
            overlays.Open(UiOverlay.Message);
        }

        /// <inheritdoc />
        public void ShowWin(LevelResult result)
        {
            if (!result.IsWin)
            {
                throw new ArgumentException("ShowWin was given a loss; use ShowLoss.", nameof(result));
            }

            RequireOpen(nameof(ShowWin));
            resultOverlay.Show(result, config);
        }

        /// <inheritdoc />
        public void ShowLoss(LevelResult result)
        {
            if (result.IsWin)
            {
                throw new ArgumentException("ShowLoss was given a win; use ShowWin.", nameof(result));
            }

            RequireOpen(nameof(ShowLoss));
            resultOverlay.Show(result, config);
        }

        /// <inheritdoc />
        public void HideResult()
        {
            EndGoldFlight();
            if (resultOverlay != null)
            {
                resultOverlay.Hide(config);
            }
        }

        /// <summary>
        /// Closes the result panel and the confirmation at once, with any gold
        /// flight ended: the level's interface is being shown afresh or hidden,
        /// so nothing of the last level's panels may still be closing.
        /// </summary>
        private void HidePanelsNow()
        {
            EndGoldFlight();
            if (resultOverlay != null)
            {
                resultOverlay.HideNow();
            }

            if (confirmOverlay != null)
            {
                confirmOverlay.HideNow();
            }
        }

        /// <inheritdoc />
        public void HoldGold(int shownGold)
        {
            hud.GoldCounter.Hold(shownGold);
        }

        /// <inheritdoc />
        public void FlyWinGold(int amount, int shownGold, int seed)
        {
            if (!resultOverlay.IsOpen)
            {
                EndGoldFlight();
                return;
            }

            coinFlight.Fly(
                resultOverlay.GoldCoin, hud.GoldTarget, CoinFlight.Layout(amount, config.CreateCoinFlightSettings(), seed),
                config.CoinFlightWinDelaySeconds, hud.GoldCounter, shownGold, hud.GoldCanvas);
        }

        /// <inheritdoc />
        public void FlyPackGold(string productId, int amount, int shownGold, int seed)
        {
            if (!goldPacks.TryGetShownCard(productId, out var card))
            {
                EndGoldFlight();
                return;
            }

            coinFlight.Fly(
                card, hud.GoldTarget, CoinFlight.Layout(amount, config.CreateCoinFlightSettings(), seed), 0f, hud.GoldCounter,
                shownGold, hud.GoldCanvas);
        }

        /// <summary>
        /// Ends a gold flight, if any, and a hold with no flight yet — a
        /// campaign win's gold before its panel — so the HUD shows the
        /// wallet's value at once.
        /// </summary>
        private void EndGoldFlight()
        {
            if (coinFlight != null)
            {
                coinFlight.Stop();
            }

            if (hud != null)
            {
                hud.GoldCounter.Release();
            }
        }

        /// <inheritdoc />
        public void ShowConfirm(ConfirmKind kind, int streakAtRisk)
        {
            RequireOpen(nameof(ShowConfirm));
            confirmOverlay.Show(kind, streakAtRisk, config);
        }

        /// <inheritdoc />
        public void HideConfirm()
        {
            if (confirmOverlay != null)
            {
                confirmOverlay.Hide(config);
            }
        }

        /// <inheritdoc />
        public void Hide()
        {
            EndGoldFlight();
            overlays.Clear();
            ApplyOverlays(false);
            HidePanelsNow();
            hud.CancelTimeBonus();
            gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard[BackKey].wasPressedThisFrame || (inputHold != null && inputHold()))
            {
                return;
            }

            // The top in-level overlay first; then the confirmation, where
            // Back is Keep playing. There is no back from a result.
            if (overlays.Back())
            {
                ButtonPressed?.Invoke();
            }
            else if (IsConfirmOpen)
            {
                ConfirmKeepPlaying?.Invoke();
                ButtonPressed?.Invoke();
            }
        }

        private void RequireOpen(string member)
        {
            if (!IsOpen)
            {
                throw new InvalidOperationException($"{name}: LevelUi.{member} was called while the level's interface is not open; call Show first.");
            }
        }

        private bool OverlaysShareOneParent()
        {
            var parts = new Component[] { notEnoughGold, goldPacks, testPurchase, message, resultOverlay, confirmOverlay };
            Transform parent = null;
            for (var i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null)
                {
                    // Reported as missing above.
                    return true;
                }

                if (i == 0)
                {
                    parent = parts[i].transform.parent;
                }
                else if (parts[i].transform.parent != parent)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Shows the open overlays alone, stacked by sibling order with the
        /// newest last: with <paramref name="isAnimated"/> each one that opens
        /// pops and each one that closes plays the short close beneath the
        /// open ones (<see cref="OverlayMotion"/>); otherwise at once.
        /// </summary>
        private void ApplyOverlays(bool isAnimated)
        {
            CacheMotions();
            var open = overlays.Overlays;
            OverlayMotion.Apply(notEnoughGoldMotion, Contains(open, UiOverlay.NotEnoughGold), isAnimated, config);
            OverlayMotion.Apply(goldPacksMotion, Contains(open, UiOverlay.GoldPacks), isAnimated, config);
            OverlayMotion.Apply(testPurchaseMotion, Contains(open, UiOverlay.TestPurchase), isAnimated, config);
            OverlayMotion.Apply(messageMotion, Contains(open, UiOverlay.Message), isAnimated, config);
            for (var i = 0; i < open.Count; i++)
            {
                OverlayObject(open[i]).transform.SetAsLastSibling();
            }
        }

        private void OnOverlaysChanged() => ApplyOverlays(true);

        /// <summary>Reads each stack overlay's motion once; <see cref="MissingSetup"/> reports one that has none.</summary>
        private void CacheMotions()
        {
            if (notEnoughGoldMotion != null)
            {
                return;
            }

            notEnoughGoldMotion = notEnoughGold.GetComponent<OverlayMotion>();
            goldPacksMotion = goldPacks.GetComponent<OverlayMotion>();
            testPurchaseMotion = testPurchase.GetComponent<OverlayMotion>();
            messageMotion = message.GetComponent<OverlayMotion>();
        }

        private static bool Contains(IReadOnlyList<UiOverlay> open, UiOverlay overlay)
        {
            for (var i = 0; i < open.Count; i++)
            {
                if (open[i] == overlay)
                {
                    return true;
                }
            }

            return false;
        }

        private Component OverlayObject(UiOverlay overlay)
        {
            switch (overlay)
            {
                case UiOverlay.NotEnoughGold:
                    return notEnoughGold;
                case UiOverlay.GoldPacks:
                    return goldPacks;
                case UiOverlay.TestPurchase:
                    return testPurchase;
                case UiOverlay.Message:
                    return message;
                default:
                    throw new ArgumentOutOfRangeException(nameof(overlay), overlay, "Not an overlay of the level's interface.");
            }
        }

        /// <summary>
        /// Listens to the overlay stack and to every view, once. Skipped while
        /// a view is unassigned: <see cref="MissingSetup"/> reports it, and
        /// the bootstrap never shows a level's interface that is not set up.
        /// </summary>
        private void Subscribe()
        {
            if (isSubscribed || hud == null || jokerBar == null || notEnoughGold == null || goldPacks == null
                || testPurchase == null || message == null || resultOverlay == null || confirmOverlay == null || coinFlight == null)
            {
                return;
            }

            coinFlight.CoinLanded += OnCoinLanded;
            overlays.Changed += OnOverlaysChanged;
            overlays.AllClosed += OnAllClosed;
            hud.MenuRequested += OnMenuRequested;
            hud.RestartRequested += OnRestartRequested;
            jokerBar.JokerPressed += OnJokerPressed;
            notEnoughGold.CloseRequested += OnNotEnoughGoldCloseRequested;
            notEnoughGold.GetGoldRequested += OnGetGoldRequested;
            goldPacks.CloseRequested += OnGoldPacksCloseRequested;
            goldPacks.GoldPackRequested += OnGoldPackRequested;
            testPurchase.CloseRequested += OnTestPurchaseCloseRequested;
            testPurchase.BuyRequested += OnTestPurchaseBuyRequested;
            message.CloseRequested += OnMessageCloseRequested;
            resultOverlay.NextRequested += OnNextRequested;
            resultOverlay.RestartRequested += OnResultRestartRequested;
            resultOverlay.MenuRequested += OnResultMenuRequested;
            confirmOverlay.LoseLifeRequested += OnLoseLifeRequested;
            confirmOverlay.KeepPlayingRequested += OnKeepPlayingRequested;
            isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!isSubscribed)
            {
                return;
            }

            // Unsubscribing from a C# event of a destroyed view is safe, so
            // this runs during teardown as well.
            coinFlight.CoinLanded -= OnCoinLanded;
            overlays.Changed -= OnOverlaysChanged;
            overlays.AllClosed -= OnAllClosed;
            hud.MenuRequested -= OnMenuRequested;
            hud.RestartRequested -= OnRestartRequested;
            jokerBar.JokerPressed -= OnJokerPressed;
            notEnoughGold.CloseRequested -= OnNotEnoughGoldCloseRequested;
            notEnoughGold.GetGoldRequested -= OnGetGoldRequested;
            goldPacks.CloseRequested -= OnGoldPacksCloseRequested;
            goldPacks.GoldPackRequested -= OnGoldPackRequested;
            testPurchase.CloseRequested -= OnTestPurchaseCloseRequested;
            testPurchase.BuyRequested -= OnTestPurchaseBuyRequested;
            message.CloseRequested -= OnMessageCloseRequested;
            resultOverlay.NextRequested -= OnNextRequested;
            resultOverlay.RestartRequested -= OnResultRestartRequested;
            resultOverlay.MenuRequested -= OnResultMenuRequested;
            confirmOverlay.LoseLifeRequested -= OnLoseLifeRequested;
            confirmOverlay.KeepPlayingRequested -= OnKeepPlayingRequested;
            isSubscribed = false;
        }

        private void OnAllClosed() => OverlaysClosed?.Invoke();

        private void OnCoinLanded() => CoinLanded?.Invoke();

        /// <summary>A close button: closes its overlay if it is on top, and sounds the press.</summary>
        private void OnCloseClicked(UiOverlay overlay)
        {
            overlays.CloseIfOnTop(overlay);
            ButtonPressed?.Invoke();
        }

        private void OnMenuRequested()
        {
            MenuRequested?.Invoke();
            ButtonPressed?.Invoke();
        }

        private void OnRestartRequested()
        {
            RestartRequested?.Invoke();
            ButtonPressed?.Invoke();
        }

        private void OnJokerPressed(JokerKind kind)
        {
            JokerPressed?.Invoke(kind);
            ButtonPressed?.Invoke();
        }

        private void OnNotEnoughGoldCloseRequested() => OnCloseClicked(UiOverlay.NotEnoughGold);

        /// <summary>Get Gold: the gold packs in Not Enough Gold's place, so the level stays paused on the way.</summary>
        private void OnGetGoldRequested()
        {
            overlays.ReplaceTop(UiOverlay.GoldPacks);
            ButtonPressed?.Invoke();
        }

        private void OnGoldPacksCloseRequested() => OnCloseClicked(UiOverlay.GoldPacks);

        private void OnGoldPackRequested(string productId)
        {
            GoldPackRequested?.Invoke(productId);
            ButtonPressed?.Invoke();
        }

        private void OnTestPurchaseCloseRequested() => OnCloseClicked(UiOverlay.TestPurchase);

        private void OnTestPurchaseBuyRequested()
        {
            overlays.Buy();
            ButtonPressed?.Invoke();
        }

        private void OnMessageCloseRequested() => OnCloseClicked(UiOverlay.Message);

        // Each panel press raises its own event, then the tap sound, so a
        // listener that stops sounds has done so first (Module 25).
        private void OnNextRequested()
        {
            NextRequested?.Invoke();
            ButtonPressed?.Invoke();
        }

        private void OnResultRestartRequested()
        {
            ResultRestartRequested?.Invoke();
            ButtonPressed?.Invoke();
        }

        private void OnResultMenuRequested()
        {
            ResultMenuRequested?.Invoke();
            ButtonPressed?.Invoke();
        }

        private void OnLoseLifeRequested()
        {
            ConfirmLoseLife?.Invoke();
            ButtonPressed?.Invoke();
        }

        private void OnKeepPlayingRequested()
        {
            ConfirmKeepPlaying?.Invoke();
            ButtonPressed?.Invoke();
        }
    }
}
