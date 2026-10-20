using System;
using System.Collections.Generic;
using GateRush.Core;
using GateRush.Meta;
using GateRush.Platform;
using GateRush.Serialization;
using UnityEngine;

namespace GateRush.Runtime
{
    /// <summary>
    /// Opens the game on the front end and builds the level it picks from its
    /// JSON <see cref="TextAsset"/>: the <see cref="LevelSession"/> and its
    /// <see cref="LevelRun"/> with the countdown, the plain helpers they need,
    /// and the views that draw them. Runs the countdown, feeds the HUD, shows
    /// the result panel, and handles Restart, Next and Menu. Holds all
    /// per-play state on this instance — Enter Play Mode runs without a domain
    /// reload, so nothing here is static.
    /// </summary>
    /// <remarks>
    /// <para><b>Front end (Module 35, D68).</b> A plain Play and the player
    /// build open on the front end's Home (<see cref="IFrontEnd"/>) with no
    /// level loaded: no board, no countdown, the level's interface hidden and
    /// the input controller unbound. The front end is a <c>GateRush.UI</c> component
    /// held as a <see cref="MonoBehaviour"/>, so this assembly never
    /// references the screens; it shows the <see cref="FrontEndState"/> built
    /// here and raises events, and every decision stays here. Menu, from the
    /// HUD or the result panel, abandons the level and opens Home
    /// (<see cref="GoToMenu"/>). A level that is won is recorded in
    /// <see cref="CompletedLevels"/>, and the whole save is saved through
    /// <see cref="SaveRepository"/> at once.</para>
    /// <para><b>Campaign and free play (Module 32, D57, D58, D63, D64).</b>
    /// Where a level starts decides its <see cref="PlayMode"/>, kept with the
    /// run; Restart keeps it. Play and Next after a campaign win start the
    /// campaign level, the one at <see cref="MetaGame.CampaignPosition"/>
    /// (<see cref="CampaignLevels.TryGetLevel"/>), and only with a life left;
    /// with none, the front end's Lives overlay opens. The level grid, Next
    /// after a free-play win and every development tool start free play.
    /// This is the only caller of <see cref="MetaGame"/>: a campaign win is
    /// reported where it is decided, a campaign loss when the countdown runs
    /// out or the player confirms leaving or restarting a level being
    /// played, and the Store's purchases and the refill; every call that changed
    /// the save is followed by one <see cref="SaveOrWarn"/>, a loss's even
    /// when no life was left to lose. Leaving or restarting a campaign level
    /// being played asks first (<see cref="ILevelUi.ShowConfirm"/>), naming
    /// the win streak that goes with the life (Module 41), with the
    /// countdown stopped and the board and the HUD's buttons held. A
    /// campaign win's panel has no Restart: a won campaign level is never
    /// replayed. The level's interface and the front end are refreshed only
    /// when what they show has changed.
    /// With no usable level order the level grid is empty and Play starts
    /// <see cref="level"/> as free play.</para>
    /// <para>A level or config that cannot be used logs the reason and draws
    /// nothing. A level whose time budget is not positive logs an error and
    /// plays without a countdown, so an unfinished level stays testable.</para>
    /// <para><b>Next</b> follows <see cref="LevelCatalog"/> over every JSON in
    /// <c>Resources/Levels</c>, loaded once at start. A file there that fails
    /// to load is logged and left out; two files sharing a level id are
    /// logged and hide Next altogether. Neither stops the current level from
    /// playing. The HUD's level number comes from the same catalog; a level
    /// without one plays with no number shown.</para>
    /// <para><b>Introductions (Module 19).</b> The same pass over the level
    /// files records which mechanics each level contains
    /// (<see cref="LevelMechanics.Of"/>), in the catalog's order. A level that
    /// loads — first load or Next — shows a card for every mechanic no earlier
    /// level contains, and its countdown starts when the last card closes;
    /// until then input is held. A restart shows none. A level outside the
    /// level order, or any level while that order is unusable, shows none
    /// either.</para>
    /// <para><b>Development tools (Module 21).</b> In the editor, a level the
    /// editor tools asked for (<see cref="DevLevelOverride"/>) skips Home
    /// and is played at once, for one Play session. In the editor and in development builds, Page Down and Page Up
    /// (<c>DevKeys</c>) go to the next and previous level through the same
    /// load as Next. A release build has neither. Both play free play: no
    /// life needed or lost, no gold, even when leaving a campaign level
    /// (D57, D64).</para>
    /// <para><b>Sound (Module 25).</b> This is the only caller of
    /// <see cref="SoundPlayer"/>: the views raise events and the handlers
    /// here play the cues — a move's from <see cref="MoveCues"/> as each
    /// stage of its presentation starts, the timer's from
    /// <see cref="TimerTicks"/>. Voices are stopped on a restart, a level
    /// change and the way to the front end. The front end's sound toggle flips
    /// <see cref="SoundSetting"/> and its haptics toggle
    /// <see cref="HapticsSetting"/>, each on its own (Module 38), and the whole
    /// save is saved at once. With no Sound Player or Sound Config assigned, one error is
    /// logged and the game runs silently; the config's problems — a fresh
    /// one has no clips — are logged once, at start, and never stop the
    /// game.</para>
    /// <para><b>Save (Module 29, D56).</b> Everything kept is one
    /// <see cref="SaveData"/>, loaded once at start through
    /// <see cref="SaveRepository"/> over <see cref="PlayerPrefsSaveStore"/>
    /// and <see cref="JsonSaveCodec"/>. What the load could not read is
    /// logged as a warning and played at its default; a save carried over
    /// from the old keys is logged once. The whole save is saved when a
    /// level is newly completed and when the sound is toggled; a save that
    /// fails is logged and the change lasts until the game is closed. The
    /// repository is given the <see cref="MetaRules"/> of
    /// <see cref="MetaConfig"/> (Module 30), from which every default of the
    /// save is built (D60); with no Meta Config assigned, or one with
    /// problems, an error is logged and nothing is drawn, as for a missing
    /// Runtime Config.</para>
    /// <para><b>Camera fit.</b> The camera keeps free the HUD bands measured
    /// from the safe area's edges: each band plus the unsafe strip on its side
    /// (<see cref="ScreenBands.TryAddInsets"/>). It is refitted when the
    /// screen's size or safe area changes, and the level's interface is given
    /// the same bands (<see cref="ILevelUi.PlaceBands"/>) at the same time.</para>
    /// <para><b>The level's interface (Modules 40 and 41, D74).</b> The HUD,
    /// the joker bar, the in-level overlays, the result panel and the
    /// confirmation are a <c>GateRush.UI</c> component held as a
    /// <see cref="MonoBehaviour"/> (<see cref="ILevelUi"/>), shown the
    /// <see cref="LevelUiState"/> and the <see cref="LevelResult"/> built
    /// here. One query,
    /// <see cref="LevelUiStateAt"/>, draws the joker buttons and decides
    /// what a press does. The level stops — the drag cancelled, the
    /// countdown stopped, the board and the HUD's buttons held — while the
    /// confirmation or any in-level overlay is open (<see cref="IsPaused"/>),
    /// and goes on when the last of them closes (<see cref="ResumeIfFree"/>).</para>
    /// <para><b>Jokers (Module 34, D65, D66, D67, D74).</b> The joker bar
    /// shows with the HUD. The Clock's button uses it at once; the Rocket's
    /// and the Broom's select it (<see cref="JokerSelection"/>), and the next
    /// valid tap on a block uses it. In the campaign, a joker with none held
    /// and a price above the balance opens Not Enough Gold over the level
    /// instead, whatever its kind; Get Gold leads to the gold packs there,
    /// and nothing is bought or used for the player on return. Every use runs in one order: check, spend
    /// (<see cref="MetaGame.TryUseJoker"/> in the campaign, the level's
    /// <see cref="FreePlayJokers"/> in free play), apply, save in the
    /// campaign, then present — a joker through the same
    /// <see cref="PresentStateChange"/> as a move. An invalid tap does
    /// nothing and keeps the selection. Menu, Restart, the confirmation, the
    /// end of the level and a new level clear it. The free-play allowance is
    /// full again on every load and restart.</para>
    /// <para><b>Store (Module 36, D62, D72).</b> Gold packs are bought through
    /// an <see cref="IPurchaseService"/>, the
    /// <see cref="SimulatedPurchaseService"/> over
    /// <see cref="SimulatedStoreConfig"/>, whose confirmations are passed to
    /// the test purchase sheet of the front end or the level's interface,
    /// whichever is open; a pack that succeeds is reported
    /// (<see cref="MetaGame.ReportGoldPurchase"/>) and saved. Jokers and the
    /// refill are bought for gold here too; a purchase refused for want of
    /// gold opens the Not Enough Gold prompt with the shortfall worked out
    /// here. Every purchase leaves the player where they were. A pack id
    /// known to the rules or to the service but not both is logged as an
    /// error at start, and the Store shows only the packs both know.</para>
    /// <para><b>Profile (Module 38, D71).</b> A name typed on Profile goes
    /// through the name rule here (<see cref="PlayerProfile.TrySetName"/>):
    /// refused, the front end shows its refusal line and the saved name;
    /// accepted and changed, it is saved. A picked avatar is saved at once.
    /// The Meta Config's default avatar is checked against the
    /// <see cref="AvatarCatalog"/> at start and a mismatch logged.</para>
    /// <para><b>Leaderboard (Module 39, D70, D73).</b> Rows come from an
    /// <see cref="ILeaderboardService"/>, the
    /// <see cref="SimulatedLeaderboardService"/> over
    /// <see cref="SimulatedLeaderboardConfig"/>'s demo players; their avatar
    /// ids are checked against the catalog at start and any it lacks logged.
    /// Each request of the front end fetches with the player's name, avatar
    /// and lifetime gold as they are then. A result that arrives after the
    /// front end closed is dropped; one that arrives after a newer request
    /// is dropped and fetched again, so the page never shows a table older
    /// than its last request.</para>
    /// </remarks>
    public sealed class LevelBootstrap : MonoBehaviour
    {
        /// <summary>
        /// The <c>Resources</c> folder the level order is read from. Public so
        /// the Play Level window (Module 21) reads the same folder.
        /// </summary>
        public const string LevelsResourcePath = "Levels";

        [Tooltip("The level Play starts, as free play, only when the level order is unusable (two files in Assets/Resources/Levels sharing a level id, or none); otherwise Play starts the campaign level and this is not read. The Level Editor's Play button and Gate Rush > Play Level… skip Home and start on their level for one Play session. In the editor and in development builds, Page Down goes to the next level and Page Up to the previous one, in level order, at any moment a level is played; at the last or first level the key only logs a line. A release build has neither. Every one of these development tools plays free play: no life needed or lost, no gold.")]
        [SerializeField] private TextAsset level;

        [SerializeField] private RuntimeConfig config;

        [Tooltip("The life cap, the minutes a life takes to come back, a new player's gold and jokers, the store's prices (the jokers and the lives refill), the win streak's reward table and the gold each gold pack gives: Assets/Config/MetaConfig.asset. The save cannot be read without them, so left empty, or with a problem, it logs an error and nothing is drawn.")]
        [SerializeField] private MetaConfig metaConfig;

