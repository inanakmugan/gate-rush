using System;
using System.Collections.Generic;
using GateRush.Core;
using GateRush.Meta;
using GateRush.Serialization;
using UnityEngine;

namespace GateRush.Runtime
{
    /// <summary>
    /// Opens the game on the menu and builds the level the menu picks from its
    /// JSON <see cref="TextAsset"/>: the <see cref="LevelSession"/> and its
    /// <see cref="LevelRun"/> with the countdown, the plain helpers they need,
    /// and the views that draw them. Runs the countdown, feeds the HUD, shows
    /// the result panel, and handles Restart, Next and Menu. Holds all
    /// per-play state on this instance — Enter Play Mode runs without a domain
    /// reload, so nothing here is static.
    /// </summary>
    /// <remarks>
    /// <para><b>Menu (Module 23, D50).</b> A plain Play and the player build
    /// open on <see cref="MenuScreen"/>'s title page with no level loaded: no
    /// board, no countdown, the HUD hidden and the input controller unbound.
    /// Menu, from the HUD or the result panel, abandons the level
    /// (<see cref="GoToMenu"/>). A level that is won is recorded in
    /// <see cref="CompletedLevels"/>, and the whole save is saved through
    /// <see cref="SaveRepository"/> at once.</para>
    /// <para><b>Campaign and free play (Module 32, D57, D58, D63, D64).</b>
    /// Where a level starts decides its <see cref="PlayMode"/>, kept with the
    /// run; Restart keeps it. Play and Next after a campaign win start the
    /// campaign level, the one at <see cref="MetaGame.CampaignPosition"/>
    /// (<see cref="CampaignLevels.TryGetLevel"/>), and only with a life left;
    /// with none, the menu's no-lives page opens. The level select, Next
    /// after a free-play win and every development tool start free play.
    /// This is the only caller of <see cref="MetaGame"/>: a campaign win is
    /// reported where it is decided, a campaign loss when the countdown runs
    /// out or the player confirms leaving or restarting a level being
    /// played, and a refill from the no-lives page; every call that changed
    /// the save is followed by one <see cref="SaveOrWarn"/>, a loss's even
    /// when no life was left to lose. Leaving or restarting a campaign level
    /// being played asks first (<see cref="ConfirmPrompt"/>), with the
    /// countdown stopped and the board and the HUD's buttons held. A
    /// campaign win's panel has no Restart: a won campaign level is never
    /// replayed. The meta lines are rebuilt only when the lives, the second
    /// to the next life or the gold they show have changed. With no usable
    /// level order the select is hidden and Play starts <see cref="level"/>
    /// as free play.</para>
    /// <para>A level or config that cannot be used logs the reason and draws
    /// nothing. A level whose time budget is not positive logs an error and
    /// plays without a countdown, so an unfinished level stays testable.</para>
    /// <para><b>Next</b> follows <see cref="LevelCatalog"/> over every JSON in
    /// <c>Resources/Levels</c>, loaded once at start. A file there that fails
    /// to load is logged and left out; two files sharing a level id are
    /// logged and hide Next altogether. Neither stops the current level from
    /// playing. The HUD's level number comes from the same catalog; a level
    /// without one plays with the level pill hidden.</para>
    /// <para><b>Introductions (Module 19).</b> The same pass over the level
    /// files records which mechanics each level contains
    /// (<see cref="LevelMechanics.Of"/>), in the catalog's order. A level that
    /// loads — first load or Next — shows a card for every mechanic no earlier
    /// level contains, and its countdown starts when the last card closes;
    /// until then input is held. A restart shows none. A level outside the
    /// level order, or any level while that order is unusable, shows none
    /// either.</para>
    /// <para><b>Development tools (Module 21).</b> In the editor, a level the
    /// editor tools asked for (<see cref="DevLevelOverride"/>) skips the menu
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
    /// change and the way to the menu. The menu's sound button flips
    /// <see cref="SoundSetting"/>, and the whole save is saved at once. With no Sound Player or Sound Config assigned, one error is
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
    /// screen's size or safe area changes.</para>
    /// <para><b>Jokers (Module 34, D65, D66, D67).</b> The joker bar shows
    /// with the HUD. The Clock's button uses it at once; the Rocket's and the
    /// Broom's select it (<see cref="JokerSelection"/>), and the next valid
    /// tap on a block uses it. Every use runs in one order: check, spend
    /// (<see cref="MetaGame.TryUseJoker"/> in the campaign, the level's
    /// <see cref="FreePlayJokers"/> in free play), apply, save in the
    /// campaign, then present — a joker through the same
    /// <see cref="PresentStateChange"/> as a move. An invalid tap does
    /// nothing and keeps the selection. Menu, Restart, the confirmation, the
    /// end of the level and a new level clear it. The free-play allowance is
    /// full again on every load and restart.</para>
    /// </remarks>
    public sealed class LevelBootstrap : MonoBehaviour
    {
        /// <summary>
        /// The <c>Resources</c> folder the level order is read from. Public so
        /// the Play Level window (Module 21) reads the same folder.
        /// </summary>
        public const string LevelsResourcePath = "Levels";

