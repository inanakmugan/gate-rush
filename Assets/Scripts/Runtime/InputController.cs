using System;
using GateRush.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GateRush.Runtime
{
    /// <summary>
    /// Wires the Input System to the drag model: pointer presses, moves and
    /// releases go to <see cref="DragController"/>, each step it takes goes to
    /// <see cref="BoardView"/> to play, and the move it produces goes to
    /// <see cref="LevelSession"/>. <b>R</b> asks for a restart. Decides
    /// nothing itself.
    /// </summary>
    /// <remarks>
    /// <para>Mouse and touch share one path through <see cref="Pointer.current"/>:
    /// a touchscreen's primary touch is a pointer like a mouse is. A move the
    /// session rejects is a bug — every drag produces a legal move by
    /// construction — so it is logged as an error, never swallowed.</para>
    /// <para>No drag starts while the view is busy — steps still playing, or a
    /// move being presented — or once the level has ended.</para>
    /// <para><b>Subscription.</b> Awake and OnEnable order across GameObjects
    /// is not guaranteed, so this component's OnEnable may run before the
    /// bootstrap calls <see cref="Initialize"/>. The subscription to
    /// <see cref="DragController.Stepped"/> is therefore made in
    /// <see cref="Initialize"/> as well; OnEnable and OnDisable re-subscribe and
    /// unsubscribe only when a drag controller exists, and a flag keeps the
    /// two paths from subscribing twice.</para>
    /// </remarks>
    public sealed class InputController : MonoBehaviour
    {
        private LevelRun run;
        private DragController drag;
        private BoardLayout layout;
        private BoardView view;
        private Camera boardCamera;
        private bool isSubscribed;

        /// <summary>
        /// Raised after a move from a drag has been applied, with the state it
        /// was applied to, the move, and the push direction when the move was a
        /// push in place (null for a move that arrived). The session's state is
        /// already the new one.
        /// </summary>
        public event Action<BoardState, Move, Direction?> MoveApplied;

        /// <summary>Raised when the player presses <b>R</b>. Any drag in progress has been cancelled.</summary>
        public event Action RestartRequested;

        /// <summary>
        /// Binds the controller to one level, replacing any earlier binding.
        /// Until this is called it ignores input.
        /// </summary>
        public void Initialize(
            LevelRun run, DragController drag, BoardLayout layout, BoardView view, Camera boardCamera)
        {
            Unsubscribe();

            this.run = run;
            this.drag = drag;
            this.layout = layout;
            this.view = view;
            this.boardCamera = boardCamera;

            if (isActiveAndEnabled)
            {
                Subscribe();
            }
        }

        /// <summary>
        /// Abandons any drag in progress and shows its block back at its origin.
        /// Does nothing when no drag is in progress.
        /// </summary>
        public void CancelDrag()
        {
            if (drag == null || !drag.IsDragging)
            {
                return;
            }

            var blockIndex = drag.BlockIndex;
            drag.Cancel();
            view.Snap(blockIndex, run.Session.State.Origins[blockIndex]);
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            CancelDrag();
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (isSubscribed || drag == null)
            {
                return;
            }

            drag.Stepped += OnStepped;
            isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!isSubscribed)
            {
                return;
            }

            drag.Stepped -= OnStepped;
            isSubscribed = false;
        }

        private void OnStepped(Coord origin)
        {
            view.EnqueueStep(origin);
        }

        private void Update()
        {
            if (run == null)
            {
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                CancelDrag();
                RestartRequested?.Invoke();
                return;
            }

            if (run.Outcome != LevelOutcome.None)
            {
                return;
            }

            var pointer = Pointer.current;
            if (pointer == null)
            {
                return;
            }

            var grid = PointerGridPosition(pointer);

            if (pointer.press.wasPressedThisFrame
                && !view.IsBusy
                && drag.TryBegin(run.Session.Context, run.Session.State, grid))
            {
                view.BeginDrag(drag.BlockIndex);
            }

            if (!drag.IsDragging)
            {
                return;
            }

            if (pointer.press.wasReleasedThisFrame)
            {
                Release(grid);
            }
            else
            {
                // The steps it takes reach the view through Stepped.
                drag.Update(grid);
            }
        }

        private void Release(Vector2 grid)
        {
            var session = run.Session;
            var before = session.State;
            var blockIndex = drag.BlockIndex;
            var move = drag.End(grid, out var push);

            if (!move.HasValue)
            {
                // No move means the block ended where it started; the queued
                // steps finish playing it back there.
                return;
            }

            if (session.TryApply(move.Value))
            {
                MoveApplied?.Invoke(before, move.Value, push);
                return;
            }

            Debug.LogError(
                $"Level {session.Context.LevelId}: the resolver rejected {move.Value}, which the drag produced as legal. " +
                "The block is shown back at its origin.",
                this);
            view.Snap(blockIndex, session.State.Origins[blockIndex]);
        }

        /// <summary>
        /// The pointer's fractional grid position. The board is centred on the
        /// view's transform, so the world point is taken relative to it.
        /// </summary>
        private Vector2 PointerGridPosition(Pointer pointer)
        {
            var screen = pointer.position.ReadValue();
            var world = boardCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0f));
            var local = (Vector2)(world - view.transform.position);
            return layout.WorldToGrid(local);
        }
    }
}