        [Tooltip("What the simulated store sells: each gold pack's product id and the price its test sheet shows, charging nothing (Module 36): Assets/Config/SimulatedStoreConfig.asset. Left empty, or with a problem, it logs an error and nothing is drawn.")]
        [SerializeField] private SimulatedStoreConfig simulatedStore;

        [Tooltip("The avatars a player can choose (Module 38): Assets/Config/AvatarCatalog.asset, the one the Front End holds. Left empty, or with a problem, it logs an error and nothing is drawn; a Meta Config Default Avatar Id the catalog lacks is logged as an error and the game goes on, showing the catalog's first avatar.")]
        [SerializeField] private AvatarCatalog avatarCatalog;

        [Tooltip("The demo players the simulated leaderboard ranks the player among, labelled as demo players on the Leaderboard tab (Module 39): Assets/Config/SimulatedLeaderboardConfig.asset. Left empty, or with a problem, it logs an error and nothing is drawn; an Avatar Id the Avatar Catalog lacks is logged as an error and the game goes on, showing the catalog's first avatar.")]
        [SerializeField] private SimulatedLeaderboardConfig simulatedLeaderboard;

        [Tooltip("The orthographic camera that shows the board. It is placed so the Board View sits centred between the HUD bands, below and above the screen's unsafe edges, and re-fitted when the screen size or safe area changes.")]
        [SerializeField] private Camera boardCamera;

        [SerializeField] private BoardView boardView;

        [Tooltip("Draws the background gradient and vignette; stretched over the camera's view whenever the camera is fitted.")]
        [SerializeField] private BackgroundView backgroundView;
        [SerializeField] private InputController inputController;

        [Tooltip("The introduction card on the canvas: shown over the level's interface when a level introduces a mechanic.")]
        [SerializeField] private IntroductionCard introductionCard;

        [Tooltip("The front end (Module 35): drag the FrontEnd component of the Front End instance under the canvas — the component's header, not the GameObject. Home, its tabs and overlays, shown while no level is loaded. It must implement IFrontEnd and report no missing part; otherwise an error is logged and nothing is drawn.")]
        [SerializeField] private MonoBehaviour frontEnd;

        [Tooltip("The level's interface (Modules 40 and 41): drag the LevelUi component of the Level UI instance under the canvas — the component's header, not the GameObject. The HUD, the joker bar, the in-level overlays, the result panel and the confirmation, shown while a level is loaded. It must implement ILevelUi and report no missing part; otherwise an error is logged and nothing is drawn.")]
        [SerializeField] private MonoBehaviour levelUi;

        [Tooltip("Plays the sound cues and vibration pulses (Module 25). Left empty, the game runs silently and logs one error.")]
        [SerializeField] private SoundPlayer soundPlayer;

        [Tooltip("Each cue's clips and levels, and the vibration lengths: Assets/Config/SoundConfig.asset. Left empty, the game runs silently and logs one error.")]
        [SerializeField] private SoundConfig soundConfig;

        private readonly Dictionary<string, TextAsset> levelAssets = new Dictionary<string, TextAsset>();

        // What each level in the catalog contains, in the catalog's order:
        // entry i belongs to the level the catalog numbers i + 1. Empty while
        // there is no catalog.
        private IReadOnlyList<IReadOnlyCollection<LevelMechanic>> mechanicsInOrder =
            Array.Empty<IReadOnlyCollection<LevelMechanic>>();

        private bool isUsable;
        private LevelCatalog catalog;

        // The front end as the interface it must implement; set by
        // HasEverythingAssigned, and only read once that has passed.
        private IFrontEnd frontEndView;

        // The level's interface as the interface it must implement; set by
        // HasEverythingAssigned, and only read once that has passed.
        private ILevelUi levelUiView;

        // Where the save is kept, the save itself, the rules it was loaded
        // with, the clock and the meta game over them. All are made in Awake
        // and stay for the scene's life.
        private SaveRepository saves;
        private SaveData save;
        private MetaRules metaRules;
        private SystemTimeProvider clock;
        private MetaGame metaGame;

        // Sells the gold packs (D72); made in Awake with the save. Its
        // products and the rules' packs, matched once: what the Store offers.
        private IPurchaseService purchases;
        private IReadOnlyList<GoldOffer> goldOffers;

        // Ranks the player for the Leaderboard tab (D73); made in Awake. True
        // while a request came in during a fetch: that fetch's result is then
        // dropped and fetched again.
        private ILeaderboardService leaderboard;
        private bool isLeaderboardAskedAgain;

        // How the level being played was started (D58); kept by a restart.
        private PlayMode mode;

        // What the open confirmation will do once a life is lost.
        private ConfirmKind pendingConfirm;

        // The gold the campaign win just reported paid, for its panel; null
        // after a free-play win, or when the win could not be reported.
        private int? campaignGoldEarned;

        // The streak rewards campaign wins gave that Home has not announced
        // yet, oldest first, and the streak of the latest of those wins
        // (Module 37). Kept across levels, since Next goes straight on to the
        // next level, and emptied only when Home announces them; never saved.
        private readonly List<StreakReward> unannouncedRewards = new List<StreakReward>();
        private int latestRewardedStreak;

        private bool hasLoggedPlayFallback;

        // What the meta lines last showed, so they are rebuilt only when it
        // changes (RefreshMeta). The wait is in whole seconds, -1 when the
        // lives are full or not shown.
        private bool hasShownMeta;
        private int shownLives;
        private int shownWaitSeconds;
        private int shownGold;

        // True once the sound player has been initialised; false leaves the
        // game silent (no Sound Player or no Sound Config assigned).
        private bool hasSound;

        // Null while the front end shows: no level is loaded.
        private LevelRun run;

        // The session whose time bonuses the HUD shows; null while not
        // listening, so a subscription is never made twice or left behind.
        private LevelSession listenedSession;

        // The Rocket or Broom selected and not yet used (D67); never saved.
        private readonly JokerSelection jokerSelection = new JokerSelection();

        // What the level's interface last showed, so it is refreshed only when
        // that changes (RefreshLevelUi).
        private bool hasShownLevelUi;
        private LevelUiState shownLevelUi;

        // The level being played's number in the level order; null for a
        // level outside it (ShowLevelNumber).
        private int? levelNumber;

        // Free play's jokers for the level being played (D66), made in Awake
        // and refilled on every load and restart.
        private FreePlayJokers freePlayJokers;

        // What the player sees of the level being played: shared with the
        // board view, and what decides the Broom's colour on a tap.
        private VisibilityLayer visibility;

        // Moves and jokers applied in this attempt at the level; seeds the bursts.
        private int moveNumber;
        private BoardLayout layout;
        private string levelName;
        private int fittedScreenWidth;
        private int fittedScreenHeight;
        private Rect fittedSafeArea;
        private bool hasReportedInsetFallback;

        private void Awake()
        {
            // Taken before anything can return: taking clears the override, so
            // even a scene that draws nothing leaves the next plain Play a
            // plain Play, opening on Home.
            var hasOverride = DevLevelOverride.TryTake(out var overrideName);

            if (!HasEverythingAssigned())
            {
                return;
            }

            isUsable = true;
            backgroundView.Initialize(config);

            // The card puts what it builds last among the canvas's children.
            // The front end and the level's interface, prefab instances under
            // the canvas, stay where the scene put them, before it, so the
            // card draws over the HUD, the joker bar and the level's panels,
            // none of which can be open together with it.
            introductionCard.Initialize(config);

            boardCamera.orthographic = true;
            boardCamera.clearFlags = CameraClearFlags.SolidColor;
            boardCamera.backgroundColor = config.BackgroundBottom;

            BuildCatalog();
            metaRules = metaConfig.ToRules();
            saves = new SaveRepository(new PlayerPrefsSaveStore(), new JsonSaveCodec(), metaRules);
            LoadSave();
            clock = new SystemTimeProvider();
            metaGame = new MetaGame(save, metaRules, clock);
            CreatePurchases();
            CheckDefaultAvatar();
            CreateLeaderboard();
            freePlayJokers = new FreePlayJokers(metaConfig.FreePlayJokersPerLevel);
            InitializeSound();
            ApplySoundSetting();

            if (!TryGetOverrideLevel(hasOverride, overrideName, out var overrideLevel) || !Load(overrideLevel, PlayMode.FreePlay))
            {
                ShowHome();
            }
        }

        /// <summary>
        /// The level the editor tools asked this Play session to start on
        /// (<see cref="DevLevelOverride"/>, Module 21), when it is a level file
        /// that loads: it is played at once, skipping Home. False for a
        /// plain Play; an override naming no such file is reported and
        /// ignored, which also leaves a plain Play. Outside the editor there
        /// is never an override.
        /// </summary>
        private bool TryGetOverrideLevel(bool hasOverride, string overrideName, out TextAsset asset)
        {
#if UNITY_EDITOR
            if (hasOverride)
            {
                if (DevLevelOverride.TryResolve(overrideName, levelAssets.Keys, out var warning))
                {
                    asset = levelAssets[overrideName];
                    return true;
                }

                Debug.LogWarning(
                    $"{warning} It must be a level in Resources/{LevelsResourcePath}; Home opens instead.",
                    this);
            }
#endif
            asset = null;
            return false;
        }

        /// <summary>
        /// Loads the save, once. What could not be read or written is logged
        /// as a warning, each problem naming its key and the default used — it
        /// never stops the game. A save read from the old keys is logged once,
        /// in words that stay true when writing it under the new key failed.
        /// </summary>
        private void LoadSave()
        {
            var load = saves.Load();
            save = load.Data;

            foreach (var problem in load.Problems)
            {
                Debug.LogWarning(problem, this);
            }

            if (load.Outcome == SaveLoadOutcome.CarriedOver)
            {
                Debug.Log(
                    $"The save was read from the old keys '{CompletedLevels.LegacySaveKey}' and '{SoundSetting.LegacySaveKey}'; they are left as they are.",
                    this);
            }
        }

        /// <summary>
        /// Gives the sound player its config and the platform's haptics, and
        /// logs what is wrong with the config — once, here, when the game
        /// starts. A config with problems still plays what it can: a cue with
        /// no clip is silent. With no Sound Player or no Sound Config
        /// assigned, one error is logged and the whole game is silent.
        /// </summary>
        private void InitializeSound()
        {
            if (soundPlayer == null || soundConfig == null)
            {
                Debug.LogError(
                    "LevelBootstrap: Sound Player and Sound Config must both be assigned for sound and vibration; the game runs silently.",
                    this);
                return;
            }

            foreach (var problem in soundConfig.Problems())
            {
                Debug.LogWarning($"{problem} The game runs on; what cannot be played is silent.", soundConfig);
            }

            soundPlayer.Initialize(soundConfig, new WebHaptics());
            hasSound = true;
        }

