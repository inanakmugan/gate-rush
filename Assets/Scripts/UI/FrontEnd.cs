using System;
using System.Collections.Generic;
using GateRush.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GateRush.UI
{
    /// <summary>
    /// The front end (Module 35, D68): three tabs — Store, Home and
    /// Leaderboard — under a top bar and over a tab bar, and a stack of
    /// overlays — Settings, Levels and Lives — over them. Implements
    /// <see cref="IFrontEnd"/>, the one way the bootstrap shows and hears it.
    /// </summary>
    /// <remarks>
    /// <para><b>No rules.</b> It shows the <see cref="FrontEndState"/> it is
    /// given and raises an event for every request the bootstrap decides on;
    /// moving between tabs and overlays (<see cref="UiNavigation"/>) is all it
    /// decides itself.</para>
    /// <para><b>Parts.</b> The screens are prefabs laid out in the Editor;
    /// code finds their parts only through the serialized fields here and on
    /// the views (<see cref="TopBar"/>, <see cref="TabBar"/>,
    /// <see cref="HomeScreen"/>, the overlays). <see cref="MissingSetup"/>
    /// names whatever is not assigned.</para>
    /// <para><b>Open and closed.</b> <see cref="Show"/> and
    /// <see cref="Hide"/> switch this component's own GameObject, so nothing
    /// here runs, Back included, while it is closed. Nothing depends on the
    /// order in which this and the bootstrap wake up. The overlays share one
    /// parent and are stacked by sibling order, newest last.</para>
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
        [SerializeField] private GameObject storePage;

        [Tooltip("The Home tab's page.")]
        [SerializeField] private HomeScreen home;

        [Tooltip("The Leaderboard tab's page.")]
        [SerializeField] private GameObject leaderboardPage;

        [SerializeField] private TopBar topBar;
        [SerializeField] private TabBar tabBar;

        [Tooltip("The three overlays must share one parent: they are stacked by sibling order.")]
        [SerializeField] private SettingsOverlay settings;

        [SerializeField] private LevelsOverlay levels;
        [SerializeField] private LivesOverlay lives;

        private readonly UiNavigation navigation = new UiNavigation();

        // The tab the notice belongs to: the notice goes when the tab changes.
        private UiTab shownTab = UiTab.Home;
        private bool isSubscribed;

        /// <inheritdoc />
        public event Action PlayRequested;

        /// <inheritdoc />
        public event Action<string> LevelRequested;

        /// <inheritdoc />
        public event Action SoundToggleRequested;

        /// <inheritdoc />
        public event Action RefillRequested;

        /// <inheritdoc />
        public event Action ButtonPressed;

        /// <inheritdoc />
        public bool IsOpen => gameObject.activeSelf;

        /// <inheritdoc />
        public string MissingSetup()
        {
            var missing = new List<string>();
            UiParts.AddIfMissing(missing, config, nameof(FrontEnd), nameof(config));
            UiParts.AddIfMissing(missing, storePage, nameof(FrontEnd), nameof(storePage));
            UiParts.AddIfMissing(missing, home, nameof(FrontEnd), nameof(home));
            UiParts.AddIfMissing(missing, leaderboardPage, nameof(FrontEnd), nameof(leaderboardPage));
            UiParts.AddIfMissing(missing, topBar, nameof(FrontEnd), nameof(topBar));
            UiParts.AddIfMissing(missing, tabBar, nameof(FrontEnd), nameof(tabBar));
            UiParts.AddIfMissing(missing, settings, nameof(FrontEnd), nameof(settings));
            UiParts.AddIfMissing(missing, levels, nameof(FrontEnd), nameof(levels));
            UiParts.AddIfMissing(missing, lives, nameof(FrontEnd), nameof(lives));

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

            if (settings != null && levels != null && lives != null
                && (settings.transform.parent != levels.transform.parent || settings.transform.parent != lives.transform.parent))
            {
                missing.Add("the Settings, Levels and Lives overlays must share one parent, which stacks them");
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
        public void Hide()
        {
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

        /// <summary>
        /// Shows the selected tab's page and the tab bar's selection, and the
        /// open overlays alone, stacked by sibling order with the newest last.
        /// </summary>
        private void ApplyNavigation()
        {
            var tab = navigation.Tab;
            storePage.SetActive(tab == UiTab.Store);
            home.gameObject.SetActive(tab == UiTab.Home);
            leaderboardPage.SetActive(tab == UiTab.Leaderboard);
            tabBar.ShowSelected(tab);

            var open = navigation.Overlays;
            settings.gameObject.SetActive(Contains(open, UiOverlay.Settings));
            levels.gameObject.SetActive(Contains(open, UiOverlay.Levels));
            lives.gameObject.SetActive(Contains(open, UiOverlay.Lives));
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
            if (isSubscribed || home == null || topBar == null || tabBar == null
                || settings == null || levels == null || lives == null)
            {
                return;
            }

            navigation.Changed += OnNavigationChanged;
            home.PlayRequested += OnPlayRequested;
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
            isSubscribed = false;
        }

        private void OnNavigationChanged()
        {
            ApplyNavigation();
            if (navigation.Tab != shownTab)
            {
                shownTab = navigation.Tab;
                home.ClearNotice();
            }
        }

        /// <summary>
        /// A close button: closes its overlay, which is the top one — the
        /// overlays above it cover it — and nothing else if it is not.
        /// </summary>
        private void CloseIfOnTop(UiOverlay overlay)
        {
            var open = navigation.Overlays;
            if (open.Count > 0 && open[open.Count - 1] == overlay)
            {
                navigation.Back();
            }

            ButtonPressed?.Invoke();
        }

        private void OnPlayRequested()
        {
            PlayRequested?.Invoke();
            ButtonPressed?.Invoke();
        }

        private void OnStoreRequested()
        {
            navigation.SelectTab(UiTab.Store);
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
            navigation.SelectTab(tab);
            ButtonPressed?.Invoke();
        }

        private void OnSettingsCloseRequested() => CloseIfOnTop(UiOverlay.Settings);

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

        private void OnLevelsCloseRequested() => CloseIfOnTop(UiOverlay.Levels);

        private void OnLevelRequested(string levelName)
        {
            LevelRequested?.Invoke(levelName);
            ButtonPressed?.Invoke();
        }

        private void OnLivesCloseRequested() => CloseIfOnTop(UiOverlay.Lives);

        private void OnRefillRequested()
        {
            RefillRequested?.Invoke();
            ButtonPressed?.Invoke();
        }
    }
}
