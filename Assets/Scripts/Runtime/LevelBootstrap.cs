using System;
using System.Collections.Generic;
using GateRush.Core;
using UnityEngine;

namespace GateRush.Runtime
{
    /// <summary>
    /// Builds one playable level from a JSON <see cref="TextAsset"/>: the
    /// <see cref="LevelSession"/> and its <see cref="LevelRun"/> with the
    /// countdown, the plain helpers they need, and the views that draw them.
    /// Runs the countdown, feeds the HUD, shows the result panel, and handles
    /// Restart and Next. The first level is <see cref="level"/>; swap it by
    /// dragging a different JSON onto it. Holds all per-play state on this
    /// instance — Enter Play Mode runs without a domain reload, so nothing here
    /// is static.
    /// </summary>
    /// <remarks>
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
    /// editor tools asked for (<see cref="DevLevelOverride"/>) replaces
    /// <see cref="level"/> for one Play session; the field itself is never
    /// written. In the editor and in development builds, Page Down and Page Up
    /// (<c>DevKeys</c>) go to the next and previous level through the same
    /// load as Next. A release build has neither.</para>
    /// <para><b>Camera fit.</b> The camera keeps free the HUD bands measured
    /// from the safe area's edges: each band plus the unsafe strip on its side
    /// (<see cref="ScreenBands.TryAddInsets"/>). It is refitted when the
    /// screen's size or safe area changes.</para>
    /// </remarks>
    public sealed class LevelBootstrap : MonoBehaviour
    {
        /// <summary>
        /// The <c>Resources</c> folder the level order is read from. Public so
        /// the Play Level window (Module 21) reads the same folder.
        /// </summary>
        public const string LevelsResourcePath = "Levels";

        [Tooltip("The first level to play, from Assets/Resources/Levels. The Level Editor's Play button and Gate Rush > Play Level… start on another level for one Play session without changing this. In the editor and in development builds, Page Down goes to the next level and Page Up to the previous one, in level order, at any moment; at the last or first level the key only logs a line. A release build has neither.")]
        [SerializeField] private TextAsset level;

        [SerializeField] private RuntimeConfig config;

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

        private readonly Dictionary<string, TextAsset> levelAssets = new Dictionary<string, TextAsset>();

        // What each level in the catalog contains, in the catalog's order:
        // entry i belongs to the level the catalog numbers i + 1. Empty while
        // there is no catalog.
        private IReadOnlyList<IReadOnlyCollection<LevelMechanic>> mechanicsInOrder =
            Array.Empty<IReadOnlyCollection<LevelMechanic>>();

        private bool isUsable;
        private LevelCatalog catalog;
        private LevelRun run;

        // The session whose time bonuses the HUD shows; null while not
        // listening, so a subscription is never made twice or left behind.
        private LevelSession listenedSession;

        // Moves applied in this attempt at the level; seeds the bursts.
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
            // even a scene that draws nothing leaves the next plain Play on
            // the scene's level.
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

            // Each puts what it builds last among the canvas's children, so
            // this order — after the HUD, which puts itself first — leaves the
            // card over the HUD and the result panel over the card.
            introductionCard.Initialize(config);
            resultPanel.Initialize(config);
            BuildCatalog();
            Load(StartLevel(hasOverride, overrideName));
        }

        /// <summary>
        /// The level this Play session starts on: the one the editor tools
        /// asked for (<see cref="DevLevelOverride"/>, Module 21) when it is a
        /// level file that loads, otherwise the scene's <see cref="level"/>.
        /// An override naming no such file is reported and ignored. The
        /// serialized field is never written, and outside the editor there is
        /// never an override.
        /// </summary>
        private TextAsset StartLevel(bool hasOverride, string overrideName)
        {
#if UNITY_EDITOR
            if (hasOverride)
            {
                if (DevLevelOverride.TryResolve(overrideName, levelAssets.Keys, out var warning))
                {
                    return levelAssets[overrideName];
                }

                Debug.LogWarning(
                    $"{warning} It must be a level in Resources/{LevelsResourcePath}; the scene's level '{level.name}' is played.",
                    this);
            }
#endif
            return level;
        }