        /// <summary>
        /// Makes the sound player follow the save's sound and haptics
        /// settings, each on its own (Module 38). The front end's toggles read
        /// them from <see cref="FrontEndState.IsSoundOn"/> and
        /// <see cref="FrontEndState.IsHapticsOn"/>.
        /// </summary>
        private void ApplySoundSetting()
        {
            if (hasSound)
            {
                soundPlayer.IsOn = save.Sound.IsOn;
                soundPlayer.IsHapticsOn = save.Haptics.IsOn;
            }
        }

        /// <summary>
        /// The front end's sound toggle: turns sound off or on, leaving
        /// vibration as it is, saves the choice at once and shows it on the
        /// toggle. Turning off stops what is playing; turning on affects the
        /// next cue. A save that fails is logged and the game goes on; the
        /// choice then lasts for this session.
        /// </summary>
        private void OnSoundToggleRequested()
        {
            save.Sound.IsOn = !save.Sound.IsOn;
            ApplySoundSetting();
            SaveOrWarn("The sound setting was changed, but saving it failed; it is remembered until the game is closed.");
            ForgetShownMeta();
            RefreshMeta();
        }

        /// <summary>
        /// The front end's haptics toggle (Module 38): turns vibration off or
        /// on, leaving sound as it is, saves the choice at once and shows it on
        /// the toggle. Where the browser cannot vibrate, the setting is kept and
        /// nothing vibrates (<see cref="WebHaptics"/>).
        /// </summary>
        private void OnHapticsToggleRequested()
        {
            save.Haptics.IsOn = !save.Haptics.IsOn;
            ApplySoundSetting();
            SaveOrWarn("The haptics setting was changed, but saving it failed; it is remembered until the game is closed.");
            ForgetShownMeta();
            RefreshMeta();
        }

        /// <summary>
        /// Profile's name field ended its edit with new text (Module 38). The
        /// name rule decides: a refused name leaves the saved one, which the
        /// front end puts back with its refusal line; an accepted one is saved
        /// when it differs from the saved name, and shown in its normalized
        /// form either way.
        /// </summary>
        private void OnNameChangeRequested(string raw)
        {
            var profile = save.Profile;
            var before = profile.Name;
            if (!profile.TrySetName(raw))
            {
                frontEndView.ShowNameRefused();
                return;
            }

            if (!string.Equals(profile.Name, before, StringComparison.Ordinal))
            {
                SaveOrWarn("The name was changed, but saving it failed; it is remembered until the game is closed.");
            }

            ForgetShownMeta();
            RefreshMeta();
        }

        /// <summary>
        /// An avatar in Profile's grid (Module 38): chosen and saved at once
        /// when it differs from the saved one. The grid only offers ids of the
        /// catalog, which are never empty.
        /// </summary>
        private void OnAvatarChangeRequested(string avatarId)
        {
            var profile = save.Profile;
            if (!string.Equals(profile.AvatarId, avatarId, StringComparison.Ordinal))
            {
                profile.SetAvatar(avatarId);
                SaveOrWarn("The avatar was changed, but saving it failed; it is remembered until the game is closed.");
            }

            ForgetShownMeta();
            RefreshMeta();
        }

        /// <summary>
        /// Logs an error, once at start, when the Meta Config's default avatar
        /// is not in the Avatar Catalog, as mismatched gold packs are. The game
        /// goes on: a new player's avatar then shows the catalog's first.
        /// </summary>
        private void CheckDefaultAvatar()
        {
            if (!avatarCatalog.Contains(metaRules.DefaultAvatarId))
            {
                Debug.LogError(
                    $"{metaConfig.name}: Default Avatar Id '{metaRules.DefaultAvatarId}' is not in {avatarCatalog.name}; " +
                    $"a new player is shown '{avatarCatalog.ShownId(metaRules.DefaultAvatarId)}' instead.",
                    metaConfig);
            }
        }

        /// <summary>
        /// Makes the leaderboard service over the Simulated Leaderboard
        /// Config, whose problems <see cref="HasEverythingAssigned"/> has
        /// already refused, and logs an error for each demo player's avatar id
        /// the Avatar Catalog lacks, once at start. The game goes on: such a
        /// row shows the catalog's first avatar.
        /// </summary>
        private void CreateLeaderboard()
        {
            leaderboard = new SimulatedLeaderboardService(simulatedLeaderboard.Players());
            foreach (var problem in simulatedLeaderboard.MissingAvatars(avatarCatalog))
            {
                Debug.LogError(problem, simulatedLeaderboard);
            }
        }

        /// <summary>
        /// The Leaderboard tab was selected, or its Retry pressed: fetches a
        /// fresh result. While a fetch is pending, the request is kept and
        /// fetched once that one returns.
        /// </summary>
        private void OnLeaderboardRequested()
        {
            if (leaderboard.IsBusy)
            {
                isLeaderboardAskedAgain = true;
                return;
            }

            FetchLeaderboard();
        }

        /// <summary>Fetches with the player's name, avatar and lifetime gold as the save holds them now (D22).</summary>
        private void FetchLeaderboard()
        {
            var profile = save.Profile;
            var player = new LeaderboardPlayer(profile.Name, profile.AvatarId, save.Wallet.LifetimeEarned);
            leaderboard.Fetch(player, OnLeaderboardFetched);
        }

        /// <summary>
        /// A fetch's answer: dropped when the front end has closed, since the
        /// next opening asks again; dropped and fetched again when a request
        /// came in while it was pending; otherwise shown. The service is no
        /// longer busy when this runs (<see cref="ILeaderboardService.IsBusy"/>).
        /// </summary>
        private void OnLeaderboardFetched(LeaderboardResult result)
        {
            if (!frontEndView.IsOpen)
            {
                isLeaderboardAskedAgain = false;
                return;
            }

            if (isLeaderboardAskedAgain)
            {
                isLeaderboardAskedAgain = false;
                FetchLeaderboard();
                return;
            }

            frontEndView.ShowLeaderboard(result);
        }

        /// <summary>
        /// Saves the whole save at once. A save that fails logs
        /// <paramref name="failureMessage"/>, which says what changed, with the
        /// store's reason, and the game goes on: the change lasts until the
        /// game is closed.
        /// </summary>
        private void SaveOrWarn(string failureMessage)
        {
            try
            {
                saves.Save(save);
            }
            catch (PlayerPrefsException e)
            {
                Debug.LogWarning($"{failureMessage} {e.Message}", this);
            }
        }

        /// <summary>
        /// Plays <paramref name="cue"/>, when there is a sound player to play
        /// it. A player that has been destroyed is left alone: while the
        /// scene is torn down, <see cref="InputController"/> cancels its drag
        /// as it is disabled and reports the drop, when the player's object
        /// may already be gone.
        /// </summary>
        private void PlayCue(SoundCue cue)
        {
            if (hasSound && soundPlayer != null)
            {
                soundPlayer.Play(cue);
            }
        }

        /// <summary>
        /// Vibrates for <paramref name="moment"/>, when there is a sound
        /// player to do it; a destroyed one is left alone, as in
        /// <see cref="PlayCue"/>.
        /// </summary>
        private void Pulse(HapticMoment moment)
        {
            if (hasSound && soundPlayer != null)
            {
                soundPlayer.Pulse(moment);
            }
        }

        /// <summary>
        /// Stops every sound that is playing: the level they belong to is
        /// restarted, replaced or left. A destroyed player is left alone, as
        /// in <see cref="PlayCue"/>.
        /// </summary>
        private void StopSounds()
        {
            if (hasSound && soundPlayer != null)
            {
                soundPlayer.StopVoices();
            }
        }

        /// <summary>The leave stage of a move's presentation has started: its cues play, and an exit vibrates.</summary>
        private void OnLeaveStarted(MoveChanges changes)
        {
            var cues = MoveCues.Leave(changes);
            for (var i = 0; i < cues.Count; i++)
            {
                PlayCue(cues[i]);
                if (cues[i] == SoundCue.Exit)
                {
                    Pulse(HapticMoment.Exit);
                }
            }
        }

        /// <summary>The arrive stage of a move's presentation has started: its cues play.</summary>
        private void OnArriveStarted(MoveChanges changes)
        {
            var cues = MoveCues.Arrive(changes);
            for (var i = 0; i < cues.Count; i++)
            {
                PlayCue(cues[i]);
            }
        }

        private void OnDragStarted()
        {
            PlayCue(SoundCue.Lift);
        }

        /// <summary>
        /// A drag ended with no move, the block back where it started. A drag
        /// that ended in a move is <see cref="OnMoveApplied"/>'s.
        /// </summary>
        private void OnDragDropped()
        {
            PlayCue(SoundCue.Drop);
        }

        private void OnButtonPressed()
        {
            PlayCue(SoundCue.UiTap);
        }

        /// <summary>
        /// Records the win of the level being played and saves at once — so a
        /// tab closed during the exit animation keeps it. Either way the level
        /// is marked completed when the level order holds it. A campaign win
        /// is also reported with the level's gold reward (D58, D63), and the
        /// mark and the gold go in one save; the streak rewards it gave are
        /// kept for Home to announce (Module 37). A free-play win saves only when
        /// the mark is news. A save that fails is logged and the game goes on;
        /// the win then lasts for this session.
        /// </summary>
        private void RecordWin()
        {
            var ctx = run.Session.Context;
            var isNewlyCompleted = catalog != null && catalog.TryGetNumber(levelName, out _)
                                   && save.Completed.MarkCompleted(ctx.LevelId);

            campaignGoldEarned = null;
            if (mode == PlayMode.Campaign)
            {
                try
                {
                    var win = metaGame.ReportCampaignWin(ctx.GoldReward);
                    campaignGoldEarned = win.GoldEarned;
                    KeepUnannouncedRewards(win);
                }
                catch (ArgumentOutOfRangeException e)
                {
                    // Nothing changed in the meta game; the mark is still saved.
                    Debug.LogError(
                        $"Level '{levelName}' has a gold reward of {ctx.GoldReward}, which is negative; its campaign win is not reported, so the campaign stays on it. {e.Message}",
                        this);
                }

                SaveOrWarn($"Level '{levelName}' was won in the campaign, but saving the win, its gold and the campaign's progress failed; they are remembered until the game is closed.");
                return;
            }

            if (isNewlyCompleted)
            {
                SaveOrWarn($"Level '{levelName}' is completed, but saving that failed; it is remembered until the game is closed.");
            }
        }

        /// <summary>
        /// Keeps the streak rewards <paramref name="win"/> gave, if any, for
        /// Home to announce (Module 37), with its streak as the latest
        /// rewarded one. The jokers are already in the inventory.
        /// </summary>
        private void KeepUnannouncedRewards(CampaignWin win)
        {
            if (win.RewardsGiven.Count == 0)
            {
                return;
            }

            unannouncedRewards.AddRange(win.RewardsGiven);
            latestRewardedStreak = win.Streak;
        }