        [Tooltip("The level Play starts, as free play, only when the level order is unusable (two files in Assets/Resources/Levels sharing a level id, or none); otherwise Play starts the campaign level and this is not read. The Level Editor's Play button and Gate Rush > Play Level… skip the menu and start on their level for one Play session. In the editor and in development builds, Page Down goes to the next level and Page Up to the previous one, in level order, at any moment a level is played; at the last or first level the key only logs a line. A release build has neither. Every one of these development tools plays free play: no life needed or lost, no gold.")]
        [SerializeField] private TextAsset level;

        [SerializeField] private RuntimeConfig config;

        [Tooltip("The life cap, the minutes a life takes to come back, a new player's gold and jokers, the store's prices (the jokers and the lives refill the no-lives page sells) and the win streak's reward table: Assets/Config/MetaConfig.asset. The save cannot be read without them, so left empty, or with a problem, it logs an error and nothing is drawn.")]
        [SerializeField] private MetaConfig metaConfig;

        [Tooltip("The orthographic camera that shows the board. It is placed so the Board View sits centred between the HUD bands, below and above the screen's unsafe edges, and re-fitted when the screen size or safe area changes.")]
        [SerializeField] private Camera boardCamera;

        [SerializeField] private BoardView boardView;

        [Tooltip("Draws the background gradient and vignette; stretched over the camera's view whenever the camera is fitted.")]
        [SerializeField] private BackgroundView backgroundView;
        [SerializeField] private InputController inputController;

        [Tooltip("The HUD on the canvas: restart button, timer and level number in the top band.")]
        [SerializeField] private HudView hudView;

        [Tooltip("The introduction card on the canvas: shown over the HUD and under the result panel when a level introduces a mechanic.")]
        [SerializeField] private IntroductionCard introductionCard;

        [Tooltip("The result panel on the canvas.")]
        [SerializeField] private ResultPanel resultPanel;

        [Tooltip("The menu on the canvas: the title screen, Settings, the level select and the no-lives page, shown while no level is loaded.")]
        [SerializeField] private MenuScreen menuScreen;

        [Tooltip("The confirmation on the canvas before Menu or Restart costs a life while a campaign level is played (Module 32). Drawn over everything else on the canvas.")]
        [SerializeField] private ConfirmPrompt confirmPrompt;

        [Tooltip("The joker bar on the canvas: the Clock, Rocket and Broom buttons in the bottom band, shown with the HUD (Module 34). Drawn under the introduction card, the result panel, the menu and the confirmation.")]
        [SerializeField] private JokerBar jokerBar;

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

        // Where the save is kept, the save itself, the rules it was loaded
        // with, the clock and the meta game over them. All are made in Awake
        // and stay for the scene's life.
        private SaveRepository saves;
        private SaveData save;
        private MetaRules metaRules;
        private SystemTimeProvider clock;
        private MetaGame metaGame;

        // How the level being played was started (D58); kept by a restart.
        private PlayMode mode;

        // What the open confirmation will do once a life is lost.
        private ConfirmedAction pendingConfirm;

        // The gold the campaign win just reported paid, for its panel; null
        // after a free-play win, or when the win could not be reported.
        private int? campaignGoldEarned;

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

        // Null while the menu shows: no level is loaded.
        private LevelRun run;

        // The session whose time bonuses the HUD shows; null while not
        // listening, so a subscription is never made twice or left behind.
        private LevelSession listenedSession;

        // The Rocket or Broom selected and not yet used (D67); never saved.
        private readonly JokerSelection jokerSelection = new JokerSelection();

