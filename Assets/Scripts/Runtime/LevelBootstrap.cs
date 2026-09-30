using System;
using System.Collections.Generic;
using GateRush.Core;
using GateRush.Serialization;
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
    /// <para><b>Camera fit.</b> The camera keeps free the HUD bands measured
    /// from the safe area's edges: each band plus the unsafe strip on its side
    /// (<see cref="ScreenBands.TryAddInsets"/>). It is refitted when the
    /// screen's size or safe area changes.</para>
    /// </remarks>
    public sealed class LevelBootstrap : MonoBehaviour
    {
        /// <summary>The <c>Resources</c> folder the level order is read from.</summary>
        private const string LevelsResourcePath = "Levels";

        [Tooltip("The first level to play, from Assets/Resources/Levels.")]
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

        [Tooltip("The result panel on the canvas.")]
        [SerializeField] private ResultPanel resultPanel;

        private readonly Dictionary<string, TextAsset> levelAssets = new Dictionary<string, TextAsset>();

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
            if (!HasEverythingAssigned())
            {
                return;
            }

            isUsable = true;
            backgroundView.Initialize(config);
            hudView.Initialize(config);
            hudView.HideTime();
            hudView.HideLevel();
            resultPanel.Initialize(config);
            BuildCatalog();
            Load(level);
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
        /// started afresh with its countdown running. A level that fails to
        /// load logs the reason and changes nothing.
        /// </summary>
        private void Load(TextAsset asset)
        {
            if (!TryParse(asset, out var ctx, out var error))
            {
                Debug.LogError($"Level '{asset.name}' failed to load; it is not played. {error}", this);
                return;
            }

            inputController.CancelDrag();

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
                new DragController(new DragSettings(config.PushThresholdCells, config.FollowRate, config.CornerAssistCells)),
                layout,
                boardView,
                boardCamera);

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

            run.Start();
            ShowRemainingTime();
        }

        private void Restart()
        {
            if (run == null)
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
        /// hides the level pill with a warning saying why it has none.
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
            Debug.LogWarning($"Level '{levelFile}' has no level number because {reason}; the HUD hides the level pill.", this);
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
        /// Reads every level in <c>Resources/Levels</c> for its id and builds
        /// the level order. A file that fails to load is logged and left out;
        /// two files sharing an id are logged and leave no order at all, which
        /// hides Next. Neither stops the current level from playing.
        /// </summary>
        private void BuildCatalog()
        {
            levelAssets.Clear();
            catalog = null;
            var entries = new List<(string name, int levelId)>();

            foreach (var asset in Resources.LoadAll<TextAsset>(LevelsResourcePath))
            {
                if (levelAssets.ContainsKey(asset.name))
                {
                    Debug.LogError(
                        $"Two level files in Resources/{LevelsResourcePath} are named '{asset.name}'; the second is left out of the level order.",
                        this);
                    continue;
                }

                if (!TryParse(asset, out var ctx, out var error))
                {
                    Debug.LogError(
                        $"Level '{asset.name}' in Resources/{LevelsResourcePath} failed to load and is left out of the level order. {error}",
                        this);
                    continue;
                }

                levelAssets.Add(asset.name, asset);
                entries.Add((asset.name, ctx.LevelId));
            }

            try
            {
                catalog = new LevelCatalog(entries);
            }
            catch (ArgumentException e)
            {
                Debug.LogError($"{e.Message} The level order is unusable, so Next is hidden; the current level still plays.", this);
            }
        }

        private static bool TryParse(TextAsset asset, out LevelContext ctx, out string error)
        {
            try
            {
                ctx = LevelSerializer.FromJson(asset.text, asset.name);
                error = null;
                return true;
            }
            catch (Exception e) when (e is LevelSerializationException || e is ArgumentException)
            {
                ctx = null;
                error = e.Message;
                return false;
            }
        }

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
                || hudView == null || resultPanel == null)
            {
                Debug.LogError(
                    "LevelBootstrap: Board Camera, Board View, Background View, Input Controller, Hud View and Result Panel " +
                    "must all be assigned; nothing is drawn.",
                    this);
                return false;
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
