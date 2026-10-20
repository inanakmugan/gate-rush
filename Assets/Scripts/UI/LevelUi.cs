using System;
using System.Collections.Generic;
using GateRush.Meta;
using GateRush.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GateRush.UI
{
    /// <summary>
    /// The level's own interface (Module 40, D74): the HUD in the top band,
    /// the joker bar in the bottom band, and a stack of overlays over the
    /// level — Not Enough Gold, the gold packs, the test purchase sheet and
    /// the message, the same prefabs as the front end's. Implements
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
    /// once (<see cref="LevelOverlayStack"/>).</para>
    /// <para><b>Back</b> is the Escape key, which is also Android's back
    /// button under the Input System. With an overlay open it closes the top
    /// one and nothing else; with none open it is not read here, and nothing
    /// else in a level reads it.</para>
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

        private readonly LevelOverlayStack overlays = new LevelOverlayStack();
        private bool isSubscribed;

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
        public bool IsOpen => gameObject.activeSelf;

        /// <inheritdoc />
        public bool IsOverlayOpen => overlays.IsOpen;

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

            UiParts.AddMissingTints(missing, this);

            // The instance is saved inactive, so the search includes inactive parents.
            if (GetComponentInParent<Canvas>(true) == null)
            {
                missing.Add("LevelUi must sit on a Canvas or under one");
            }

            if (!OverlaysShareOneParent())
            {
                missing.Add("the Not Enough Gold, Gold Packs, Test Purchase and Message overlays must share one parent, which stacks them");
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
        public void Show(LevelUiState state)
        {
            gameObject.SetActive(true);
            UiTint.ApplyAll(this, config);
            overlays.Clear();
            ApplyOverlays();
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
        public void Hide()
        {
            overlays.Clear();
            ApplyOverlays();
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
            if (keyboard != null && keyboard[BackKey].wasPressedThisFrame && overlays.Back())
            {
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
            var parts = new Component[] { notEnoughGold, goldPacks, testPurchase, message };
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

        /// <summary>Shows the open overlays alone, stacked by sibling order with the newest last.</summary>
        private void ApplyOverlays()
        {
            var open = overlays.Overlays;
            notEnoughGold.gameObject.SetActive(Contains(open, UiOverlay.NotEnoughGold));
            goldPacks.gameObject.SetActive(Contains(open, UiOverlay.GoldPacks));
            testPurchase.gameObject.SetActive(Contains(open, UiOverlay.TestPurchase));
            message.gameObject.SetActive(Contains(open, UiOverlay.Message));
            for (var i = 0; i < open.Count; i++)
            {
                OverlayObject(open[i]).transform.SetAsLastSibling();
            }
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
                || testPurchase == null || message == null)
            {
                return;
            }

            overlays.Changed += ApplyOverlays;
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
            overlays.Changed -= ApplyOverlays;
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
            isSubscribed = false;
        }

        private void OnAllClosed() => OverlaysClosed?.Invoke();

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
    }
}