        private void OnEnable()
        {
            if (!isUsable)
            {
                return;
            }

            inputController.MoveApplied += OnMoveApplied;
            inputController.RestartRequested += Restart;
            hudView.RestartRequested += Restart;
            resultPanel.RestartRequested += Restart;
            resultPanel.NextRequested += Next;
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
            inputController.RestartRequested -= Restart;
            hudView.RestartRequested -= Restart;
            resultPanel.RestartRequested -= Restart;
            resultPanel.NextRequested -= Next;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            inputController.DevLevelStepRequested -= OnDevLevelStep;
#endif
            StopListeningForTimeBonus();
        }

        private void Update()
        {
            if (run == null)
            {
                return;
            }

            FitCameraToScreen();

            var wasPlaying = run.Outcome == LevelOutcome.None;
            run.Tick(Time.unscaledDeltaTime);
            if (wasPlaying && run.Outcome == LevelOutcome.Lost)
            {
                inputController.CancelDrag();
                resultPanel.ShowLoss(config.LossTitle);
            }

            ShowRemainingTime();
        }

        /// <summary>
        /// Replaces whatever is being played with <paramref name="asset"/>,
        /// started afresh. Its countdown starts at once, or — when the level
        /// introduces mechanics — when the last introduction card closes. A
        /// level that fails to load logs the reason and changes nothing.
        /// </summary>
        private void Load(TextAsset asset)
        {
            if (!TryParse(asset, out var ctx, out var error))
            {
                Debug.LogError($"Level '{asset.name}' failed to load; it is not played. {error}", this);
                return;
            }

            inputController.CancelDrag();

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

            boardView.Initialize(config, ctx, layout, new VisibilityLayer(ctx), machine);
            boardView.Rebuild(session.State);
            inputController.Initialize(
                run,
                new DragController(config.CreateDragSettings()),
                layout,
                boardView,
                boardCamera,
                IsIntroducing);

            boardCamera.orthographic = true;
            boardCamera.clearFlags = CameraClearFlags.SolidColor;
            boardCamera.backgroundColor = config.BackgroundBottom;

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
        /// True while an introduction card is open: the one query that holds
        /// the pointer and <b>R</b> (<see cref="InputController"/>) and the HUD's
        /// restart button (<see cref="Restart"/>).
        /// </summary>
        private bool IsIntroducing() => introductionCard.IsOpen;

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

        private void Restart()
        {
            if (run == null || IsIntroducing())
            {
                return;
            }

            inputController.CancelDrag();
            run.Restart();
            moveNumber = 0;
            hudView.CancelTimeBonus();
            boardView.Rebuild(run.Session.State);
            resultPanel.Hide();
            ShowRemainingTime();
        }

        private void Next()
        {
            if (TryGetNextLevel(out var next))
            {
                Load(next);
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
            Load(levelAssets[neighbour]);
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
            moveNumber++;

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

            boardView.Present(after, changes, moveNumber, OnPresentationDone);
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
            if (run.Countdown != null && run.Outcome == LevelOutcome.None)
            {
                hudView.ShowTimeBonus(seconds);
            }
        }

        /// <summary>
        /// The board now shows the state a move produced. A win was decided —
        /// and the countdown stopped — the moment that state was applied; the
        /// panel waits until the player has seen the block leave.
        /// </summary>
        private void OnPresentationDone()
        {
            if (run.Outcome != LevelOutcome.Won)
            {
                return;
            }

            Debug.Log($"Level '{levelName}' solved.", this);
            var title = ResultTitle.ForWin(
                catalog, levelName, run.Session.Context.LevelId, config.WinTitle, config.AllDoneTitle);
            resultPanel.ShowWin(title, TryGetNextLevel(out _));
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

        private bool TryGetNextLevel(out TextAsset next)
        {
            next = null;
            return catalog != null
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
                    $"{roster.CatalogError} The level order is unusable, so Next is hidden and no introduction card shows; the current level still plays.",
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

            if (level == null)
            {
                Debug.LogError("LevelBootstrap: Level is not assigned; nothing is drawn.", this);
                isAssigned = false;
            }

            if (boardCamera == null || boardView == null || backgroundView == null || inputController == null
                || hudView == null || introductionCard == null || resultPanel == null)
            {
                Debug.LogError(
                    "LevelBootstrap: Board Camera, Board View, Background View, Input Controller, Hud View, Introduction Card " +
                    "and Result Panel must all be assigned; nothing is drawn.",
                    this);
                return false;
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

            return isAssigned;
        }
    }
}
