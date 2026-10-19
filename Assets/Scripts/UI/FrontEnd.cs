using System;
using System.Collections.Generic;
using GateRush.Meta;
using GateRush.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GateRush.UI
{
    /// <summary>
    /// The front end (Modules 35 and 36, D68): three tabs — Store, Home and
    /// Leaderboard — under a top bar and over a tab bar, and a stack of
    /// overlays — Settings, Levels, Lives, Not Enough Gold, Message and Test
    /// Purchase — over them. Implements <see cref="IFrontEnd"/>, the one way
    /// the bootstrap shows and hears it.
    /// </summary>
    /// <remarks>
    /// <para><b>No rules.</b> It shows the <see cref="FrontEndState"/> it is
    /// given and raises an event for every request the bootstrap decides on;
    /// moving between tabs and overlays (<see cref="UiNavigation"/>) is all it
    /// decides itself.</para>
    /// <para><b>Parts.</b> The screens are prefabs laid out in the Editor;
    /// code finds their parts only through the serialized fields here and on
    /// the views (<see cref="TopBar"/>, <see cref="TabBar"/>,
    /// <see cref="HomeScreen"/>, <see cref="StoreScreen"/>, the overlays).
    /// <see cref="MissingSetup"/> names whatever is not assigned.</para>
    /// <para><b>Open and closed.</b> <see cref="Show"/> and
    /// <see cref="Hide"/> switch this component's own GameObject, so nothing
    /// here runs, Back included, while it is closed. Nothing depends on the
    /// order in which this and the bootstrap wake up. The overlays share one
    /// parent and are stacked by sibling order, newest last.</para>
    /// <para><b>The test purchase sheet</b> (D72) answers exactly once: Buy
    /// answers yes; anything else that closes it — Cancel, its close button,
    /// Back, or the front end being hidden — answers no. Its scrim takes every
    /// tap, so nothing under it can be pressed while it is open.</para>
    /// <para><b>The Store</b> scrolls to the top whenever it is selected —
    /// by its tab, the gold "+" or Get Gold — so the gold packs are the first
    /// thing seen.</para>
    /// <para><b>Back</b> is the Escape key, which is also Android's back
    /// button under the Input System.</para>
    /// <para>No animation yet: screen transitions are Phase 7.</para>
    /// </remarks>
    public sealed class FrontEnd : MonoBehaviour, IFrontEnd
    {
        // The key Back is read from: Escape, which the Input System also
        // reports for Android's back button. Not a tunable.
        private const Key BackKey = Key.Escape;

        [Tooltip("Assets/Config/UiConfig.asset: every text, format and colour set from code.")]
        [SerializeField] private UiConfig config;

        [Tooltip("The Store tab's page.")]
        [SerializeField] private StoreScreen store;

        [Tooltip("The Home tab's page.")]
        [SerializeField] private HomeScreen home;

        [Tooltip("The Leaderboard tab's page.")]
        [SerializeField] private GameObject leaderboardPage;

        [SerializeField] private TopBar topBar;
        [SerializeField] private TabBar tabBar;

        [Tooltip("Every overlay must share one parent: they are stacked by sibling order.")]
        [SerializeField] private SettingsOverlay settings;

        [SerializeField] private LevelsOverlay levels;
        [SerializeField] private LivesOverlay lives;
        [SerializeField] private NotEnoughGoldOverlay notEnoughGold;
        [SerializeField] private MessageOverlay message;
        [SerializeField] private TestPurchaseOverlay testPurchase;

        private readonly UiNavigation navigation = new UiNavigation();

        // The tab the notice belongs to: the notice goes when the tab changes.
        private UiTab shownTab = UiTab.Home;
        private bool isSubscribed;

        // The open test purchase sheet's answer; null when none is waiting.
        private Action<bool> pendingAnswer;

        /// <inheritdoc />
        public event Action PlayRequested;

        /// <inheritdoc />
        public event Action<string> LevelRequested;

        /// <inheritdoc />
        public event Action SoundToggleRequested;

        /// <inheritdoc />
        public event Action RefillRequested;

        /// <inheritdoc />
        public event Action<string> GoldPackRequested;

        /// <inheritdoc />
        public event Action<JokerKind> JokerPurchaseRequested;

        /// <inheritdoc />
        public event Action ButtonPressed;

        /// <inheritdoc />
        public bool IsOpen => gameObject.activeSelf;

        /// <inheritdoc />
        public string MissingSetup()
        {
            var missing = new List<string>();
            UiParts.AddIfMissing(missing, config, nameof(FrontEnd), nameof(config));
            UiParts.AddIfMissing(missing, store, nameof(FrontEnd), nameof(store));
            UiParts.AddIfMissing(missing, home, nameof(FrontEnd), nameof(home));
            UiParts.AddIfMissing(missing, leaderboardPage, nameof(FrontEnd), nameof(leaderboardPage));
            UiParts.AddIfMissing(missing, topBar, nameof(FrontEnd), nameof(topBar));
            UiParts.AddIfMissing(missing, tabBar, nameof(FrontEnd), nameof(tabBar));
            UiParts.AddIfMissing(missing, settings, nameof(FrontEnd), nameof(settings));
            UiParts.AddIfMissing(missing, levels, nameof(FrontEnd), nameof(levels));
            UiParts.AddIfMissing(missing, lives, nameof(FrontEnd), nameof(lives));
            UiParts.AddIfMissing(missing, notEnoughGold, nameof(FrontEnd), nameof(notEnoughGold));
            UiParts.AddIfMissing(missing, message, nameof(FrontEnd), nameof(message));
            UiParts.AddIfMissing(missing, testPurchase, nameof(FrontEnd), nameof(testPurchase));

            if (store != null)
            {
                store.AddMissing(missing);
            }

            if (home != null)
            {
                home.AddMissing(missing);
            }

            if (topBar != null)
            {
                topBar.AddMissing(missing);
            }

            if (tabBar != null)
            {
                tabBar.AddMissing(missing);
            }

            if (settings != null)
            {
                settings.AddMissing(missing);
            }

            if (levels != null)
            {
                levels.AddMissing(missing);
            }

            if (lives != null)
            {
                lives.AddMissing(missing);
            }

            if (notEnoughGold != null)
            {
                notEnoughGold.AddMissing(missing);
            }

            if (message != null)
            {
                message.AddMissing(missing);
            }

            if (testPurchase != null)
            {
                testPurchase.AddMissing(missing);
            }

            var tints = GetComponentsInChildren<UiTint>(true);
            for (var i = 0; i < tints.Length; i++)
            {
                tints[i].AddMissing(missing);
            }

            // The instance is saved inactive, so the search includes inactive parents.
            if (GetComponentInParent<Canvas>(true) == null)
            {
                missing.Add("FrontEnd must sit on a Canvas or under one");
            }

            if (!OverlaysShareOneParent())
            {
                missing.Add("the Settings, Levels, Lives, Not Enough Gold, Message and Test Purchase overlays must share one parent, which stacks them");
            }

            if (config != null)
            {
                missing.AddRange(config.Problems());
            }

            return missing.Count == 0
                ? null
                : $"{name}: the front end is not set up: {string.Join("; ", missing)}.";
        }

        /// <inheritdoc />
        public void Show(FrontEndState state)
        {
            gameObject.SetActive(true);
            UiTint.ApplyAll(this, config);
            navigation.Reset();
            shownTab = navigation.Tab;
            ApplyNavigation();
            home.ClearNotice();
            Refresh(state);
        }

        /// <inheritdoc />
        public void Refresh(FrontEndState state)
        {
            topBar.Show(state, config);
            home.Show(state, config);
            store.Show(state, config);
            settings.Show(state);
            levels.Show(state.Levels ?? Array.Empty<LevelEntry>(), config);
            lives.Show(state, config);
        }

        /// <inheritdoc />
        public void ShowLives()
        {
            RequireOpen(nameof(ShowLives));
            navigation.Open(UiOverlay.Lives);
        }

        /// <inheritdoc />
        public void ShowNotice(string text)
        {
            RequireOpen(nameof(ShowNotice));
            navigation.SelectTab(UiTab.Home);
            home.ShowNotice(text);
        }

        /// <inheritdoc />
        public void ShowNotEnoughGold(int shortfall)
        {
            RequireOpen(nameof(ShowNotEnoughGold));
            notEnoughGold.Show(shortfall, config);
            navigation.Open(UiOverlay.NotEnoughGold);
        }

        /// <inheritdoc />
        public void ShowPurchaseFailed()
        {
            RequireOpen(nameof(ShowPurchaseFailed));
            message.Show(config.PurchaseFailedTitle, config.PurchaseFailedText);
            navigation.Open(UiOverlay.Message);
        }

        /// <inheritdoc />
        public void ConfirmTestPurchase(GoldOffer offer, Action<bool> answer)
        {
            if (answer == null)
            {
                throw new ArgumentNullException(nameof(answer));
            }

            RequireOpen(nameof(ConfirmTestPurchase));
            if (pendingAnswer != null)
            {
                throw new InvalidOperationException(
                    $"{name}: FrontEnd.{nameof(ConfirmTestPurchase)} was called while a test purchase is waiting for its answer.");
            }

            pendingAnswer = answer;
            testPurchase.Show(offer, config);
            navigation.Open(UiOverlay.TestPurchase);
        }

        /// <inheritdoc />
        public void Hide()
        {
            gameObject.SetActive(false);
            AnswerPending(false);
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
            if (keyboard != null && keyboard[BackKey].wasPressedThisFrame && navigation.Back())
            {
                ButtonPressed?.Invoke();
            }
        }

        private void RequireOpen(string member)
        {
            if (!IsOpen)
            {
                throw new InvalidOperationException($"{name}: FrontEnd.{member} was called while the front end is not open; call Show first.");
            }
        }

        /// <summary>Calls the waiting test purchase answer, if any, once, with <paramref name="isBought"/>.</summary>
        private void AnswerPending(bool isBought)
        {
            var answer = pendingAnswer;
            pendingAnswer = null;
            answer?.Invoke(isBought);
        }

        private bool OverlaysShareOneParent()
        {
            var overlays = new Component[] { settings, levels, lives, notEnoughGold, message, testPurchase };
            Transform parent = null;
            for (var i = 0; i < overlays.Length; i++)
            {
                if (overlays[i] == null)
                {
                    // Reported as missing above.
                    return true;
                }

                if (i == 0)
                {
                    parent = overlays[i].transform.parent;
                }
                else if (overlays[i].transform.parent != parent)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Shows the selected tab's page and the tab bar's selection, and the
        /// open overlays alone, stacked by sibling order with the newest last.
        /// </summary>
        private void ApplyNavigation()
        {
            var tab = navigation.Tab;
            store.gameObject.SetActive(tab == UiTab.Store);
            home.gameObject.SetActive(tab == UiTab.Home);
            leaderboardPage.SetActive(tab == UiTab.Leaderboard);
            tabBar.ShowSelected(tab);

            var open = navigation.Overlays;
            settings.gameObject.SetActive(Contains(open, UiOverlay.Settings));
            levels.gameObject.SetActive(Contains(open, UiOverlay.Levels));
            lives.gameObject.SetActive(Contains(open, UiOverlay.Lives));
            notEnoughGold.gameObject.SetActive(Contains(open, UiOverlay.NotEnoughGold));
            message.gameObject.SetActive(Contains(open, UiOverlay.Message));
            testPurchase.gameObject.SetActive(Contains(open, UiOverlay.TestPurchase));
            for (var i = 0; i < open.Count; i++)
            {
                OverlayObject(open[i]).transform.SetAsLastSibling();
            }
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

        private Component OverlayObject(UiOverlay overlay)
        {
            switch (overlay)
            {
                case UiOverlay.Settings:
                    return settings;
                case UiOverlay.Levels:
                    return levels;
                case UiOverlay.Lives:
                    return lives;
                case UiOverlay.NotEnoughGold:
                    return notEnoughGold;
                case UiOverlay.Message:
                    return message;
                case UiOverlay.TestPurchase:
                    return testPurchase;
                default:
                    throw new ArgumentOutOfRangeException(nameof(overlay), overlay, "Not a front end overlay.");
            }
        }

        /// <summary>
        /// Listens to the navigation and to every view, once. Skipped while a
        /// view is unassigned: <see cref="MissingSetup"/> reports it, and the
        /// bootstrap never shows a front end that is not set up.
        /// </summary>
        private void Subscribe()
        {
            if (isSubscribed || store == null || home == null || topBar == null || tabBar == null
                || settings == null || levels == null || lives == null
                || notEnoughGold == null || message == null || testPurchase == null)
            {
                return;
            }

            navigation.Changed += OnNavigationChanged;
            home.PlayRequested += OnPlayRequested;
            store.GoldPackRequested += OnGoldPackRequested;
            store.JokerPurchaseRequested += OnJokerPurchaseRequested;
            store.RefillRequested += OnRefillRequested;
            topBar.StoreRequested += OnStoreRequested;
            topBar.LivesRequested += OnLivesRequested;
            topBar.SettingsRequested += OnSettingsRequested;
            tabBar.TabSelected += OnTabSelected;
            settings.CloseRequested += OnSettingsCloseRequested;
            settings.SoundToggleRequested += OnSoundToggleRequested;
            settings.LevelsRequested += OnLevelsRequested;
            levels.CloseRequested += OnLevelsCloseRequested;
            levels.LevelRequested += OnLevelRequested;
            lives.CloseRequested += OnLivesCloseRequested;
            lives.RefillRequested += OnRefillRequested;
            notEnoughGold.CloseRequested += OnNotEnoughGoldCloseRequested;
            notEnoughGold.GetGoldRequested += OnGetGoldRequested;
            message.CloseRequested += OnMessageCloseRequested;
            testPurchase.CloseRequested += OnTestPurchaseCloseRequested;
            testPurchase.BuyRequested += OnTestPurchaseBuyRequested;
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
            navigation.Changed -= OnNavigationChanged;
            home.PlayRequested -= OnPlayRequested;
            store.GoldPackRequested -= OnGoldPackRequested;
            store.JokerPurchaseRequested -= OnJokerPurchaseRequested;
            store.RefillRequested -= OnRefillRequested;
            topBar.StoreRequested -= OnStoreRequested;
            topBar.LivesRequested -= OnLivesRequested;
            topBar.SettingsRequested -= OnSettingsRequested;
            tabBar.TabSelected -= OnTabSelected;
            settings.CloseRequested -= OnSettingsCloseRequested;
            settings.SoundToggleRequested -= OnSoundToggleRequested;
            settings.LevelsRequested -= OnLevelsRequested;
            levels.CloseRequested -= OnLevelsCloseRequested;
            levels.LevelRequested -= OnLevelRequested;
            lives.CloseRequested -= OnLivesCloseRequested;
            lives.RefillRequested -= OnRefillRequested;
            notEnoughGold.CloseRequested -= OnNotEnoughGoldCloseRequested;
            notEnoughGold.GetGoldRequested -= OnGetGoldRequested;
            message.CloseRequested -= OnMessageCloseRequested;
            testPurchase.CloseRequested -= OnTestPurchaseCloseRequested;
            testPurchase.BuyRequested -= OnTestPurchaseBuyRequested;
            isSubscribed = false;
        }

        /// <summary>
        /// Shows where the navigation now is. A test purchase sheet that was
        /// closed by anything but Buy — Cancel, its close button, Back, a tab —
        /// is answered no here, the one place every way of closing it passes.
        /// </summary>
        private void OnNavigationChanged()
        {
            ApplyNavigation();
            if (navigation.Tab != shownTab)
            {
                shownTab = navigation.Tab;
                home.ClearNotice();
            }

            if (pendingAnswer != null && !Contains(navigation.Overlays, UiOverlay.TestPurchase))
            {
                AnswerPending(false);
            }
        }

        /// <summary>Closes <paramref name="overlay"/> when it is the top one — the overlays above it cover it.</summary>
        private void CloseIfOnTop(UiOverlay overlay)
        {
            var open = navigation.Overlays;
            if (open.Count > 0 && open[open.Count - 1] == overlay)
            {
                navigation.Back();
            }
        }

        /// <summary>A close button: closes its overlay if it is on top, and sounds the press.</summary>
        private void OnCloseClicked(UiOverlay overlay)
        {
            CloseIfOnTop(overlay);
            ButtonPressed?.Invoke();
        }

        /// <summary>Selects the Store, closing every overlay, and scrolls it to the gold packs.</summary>
        private void SelectStore()
        {
            navigation.SelectTab(UiTab.Store);
            store.ScrollToTop();
        }

        private void OnPlayRequested()
        {
            PlayRequested?.Invoke();
            ButtonPressed?.Invoke();
        }

        private void OnGoldPackRequested(string productId)
        {
            GoldPackRequested?.Invoke(productId);
            ButtonPressed?.Invoke();
        }

        private void OnJokerPurchaseRequested(JokerKind kind)
        {
            JokerPurchaseRequested?.Invoke(kind);
            ButtonPressed?.Invoke();
        }

        private void OnStoreRequested()
        {
            SelectStore();
            ButtonPressed?.Invoke();
        }

        private void OnLivesRequested()
        {
            navigation.Open(UiOverlay.Lives);
            ButtonPressed?.Invoke();
        }

        private void OnSettingsRequested()
        {
            navigation.Open(UiOverlay.Settings);
            ButtonPressed?.Invoke();
        }

        private void OnTabSelected(UiTab tab)
        {
            if (tab == UiTab.Store)
            {
                SelectStore();
            }
            else
            {
                navigation.SelectTab(tab);
            }

            ButtonPressed?.Invoke();
        }

        private void OnSettingsCloseRequested() => OnCloseClicked(UiOverlay.Settings);

        private void OnSoundToggleRequested()
        {
            SoundToggleRequested?.Invoke();
            ButtonPressed?.Invoke();
        }

        private void OnLevelsRequested()
        {
            navigation.Open(UiOverlay.Levels);
            ButtonPressed?.Invoke();
        }

        private void OnLevelsCloseRequested() => OnCloseClicked(UiOverlay.Levels);

        private void OnLevelRequested(string levelName)
        {
            LevelRequested?.Invoke(levelName);
            ButtonPressed?.Invoke();
        }

        private void OnLivesCloseRequested() => OnCloseClicked(UiOverlay.Lives);

        private void OnRefillRequested()
        {
            RefillRequested?.Invoke();
            ButtonPressed?.Invoke();
        }

        private void OnNotEnoughGoldCloseRequested() => OnCloseClicked(UiOverlay.NotEnoughGold);

        private void OnGetGoldRequested()
        {
            SelectStore();
            ButtonPressed?.Invoke();
        }

        private void OnMessageCloseRequested() => OnCloseClicked(UiOverlay.Message);

        private void OnTestPurchaseCloseRequested() => OnCloseClicked(UiOverlay.TestPurchase);

        /// <summary>
        /// Buy: the answer is taken before the sheet closes, so closing it
        /// does not answer no, and given after, so whatever the answer opens
        /// lands on top.
        /// </summary>
        private void OnTestPurchaseBuyRequested()
        {
            var answer = pendingAnswer;
            pendingAnswer = null;
            CloseIfOnTop(UiOverlay.TestPurchase);
            answer?.Invoke(true);
            ButtonPressed?.Invoke();
        }
    }
}
