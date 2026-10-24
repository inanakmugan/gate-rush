using System;
using System.Collections.Generic;
using DG.Tweening;
using GateRush.Meta;
using GateRush.Platform;
using GateRush.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GateRush.UI
{
    /// <summary>
    /// The front end (Modules 35 to 38, D68): three tabs — Store, Home and
    /// Leaderboard — under a top bar and over a tab bar, and a stack of
    /// overlays — Settings, Levels, Lives, Not Enough Gold, Message, Test
    /// Purchase, Win Streak, Streak Reward, Profile and End of Content — over
    /// them. Implements <see cref="IFrontEnd"/>, the one way the bootstrap
    /// shows and hears it.
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
    /// <see cref="MissingSetup"/> names whatever is not assigned. The streak
    /// bar sits inside Home Page, which is laid out by hand, so it is held
    /// here rather than by <see cref="HomeScreen"/>.</para>
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
    /// <para><b>The Leaderboard</b> (Module 39) shows Loading and asks for a
    /// fresh table each time it is selected, and when Retry is pressed; a
    /// result that arrives while another tab is selected is ignored.</para>
    /// <para><b>Back</b> is the Escape key, which is also Android's back
    /// button under the Input System.</para>
    /// <para><b>Privacy</b> (Module 49) is the front end's own business, as
    /// Levels is: the Settings row opens the message prompt at its tall size
    /// with the UI Config's privacy text, over Settings, and OK returns to
    /// Settings. <b>End of Content</b> opens only when the bootstrap asks
    /// (<see cref="ShowEndOfContent"/>); its close button closes it, and its
    /// Restart is the bootstrap's to decide.</para>
    /// <para><b>The painting</b> behind everything (Module 45) follows the
    /// campaign level of every state shown, by the UI Config's list.</para>
    /// <para><b>Motion (Module 47).</b> What is shown changes at once in the
    /// navigation; only the look follows. A new tab's page fades in over the
    /// old one (<see cref="TabPages"/>), its tween carrying this front end as
    /// its id, and the Tab Bar's newly selected tab pops once. Every overlay
    /// opens with the shared pop and closes with its short reverse
    /// (<see cref="OverlayMotion"/>). <see cref="Show"/> shows everything at
    /// once; a disable ends a page fade.</para>
    /// <para><b>Held input.</b> While the screen fade holds input
    /// (<see cref="HoldInput"/>) Back is ignored; the fade itself takes every
    /// press.</para>
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
        [SerializeField] private LeaderboardScreen leaderboard;

        [SerializeField] private TopBar topBar;
        [SerializeField] private TabBar tabBar;

        [Tooltip("Every overlay must share one parent: they are stacked by sibling order.")]
        [SerializeField] private SettingsOverlay settings;

        [SerializeField] private LevelsOverlay levels;
        [SerializeField] private LivesOverlay lives;
        [SerializeField] private NotEnoughGoldOverlay notEnoughGold;
        [SerializeField] private MessageOverlay message;
        [SerializeField] private TestPurchaseOverlay testPurchase;
        [SerializeField] private WinStreakOverlay winStreak;
        [SerializeField] private StreakRewardOverlay streakReward;
        [SerializeField] private ProfileOverlay profile;
        [SerializeField] private EndOfContentOverlay endOfContent;

        [Tooltip("The streak bar: a nested instance under the Home Page instance's Streak Area, placed by the UI builder.")]
        [SerializeField] private StreakBar streakBar;

        [Tooltip("Assets/Config/JokerIcons.asset: the joker icons the streak's screens draw.")]
        [SerializeField] private JokerIcons jokerIcons;

        [Tooltip("Assets/Config/AvatarCatalog.asset: the avatars the top bar and Profile draw, the same asset the bootstrap holds.")]
        [SerializeField] private AvatarCatalog avatarCatalog;

        [Tooltip("The painting behind everything (Module 45): the UI Config's background for the campaign level.")]
        [SerializeField] private Image painting;

        [Tooltip("Envelops the screen with the painting, cropped and centred, never stretched; its ratio follows the painting's sprite.")]
        [SerializeField] private AspectRatioFitter paintingFit;

        [Tooltip("The coins bought gold flies to the Top Bar's gold in (Module 47), on their own canvas above every overlay.")]
        [SerializeField] private CoinFlightView coinFlight;

        private readonly UiNavigation navigation = new UiNavigation();
        private readonly TabPages pages = new TabPages();

        // The screen fade's query (Module 47): while it answers true, Back is
        // ignored. Null until the bootstrap sets it.
        private Func<bool> inputHold;

        // The front end's overlays, in the order of their motions.
        private readonly UiOverlay[] frontEndOverlays =
        {
            UiOverlay.Settings, UiOverlay.Levels, UiOverlay.Lives, UiOverlay.NotEnoughGold, UiOverlay.Message,
            UiOverlay.TestPurchase, UiOverlay.WinStreak, UiOverlay.StreakReward, UiOverlay.Profile, UiOverlay.EndOfContent
        };

        // Each overlay's shared open and close, and each page's group, read
        // once; MissingSetup reports any that is missing.
        private OverlayMotion[] motions;
        private CanvasGroup storeGroup;
        private CanvasGroup homeGroup;
        private CanvasGroup leaderboardGroup;

        // The tab last shown, so a tab change can end a gold flight and ask
        // the Leaderboard for a fresh table.
        private UiTab shownTab = UiTab.Home;
        private bool isSubscribed;

        // The open test purchase sheet's answer; null when none is waiting.
        private Action<bool> pendingAnswer;

        // The state last given to Show or Refresh, which the Streak Reward
        // overlay reads for the jokers still locked (D76); a default state
        // holds no lists, so it is never read before hasShownState.
        private FrontEndState shownState;
        private bool hasShownState;

        /// <inheritdoc />
        public event Action PlayRequested;

        /// <inheritdoc />
        public event Action<string> LevelRequested;

        /// <inheritdoc />
        public event Action SoundToggleRequested;

        /// <inheritdoc />
        public event Action HapticsToggleRequested;

        /// <inheritdoc />
        public event Action<string> NameChangeRequested;

        /// <inheritdoc />
        public event Action<string> AvatarChangeRequested;

        /// <inheritdoc />
        public event Action RefillRequested;

        /// <inheritdoc />
        public event Action<string> GoldPackRequested;

        /// <inheritdoc />
        public event Action<JokerKind> JokerPurchaseRequested;

        /// <inheritdoc />
        public event Action LeaderboardRequested;

        /// <inheritdoc />
        public event Action EndOfContentRestartRequested;

        /// <inheritdoc />
        public event Action ButtonPressed;

        /// <inheritdoc />
        public event Action CoinLanded;

        /// <inheritdoc />
        public bool IsOpen => gameObject.activeSelf;

        /// <inheritdoc />
        public string MissingSetup()
        {
            var missing = new List<string>();
            UiParts.AddIfMissing(missing, config, nameof(FrontEnd), nameof(config));
            UiParts.AddIfMissing(missing, store, nameof(FrontEnd), nameof(store));
            UiParts.AddIfMissing(missing, home, nameof(FrontEnd), nameof(home));
            UiParts.AddIfMissing(missing, leaderboard, nameof(FrontEnd), nameof(leaderboard));
            UiParts.AddIfMissing(missing, topBar, nameof(FrontEnd), nameof(topBar));
            UiParts.AddIfMissing(missing, tabBar, nameof(FrontEnd), nameof(tabBar));
            UiParts.AddIfMissing(missing, settings, nameof(FrontEnd), nameof(settings));
            UiParts.AddIfMissing(missing, levels, nameof(FrontEnd), nameof(levels));
            UiParts.AddIfMissing(missing, lives, nameof(FrontEnd), nameof(lives));
            UiParts.AddIfMissing(missing, notEnoughGold, nameof(FrontEnd), nameof(notEnoughGold));
            UiParts.AddIfMissing(missing, message, nameof(FrontEnd), nameof(message));
            UiParts.AddIfMissing(missing, testPurchase, nameof(FrontEnd), nameof(testPurchase));
            UiParts.AddIfMissing(missing, winStreak, nameof(FrontEnd), nameof(winStreak));
            UiParts.AddIfMissing(missing, streakReward, nameof(FrontEnd), nameof(streakReward));
            UiParts.AddIfMissing(missing, profile, nameof(FrontEnd), nameof(profile));
            UiParts.AddIfMissing(missing, endOfContent, nameof(FrontEnd), nameof(endOfContent));
            UiParts.AddIfMissing(missing, streakBar, nameof(FrontEnd), nameof(streakBar));
            UiParts.AddIfMissing(missing, jokerIcons, nameof(FrontEnd), nameof(jokerIcons));
            UiParts.AddIfMissing(missing, avatarCatalog, nameof(FrontEnd), nameof(avatarCatalog));
            UiParts.AddIfMissing(missing, painting, nameof(FrontEnd), nameof(painting));
            UiParts.AddIfMissing(missing, paintingFit, nameof(FrontEnd), nameof(paintingFit));
            UiParts.AddIfMissing(missing, coinFlight, nameof(FrontEnd), nameof(coinFlight));
            if (coinFlight != null)
            {
                coinFlight.AddMissing(missing);
            }

            if (store != null)
            {
                store.AddMissing(missing);
            }

            if (home != null)
            {
                home.AddMissing(missing);
            }

            if (leaderboard != null)
            {
                leaderboard.AddMissing(missing);
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

            if (winStreak != null)
            {
                winStreak.AddMissing(missing);
            }

            if (streakReward != null)
            {
                streakReward.AddMissing(missing);
            }

            if (profile != null)
            {
                profile.AddMissing(missing);
            }

            if (endOfContent != null)
            {
                endOfContent.AddMissing(missing);
            }

            if (streakBar != null)
            {
                streakBar.AddMissing(missing);
            }

            OverlayMotion.AddMissingOn(missing, settings, nameof(settings));
            OverlayMotion.AddMissingOn(missing, levels, nameof(levels));
            OverlayMotion.AddMissingOn(missing, lives, nameof(lives));
            OverlayMotion.AddMissingOn(missing, notEnoughGold, nameof(notEnoughGold));
            OverlayMotion.AddMissingOn(missing, message, nameof(message));
            OverlayMotion.AddMissingOn(missing, testPurchase, nameof(testPurchase));
            OverlayMotion.AddMissingOn(missing, winStreak, nameof(winStreak));
            OverlayMotion.AddMissingOn(missing, streakReward, nameof(streakReward));
            OverlayMotion.AddMissingOn(missing, profile, nameof(profile));
            OverlayMotion.AddMissingOn(missing, endOfContent, nameof(endOfContent));
            AddPageGroupMissing(missing, store, nameof(store));
            AddPageGroupMissing(missing, home, nameof(home));
            AddPageGroupMissing(missing, leaderboard, nameof(leaderboard));

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
                missing.Add("the Settings, Levels, Lives, Not Enough Gold, Message, Test Purchase, Win Streak, Streak Reward, Profile and End of Content overlays must share one parent, which stacks them");
            }

            if (config != null)
            {
                missing.AddRange(config.Problems());
            }

            if (jokerIcons != null)
            {
                missing.AddRange(jokerIcons.Problems());
            }

            if (avatarCatalog != null)
            {
                missing.AddRange(avatarCatalog.Problems());
            }

            return missing.Count == 0
                ? null
                : $"{name}: the front end is not set up: {string.Join("; ", missing)}.";
        }

        /// <inheritdoc />
        public ScreenFadeSettings ScreenFadeSettings => config.CreateScreenFadeSettings();

        /// <inheritdoc />
        public void HoldInput(Func<bool> isHeld)
        {
            inputHold = isHeld ?? throw new ArgumentNullException(nameof(isHeld));
        }

        /// <inheritdoc />
        public void Show(FrontEndState state)
        {
            gameObject.SetActive(true);
            UiTint.ApplyAll(this, config);
            EndGoldFlight();
            navigation.Reset();
            shownTab = navigation.Tab;
            ApplyNavigation(false);
            Refresh(state);
        }

        /// <inheritdoc />
        public void Refresh(FrontEndState state)
        {
            shownState = state;
            hasShownState = true;
            ShowPainting(state.CampaignLevelNumber);
            topBar.Show(state, config, avatarCatalog);
            home.Show(state, config);
            store.Show(state, config);
            settings.Show(state, config);
            profile.Show(state, config, avatarCatalog);
            levels.Show(state.Levels ?? Array.Empty<LevelEntry>(), config);
            lives.Show(state, config);
            streakBar.Show(state, config, jokerIcons);
            winStreak.Show(state, config, jokerIcons);
        }

        /// <inheritdoc />
        public void ShowLives()
        {
            RequireOpen(nameof(ShowLives));
            navigation.Open(UiOverlay.Lives);
        }

        /// <inheritdoc />
        public void ShowEndOfContent()
        {
            RequireOpen(nameof(ShowEndOfContent));
            navigation.SelectTab(UiTab.Home);
            endOfContent.Show(config);
            navigation.Open(UiOverlay.EndOfContent);
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
        public void ShowStreakReward(int latestStreak, IReadOnlyList<StreakReward> rewards)
        {
            if (rewards == null)
            {
                throw new ArgumentNullException(nameof(rewards));
            }

            if (rewards.Count == 0)
            {
                throw new ArgumentException("A streak reward announcement needs at least one reward.", nameof(rewards));
            }

            RequireOpen(nameof(ShowStreakReward));
            if (!hasShownState)
            {
                throw new InvalidOperationException(
                    $"{name}: FrontEnd.{nameof(ShowStreakReward)} was called before {nameof(Show)} or {nameof(Refresh)} gave it a " +
                    $"{nameof(FrontEndState)}; the overlay needs it to tell which jokers are still locked. Call Show first.");
            }

            streakReward.Show(latestStreak, rewards, shownState, config, jokerIcons);
            navigation.Open(UiOverlay.StreakReward);
        }

        /// <inheritdoc />
        public void ShowNameRefused()
        {
            RequireOpen(nameof(ShowNameRefused));
            profile.ShowNameRefused(config);
        }

        /// <inheritdoc />
        public void ShowLeaderboard(LeaderboardResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            RequireOpen(nameof(ShowLeaderboard));
            if (navigation.Tab != UiTab.Leaderboard)
            {
                return;
            }

            leaderboard.Show(result, config, avatarCatalog);
        }

        /// <inheritdoc />
        public void FlyPackGold(string productId, int amount, int shownGold, int seed)
        {
            if (!store.TryGetShownPackCard(productId, out var card))
            {
                EndGoldFlight();
                return;
            }

            coinFlight.Fly(
                card, topBar.GoldTarget, CoinFlight.Layout(amount, config.CreateCoinFlightSettings(), seed), 0f, topBar.GoldCounter,
                shownGold, topBar.GoldCanvas);
        }

        /// <inheritdoc />
        public void Hide()
        {
            EndGoldFlight();
            gameObject.SetActive(false);
            AnswerPending(false);
        }

        /// <summary>Ends a gold flight, if any, so the Top Bar shows the wallet's value at once.</summary>
        private void EndGoldFlight()
        {
            if (coinFlight != null)
            {
                coinFlight.Stop();
            }

            if (topBar != null)
            {
                topBar.GoldCounter.Release();
            }
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            // Pages are not switched from inside a disable; the next Show
            // shows the right one at once.
            DOTween.Kill(this);
            pages.Finish();
            RestPageAlphas();
            Unsubscribe();
        }

        private void OnDestroy()
        {
            DOTween.Kill(this);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard[BackKey].wasPressedThisFrame || (inputHold != null && inputHold()))
            {
                return;
            }

            if (navigation.Back())
            {
                ButtonPressed?.Invoke();
            }
        }

        /// <summary>
        /// Shows the painting for <paramref name="campaignLevelNumber"/>, null
        /// once the campaign is over, at its own aspect ratio. The UI Config's
        /// problems, which <see cref="MissingSetup"/> reports, guarantee one.
        /// </summary>
        private void ShowPainting(int? campaignLevelNumber)
        {
            var sprite = config.BackgroundFor(campaignLevelNumber);
            painting.sprite = sprite;
            if (sprite != null)
            {
                var size = sprite.rect.size;
                paintingFit.aspectRatio = size.x / size.y;
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
            var overlays = new Component[]
            {
                settings, levels, lives, notEnoughGold, message, testPurchase, winStreak, streakReward, profile, endOfContent
            };
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
        /// With <paramref name="isAnimated"/> a new page fades in over the old
        /// one, the newly selected tab pops, and overlays pop open or close
        /// with the short reverse beneath the open ones; otherwise all at once.
        /// </summary>
        private void ApplyNavigation(bool isAnimated)
        {
            CacheParts();
            var tab = navigation.Tab;
            ApplyPages(tab, isAnimated);
            tabBar.ShowSelected(tab, isAnimated, config);

            var open = navigation.Overlays;
            for (var i = 0; i < frontEndOverlays.Length; i++)
            {
                OverlayMotion.Apply(motions[i], Contains(open, frontEndOverlays[i]), isAnimated, config);
            }

            for (var i = 0; i < open.Count; i++)
            {
                OverlayObject(open[i]).transform.SetAsLastSibling();
            }
        }

        /// <summary>Reads each overlay's motion and each page's group once; <see cref="MissingSetup"/> reports one missing.</summary>
        private void CacheParts()
        {
            if (motions != null)
            {
                return;
            }

            motions = new OverlayMotion[frontEndOverlays.Length];
            for (var i = 0; i < frontEndOverlays.Length; i++)
            {
                motions[i] = OverlayObject(frontEndOverlays[i]).GetComponent<OverlayMotion>();
            }

            storeGroup = store.GetComponent<CanvasGroup>();
            homeGroup = home.GetComponent<CanvasGroup>();
            leaderboardGroup = leaderboard.GetComponent<CanvasGroup>();
        }

        /// <summary>
        /// Shows <paramref name="tab"/>'s page (<see cref="TabPages"/>): a fade
        /// already bringing it in runs on; any other fade is finished first;
        /// then, with <paramref name="isAnimated"/>, the page fades in over the
        /// one shown, drawn above it, or is shown at once.
        /// </summary>
        private void ApplyPages(UiTab tab, bool isAnimated)
        {
            if (pages.IsFading && isAnimated && pages.Incoming == tab)
            {
                return;
            }

            EndPageFade();
            var fades = pages.Select(tab, isAnimated);
            ShowActivePages();
            if (!fades)
            {
                return;
            }

            var page = PageGroup(tab);
            page.transform.SetAsLastSibling();
            page.alpha = 0f;
            DOTween.To(() => page.alpha, alpha => page.alpha = alpha, 1f, config.TabFadeSeconds)
                .SetEase(config.TabFadeEase)
                .SetUpdate(true)
                .SetId(this)
                .OnComplete(EndPageFade);
        }

        /// <summary>Finishes a page fade at once, if any: the incoming page fully shown, the old one gone. Parts may be missing during teardown.</summary>
        private void EndPageFade()
        {
            DOTween.Kill(this);
            if (!pages.IsFading)
            {
                return;
            }

            pages.Finish();
            RestPageAlphas();
            ShowActivePages();
        }

        /// <summary>Every page opaque. The groups may be missing during teardown.</summary>
        private void RestPageAlphas()
        {
            if (storeGroup != null)
            {
                storeGroup.alpha = 1f;
            }

            if (homeGroup != null)
            {
                homeGroup.alpha = 1f;
            }

            if (leaderboardGroup != null)
            {
                leaderboardGroup.alpha = 1f;
            }
        }

        private void ShowActivePages()
        {
            store.gameObject.SetActive(pages.IsActive(UiTab.Store));
            home.gameObject.SetActive(pages.IsActive(UiTab.Home));
            leaderboard.gameObject.SetActive(pages.IsActive(UiTab.Leaderboard));
        }

        private CanvasGroup PageGroup(UiTab tab)
        {
            switch (tab)
            {
                case UiTab.Store:
                    return storeGroup;
                case UiTab.Home:
                    return homeGroup;
                case UiTab.Leaderboard:
                    return leaderboardGroup;
                default:
                    throw new ArgumentOutOfRangeException(nameof(tab), tab, "Not a front end tab.");
            }
        }

        /// <summary>Adds a page with no CanvasGroup on its root, which its fade needs, to <paramref name="missing"/>.</summary>
        private static void AddPageGroupMissing(List<string> missing, Component page, string field)
        {
            if (page != null && page.GetComponent<CanvasGroup>() == null)
            {
                missing.Add($"a CanvasGroup on the root of '{field}', for its fade");
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
                case UiOverlay.WinStreak:
                    return winStreak;
                case UiOverlay.StreakReward:
                    return streakReward;
                case UiOverlay.Profile:
                    return profile;
                case UiOverlay.EndOfContent:
                    return endOfContent;
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
            if (isSubscribed || store == null || home == null || leaderboard == null || topBar == null || tabBar == null
                || settings == null || levels == null || lives == null
                || notEnoughGold == null || message == null || testPurchase == null
                || winStreak == null || streakReward == null || profile == null || endOfContent == null || streakBar == null
                || coinFlight == null)
            {
                return;
            }

            coinFlight.CoinLanded += OnCoinLanded;
            navigation.Changed += OnNavigationChanged;
            home.PlayRequested += OnPlayRequested;
            store.GoldPackRequested += OnGoldPackRequested;
            store.JokerPurchaseRequested += OnJokerPurchaseRequested;
            store.RefillRequested += OnRefillRequested;
            leaderboard.RetryRequested += OnLeaderboardRetryRequested;
            topBar.StoreRequested += OnStoreRequested;
            topBar.LivesRequested += OnLivesRequested;
            topBar.SettingsRequested += OnSettingsRequested;
            topBar.ProfileRequested += OnProfileRequested;
            tabBar.TabSelected += OnTabSelected;
            settings.CloseRequested += OnSettingsCloseRequested;
            settings.SoundToggleRequested += OnSoundToggleRequested;
            settings.HapticsToggleRequested += OnHapticsToggleRequested;
            settings.LevelsRequested += OnLevelsRequested;
            settings.PrivacyRequested += OnPrivacyRequested;
            levels.CloseRequested += OnLevelsCloseRequested;
            levels.LevelRequested += OnLevelRequested;
            lives.CloseRequested += OnLivesCloseRequested;
            lives.RefillRequested += OnRefillRequested;
            notEnoughGold.CloseRequested += OnNotEnoughGoldCloseRequested;
            notEnoughGold.GetGoldRequested += OnGetGoldRequested;
            message.CloseRequested += OnMessageCloseRequested;
            testPurchase.CloseRequested += OnTestPurchaseCloseRequested;
            testPurchase.BuyRequested += OnTestPurchaseBuyRequested;
            streakBar.Pressed += OnStreakBarPressed;
            winStreak.CloseRequested += OnWinStreakCloseRequested;
            streakReward.CollectRequested += OnStreakRewardCollectRequested;
            profile.CloseRequested += OnProfileCloseRequested;
            profile.NameChangeRequested += OnNameChangeRequested;
            profile.AvatarChangeRequested += OnAvatarChangeRequested;
            endOfContent.CloseRequested += OnEndOfContentCloseRequested;
            endOfContent.RestartRequested += OnEndOfContentRestartRequested;
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
            navigation.Changed -= OnNavigationChanged;
            home.PlayRequested -= OnPlayRequested;
            store.GoldPackRequested -= OnGoldPackRequested;
            store.JokerPurchaseRequested -= OnJokerPurchaseRequested;
            store.RefillRequested -= OnRefillRequested;
            leaderboard.RetryRequested -= OnLeaderboardRetryRequested;
            topBar.StoreRequested -= OnStoreRequested;
            topBar.LivesRequested -= OnLivesRequested;
            topBar.SettingsRequested -= OnSettingsRequested;
            topBar.ProfileRequested -= OnProfileRequested;
            tabBar.TabSelected -= OnTabSelected;
            settings.CloseRequested -= OnSettingsCloseRequested;
            settings.SoundToggleRequested -= OnSoundToggleRequested;
            settings.HapticsToggleRequested -= OnHapticsToggleRequested;
            settings.LevelsRequested -= OnLevelsRequested;
            settings.PrivacyRequested -= OnPrivacyRequested;
            levels.CloseRequested -= OnLevelsCloseRequested;
            levels.LevelRequested -= OnLevelRequested;
            lives.CloseRequested -= OnLivesCloseRequested;
            lives.RefillRequested -= OnRefillRequested;
            notEnoughGold.CloseRequested -= OnNotEnoughGoldCloseRequested;
            notEnoughGold.GetGoldRequested -= OnGetGoldRequested;
            message.CloseRequested -= OnMessageCloseRequested;
            testPurchase.CloseRequested -= OnTestPurchaseCloseRequested;
            testPurchase.BuyRequested -= OnTestPurchaseBuyRequested;
            streakBar.Pressed -= OnStreakBarPressed;
            winStreak.CloseRequested -= OnWinStreakCloseRequested;
            streakReward.CollectRequested -= OnStreakRewardCollectRequested;
            profile.CloseRequested -= OnProfileCloseRequested;
            profile.NameChangeRequested -= OnNameChangeRequested;
            profile.AvatarChangeRequested -= OnAvatarChangeRequested;
            endOfContent.CloseRequested -= OnEndOfContentCloseRequested;
            endOfContent.RestartRequested -= OnEndOfContentRestartRequested;
            isSubscribed = false;
        }

        /// <summary>
        /// Shows where the navigation now is. A test purchase sheet that was
        /// closed by anything but Buy — Cancel, its close button, Back, a tab —
        /// is answered no here, the one place every way of closing it passes.
        /// Arriving on the Leaderboard asks for a fresh table, after the page is
        /// shown, so a result that comes back at once lands on a laid-out page.
        /// </summary>
        private void OnNavigationChanged()
        {
            ApplyNavigation(true);
            if (navigation.Tab != shownTab)
            {
                shownTab = navigation.Tab;
                EndGoldFlight();
                if (shownTab == UiTab.Leaderboard)
                {
                    RequestLeaderboard();
                }
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

        /// <summary>Shows Loading on the Leaderboard page and asks the bootstrap for a table.</summary>
        private void RequestLeaderboard()
        {
            leaderboard.ShowLoading(config);
            LeaderboardRequested?.Invoke();
        }

        private void OnLeaderboardRetryRequested()
        {
            RequestLeaderboard();
            ButtonPressed?.Invoke();
        }

        private void OnCoinLanded() => CoinLanded?.Invoke();

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

        /// <summary>The Settings overlay's Privacy row: the privacy text in the tall message prompt, over Settings; OK returns to it.</summary>
        private void OnPrivacyRequested()
        {
            message.ShowLong(config.PrivacyTitle, config.PrivacyText);
            navigation.Open(UiOverlay.Message);
            ButtonPressed?.Invoke();
        }

        private void OnEndOfContentCloseRequested() => OnCloseClicked(UiOverlay.EndOfContent);

        /// <summary>The overlay stays open under the screen fade into level 1; the next <see cref="Show"/> closes it.</summary>
        private void OnEndOfContentRestartRequested()
        {
            EndOfContentRestartRequested?.Invoke();
            ButtonPressed?.Invoke();
        }

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

        private void OnStreakBarPressed()
        {
            navigation.Open(UiOverlay.WinStreak);
            ButtonPressed?.Invoke();
        }

        private void OnWinStreakCloseRequested() => OnCloseClicked(UiOverlay.WinStreak);

        private void OnStreakRewardCollectRequested() => OnCloseClicked(UiOverlay.StreakReward);

        /// <summary>The top bar's avatar: opens Profile with its refusal line cleared.</summary>
        private void OnProfileRequested()
        {
            profile.ClearRefusal();
            navigation.Open(UiOverlay.Profile);
            ButtonPressed?.Invoke();
        }

        private void OnProfileCloseRequested() => OnCloseClicked(UiOverlay.Profile);

        private void OnHapticsToggleRequested()
        {
            HapticsToggleRequested?.Invoke();
            ButtonPressed?.Invoke();
        }

        /// <summary>A finished name edit is no button press, so it sounds nothing.</summary>
        private void OnNameChangeRequested(string raw)
        {
            NameChangeRequested?.Invoke(raw);
        }

        private void OnAvatarChangeRequested(string avatarId)
        {
            AvatarChangeRequested?.Invoke(avatarId);
            ButtonPressed?.Invoke();
        }

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