        /// <summary>
        /// Opens the Streak Reward overlay with every reward kept since the
        /// last announcement, once; does nothing when none is kept. The list
        /// is emptied before the front end is called, so a call that throws
        /// never announces them twice. The front end must be open.
        /// </summary>
        private void AnnounceStreakRewards()
        {
            if (unannouncedRewards.Count == 0)
            {
                return;
            }

            var rewards = unannouncedRewards.ToArray();
            unannouncedRewards.Clear();
            frontEndView.ShowStreakReward(latestRewardedStreak, rewards);
        }

        /// <summary>
        /// Reports a campaign loss (D57) and saves after it, whether or not a
        /// life was left to lose: the loss resets the streak either way
        /// (Module 31).
        /// </summary>
        private void ReportCampaignLoss()
        {
            metaGame.ReportCampaignLoss();
            SaveOrWarn($"Level '{levelName}' was lost in the campaign, but saving the lost life failed; it is remembered until the game is closed.");
        }

        /// <summary>
        /// Shows the meta game again when what is shown has changed: during a
        /// level, the level's interface (<see cref="RefreshLevelUi"/>); with no
        /// level loaded, the front end's whole state, which a passing second
        /// changes only while a life is coming back — an open Lives overlay
        /// stays open as lives come back. Reads the clock once, and allocates
        /// nothing when nothing changed, so it runs every frame. Reading the
        /// lives settles them, which needs no save (<see cref="MetaGame"/>).
        /// </summary>
        private void RefreshMeta()
        {
            var nowUtc = clock.UtcNow;
            if (run != null)
            {
                RefreshLevelUi(nowUtc);
                return;
            }

            var meta = metaGame.Save;
            var gold = meta.Wallet.Balance;
            var count = meta.Lives.CountAt(nowUtc);
            var untilNext = meta.Lives.UntilNextLifeAt(nowUtc);
            var waitSeconds = untilNext.HasValue ? MetaText.WholeSeconds(untilNext.Value) : -1;
            if (hasShownMeta && count == shownLives && waitSeconds == shownWaitSeconds && gold == shownGold)
            {
                return;
            }

            hasShownMeta = true;
            shownLives = count;
            shownWaitSeconds = waitSeconds;
            shownGold = gold;
            if (frontEndView.IsOpen)
            {
                frontEndView.Refresh(FrontEndStateAt(nowUtc));
            }
        }

        /// <summary>
        /// Shows the level's interface again when its state at
        /// <paramref name="nowUtc"/> differs from what it last showed: so every
        /// use, purchase, selection, win, loss, restart, change of gold or
        /// lives, and card, confirmation or overlay opening or closing shows at
        /// once. Allocates nothing when nothing changed.
        /// </summary>
        private void RefreshLevelUi(DateTime nowUtc)
        {
            var state = LevelUiStateAt(nowUtc);
            if (hasShownLevelUi && state.Equals(shownLevelUi))
            {
                return;
            }

            hasShownLevelUi = true;
            shownLevelUi = state;
            levelUiView.Refresh(state);
        }

        /// <summary>
        /// What the level's interface shows at <paramref name="nowUtc"/>, for
        /// the level being played: the one query that draws the joker buttons
        /// and decides what a press on one does (<see cref="OnJokerPressed"/>).
        /// A joker is usable while the level is being played and nothing holds
        /// it; the Clock also needs a countdown.
        /// </summary>
        private LevelUiState LevelUiStateAt(DateTime nowUtc) =>
            LevelUiState.Of(
                mode, levelNumber, metaGame, metaRules, nowUtc, freePlayJokers, IsBeingPlayed() && !IsHeld(), run.Countdown != null,
                jokerSelection.Selected, goldOffers);

        /// <summary>Makes the next <see cref="RefreshMeta"/> show everything again, for a new screen, level or setting.</summary>
        private void ForgetShownMeta()
        {
            hasShownMeta = false;
            hasShownLevelUi = false;
        }

        /// <summary>
        /// What the front end shows at <paramref name="nowUtc"/>: the one place
        /// its state is built (<see cref="FrontEndState.Of"/>).
        /// </summary>
        private FrontEndState FrontEndStateAt(DateTime nowUtc) =>
            FrontEndState.Of(metaGame, metaRules, nowUtc, catalog, save.Sound.IsOn, purchases.Products);

        /// <summary>
        /// Opens Home (<see cref="OpenHome"/>) and announces the streak
        /// rewards won since Home was last shown (Module 37).
        /// </summary>
        private void ShowHome()
        {
            OpenHome();
            AnnounceStreakRewards();
        }

        /// <summary>
        /// Opens the front end on Home over the background, with its state read
        /// afresh: the gold, the lives, the campaign level, the win streak and
        /// the level grid with every level of the level order and its
        /// completed mark. With no usable level order the grid is empty.
        /// Announces nothing; <see cref="ShowHome"/> does.
        /// </summary>
        private void OpenHome()
        {
            levelUiView.Hide();
            frontEndView.Show(FrontEndStateAt(clock.UtcNow));
            ForgetShownMeta();
            RefreshMeta();

            fittedScreenWidth = 0;
            fittedScreenHeight = 0;
            FitBackgroundToScreen();
        }

        /// <summary>
        /// Abandons the level being played and opens Home (Module 35): what
        /// Menu on the HUD and on the result panel does. Reports nothing: the
        /// callers that cost a life have reported it. Does nothing while no
        /// level is loaded.
        /// </summary>
        private void GoToMenu()
        {
            if (LeaveLevel())
            {
                ShowHome();
            }
        }

        /// <summary>
        /// Abandons the level being played, leaving no level loaded. Nothing
        /// of the level is left to act afterwards: the drag is cancelled and
        /// input unbound; the run is released, so its countdown is stopped and
        /// neither a tick, a move nor a bonus reaches it; the board is cleared,
        /// which abandons a presentation in progress without reporting it
        /// done, blocks still passing through their gates included; every
        /// sound is stopped (Module 25); the card is hidden without its
        /// callback; the confirmation, the result panel and a time bonus are
        /// hidden with their tweens killed. False, with nothing done, when no
        /// level is loaded.
        /// </summary>
        private bool LeaveLevel()
        {
            if (run == null)
            {
                return false;
            }

            inputController.Unbind();
            ClearJokerSelection();
            StopSounds();
            StopListeningForTimeBonus();
            run.Release();
            run = null;
            levelName = null;
            moveNumber = 0;

            boardView.Clear();
            introductionCard.Hide();
            levelUiView.HideConfirm();
            levelUiView.HideResult();
            return true;
        }

        /// <summary>
        /// Play on Home: the campaign level (D58). With no usable
        /// level order — or no level in it — the scene's <see cref="level"/>
        /// as free play, logged once.
        /// </summary>
        private void OnPlayRequested()
        {
            if (catalog == null || catalog.Names.Count == 0)
            {
                if (!hasLoggedPlayFallback)
                {
                    hasLoggedPlayFallback = true;
                    Debug.LogWarning(
                        $"The level order is unusable, so Play starts the scene's level '{level.name}' as free play: no life is needed or lost and no gold is paid. Reported once.",
                        this);
                }

                Load(level, PlayMode.FreePlay);
                return;
            }

            StartCampaignLevel();
        }

        /// <summary>
        /// Starts the campaign level, the one at
        /// <see cref="MetaGame.CampaignPosition"/> in the level order — from
        /// Play, or from Next after a campaign win. Past the last level the
        /// campaign is over: Home's notice says so and nothing starts, lives
        /// or none. With no life left the Lives overlay opens instead
        /// (D57). Starting spends nothing.
        /// </summary>
        private void StartCampaignLevel()
        {
            if (!CampaignLevels.TryGetLevel(catalog, metaGame.CampaignPosition, out var campaignName))
            {
                // The notice selects Home, which closes every overlay, so the
                // streak rewards are announced after it.
                if (!frontEndView.IsOpen)
                {
                    LeaveLevel();
                    OpenHome();
                }

                frontEndView.ShowNotice(config.CampaignDoneNotice);
                AnnounceStreakRewards();
                return;
            }

            if (!metaGame.CanStartCampaignLevel())
            {
                ShowLivesOverlay();
                return;
            }

            // Every name in the catalog is a level that loaded, kept in
            // levelAssets under that name (BuildCatalog), so this cannot miss.
            Load(levelAssets[campaignName], PlayMode.Campaign);
        }

        /// <summary>
        /// Leaves any level being played, opens Home when it was not already
        /// open, and opens the Lives overlay over the current tab: the time to
        /// the next life, and the refill, priced from the rules and pressable
        /// whenever the lives are not full (D62, <see cref="FrontEndState.IsLivesFull"/>).
        /// It stays open as lives come back; the player closes it.
        /// </summary>
        private void ShowLivesOverlay()
        {
            if (LeaveLevel() || !frontEndView.IsOpen)
            {
                ShowHome();
            }

            frontEndView.ShowLives();
        }

        /// <summary>
        /// The refill button, on the Store or the Lives overlay (D62): refills
        /// the lives and saves, and the front end shows them full where it is
        /// — the Lives overlay stays open, the Store stays. Lives that came
        /// back meanwhile and are full cost nothing and show the same. Too
        /// little gold opens the Not Enough Gold prompt.
        /// </summary>
        private void OnRefillRequested()
        {
            switch (metaGame.TryRefillLives())
            {
                case PurchaseResult.Bought:
                    SaveOrWarn("The lives were refilled, but saving that failed; the refill is remembered until the game is closed.");
                    break;
                case PurchaseResult.AlreadyFull:
                    break;
                case PurchaseResult.NotEnoughGold:
                    ShowShortfall(metaRules.LivesRefillPrice);
                    break;
            }

            ForgetShownMeta();
            RefreshMeta();
        }

        /// <summary>
        /// A joker's buy button on the Store (D62): one bought and saved, and
        /// its card's count goes up where the player is; too little gold opens
        /// the Not Enough Gold prompt.
        /// </summary>
        private void OnJokerPurchaseRequested(JokerKind kind)
        {
            switch (metaGame.TryBuyJoker(kind))
            {
                case PurchaseResult.Bought:
                    SaveOrWarn($"A {kind} was bought, but saving that failed; it is remembered until the game is closed.");
                    ForgetShownMeta();
                    RefreshMeta();
                    break;
                case PurchaseResult.NotEnoughGold:
                    ShowShortfall(metaRules.JokerPrice(kind));
                    break;
                case PurchaseResult.AlreadyFull:
                    Debug.LogError($"Buying a {kind} answered AlreadyFull, which only a refill can; nothing changes.", this);
                    break;
            }
        }

        /// <summary>
        /// Opens the Not Enough Gold prompt for a purchase of
        /// <paramref name="price"/> that was refused for want of gold.
        /// </summary>
        private void ShowShortfall(int price)
        {
            // Called only after a NotEnoughGold, which Wallet.TrySpend gives
            // exactly when the price is above the balance: always positive.
            frontEndView.ShowNotEnoughGold(price - metaGame.Save.Wallet.Balance);
        }

