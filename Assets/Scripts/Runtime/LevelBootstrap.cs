using System;
using System.Collections.Generic;
using System.Globalization;
using GateRush.Core;
using GateRush.Serialization;
using TMPro;
using UnityEngine;

namespace GateRush.Runtime
{
    /// <summary>
    /// Builds one playable level from a JSON <see cref="TextAsset"/>: the
    /// <see cref="LevelSession"/> and its <see cref="LevelRun"/> with the
    /// countdown, the plain helpers they need, and the views that draw them.
    /// Runs the countdown, shows the result panel, and handles Restart and
    /// Next. The first level is <see cref="level"/>; swap it by dragging a
    /// different JSON onto it. Holds all per-play state on this instance —
    /// Enter Play Mode runs without a domain reload, so nothing here is static.
    /// </summary>
    /// <remarks>
    /// <para>A level or config that cannot be used logs the reason and draws
    /// nothing. A level whose time budget is not positive logs an error and
    /// plays without a countdown, so an unfinished level stays testable.</para>
    /// <para><b>Next</b> follows <see cref="LevelCatalog"/> over every JSON in
    /// <c>Resources/Levels</c>, loaded once at start. A file there that fails
    /// to load is logged and left out; two files sharing a level id are
    /// logged and hide Next altogether. Neither stops the current level from
    /// playing.</para>
    /// </remarks>
    public sealed class LevelBootstrap : MonoBehaviour
    {
        /// <summary>The <c>Resources</c> folder the level order is read from.</summary>
        private const string LevelsResourcePath = "Levels";

        [Tooltip("The first level to play, from Assets/Resources/Levels.")]
        [SerializeField] private TextAsset level;

        [SerializeField] private RuntimeConfig config;

        [Tooltip("The orthographic camera that shows the board. It is placed so the Board View sits centred between the HUD bands, and re-fitted when the screen size changes.")]
        [SerializeField] private Camera boardCamera;

        [SerializeField] private BoardView boardView;

        [Tooltip("Draws the background gradient and vignette; stretched over the camera's view whenever the camera is fitted.")]
        [SerializeField] private BackgroundView backgroundView;
        [SerializeField] private InputController inputController;

        [Tooltip("Shows the remaining time in whole seconds at the top of the screen. Hidden for a level without a countdown.")]
        [SerializeField] private TMP_Text timerLabel;

        [SerializeField] private ResultPanel resultPanel;

        private readonly Dictionary<string, TextAsset> levelAssets = new Dictionary<string, TextAsset>();

        private bool isUsable;
        private LevelCatalog catalog;
        private LevelRun run;
        private BoardLayout layout;
        private string levelName;
        private int shownSeconds = -1;
        private int fittedScreenWidth;
        private int fittedScreenHeight;

        private void Awake()
        {
            if (!HasEverythingAssigned())
            {
                return;
            }

            isUsable = true;
            backgroundView.Initialize(config);
            resultPanel.Hide();
            timerLabel.gameObject.SetActive(false);
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
            resultPanel.RestartRequested += Restart;
            resultPanel.NextRequested += Next;
        }

        private void OnDisable()
        {
            if (!isUsable)
            {
                return;
            }

            inputController.MoveApplied -= OnMoveApplied;
            inputController.RestartRequested -= Restart;
            resultPanel.RestartRequested -= Restart;
            resultPanel.NextRequested -= Next;
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
            run = new LevelRun(session, countdown);
            levelName = asset.name;
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
            timerLabel.gameObject.SetActive(countdown != null);
            shownSeconds = -1;

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
        /// changed, so the view must be handed the new state whatever happens:
        /// a diff that fails — a bug, since the resolver cleared the block at a
        /// gate the diff cannot find — is logged and the board is redrawn
        /// without a clear effect rather than left showing the old picture.
        /// </summary>
        private void OnMoveApplied(BoardState before, Move move, Direction? push)
        {
            var session = run.Session;
            IReadOnlyList<ClearedBlock> clears;
            try
            {
                clears = ResolutionDiff.Between(session.Context, before, session.State, move, push);
            }
            catch (InvalidOperationException e)
            {
                Debug.LogError(
                    $"Level '{levelName}': the clear effect for {move} (push {push?.ToString() ?? "none"}) could not be worked out; " +
                    $"the board is redrawn from the new state without it. {e.Message}",
                    this);
                clears = Array.Empty<ClearedBlock>();
            }

            boardView.Present(session.State, clears, OnPresentationDone);
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
            resultPanel.ShowWin(config.WinTitle, TryGetNextLevel(out _));
        }

        private void ShowRemainingTime()
        {
            var countdown = run.Countdown;
            if (countdown == null || countdown.WholeSecondsRemaining == shownSeconds)
            {
                return;
            }

            shownSeconds = countdown.WholeSecondsRemaining;
            timerLabel.text = shownSeconds.ToString(CultureInfo.InvariantCulture);
        }

        private void FitCameraToScreen()
        {
            if (Screen.width == fittedScreenWidth && Screen.height == fittedScreenHeight)
            {
                return;
            }

            fittedScreenWidth = Screen.width;
            fittedScreenHeight = Screen.height;
            if (fittedScreenWidth <= 0 || fittedScreenHeight <= 0)
            {
                return;
            }

            var top = config.TopBandScreenFraction;
            var bottom = config.BottomBandScreenFraction;
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
                || timerLabel == null || resultPanel == null)
            {
                Debug.LogError(
                    "LevelBootstrap: Board Camera, Board View, Background View, Input Controller, Timer Label and Result Panel " +
                    "must all be assigned; nothing is drawn.",
                    this);
                return false;
            }

            var panelProblem = resultPanel.MissingReferences();
            if (panelProblem.Length > 0)
            {
                Debug.LogError($"{panelProblem} Nothing is drawn.", resultPanel);
                isAssigned = false;
            }

            return isAssigned;
        }
    }
}