        // The joker bar's buttons, left to right, and what each last showed,
        // so the bar is rebuilt only when it changes (RefreshJokerBar).
        private readonly JokerKind[] barKinds = { JokerKind.Clock, JokerKind.Rocket, JokerKind.Broom };
        private int[] shownJokerCounts;
        private bool hasShownJokers;
        private PlayMode shownJokerMode;
        private bool shownJokersUsable;
        private bool shownClockUsable;
        private int shownJokerGold;

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
            // plain Play, opening on the menu.
            var hasOverride = DevLevelOverride.TryTake(out var overrideName);

            if (!HasEverythingAssigned())
            {
                return;
            }

            isUsable = true;
            backgroundView.Initialize(config);
            hudView.Initialize(config);
            hudView.HideTime();
            hudView.HideLevel();

            // Right after the HUD: everything initialised below goes over it.
            jokerBar.Initialize(config);
            shownJokerCounts = new int[barKinds.Length];

            // Each puts what it builds last among the canvas's children, so
            // this order — after the HUD, which puts itself first — leaves the
            // card over the HUD, the result panel over the card, and the menu
            // over all of them; the menu never shows together with the others.
            // The confirmation comes last and draws over everything; nothing
            // it covers can be open together with it.
            introductionCard.Initialize(config);
            resultPanel.Initialize(config);
            menuScreen.Initialize(config);
            confirmPrompt.Initialize(config);

            boardCamera.orthographic = true;
            boardCamera.clearFlags = CameraClearFlags.SolidColor;
            boardCamera.backgroundColor = config.BackgroundBottom;

            BuildCatalog();
            metaRules = metaConfig.ToRules();
            saves = new SaveRepository(new PlayerPrefsSaveStore(), new JsonSaveCodec(), metaRules);
            LoadSave();
            clock = new SystemTimeProvider();
            metaGame = new MetaGame(save, metaRules, clock);
            freePlayJokers = new FreePlayJokers(metaConfig.FreePlayJokersPerLevel);
            InitializeSound();
            ApplySoundSetting();

            if (!TryGetOverrideLevel(hasOverride, overrideName, out var overrideLevel) || !Load(overrideLevel, PlayMode.FreePlay))
            {
                ShowMenu();
            }
        }

        /// <summary>
        /// The level the editor tools asked this Play session to start on
        /// (<see cref="DevLevelOverride"/>, Module 21), when it is a level file
        /// that loads: it is played at once, skipping the menu. False for a
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
                    $"{warning} It must be a level in Resources/{LevelsResourcePath}; the title screen opens instead.",
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

        /// <summary>Makes the sound player and the menu's sound button follow the save's sound setting.</summary>
        private void ApplySoundSetting()
        {
            if (hasSound)
            {
                soundPlayer.IsOn = save.Sound.IsOn;
            }

            menuScreen.ShowSound(save.Sound.IsOn);
        }