        /// <summary>
        /// Makes the purchase service over the Simulated Store Config, whose
        /// confirmations go to the open test sheet, and matches its
        /// products with the rules' gold packs once. Every id known to one
        /// side only is logged as an error; the game goes on, and the Store
        /// shows the packs both know.
        /// </summary>
        private void CreatePurchases()
        {
            purchases = new SimulatedPurchaseService(simulatedStore.Products(), ConfirmOnTestSheet);
            goldOffers = GoldOffers.Match(metaRules.GoldPacks, purchases.Products);
            foreach (var problem in GoldOffers.Mismatches(metaRules.GoldPacks, purchases.Products))
            {
                Debug.LogError(problem, simulatedStore);
            }
        }

        /// <summary>
        /// The simulated service's confirmation: the test purchase sheet for
        /// <paramref name="product"/>'s offer, on the front end when it is
        /// open, otherwise on the level's interface when that is — the two are
        /// never open together. Answers no when neither is open, which no
        /// press can reach.
        /// </summary>
        private void ConfirmOnTestSheet(StoreProduct product, Action<bool> answer)
        {
            if (!TryGetOffer(product.Id, out var offer))
            {
                answer(false);
                return;
            }

            if (frontEndView.IsOpen)
            {
                frontEndView.ConfirmTestPurchase(offer, answer);
            }
            else if (levelUiView.IsOpen)
            {
                levelUiView.ConfirmTestPurchase(offer, answer);
            }
            else
            {
                answer(false);
            }
        }

        /// <summary>
        /// A gold pack's price button (D72), on the Store or in the level's
        /// gold packs: buys it through the purchase service. A press while a
        /// purchase waits for its outcome is ignored.
        /// </summary>
        private void OnGoldPackRequested(string productId)
        {
            if (purchases.IsBusy)
            {
                return;
            }

            // Only offered packs are bought, so ReportGoldPurchase never meets
            // an id the rules do not know.
            if (!TryGetOffer(productId, out _))
            {
                Debug.LogError($"A gold pack button asked for '{productId}', which is not on offer; nothing changes.", this);
                return;
            }

            purchases.Purchase(productId, outcome => OnGoldPurchaseDone(productId, outcome));
        }

        /// <summary>
        /// A gold pack's outcome: Succeeded adds its gold and saves, and the
        /// player stays where they are — in a level, the gold packs stay open
        /// and the joker is pressed again by the player (D74); Cancelled
        /// changes nothing; Failed opens the message saying that nothing was
        /// charged, on whichever of the front end and the level's interface is
        /// open.
        /// </summary>
        private void OnGoldPurchaseDone(string productId, PurchaseOutcome outcome)
        {
            switch (outcome)
            {
                case PurchaseOutcome.Succeeded:
                    // Paid for, so credited even if the front end has closed.
                    var gold = metaGame.ReportGoldPurchase(productId);
                    SaveOrWarn($"{gold} gold was bought, but saving it failed; it is kept until the game is closed.");
                    ForgetShownMeta();
                    RefreshMeta();
                    break;
                case PurchaseOutcome.Cancelled:
                    break;
                case PurchaseOutcome.Failed:
                    if (frontEndView.IsOpen)
                    {
                        frontEndView.ShowPurchaseFailed();
                    }
                    else if (levelUiView.IsOpen)
                    {
                        levelUiView.ShowPurchaseFailed();
                    }

                    break;
            }
        }

        private bool TryGetOffer(string productId, out GoldOffer offer)
        {
            for (var i = 0; i < goldOffers.Count; i++)
            {
                if (string.Equals(goldOffers[i].ProductId, productId, StringComparison.Ordinal))
                {
                    offer = goldOffers[i];
                    return true;
                }
            }

            offer = default;
            return false;
        }

        /// <summary>A level's button in the level grid: that level as free play, whatever is completed (D58).</summary>
        private void OnLevelRequested(string requestedName)
        {
            if (levelAssets.TryGetValue(requestedName, out var asset))
            {
                Load(asset, PlayMode.FreePlay);
                return;
            }

            Debug.LogError($"The level grid asked for '{requestedName}', which is not a level that loaded; nothing changes.", this);
        }

        private void OnEnable()
        {
            if (!isUsable)
            {
                return;
            }

            inputController.MoveApplied += OnMoveApplied;
            inputController.RestartRequested += OnRestartRequested;
            levelUiView.RestartRequested += OnRestartRequested;
            levelUiView.MenuRequested += OnMenuRequested;
            levelUiView.GoldPackRequested += OnGoldPackRequested;
            levelUiView.OverlaysClosed += OnLevelOverlaysClosed;
            levelUiView.ResultRestartRequested += OnRestartRequested;
            levelUiView.NextRequested += Next;
            levelUiView.ResultMenuRequested += GoToMenu;
            levelUiView.ConfirmLoseLife += OnConfirmed;
            levelUiView.ConfirmKeepPlaying += OnConfirmCancelled;
            frontEndView.PlayRequested += OnPlayRequested;
            frontEndView.LevelRequested += OnLevelRequested;
            frontEndView.SoundToggleRequested += OnSoundToggleRequested;
            frontEndView.HapticsToggleRequested += OnHapticsToggleRequested;
            frontEndView.NameChangeRequested += OnNameChangeRequested;
            frontEndView.AvatarChangeRequested += OnAvatarChangeRequested;
            frontEndView.RefillRequested += OnRefillRequested;
            frontEndView.GoldPackRequested += OnGoldPackRequested;
            frontEndView.JokerPurchaseRequested += OnJokerPurchaseRequested;
            frontEndView.LeaderboardRequested += OnLeaderboardRequested;
            boardView.LeaveStarted += OnLeaveStarted;
            boardView.ArriveStarted += OnArriveStarted;
            inputController.DragStarted += OnDragStarted;
            inputController.DragDropped += OnDragDropped;
            levelUiView.ButtonPressed += OnButtonPressed;
            introductionCard.ButtonPressed += OnButtonPressed;
            frontEndView.ButtonPressed += OnButtonPressed;
            inputController.BoardTapped += OnBoardTapped;
            levelUiView.JokerPressed += OnJokerPressed;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            inputController.DevLevelStepRequested += OnDevLevelStep;
#endif
            ListenForTimeBonus();
        }

        private void OnDisable()
        {
            if (!isUsable)
            {
                return;
            }

            inputController.MoveApplied -= OnMoveApplied;
            inputController.RestartRequested -= OnRestartRequested;
            levelUiView.RestartRequested -= OnRestartRequested;
            levelUiView.MenuRequested -= OnMenuRequested;
            levelUiView.GoldPackRequested -= OnGoldPackRequested;
            levelUiView.OverlaysClosed -= OnLevelOverlaysClosed;
            levelUiView.ResultRestartRequested -= OnRestartRequested;
            levelUiView.NextRequested -= Next;
            levelUiView.ResultMenuRequested -= GoToMenu;
            levelUiView.ConfirmLoseLife -= OnConfirmed;
            levelUiView.ConfirmKeepPlaying -= OnConfirmCancelled;
            frontEndView.PlayRequested -= OnPlayRequested;
            frontEndView.LevelRequested -= OnLevelRequested;
            frontEndView.SoundToggleRequested -= OnSoundToggleRequested;
            frontEndView.HapticsToggleRequested -= OnHapticsToggleRequested;
            frontEndView.NameChangeRequested -= OnNameChangeRequested;
            frontEndView.AvatarChangeRequested -= OnAvatarChangeRequested;
            frontEndView.RefillRequested -= OnRefillRequested;
            frontEndView.GoldPackRequested -= OnGoldPackRequested;
            frontEndView.JokerPurchaseRequested -= OnJokerPurchaseRequested;
            frontEndView.LeaderboardRequested -= OnLeaderboardRequested;
            boardView.LeaveStarted -= OnLeaveStarted;
            boardView.ArriveStarted -= OnArriveStarted;
            inputController.DragStarted -= OnDragStarted;
            inputController.DragDropped -= OnDragDropped;
            levelUiView.ButtonPressed -= OnButtonPressed;
            introductionCard.ButtonPressed -= OnButtonPressed;
            frontEndView.ButtonPressed -= OnButtonPressed;
            inputController.BoardTapped -= OnBoardTapped;
            levelUiView.JokerPressed -= OnJokerPressed;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            inputController.DevLevelStepRequested -= OnDevLevelStep;
#endif
            StopListeningForTimeBonus();
        }

        private void Update()
        {
            if (run == null)
            {
                // The front end: no level, so nothing ticks; only the background
                // follows the screen.
                if (isUsable)
                {
                    FitBackgroundToScreen();
                    RefreshMeta();
                }

                return;
            }

            FitCameraToScreen();

            var wasPlaying = run.Outcome == LevelOutcome.None;
            var countdown = run.Countdown;
            var secondsBefore = countdown != null ? countdown.WholeSecondsRemaining : 0;
            run.Tick(Time.unscaledDeltaTime);
            if (wasPlaying && run.Outcome == LevelOutcome.Lost)
            {
                inputController.CancelDrag();
                ClearJokerSelection();
                if (mode == PlayMode.Campaign)
                {
                    ReportCampaignLoss();
                }

                levelUiView.ShowLoss(LevelResult.Loss());
                PlayCue(SoundCue.Loss);
                Pulse(HapticMoment.Loss);
            }
            else if (hasSound && countdown != null && run.Outcome == LevelOutcome.None
                     && TimerTicks.ShouldTick(secondsBefore, countdown.WholeSecondsRemaining, soundConfig.TickFromSeconds))
            {
                // Only a level still being played ticks: a won or lost one has
                // stopped its countdown, and one held by a card has not started it.
                PlayCue(SoundCue.TimerTick);
            }

            ShowRemainingTime();
            RefreshMeta();
        }

