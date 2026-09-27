using System;
using GateRush.Core;
using GateRush.Serialization;
using UnityEngine;

namespace GateRush.Runtime
{
    /// <summary>
    /// Builds one playable level from a JSON <see cref="TextAsset"/>: the
    /// <see cref="LevelSession"/>, the plain helpers it needs, and the views
    /// that draw it. Swap the level by dragging a different JSON onto
    /// <see cref="level"/>. Holds all per-play state on this instance — Enter
    /// Play Mode runs without a domain reload, so nothing here is static.
    /// </summary>
    /// <remarks>
    /// A level or config that cannot be used logs the reason and draws
    /// nothing. The first time the level reads solved, one line is logged;
    /// a restart re-arms it.
    /// </remarks>
    public sealed class LevelBootstrap : MonoBehaviour
    {
        [Tooltip("The level JSON to play, from Assets/Resources/Levels.")]
        [SerializeField] private TextAsset level;

        [SerializeField] private RuntimeConfig config;

        [Tooltip("The orthographic camera that shows the board. It is centred on the Board View and re-fitted when the screen size changes.")]
        [SerializeField] private Camera boardCamera;

        [SerializeField] private BoardView boardView;
        [SerializeField] private InputController inputController;

        private LevelSession session;
        private BoardLayout layout;
        private bool isWinLogged;
        private int fittedScreenWidth;
        private int fittedScreenHeight;

        private void Awake()
        {
            if (!HasEverythingAssigned())
            {
                return;
            }

            LevelContext ctx;
            try
            {
                ctx = LevelSerializer.FromJson(level.text, level.name);
            }
            catch (Exception e) when (e is LevelSerializationException || e is ArgumentException)
            {
                Debug.LogError($"Level '{level.name}' failed to load; nothing is drawn. {e.Message}", this);
                return;
            }

            LogLockIdsOutsideBadgePalette(ctx);

            session = new LevelSession(ctx);
            layout = new BoardLayout(ctx.Width, ctx.Height, config.CellSize);

            boardView.Initialize(config, ctx, layout, new VisibilityLayer(ctx));
            inputController.Initialize(
                session, new DragController(config.PushThresholdCells), layout, boardView, boardCamera);

            boardCamera.orthographic = true;
            boardCamera.clearFlags = CameraClearFlags.SolidColor;
            boardCamera.backgroundColor = config.BackgroundColor;
            var boardCenter = boardView.transform.position;
            boardCamera.transform.position = new Vector3(boardCenter.x, boardCenter.y, boardCamera.transform.position.z);
        }

        private void OnEnable()
        {
            if (session == null)
            {
                return;
            }

            session.StateChanged += OnStateChanged;
            OnStateChanged();
        }

        private void OnDisable()
        {
            if (session == null)
            {
                return;
            }

            session.StateChanged -= OnStateChanged;
        }

        private void Update()
        {
            if (layout == null || (Screen.width == fittedScreenWidth && Screen.height == fittedScreenHeight))
            {
                return;
            }

            fittedScreenWidth = Screen.width;
            fittedScreenHeight = Screen.height;
            if (fittedScreenWidth > 0 && fittedScreenHeight > 0)
            {
                boardCamera.orthographicSize = layout.FitOrthographicSize(
                    (float)fittedScreenWidth / fittedScreenHeight, config.CameraMarginCells);
            }
        }

        private void OnStateChanged()
        {
            boardView.Rebuild(session.State);

            if (!session.IsSolved)
            {
                isWinLogged = false;
                return;
            }

            if (!isWinLogged)
            {
                isWinLogged = true;
                Debug.Log($"Level '{level.name}' solved.", this);
            }
        }

        private bool HasEverythingAssigned()
        {
            var isUsable = true;

            if (config == null)
            {
                Debug.LogError("LevelBootstrap: Config is not assigned; nothing is drawn.", this);
                return false;
            }

            foreach (var problem in config.Problems())
            {
                Debug.LogError($"{problem} Nothing is drawn.", this);
                isUsable = false;
            }

            if (level == null)
            {
                Debug.LogError("LevelBootstrap: Level is not assigned; nothing is drawn.", this);
                isUsable = false;
            }

            if (boardCamera == null || boardView == null || inputController == null)
            {
                Debug.LogError(
                    "LevelBootstrap: Board Camera, Board View and Input Controller must all be assigned; nothing is drawn.",
                    this);
                isUsable = false;
            }

            return isUsable;
        }

        /// <summary>
        /// M8: the lock identifier doubles as the badge colour, so the palette is
        /// indexed by lock id. An id outside it is an error worth a message at
        /// load rather than a silent colour at draw time; the lock and its keys
        /// are then drawn in the unknown-badge colour.
        /// </summary>
        private void LogLockIdsOutsideBadgePalette(LevelContext ctx)
        {
            var owners = ctx.LockOwnerIndices;
            for (var i = 0; i < owners.Count; i++)
            {
                var lockId = ctx.SpecAt(owners[i]).LockId.Value;
                if (!config.TryGetBadgeColor(lockId, out _))
                {
                    Debug.LogError(
                        $"Level '{level.name}': lock {lockId} (block slot {owners[i]}) has no entry in " +
                        $"{config.name}'s lock badge palette; its lock and key badges are drawn in the unknown-badge colour.",
                        this);
                }
            }
        }
    }
}