        /// <summary>
        /// The menu's sound button: turns sound and vibration off or on
        /// together and saves the choice at once. Turning off stops what is
        /// playing; turning on affects the next cue. A save that fails is
        /// logged and the game goes on; the choice then lasts for this
        /// session.
        /// </summary>
        private void OnSoundToggleRequested()
        {
            save.Sound.IsOn = !save.Sound.IsOn;
            ApplySoundSetting();
            SaveOrWarn("The sound setting was changed, but saving it failed; it is remembered until the game is closed.");
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
        /// mark and the gold go in one save; a free-play win saves only when
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
                    campaignGoldEarned = metaGame.ReportCampaignWin(ctx.GoldReward).GoldEarned;
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
        /// Rebuilds the meta lines when what they show has changed: on the
        /// menu, the title page's gold and lives and, on the no-lives page,
        /// the time to the next life — that page gives way to the title page
        /// as soon as a life is back; during a campaign level, the HUD's lives
        /// and gold; during free play, the HUD's Free Play and gold, which no
        /// passing second changes. Reads the clock once, and allocates
        /// nothing when nothing changed, so it runs every frame. Reading the
        /// lives settles them, which needs no save (<see cref="MetaGame"/>).
        /// </summary>
        private void RefreshMeta()
        {
            var meta = metaGame.Save;
            var nowUtc = clock.UtcNow;
            var gold = meta.Wallet.Balance;
            var isFreePlay = run != null && mode == PlayMode.FreePlay;
            var count = isFreePlay ? -1 : meta.Lives.CountAt(nowUtc);
            var untilNext = isFreePlay ? null : meta.Lives.UntilNextLifeAt(nowUtc);
            var waitSeconds = untilNext.HasValue ? MetaText.WholeSeconds(untilNext.Value) : -1;
            if (hasShownMeta && count == shownLives && waitSeconds == shownWaitSeconds && gold == shownGold)
            {
                return;
            }

            hasShownMeta = true;
            shownLives = count;
            shownWaitSeconds = waitSeconds;
            shownGold = gold;

            var goldText = MetaText.Gold(gold, config);
            if (isFreePlay)
            {
                hudView.ShowMeta(MetaText.Joined(config.FreePlayLabel, goldText, config));
                return;
            }

            var livesText = MetaText.Lives(count, metaRules.LifeCap, untilNext, config);
            if (run != null)
            {
                hudView.ShowMeta(MetaText.Joined(livesText, goldText, config));
                return;
            }

            menuScreen.ShowMeta(MetaText.Joined(goldText, livesText, config));
            if (menuScreen.IsOpen && menuScreen.Page == MenuPage.NoLives)
            {
                if (count > 0)
                {
                    menuScreen.ShowTitlePage();
                }
                else if (untilNext.HasValue)
                {
                    menuScreen.ShowNextLife(MetaText.NextLife(untilNext.Value, config));
                }
            }
        }

        /// <summary>Makes the next <see cref="RefreshMeta"/> rebuild every meta line, for a new page or level.</summary>
        private void ForgetShownMeta()
        {
            hasShownMeta = false;
        }

        /// <summary>
        /// Opens the menu's title page over the background, with the meta
        /// line read afresh and the level select listing every level of the
        /// level order and its completed mark. With no usable level order the
        /// select is empty, which hides the Levels button.
        /// </summary>
        private void ShowMenu()
        {
            hudView.Hide();
            jokerBar.Hide();
            var entries = catalog != null
                ? MenuLevels.Entries(catalog, save.Completed)
                : Array.Empty<MenuLevelEntry>();
            menuScreen.ShowTitle(entries, string.Empty);
            ForgetShownMeta();
            RefreshMeta();

            fittedScreenWidth = 0;
            fittedScreenHeight = 0;
            FitBackgroundToScreen();
        }

        /// <summary>
        /// Abandons the level being played and opens the menu (Module 23).
        /// Reports nothing: the callers that cost a life have reported it.
        /// Does nothing while the menu already shows.
        /// </summary>
        private void GoToMenu()
        {
            if (LeaveLevel())
            {
                ShowMenu();
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
            confirmPrompt.Hide();
            resultPanel.Hide();
            return true;
        }

        /// <summary>
        /// Play on the title page: the campaign level (D58). With no usable
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
        /// campaign is over: the title page says so and nothing starts, lives
        /// or none. With no life left the no-lives page opens instead
        /// (D57). Starting spends nothing.
        /// </summary>
        private void StartCampaignLevel()
        {
            if (!CampaignLevels.TryGetLevel(catalog, metaGame.CampaignPosition, out var campaignName))
            {
                if (!menuScreen.IsOpen)
                {
                    GoToMenu();
                }

                menuScreen.ShowNotice(config.CampaignDoneNotice);
                return;
            }

            if (!metaGame.CanStartCampaignLevel())
            {
                ShowNoLivesPage();
                return;
            }

            // Every name in the catalog is a level that loaded, kept in
            // levelAssets under that name (BuildCatalog), so this cannot miss.
            Load(levelAssets[campaignName], PlayMode.Campaign);
        }

        /// <summary>
        /// Leaves any level being played and opens the menu's no-lives page:
        /// the time to the next life, and the refill, priced from the rules
        /// and pressable only when the balance covers it (D62).
        /// </summary>
        private void ShowNoLivesPage()
        {
            LeaveLevel();
            ShowMenu();

            var price = metaRules.LivesRefillPrice;
            var untilNext = metaGame.Save.Lives.UntilNextLifeAt(clock.UtcNow);

            // With no life left the lives are never full, so the wait is never
            // null here; a zero wait reads 00:00 and the next refresh moves on.
            menuScreen.ShowNoLives(
                MetaText.NextLife(untilNext ?? TimeSpan.Zero, config),
                MetaText.Refill(price, config),
                metaGame.Save.Wallet.Balance >= price);
            ForgetShownMeta();
        }

        /// <summary>
        /// The no-lives page's refill button (D62): refills the lives and
        /// saves, then opens the title page. Lives that came back meanwhile
        /// and are full cost nothing and open the title page too.
        /// </summary>
        private void OnRefillRequested()
        {
            switch (metaGame.TryRefillLives())
            {
                case PurchaseResult.Bought:
                    SaveOrWarn("The lives were refilled, but saving that failed; the refill is remembered until the game is closed.");
                    menuScreen.ShowTitlePage();
                    break;
                case PurchaseResult.AlreadyFull:
                    menuScreen.ShowTitlePage();
                    break;
                case PurchaseResult.NotEnoughGold:
                    // The button is not pressable then, so this is a bug.
                    Debug.LogError("The refill button was pressed with too little gold for the refill; nothing changes.", this);
                    break;
            }

            ForgetShownMeta();
        }

        /// <summary>A level's button on the level select: that level as free play, whatever is completed (D58).</summary>
        private void OnLevelRequested(string requestedName)
        {
            if (levelAssets.TryGetValue(requestedName, out var asset))
            {
                Load(asset, PlayMode.FreePlay);
                return;
            }

            Debug.LogError($"The level select asked for '{requestedName}', which is not a level that loaded; nothing changes.", this);
        }

        private void OnEnable()
        {
            if (!isUsable)
            {
                return;
            }

            inputController.MoveApplied += OnMoveApplied;
            inputController.RestartRequested += OnRestartRequested;
            hudView.RestartRequested += OnRestartRequested;
            hudView.MenuRequested += OnMenuRequested;
            resultPanel.RestartRequested += OnRestartRequested;
            resultPanel.NextRequested += Next;
            resultPanel.MenuRequested += GoToMenu;
            menuScreen.PlayRequested += OnPlayRequested;
            menuScreen.LevelRequested += OnLevelRequested;
            menuScreen.SoundToggleRequested += OnSoundToggleRequested;
            menuScreen.RefillRequested += OnRefillRequested;
            confirmPrompt.Confirmed += OnConfirmed;
            confirmPrompt.Cancelled += OnConfirmCancelled;
            boardView.LeaveStarted += OnLeaveStarted;
            boardView.ArriveStarted += OnArriveStarted;
            inputController.DragStarted += OnDragStarted;
            inputController.DragDropped += OnDragDropped;
            hudView.ButtonPressed += OnButtonPressed;
            introductionCard.ButtonPressed += OnButtonPressed;
            resultPanel.ButtonPressed += OnButtonPressed;
            menuScreen.ButtonPressed += OnButtonPressed;
            confirmPrompt.ButtonPressed += OnButtonPressed;
            inputController.BoardTapped += OnBoardTapped;
            jokerBar.JokerPressed += OnJokerPressed;
            jokerBar.ButtonPressed += OnButtonPressed;
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
            hudView.RestartRequested -= OnRestartRequested;
            hudView.MenuRequested -= OnMenuRequested;
            resultPanel.RestartRequested -= OnRestartRequested;
            resultPanel.NextRequested -= Next;
            resultPanel.MenuRequested -= GoToMenu;
            menuScreen.PlayRequested -= OnPlayRequested;
            menuScreen.LevelRequested -= OnLevelRequested;
            menuScreen.SoundToggleRequested -= OnSoundToggleRequested;
            menuScreen.RefillRequested -= OnRefillRequested;
            confirmPrompt.Confirmed -= OnConfirmed;
            confirmPrompt.Cancelled -= OnConfirmCancelled;
            boardView.LeaveStarted -= OnLeaveStarted;
            boardView.ArriveStarted -= OnArriveStarted;
            inputController.DragStarted -= OnDragStarted;
            inputController.DragDropped -= OnDragDropped;
            hudView.ButtonPressed -= OnButtonPressed;
            introductionCard.ButtonPressed -= OnButtonPressed;
            resultPanel.ButtonPressed -= OnButtonPressed;
            menuScreen.ButtonPressed -= OnButtonPressed;
            confirmPrompt.ButtonPressed -= OnButtonPressed;
            inputController.BoardTapped -= OnBoardTapped;
            jokerBar.JokerPressed -= OnJokerPressed;
            jokerBar.ButtonPressed -= OnButtonPressed;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            inputController.DevLevelStepRequested -= OnDevLevelStep;
#endif
            StopListeningForTimeBonus();
        }

        private void Update()
        {
            if (run == null)
            {
                // The menu: no level, so nothing ticks; only the background
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

                resultPanel.ShowLoss(config.LossTitle);
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
            RefreshJokerBar();
        }

        /// <summary>
        /// Replaces whatever is being played with <paramref name="asset"/>,
        /// started afresh and played as <paramref name="playMode"/>. Its
        /// countdown starts at once, or — when the level introduces mechanics
        /// — when the last introduction card closes. The menu, if it was
        /// showing, closes and the HUD shows. A level being left reports
        /// nothing, an open confirmation included: only the development tools
        /// replace a level being played. A level that fails to load logs the
        /// reason, changes nothing — a menu that was showing stays — and
        /// answers false.
        /// </summary>
        private bool Load(TextAsset asset, PlayMode playMode)
        {
            if (!TryParse(asset, out var ctx, out var error))
            {
                Debug.LogError($"Level '{asset.name}' failed to load; it is not played. {error}", this);
                return false;
            }

            menuScreen.Hide();
            confirmPrompt.Hide();
            hudView.Show();
            jokerBar.Show();
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
            hudView.CancelTimeBonus();
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

            fittedScreenWidth = 0;
            fittedScreenHeight = 0;
            FitCameraToScreen();

            resultPanel.Hide();
            if (countdown == null)
            {
                hudView.HideTime();
            }

            ShowLevelNumber(asset.name);
            ShowRemainingTime();
            ForgetShownMeta();
            RefreshMeta();
            ForgetShownJokers();
            RefreshJokerBar();

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
        /// True while an introduction card or the confirmation is open: the
        /// one query that holds the pointer and <b>R</b>
        /// (<see cref="InputController"/>) and the HUD's restart and menu
        /// buttons (<see cref="OnRestartRequested"/>,
        /// <see cref="OnMenuRequested"/>). Neither opens during a drag: a card
        /// opens as a level loads, which cancels any drag first, and
        /// <see cref="AskToConfirm"/> cancels it before opening.
        /// </summary>
        private bool IsHeld() => introductionCard.IsOpen || confirmPrompt.IsOpen;

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
        /// only with a life left, opening the no-lives page otherwise. Held
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
                    AskToConfirm(ConfirmedAction.Restart);
                    return;
                }

                if (run.Outcome == LevelOutcome.Won)
                {
                    return;
                }

                if (!metaGame.CanStartCampaignLevel())
                {
                    ShowNoLivesPage();
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
                AskToConfirm(ConfirmedAction.Leave);
                return;
            }

            GoToMenu();
        }

        /// <summary>
        /// Opens the confirmation that <paramref name="action"/> will cost a
        /// life (D57, D64), with the drag cancelled first, then the countdown
        /// stopped where it stands; the board and the HUD's buttons are held
        /// while it is open (<see cref="IsHeld"/>).
        /// </summary>
        private void AskToConfirm(ConfirmedAction action)
        {
            inputController.CancelDrag();
            ClearJokerSelection();
            run.Countdown?.Stop();
            pendingConfirm = action;
            confirmPrompt.Show(action == ConfirmedAction.Leave ? config.LeaveLevelTitle : config.RestartLevelTitle);
        }

        /// <summary>"Keep playing": closes the confirmation and resumes the countdown from the same remaining time.</summary>
        private void OnConfirmCancelled()
        {
            confirmPrompt.Hide();
            if (IsBeingPlayed())
            {
                run.Countdown?.Start();
            }
        }

        /// <summary>
        /// "Lose a life": reports the loss and saves, then leaves or restarts.
        /// A restart needs a life, so with none left after the loss the
        /// no-lives page opens instead.
        /// </summary>
        private void OnConfirmed()
        {
            confirmPrompt.Hide();
            if (!IsBeingPlayed())
            {
                return;
            }

            ReportCampaignLoss();
            if (pendingConfirm == ConfirmedAction.Leave)
            {
                GoToMenu();
            }
            else if (metaGame.CanStartCampaignLevel())
            {
                Restart();
            }
            else
            {
                ShowNoLivesPage();
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
            hudView.CancelTimeBonus();
            boardView.Rebuild(run.Session.State);
            resultPanel.Hide();
            ShowRemainingTime();
            ForgetShownJokers();
            RefreshJokerBar();
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
        /// A press of a joker's button (Module 34). The Clock is used at once
        /// (<see cref="UseClock"/>). The Rocket and the Broom are selected,
        /// unselected or switched (<see cref="JokerSelection.Press"/>) and
        /// nothing is spent (D67). The buttons are not pressable while the
        /// level is not being played or is held; a press that comes then
        /// anyway changes nothing.
        /// </summary>
        private void OnJokerPressed(JokerKind kind)
        {
            if (kind == JokerKind.Clock)
            {
                UseClock();
                return;
            }

            if (!IsBeingPlayed() || IsHeld())
            {
                return;
            }

            jokerSelection.Press(kind);
            jokerBar.ShowSelected(jokerSelection.Selected);
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
                RefreshJokerBar();
                return;
            }

            var seconds = metaRules.ClockBonusSeconds;
            run.Countdown.AddBonus(seconds);
            SaveJokerSpend(JokerKind.Clock);
            ShowTimeBonus(seconds);
            RefreshJokerBar();
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
                RefreshJokerBar();
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

            RefreshJokerBar();
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

        /// <summary>Unselects any joker and shows no button highlighted.</summary>
        private void ClearJokerSelection()
        {
            jokerSelection.Clear();
            jokerBar.ShowSelected(null);
        }

        /// <summary>
        /// True while a Rocket or a Broom is selected: the one query that
        /// turns a press on the board into a tap (<see cref="InputController"/>).
        /// </summary>
        private bool IsAiming() => jokerSelection.Selected.HasValue;

        /// <summary>
        /// Rebuilds the joker bar's buttons when what they show has changed:
        /// the mode, whether the level can use a joker now — being played and
        /// not held — and the Clock too — with a countdown —, the counts held
        /// or left, and in the campaign the gold. So every use, purchase, win,
        /// loss, restart, change of gold, and card or confirmation opening or
        /// closing shows at once. Allocates nothing when nothing changed, so it
        /// runs every frame a level is loaded.
        /// </summary>
        private void RefreshJokerBar()
        {
            if (run == null)
            {
                return;
            }

            var isUsable = IsBeingPlayed() && !IsHeld();
            var isClockUsable = isUsable && run.Countdown != null;
            var gold = metaGame.Save.Wallet.Balance;
            var isCampaign = mode == PlayMode.Campaign;

            var isChanged = !hasShownJokers || mode != shownJokerMode || isUsable != shownJokersUsable
                            || isClockUsable != shownClockUsable || (isCampaign && gold != shownJokerGold);
            for (var i = 0; i < barKinds.Length; i++)
            {
                var count = JokerCount(barKinds[i]);
                if (count != shownJokerCounts[i])
                {
                    isChanged = true;
                    shownJokerCounts[i] = count;
                }
            }

            if (!isChanged)
            {
                return;
            }

            hasShownJokers = true;
            shownJokerMode = mode;
            shownJokersUsable = isUsable;
            shownClockUsable = isClockUsable;
            shownJokerGold = gold;

            for (var i = 0; i < barKinds.Length; i++)
            {
                var kind = barKinds[i];
                var isKindUsable = kind == JokerKind.Clock ? isClockUsable : isUsable;
                var button = isCampaign
                    ? JokerButtons.Campaign(kind, shownJokerCounts[i], metaRules.JokerPrice(kind), gold, isKindUsable, config)
                    : JokerButtons.FreePlay(kind, shownJokerCounts[i], isKindUsable, config);
                jokerBar.ShowButton(kind, button);
            }
        }

        /// <summary>How many <paramref name="kind"/> the level being played can use: held in the campaign, left in free play.</summary>
        private int JokerCount(JokerKind kind) =>
            mode == PlayMode.Campaign ? metaGame.Save.Jokers.CountOf(kind) : freePlayJokers.CountOf(kind);

        /// <summary>Makes the next <see cref="RefreshJokerBar"/> rebuild every button, for a new level or a restart.</summary>
        private void ForgetShownJokers()
        {
            hasShownJokers = false;
        }

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
            hudView.ShowTimeBonus(seconds);
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
            // panel over the menu.
            if (run == null || run.Outcome != LevelOutcome.Won)
            {
                return;
            }

            Debug.Log($"Level '{levelName}' solved.", this);

            // One query decides Next and the closing title together: in the
            // campaign, whether the position the win advanced to holds a
            // level (the campaign's one source of truth); in free play,
            // whether the level order has one after this level.
            bool hasNext;
            bool isLastLevel;
            string goldText = null;
            if (mode == PlayMode.Campaign)
            {
                hasNext = CampaignLevels.TryGetLevel(catalog, metaGame.CampaignPosition, out _);
                isLastLevel = !hasNext;
                if (campaignGoldEarned.HasValue)
                {
                    goldText = MetaText.GoldEarned(campaignGoldEarned.Value, config);
                }
            }
            else
            {
                hasNext = TryGetNextLevel(out _);
                isLastLevel = ResultTitle.IsLastLevel(catalog, levelName, run.Session.Context.LevelId);
            }

            var title = isLastLevel ? config.AllDoneTitle : config.WinTitle;
            var message = isLastLevel ? config.AllDoneMessage : null;

            // A won campaign level is never replayed (D58): its panel has no Restart.
            resultPanel.ShowWin(title, message, goldText, hasNext, mode == PlayMode.FreePlay);
            PlayCue(SoundCue.Win);
            Pulse(HapticMoment.Win);
        }

        /// <summary>The HUD skips the work when the displayed second has not changed, so this runs every frame.</summary>
        private void ShowRemainingTime()
        {
            var countdown = run.Countdown;
            if (countdown != null)
            {
                hudView.ShowTime(countdown.RemainingSeconds);
            }
        }

        /// <summary>
        /// Shows the level number of the file <paramref name="levelFile"/>, or
        /// hides the level pill with a warning saying why it has none. A level
        /// with no number has no place in the level order either, so the same
        /// warning says that it shows no introduction cards.
        /// </summary>
        private void ShowLevelNumber(string levelFile)
        {
            if (catalog != null && catalog.TryGetNumber(levelFile, out var number))
            {
                hudView.ShowLevel(number);
                return;
            }

            hudView.HideLevel();
            var reason = catalog == null
                ? "the level order is unusable"
                : $"it is not one of the levels in Resources/{LevelsResourcePath}";
            Debug.LogWarning(
                $"Level '{levelFile}' has no level number because {reason}; the HUD hides the level pill and the level shows no introduction cards.",
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
                    $"{roster.CatalogError} The level order is unusable, so the level select and Next are hidden and no introduction card shows; Play starts the scene's level.",
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

            if (level == null)
            {
                Debug.LogError("LevelBootstrap: Level is not assigned; nothing is drawn.", this);
                isAssigned = false;
            }

            if (boardCamera == null || boardView == null || backgroundView == null || inputController == null
                || hudView == null || introductionCard == null || resultPanel == null || menuScreen == null
                || confirmPrompt == null || jokerBar == null)
            {
                Debug.LogError(
                    "LevelBootstrap: Board Camera, Board View, Background View, Input Controller, Hud View, Introduction Card, " +
                    "Result Panel, Menu Screen, Confirm Prompt and Joker Bar must all be assigned; nothing is drawn.",
                    this);
                return false;
            }

            var barProblem = jokerBar.MissingCanvas();
            if (barProblem.Length > 0)
            {
                Debug.LogError($"{barProblem} Nothing is drawn.", jokerBar);
                isAssigned = false;
            }

            var menuProblem = menuScreen.MissingCanvas();
            if (menuProblem.Length > 0)
            {
                Debug.LogError($"{menuProblem} Nothing is drawn.", menuScreen);
                isAssigned = false;
            }

            var cardProblem = introductionCard.MissingCanvas();
            if (cardProblem.Length > 0)
            {
                Debug.LogError($"{cardProblem} Nothing is drawn.", introductionCard);
                isAssigned = false;
            }

            var hudProblem = hudView.MissingCanvas();
            if (hudProblem.Length > 0)
            {
                Debug.LogError($"{hudProblem} Nothing is drawn.", hudView);
                isAssigned = false;
            }

            var panelProblem = resultPanel.MissingCanvas();
            if (panelProblem.Length > 0)
            {
                Debug.LogError($"{panelProblem} Nothing is drawn.", resultPanel);
                isAssigned = false;
            }

            var promptProblem = confirmPrompt.MissingCanvas();
            if (promptProblem.Length > 0)
            {
                Debug.LogError($"{promptProblem} Nothing is drawn.", confirmPrompt);
                isAssigned = false;
            }

            return isAssigned;
        }

        /// <summary>What a confirmed prompt does once the life is lost.</summary>
        private enum ConfirmedAction
        {
            /// <summary>Leave the level for the menu.</summary>
            Leave,

            /// <summary>Restart the level, when a life is left for it.</summary>
            Restart
        }
    }
}