        /// <summary>
        /// Replaces whatever is being played with <paramref name="asset"/>,
        /// started afresh and played as <paramref name="playMode"/>. Its
        /// countdown starts at once, or — when the level introduces mechanics
        /// — when the last introduction card closes. The front end, if it was
        /// showing, closes and the level's interface shows, with no overlay
        /// open. A level being left reports nothing, an open confirmation or
        /// in-level overlay included: only the development tools
        /// replace a level being played. A level that fails to load logs the
        /// reason, changes nothing — a front end that was showing stays — and
        /// answers false.
        /// </summary>
        private bool Load(TextAsset asset, PlayMode playMode)
        {
            if (!TryParse(asset, out var ctx, out var error))
            {
                Debug.LogError($"Level '{asset.name}' failed to load; it is not played. {error}", this);
                return false;
            }

            frontEndView.Hide();
            levelUiView.HideConfirm();
            inputController.CancelDrag();
            ClearJokerSelection();
            freePlayJokers.Refill();
            StopSounds();

            // Cards of the level being left go, without starting its run.
            introductionCard.Hide();

            Countdown countdown = null;
            if (ctx.SuggestedTimeBudgetSeconds > 0)
            {
                countdown = new Countdown(ctx.SuggestedTimeBudgetSeconds);
            }
            else
            {
                Debug.LogError(
                    $"Level '{asset.name}' has a time budget of {ctx.SuggestedTimeBudgetSeconds} s, which is not positive; " +
                    "it runs without a countdown.",
                    this);
            }

            var session = new LevelSession(ctx);
            StopListeningForTimeBonus();
            run = new LevelRun(session, countdown);
            levelName = asset.name;
            mode = playMode;
            campaignGoldEarned = null;
            moveNumber = 0;
            if (isActiveAndEnabled)
            {
                ListenForTimeBonus();
            }

            // One machine rule for the fit's room and for where BoardView draws
            // each machine (D48).
            var machine = config.CreateGeneratorMachine();
            layout = new BoardLayout(ctx.Width, ctx.Height, config.CellSize, config.FrameThicknessCells, machine.Reach(ctx));

            visibility = new VisibilityLayer(ctx);
            boardView.Initialize(config, ctx, layout, visibility, machine);
            boardView.Rebuild(session.State);
            inputController.Initialize(
                run,
                new DragController(config.CreateDragSettings()),
                layout,
                boardView,
                boardCamera,
                IsHeld,
                IsAiming);

            // Shown afresh: no overlay, no time bonus, the new level's state.
            ShowLevelNumber(asset.name);
            ForgetShownMeta();
            var state = LevelUiStateAt(clock.UtcNow);
            levelUiView.Show(state);
            hasShownLevelUi = true;
            shownLevelUi = state;

            fittedScreenWidth = 0;
            fittedScreenHeight = 0;
            FitCameraToScreen();

            if (countdown == null)
            {
                levelUiView.HideTime();
            }

            ShowRemainingTime();

            // The run is not started while a card is open: an unstarted
            // countdown ignores every tick, so the timer stands at the whole
            // budget until the last card closes.
            var introductions = IntroductionsOf(asset.name);
            if (introductions.Count > 0)
            {
                introductionCard.Show(introductions, StartRun);
            }
            else
            {
                StartRun();
            }

            return true;
        }

        /// <summary>
        /// Starts the countdown of the level just loaded. Called at once for a
        /// level with no cards, otherwise by the last card closing — and only
        /// then: <see cref="Load"/> hides the card, dropping this callback,
        /// before it replaces <see cref="run"/>, so the run started is always
        /// the one the cards were shown for.
        /// </summary>
        private void StartRun()
        {
            run.Start();
            ShowRemainingTime();
        }

        /// <summary>
        /// True while an introduction card is open or the level is paused
        /// (<see cref="IsPaused"/>): the one query that holds the pointer and
        /// <b>R</b> (<see cref="InputController"/>), the HUD's restart and menu
        /// buttons (<see cref="OnRestartRequested"/>,
        /// <see cref="OnMenuRequested"/>) and the jokers. None opens during a
        /// drag: a card opens as a level loads, which cancels any drag first,
        /// and <see cref="PauseLevel"/> cancels it before a pause.
        /// </summary>
        private bool IsHeld() => introductionCard.IsOpen || IsPaused();

        /// <summary>
        /// True while the confirmation or any in-level overlay is open (D64,
        /// D74): the countdown stands still and the board takes no input. The
        /// one test both kinds of pause share.
        /// </summary>
        private bool IsPaused() => levelUiView.IsConfirmOpen || levelUiView.IsOverlayOpen;

        /// <summary>
        /// Stops the level for a pause that is about to open: the drag is
        /// cancelled, then the countdown stopped where it stands. The pause's
        /// own view then holds the board through <see cref="IsHeld"/>.
        /// </summary>
        private void PauseLevel()
        {
            inputController.CancelDrag();
            run.Countdown?.Stop();
        }

        /// <summary>
        /// Resumes the countdown from the same remaining time once nothing
        /// pauses the level any more — the confirmation answered "Keep
        /// playing", or the last in-level overlay closed — and only while it is
        /// being played. While something still pauses it, nothing changes.
        /// </summary>
        private void ResumeIfFree()
        {
            if (IsBeingPlayed() && !IsPaused())
            {
                run.Countdown?.Start();
            }
        }

        /// <summary>The player closed the last in-level overlay: the level goes on (D74).</summary>
        private void OnLevelOverlaysClosed()
        {
            ResumeIfFree();
            RefreshMeta();
        }

        /// <summary>
        /// True while the level is being played: loaded, neither won nor lost
        /// — a level that has ended has already cost what it costs (D57).
        /// </summary>
        private bool IsBeingPlayed() => run != null && run.Outcome == LevelOutcome.None;

        /// <summary>
        /// Restart from the HUD, from <b>R</b> or from the result panel. A
        /// campaign level being played asks first (<see cref="AskToConfirm"/>);
        /// free play restarts at once. After the level has ended it never
        /// asks: a campaign win cannot be restarted at all — its panel has no
        /// Restart and <b>R</b> does nothing — and a campaign loss restarts
        /// only with a life left, opening the Lives overlay otherwise. Held
        /// while a card or the confirmation is open.
        /// </summary>
        private void OnRestartRequested()
        {
            if (run == null || IsHeld())
            {
                return;
            }

            if (mode == PlayMode.Campaign)
            {
                if (IsBeingPlayed())
                {
                    AskToConfirm(ConfirmKind.Restart);
                    return;
                }

                if (run.Outcome == LevelOutcome.Won)
                {
                    return;
                }

                if (!metaGame.CanStartCampaignLevel())
                {
                    ShowLivesOverlay();
                    return;
                }
            }

            Restart();
        }

        /// <summary>
        /// Menu from the HUD: a campaign level being played asks first; free
        /// play goes at once. Held while a card or the confirmation is open.
        /// The result panel's Menu never asks (<see cref="GoToMenu"/>).
        /// </summary>
        private void OnMenuRequested()
        {
            if (run == null || IsHeld())
            {
                return;
            }

            if (mode == PlayMode.Campaign && IsBeingPlayed())
            {
                AskToConfirm(ConfirmKind.Leave);
                return;
            }

            GoToMenu();
        }

        /// <summary>
        /// Opens the confirmation that <paramref name="kind"/> will cost a
        /// life (D57, D64) and the win streak of the save with it (D74), with
        /// the selection cleared (D67) and the level paused
        /// (<see cref="PauseLevel"/>); the board and the HUD's buttons are held
        /// while it is open (<see cref="IsHeld"/>). Only a campaign level asks,
        /// so the save's streak is the one a loss resets.
        /// </summary>
        private void AskToConfirm(ConfirmKind kind)
        {
            ClearJokerSelection();
            PauseLevel();
            pendingConfirm = kind;
            levelUiView.ShowConfirm(kind, metaGame.Save.Streak.Current);
        }

        /// <summary>
        /// "Keep playing", or Back on the confirmation: closes it and resumes
        /// the countdown from the same remaining time.
        /// </summary>
        private void OnConfirmCancelled()
        {
            levelUiView.HideConfirm();
            ResumeIfFree();
        }

        /// <summary>
        /// "Lose a life": reports the loss and saves, then leaves or restarts.
        /// A restart needs a life, so with none left after the loss the
        /// Lives overlay opens instead.
        /// </summary>
        private void OnConfirmed()
        {
            levelUiView.HideConfirm();
            if (!IsBeingPlayed())
            {
                return;
            }

            ReportCampaignLoss();
            if (pendingConfirm == ConfirmKind.Leave)
            {
                GoToMenu();
            }
            else if (metaGame.CanStartCampaignLevel())
            {
                Restart();
            }
            else
            {
                ShowLivesOverlay();
            }
        }

        /// <summary>
        /// The mechanics the level in the file <paramref name="levelFile"/>
        /// introduces: those it contains and no earlier level in the catalog's
        /// order does, plus How to Play on the first. None for a level the
        /// catalog does not hold, or when there is no catalog;
        /// <see cref="ShowLevelNumber"/> warns of both.
        /// </summary>
        private IReadOnlyList<LevelMechanic> IntroductionsOf(string levelFile)
        {
            if (catalog == null || !catalog.TryGetNumber(levelFile, out var number))
            {
                return Array.Empty<LevelMechanic>();
            }

            return LevelMechanics.IntroducedBy(mechanicsInOrder, number - 1);
        }

        /// <summary>
        /// Restarts the level being played, keeping its mode. Its callers
        /// have already decided what the restart costs.
        /// </summary>
        private void Restart()
        {
            if (run == null || IsHeld())
            {
                return;
            }

            inputController.CancelDrag();
            ClearJokerSelection();
            StopSounds();
            run.Restart();
            moveNumber = 0;
            freePlayJokers.Refill();
            levelUiView.CancelTimeBonus();
            boardView.Rebuild(run.Session.State);
            levelUiView.HideResult();
            ShowRemainingTime();
            ForgetShownMeta();
            RefreshMeta();
        }

        /// <summary>
        /// Next on a win's panel. After a campaign win, the campaign level —
        /// the one the win advanced the position to — with the life check of
        /// <see cref="StartCampaignLevel"/>; after a free-play win, the next
        /// level in level order, as free play.
        /// </summary>
        private void Next()
        {
            if (mode == PlayMode.Campaign)
            {
                StartCampaignLevel();
                return;
            }

            if (TryGetNextLevel(out var next))
            {
                Load(next, PlayMode.FreePlay);
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Goes to the next or previous level in catalog order when a
        /// development level key goes down (<see cref="DevKeys"/>, Module 21),
        /// through <see cref="Load"/> exactly as <see cref="Next"/> does, so
        /// cards, countdown and HUD behave as on a real level change. At the
        /// last or first level, or with no usable level order, it logs a line
        /// and changes nothing.
        /// </summary>
        private void OnDevLevelStep(int step)
        {
            if (run == null)
            {
                return;
            }

            var isNext = step == DevKeys.NextStep;
            var key = isNext ? DevKeys.NextLevel : DevKeys.PreviousLevel;
            if (catalog == null)
            {
                Debug.Log($"{key}: the level order is unusable, so there is no level to go to; nothing changes.", this);
                return;
            }

            var levelId = run.Session.Context.LevelId;
            string neighbour;
            var hasNeighbour = isNext
                ? catalog.TryGetNext(levelId, out neighbour)
                : catalog.TryGetPrevious(levelId, out neighbour);
            if (!hasNeighbour)
            {
                var end = isNext ? "last" : "first";
                Debug.Log($"{key}: '{levelName}' is the {end} level; nothing changes.", this);
                return;
            }

            // Every name in the catalog is a level that loaded, kept in
            // levelAssets under that name (BuildCatalog), so this cannot miss.
            Load(levelAssets[neighbour], PlayMode.FreePlay);
        }
#endif

        /// <summary>
        /// Presents the state a move produced. The session has already
        /// changed, so the view must be handed the new state whatever happens.
        /// What changed is worked out by <see cref="MoveChanges"/>; when that
        /// fails — a bug, since the states and the move came from the resolver —
        /// it is logged, and the board is presented with what could still be
        /// worked out: every change but the clear when only the clear's gate is
        /// missing, nothing at all otherwise. Either way the board ends on the
        /// new state rather than the old picture.
        /// </summary>
        private void OnMoveApplied(BoardState before, Move move, Direction? push)
        {
            var session = run.Session;
            var ctx = session.Context;
            var after = session.State;

            MoveChanges changes;
            try
            {
                changes = MoveChanges.Between(ctx, before, after, move, push);
            }
            catch (InvalidOperationException e)
            {
                changes = FallbackChanges(ctx, before, after);
                var shown = changes.HasArrivals
                    ? "the other changes play, without the clear effect"
                    : "the board is redrawn from the new state with no effects";
                Debug.LogError(
                    $"Level '{levelName}': what {move} (push {push?.ToString() ?? "none"}) changed could not be worked out; " +
                    $"{shown}. {e.Message}",
                    this);
            }

            // The block was released before the move was applied, so only now
            // is it known whether it stays on the board: a move that clears
            // nothing is a drop; one that clears sounds from its leave stage.
            if (changes.Clears.Count == 0)
            {
                PlayCue(SoundCue.Drop);
            }

            PresentStateChange(after, changes);
        }

        /// <summary>
        /// The one path after a move or a joker changed the session's state:
        /// counts it for the bursts' seeds, records a win, and presents the
        /// new state. The win was decided when the state was applied; it is
        /// recorded now, not when the panel opens, and the panel opens once
        /// the presentation is done (<see cref="OnPresentationDone"/>).
        /// </summary>
        private void PresentStateChange(BoardState after, MoveChanges changes)
        {
            moveNumber++;
            if (run.Outcome == LevelOutcome.Won)
            {
                ClearJokerSelection();
                RecordWin();
            }

            boardView.Present(after, changes, moveNumber, OnPresentationDone);
        }

        /// <summary>
        /// A press of a joker's button (Modules 34 and 40). Read against the
        /// same state that drew the button (<see cref="LevelUiStateAt"/>): in
        /// the campaign, a joker with none held and a price above the balance
        /// pauses the level and opens Not Enough Gold with the shortfall,
        /// whatever its kind, selecting and using nothing and keeping any
        /// selection (D74). Otherwise the Clock is used at once
        /// (<see cref="UseClock"/>), and the Rocket and the Broom are selected,
        /// unselected or switched (<see cref="JokerSelection.Press"/>) with
        /// nothing spent (D67). The buttons are not pressable while the level
        /// is not being played or is held; a press that comes then anyway
        /// changes nothing.
        /// </summary>
        private void OnJokerPressed(JokerKind kind)
        {
            if (!IsBeingPlayed() || IsHeld())
            {
                return;
            }

            var button = LevelUiStateAt(clock.UtcNow).Button(kind);
            if (button.IsShortOfGold)
            {
                PauseLevel();
                levelUiView.ShowNotEnoughGold(button.Shortfall);
                RefreshMeta();
                return;
            }

            if (kind == JokerKind.Clock)
            {
                UseClock();
                return;
            }

            jokerSelection.Press(kind);
            RefreshMeta();
        }

        /// <summary>
        /// Uses a Clock (D65): clears any selection, then checks that the
        /// level is being played, is not held and has a countdown; spends one;
        /// adds <see cref="MetaRules.ClockBonusSeconds"/> to the countdown;
        /// saves in the campaign; and shows the seconds beside the timer with
        /// the time bonus's cue.
        /// </summary>
        private void UseClock()
        {
            ClearJokerSelection();
            if (!IsBeingPlayed() || IsHeld() || run.Countdown == null)
            {
                return;
            }

            if (!TrySpendJoker(JokerKind.Clock))
            {
                RefreshMeta();
                return;
            }

            var seconds = metaRules.ClockBonusSeconds;
            run.Countdown.AddBonus(seconds);
            SaveJokerSpend(JokerKind.Clock);
            ShowTimeBonus(seconds);
            RefreshMeta();
        }

        /// <summary>
        /// A press on the board while a Rocket or a Broom is selected (D67).
        /// The block is the one the drag would grab there
        /// (<see cref="DragController.BlockAt"/>). A Rocket needs
        /// <see cref="LevelSession.CanRocket"/>; a Broom takes the colour the
        /// player sees on that block — none for a frozen block or one not
        /// shown — and needs <see cref="LevelSession.CanSweep"/>. A tap that
        /// is not a valid use does nothing, the selection staying. A valid one
        /// spends the joker, clears the selection, applies it, saves in the
        /// campaign — after a spend, whatever the apply did — and presents
        /// it. A resolver that refuses after a valid check is a bug: it is
        /// logged, and the joker stays spent.
        /// </summary>
        private void OnBoardTapped(Coord cell)
        {
            var selected = jokerSelection.Selected;
            if (!selected.HasValue || !IsBeingPlayed() || IsHeld())
            {
                return;
            }

            var kind = selected.Value;
            var session = run.Session;
            var before = session.State;
            var blockIndex = DragController.BlockAt(session.Context, before, cell);
            if (blockIndex < 0)
            {
                return;
            }

            var color = default(BlockColor);
            if (kind == JokerKind.Rocket)
            {
                if (!session.CanRocket(blockIndex))
                {
                    return;
                }
            }
            else
            {
                var shown = visibility.Block(before, blockIndex);
                if (!shown.IsShown || !shown.OuterColor.HasValue)
                {
                    return;
                }

                color = shown.OuterColor.Value;
                if (!session.CanSweep(color))
                {
                    return;
                }
            }

            if (!TrySpendJoker(kind))
            {
                ClearJokerSelection();
                RefreshMeta();
                return;
            }

            ClearJokerSelection();
            var isApplied = kind == JokerKind.Rocket ? session.TryRocket(blockIndex) : session.TrySweep(color);
            SaveJokerSpend(kind);
            if (isApplied)
            {
                OnJokerApplied(before, kind);
            }
            else
            {
                Debug.LogError(
                    $"Level '{levelName}': the {kind} on block slot {blockIndex} passed its check but the resolver refused it; " +
                    "the joker stays spent and the board is unchanged.",
                    this);
            }

            RefreshMeta();
        }

        /// <summary>
        /// Presents the state a joker produced, as <see cref="OnMoveApplied"/>
        /// does a move's: what changed is <see cref="MoveChanges.ForJoker"/>'s,
        /// and when that fails — a bug — it is logged and the board is
        /// presented with what could still be worked out.
        /// </summary>
        private void OnJokerApplied(BoardState before, JokerKind kind)
        {
            var ctx = run.Session.Context;
            var after = run.Session.State;

            MoveChanges changes;
            try
            {
                changes = MoveChanges.ForJoker(ctx, before, after);
            }
            catch (InvalidOperationException e)
            {
                changes = FallbackChanges(ctx, before, after);
                var shown = changes.HasArrivals
                    ? "the other changes play, without the clear effects"
                    : "the board is redrawn from the new state with no effects";
                Debug.LogError($"Level '{levelName}': what the {kind} changed could not be worked out; {shown}. {e.Message}", this);
            }

            PresentStateChange(after, changes);
        }

        /// <summary>
        /// Spends one <paramref name="kind"/> (D65, D66): in the campaign
        /// through <see cref="MetaGame.TryUseJoker"/>, from the inventory or
        /// bought at once; in free play from the level's allowance. False,
        /// with nothing spent, when there is none to spend — the button was
        /// not pressable then, so that is logged as a bug.
        /// </summary>
        private bool TrySpendJoker(JokerKind kind)
        {
            if (mode == PlayMode.Campaign)
            {
                if (metaGame.TryUseJoker(kind) != JokerUse.NotEnoughGold)
                {
                    return true;
                }

                Debug.LogError($"A {kind} was used with none held and too little gold to buy one; nothing is spent or used.", this);
                return false;
            }

            if (freePlayJokers.TryUse(kind))
            {
                return true;
            }

            Debug.LogError($"A {kind} was used in free play with none left in this level; nothing is used.", this);
            return false;
        }

        /// <summary>
        /// Saves after a joker was spent in the campaign, where the spend
        /// changed the inventory or the gold. Free play saves nothing (D66).
        /// </summary>
        private void SaveJokerSpend(JokerKind kind)
        {
            if (mode == PlayMode.Campaign)
            {
                SaveOrWarn($"A {kind} was used, but saving the joker or the gold it cost failed; the change is remembered until the game is closed.");
            }
        }

        /// <summary>
        /// Unselects any joker. The bar shows it at the next
        /// <see cref="RefreshMeta"/>, which compares the selection with what it
        /// last showed.
        /// </summary>
        private void ClearJokerSelection()
        {
            jokerSelection.Clear();
        }

        /// <summary>
        /// True while a Rocket or a Broom is selected: the one query that
        /// turns a press on the board into a tap (<see cref="InputController"/>).
        /// </summary>
        private bool IsAiming() => jokerSelection.Selected.HasValue;

        /// <summary>
        /// Every change but the clear, or none when even those cannot be worked
        /// out; the second failure is logged on its own.
        /// </summary>
        private MoveChanges FallbackChanges(LevelContext ctx, BoardState before, BoardState after)
        {
            try
            {
                return MoveChanges.WithoutClears(ctx, before, after);
            }
            catch (InvalidOperationException e)
            {
                Debug.LogError($"Level '{levelName}': no change of this move can be animated. {e.Message}", this);
                return MoveChanges.None;
            }
        }

        private void ListenForTimeBonus()
        {
            var session = run?.Session;
            if (session == null || session == listenedSession)
            {
                return;
            }

            StopListeningForTimeBonus();
            session.TimeBonusEarned += OnTimeBonusEarned;
            listenedSession = session;
        }

        private void StopListeningForTimeBonus()
        {
            if (listenedSession == null)
            {
                return;
            }

            listenedSession.TimeBonusEarned -= OnTimeBonusEarned;
            listenedSession = null;
        }

        /// <summary>
        /// Shows a time bonus beside the timer (M10), as the countdown takes it.
        /// The run subscribed first, so the countdown has already risen; a
        /// level without a countdown, or one already ended, shows nothing.
        /// </summary>
        private void OnTimeBonusEarned(int seconds)
        {
            if (run != null && run.Countdown != null && run.Outcome == LevelOutcome.None)
            {
                ShowTimeBonus(seconds);
            }
        }

        /// <summary>
        /// Shows <paramref name="seconds"/> beside the timer and plays the
        /// time bonus's cue: the one look for a time-bonus block (M10) and a
        /// Clock (Module 34).
        /// </summary>
        private void ShowTimeBonus(int seconds)
        {
            levelUiView.ShowTimeBonus(seconds);
            PlayCue(SoundCue.TimeBonus);
        }

        /// <summary>
        /// The board now shows the state a move produced. A win was decided —
        /// and the countdown stopped — the moment that state was applied; the
        /// panel waits until the player has seen the block leave.
        /// </summary>
        private void OnPresentationDone()
        {
            // GoToMenu clears the board, which drops this callback; the null
            // check only keeps a report that slipped through from opening a
            // panel over the front end.
            if (run == null || run.Outcome != LevelOutcome.Won)
            {
                return;
            }

            Debug.Log($"Level '{levelName}' solved.", this);

            // One query decides Next and, in the campaign, the closing message
            // together: whether the position the win advanced to holds a
            // level (the campaign's one source of truth). In free play, Next
            // follows the level order, and there is no closing message. A
            // won campaign level is never replayed (D58): its panel has no
            // Restart (LevelResult).
            var result = mode == PlayMode.Campaign
                ? LevelResult.CampaignWin(campaignGoldEarned, CampaignLevels.TryGetLevel(catalog, metaGame.CampaignPosition, out _))
                : LevelResult.FreePlayWin(TryGetNextLevel(out _));
            levelUiView.ShowWin(result);
            PlayCue(SoundCue.Win);
            Pulse(HapticMoment.Win);
        }

        /// <summary>The HUD skips the work when the displayed second has not changed, so this runs every frame.</summary>
        private void ShowRemainingTime()
        {
            var countdown = run.Countdown;
            if (countdown != null)
            {
                levelUiView.ShowTime(countdown.RemainingSeconds);
            }
        }

        /// <summary>
        /// Keeps the level number of the file <paramref name="levelFile"/> for
        /// the level's interface, or none with a warning saying why it has
        /// none. A level with no number has no place in the level order
        /// either, so the same warning says that it shows no introduction
        /// cards.
        /// </summary>
        private void ShowLevelNumber(string levelFile)
        {
            if (catalog != null && catalog.TryGetNumber(levelFile, out var number))
            {
                levelNumber = number;
                return;
            }

            levelNumber = null;
            var reason = catalog == null
                ? "the level order is unusable"
                : $"it is not one of the levels in Resources/{LevelsResourcePath}";
            Debug.LogWarning(
                $"Level '{levelFile}' has no level number because {reason}; the HUD shows no number and the level shows no introduction cards.",
                this);
        }

        private void FitCameraToScreen()
        {
            var safeArea = Screen.safeArea;
            if (Screen.width == fittedScreenWidth && Screen.height == fittedScreenHeight && safeArea == fittedSafeArea)
            {
                return;
            }

            fittedScreenWidth = Screen.width;
            fittedScreenHeight = Screen.height;
            fittedSafeArea = safeArea;
            if (fittedScreenWidth <= 0 || fittedScreenHeight <= 0)
            {
                return;
            }

            if (!ScreenBands.TryAddInsets(
                    config.TopBandScreenFraction, config.BottomBandScreenFraction, safeArea, fittedScreenHeight,
                    out var top, out var bottom)
                && !hasReportedInsetFallback)
            {
                hasReportedInsetFallback = true;
                Debug.LogError(
                    $"The safe area {safeArea} on a {fittedScreenWidth}x{fittedScreenHeight} screen, added to the HUD bands, " +
                    "would leave no room for the board; the camera is fitted to the bands alone, so the board may reach " +
                    "under the unsafe edges and the HUD. Reported once.",
                    this);
            }

            // The bands the camera keeps free, measured by the same rule.
            levelUiView.PlaceBands(
                ScreenBands.HudBandAnchors(safeArea, fittedScreenWidth, fittedScreenHeight, config.TopBandScreenFraction),
                ScreenBands.BottomBandAnchors(safeArea, fittedScreenWidth, fittedScreenHeight, config.BottomBandScreenFraction));

            var size = layout.FitOrthographicSize(
                (float)fittedScreenWidth / fittedScreenHeight, config.SideMarginCells, top, bottom);
            boardCamera.orthographicSize = size;

            // The board stays centred on its view; the camera moves so the
            // board and its machines sit centred between the HUD bands.
            var boardCenter = boardView.transform.position;
            var offset = layout.CameraCenterOffset(size, top, bottom);
            boardCamera.transform.position = new Vector3(
                boardCenter.x + offset.x,
                boardCenter.y + offset.y,
                boardCamera.transform.position.z);

            backgroundView.Fit(boardCamera);
        }

        /// <summary>
        /// While no level is loaded there is no board to fit the camera to:
        /// the camera stays as it is and the background is stretched over its
        /// view again when the screen's size has changed. <see cref="Load"/>
        /// zeroes the fitted size, so the next level is fitted afresh.
        /// </summary>
        private void FitBackgroundToScreen()
        {
            if (Screen.width == fittedScreenWidth && Screen.height == fittedScreenHeight)
            {
                return;
            }

            fittedScreenWidth = Screen.width;
            fittedScreenHeight = Screen.height;
            if (fittedScreenWidth > 0 && fittedScreenHeight > 0)
            {
                backgroundView.Fit(boardCamera);
            }
        }

        private bool TryGetNextLevel(out TextAsset next)
        {
            next = null;
            return run != null
                   && catalog != null
                   && catalog.TryGetNext(run.Session.Context.LevelId, out var name)
                   && levelAssets.TryGetValue(name, out next);
        }

        /// <summary>
        /// Reads every level in <c>Resources/Levels</c> once, for its id and
        /// for the mechanics it contains, and builds the level order and, in
        /// that order, what each level contains. A file that fails to load is
        /// logged and left out: it has no place in the order and introduces
        /// nothing, so a later level introduces what it would have. Two files
        /// sharing an id are logged and leave no order at all, which hides
        /// Next and every introduction card. Neither stops the current level
        /// from playing.
        /// </summary>
        /// <remarks>
        /// What the files say is decided by <see cref="LevelRoster.Read"/>, the
        /// reading the Play Level window (Module 21) goes by too; this method
        /// only fetches the assets, reports what the roster left out, and keeps
        /// each loaded level's asset by name.
        /// </remarks>
        private void BuildCatalog()
        {
            levelAssets.Clear();
            mechanicsInOrder = Array.Empty<IReadOnlyCollection<LevelMechanic>>();
            catalog = null;

            // Of two files with one name the roster keeps the first, so the
            // first is the asset kept for that name here.
            var firstAssetByName = new Dictionary<string, TextAsset>();
            var files = new List<(string name, string json)>();
            foreach (var asset in Resources.LoadAll<TextAsset>(LevelsResourcePath))
            {
                if (!firstAssetByName.ContainsKey(asset.name))
                {
                    firstAssetByName.Add(asset.name, asset);
                }

                files.Add((asset.name, asset.text));
            }

            var roster = LevelRoster.Read(files);

            foreach (var failure in roster.Failed)
            {
                if (failure.IsDuplicateName)
                {
                    Debug.LogError(
                        $"Two level files in Resources/{LevelsResourcePath} are named '{failure.Name}'; the second is left out of the level order.",
                        this);
                }
                else
                {
                    Debug.LogError(
                        $"Level '{failure.Name}' in Resources/{LevelsResourcePath} failed to load; it is left out of the level order " +
                        $"and introduces no mechanic. {failure.Error}",
                        this);
                }
            }

            // Every loaded name was read from an asset of that name above.
            var loaded = roster.Loaded;
            for (var i = 0; i < loaded.Count; i++)
            {
                levelAssets.Add(loaded[i], firstAssetByName[loaded[i]]);
            }

            if (roster.Catalog == null)
            {
                Debug.LogError(
                    $"{roster.CatalogError} The level order is unusable, so the level grid is empty, Next is hidden and no introduction card shows; Play starts the scene's level.",
                    this);
                return;
            }

            catalog = roster.Catalog;
            mechanicsInOrder = roster.MechanicsInOrder;
        }

        private static bool TryParse(TextAsset asset, out LevelContext ctx, out string error) =>
            LevelRoster.TryParse(asset.text, asset.name, out ctx, out error);

        private bool HasEverythingAssigned()
        {
            if (config == null)
            {
                Debug.LogError("LevelBootstrap: Config is not assigned; nothing is drawn.", this);
                return false;
            }

            var isAssigned = true;

            foreach (var problem in config.Problems())
            {
                Debug.LogError($"{problem} Nothing is drawn.", this);
                isAssigned = false;
            }

            if (metaConfig == null)
            {
                Debug.LogError("LevelBootstrap: Meta Config is not assigned; nothing is drawn.", this);
                isAssigned = false;
            }
            else
            {
                foreach (var problem in metaConfig.Problems())
                {
                    Debug.LogError($"{problem} Nothing is drawn.", metaConfig);
                    isAssigned = false;
                }
            }

            if (simulatedStore == null)
            {
                Debug.LogError("LevelBootstrap: Simulated Store is not assigned; nothing is drawn.", this);
                isAssigned = false;
            }
            else
            {
                foreach (var problem in simulatedStore.Problems())
                {
                    Debug.LogError($"{problem} Nothing is drawn.", simulatedStore);
                    isAssigned = false;
                }
            }

            if (simulatedLeaderboard == null)
            {
                Debug.LogError("LevelBootstrap: Simulated Leaderboard is not assigned; nothing is drawn.", this);
                isAssigned = false;
            }
            else
            {
                foreach (var problem in simulatedLeaderboard.Problems())
                {
                    Debug.LogError($"{problem} Nothing is drawn.", simulatedLeaderboard);
                    isAssigned = false;
                }
            }

            if (avatarCatalog == null)
            {
                Debug.LogError("LevelBootstrap: Avatar Catalog is not assigned; nothing is drawn.", this);
                isAssigned = false;
            }
            else
            {
                foreach (var problem in avatarCatalog.Problems())
                {
                    Debug.LogError($"{problem} Nothing is drawn.", avatarCatalog);
                    isAssigned = false;
                }
            }

            if (level == null)
            {
                Debug.LogError("LevelBootstrap: Level is not assigned; nothing is drawn.", this);
                isAssigned = false;
            }

            if (boardCamera == null || boardView == null || backgroundView == null || inputController == null
                || introductionCard == null || frontEnd == null || levelUi == null)
            {
                Debug.LogError(
                    "LevelBootstrap: Board Camera, Board View, Background View, Input Controller, Introduction Card, " +
                    "Front End and Level Ui must all be assigned; nothing is drawn.",
                    this);
                return false;
            }

            frontEndView = frontEnd as IFrontEnd;
            if (frontEndView == null)
            {
                Debug.LogError(
                    $"LevelBootstrap: Front End holds a {frontEnd.GetType().Name}, which does not implement IFrontEnd; " +
                    "drag the FrontEnd component of the Front End instance there. Nothing is drawn.",
                    frontEnd);
                isAssigned = false;
            }
            else
            {
                var setupProblem = frontEndView.MissingSetup();
                if (setupProblem != null)
                {
                    Debug.LogError($"{setupProblem} Nothing is drawn.", frontEnd);
                    isAssigned = false;
                }
            }

            levelUiView = levelUi as ILevelUi;
            if (levelUiView == null)
            {
                Debug.LogError(
                    $"LevelBootstrap: Level Ui holds a {levelUi.GetType().Name}, which does not implement ILevelUi; " +
                    "drag the LevelUi component of the Level UI instance there. Nothing is drawn.",
                    levelUi);
                isAssigned = false;
            }
            else
            {
                var levelUiProblem = levelUiView.MissingSetup();
                if (levelUiProblem != null)
                {
                    Debug.LogError($"{levelUiProblem} Nothing is drawn.", levelUi);
                    isAssigned = false;
                }
            }

            var cardProblem = introductionCard.MissingCanvas();
            if (cardProblem.Length > 0)
            {
                Debug.LogError($"{cardProblem} Nothing is drawn.", introductionCard);
                isAssigned = false;
            }

            return isAssigned;
        }
    }
}
