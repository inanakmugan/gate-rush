using System;
using System.Collections.Generic;
using GateRush.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace GateRush.Runtime
{
    /// <summary>
    /// Wires the Input System to the drag model: pointer presses, moves and
    /// releases go to <see cref="DragController"/>, the block's continuous
    /// position and its settle on release go to <see cref="BoardView"/>, and
    /// the move the release produces goes to <see cref="LevelSession"/>.
    /// <b>R</b> asks for a restart. Decides nothing itself.
    /// </summary>
    /// <remarks>
    /// <para>Mouse and touch share one path through <see cref="Pointer.current"/>:
    /// a touchscreen's primary touch is a pointer like a mouse is. A move the
    /// session rejects is a bug — every drag produces a legal move by
    /// construction — so it is logged as an error, never swallowed.</para>
    /// <para>No drag starts while the view is busy — a block settling, or a
    /// move being presented — or once the level has ended. While the level is
    /// held for an introduction card (Module 19) no input is read at all,
    /// <b>R</b> included — except, in the editor and development builds, the
    /// level keys of <c>DevKeys</c> (Module 21).</para>
    /// <para><b>UI first.</b> A press on the HUD or the result panel belongs to
    /// the UI and never also starts a drag. On the frame of a press the
    /// pointer's position is raycast against the UI directly, rather than asked
    /// of the input module, whose state on the first frame of a touch may still
    /// be the previous frame's, depending on script order. With no
    /// EventSystem, nothing is UI.</para>
    /// <para>The drag advances on <see cref="Time.unscaledDeltaTime"/>, the
    /// clock the countdown runs on.</para>
    /// </remarks>
    public sealed class InputController : MonoBehaviour
    {
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();

        private PointerEventData uiPointer;
        private EventSystem uiPointerSystem;

        private LevelRun run;
        private DragController drag;
        private BoardLayout layout;
        private BoardView view;
        private Camera boardCamera;
        private Func<bool> isHeld;

        /// <summary>
        /// Raised after a move from a drag has been applied, with the state it
        /// was applied to, the move, and the push direction when the move was a
        /// push in place (null for a move that arrived). The session's state is
        /// already the new one.
        /// </summary>
        public event Action<BoardState, Move, Direction?> MoveApplied;

        /// <summary>Raised when the player presses <b>R</b>. Any drag in progress has been cancelled.</summary>
        public event Action RestartRequested;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Raised when a development level key (<see cref="DevKeys"/>) goes
        /// down, with <see cref="DevKeys.NextStep"/> or
        /// <see cref="DevKeys.PreviousStep"/> — at any moment a level is
        /// bound, a card or a result included. Editor and development builds
        /// only (Module 21).
        /// </summary>
        public event Action<int> DevLevelStepRequested;
#endif

        /// <summary>
        /// Binds the controller to one level, replacing any earlier binding.
        /// Until this is called, and after <see cref="Unbind"/>, it ignores
        /// input.
        /// </summary>
        /// <param name="isHeld">
        /// Asked every frame: while it answers true — an introduction card is
        /// open (Module 19) — neither <b>R</b> nor the pointer is read. The
        /// caller must not start holding during a drag; it cancels any drag
        /// first.
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="isHeld"/> is null.</exception>
        public void Initialize(
            LevelRun run, DragController drag, BoardLayout layout, BoardView view, Camera boardCamera, Func<bool> isHeld)
        {
            this.isHeld = isHeld ?? throw new ArgumentNullException(nameof(isHeld));
            this.run = run;
            this.drag = drag;
            this.layout = layout;
            this.view = view;
            this.boardCamera = boardCamera;
        }

        /// <summary>
        /// Lets go of the level this controller was bound to, cancelling any
        /// drag in progress first: from here until the next
        /// <see cref="Initialize"/> no input is read at all — no pointer, no
        /// <b>R</b>, no development key — because there is no board to act on
        /// (the menu, Module 23). Does nothing when nothing is bound, so it is
        /// safe to call more than once.
        /// </summary>
        public void Unbind()
        {
            CancelDrag();
            run = null;
            drag = null;
            layout = null;
            view = null;
            boardCamera = null;
            isHeld = null;
        }

        /// <summary>
        /// Abandons any drag in progress and shows its block back at its origin.
        /// Does nothing when no drag is in progress. A view that has already
        /// been destroyed is left alone: <see cref="OnDisable"/> runs this
        /// while the scene is torn down, when the view's objects may be gone
        /// before this component is disabled.
        /// </summary>
        public void CancelDrag()
        {
            if (drag == null || !drag.IsDragging)
            {
                return;
            }

            var blockIndex = drag.BlockIndex;
            drag.Cancel();
            if (view != null)
            {
                view.Snap(blockIndex, run.Session.State.Origins[blockIndex]);
            }
        }

        private void OnDisable()
        {
            CancelDrag();
        }

        private void Update()
        {
            if (run == null)
            {
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Before the hold and the outcome checks below: the level keys
            // work during a card and after a result too.
            if (DevKeys.TryReadLevelStep(Keyboard.current, out var levelStep))
            {
                DevLevelStepRequested?.Invoke(levelStep);
                return;
            }
#endif

            if (isHeld())
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

            var screen = pointer.position.ReadValue();
            var grid = ScreenToGrid(screen);

            if (pointer.press.wasPressedThisFrame
                && !view.IsBusy
                && !IsOverUi(screen)
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
                view.ShowDragged(drag.Update(grid, Time.unscaledDeltaTime), drag.Pull);
            }
        }

        private void Release(Vector2 grid)
        {
            var session = run.Session;
            var before = session.State;
            var blockIndex = drag.BlockIndex;
            var move = drag.End(grid, out var push);

            // Settle before applying: the presentation the move starts waits
            // for the settle to finish. No move settles the block back at its
            // start.
            view.Settle(move.HasValue ? move.Value.TargetOrigin : before.Origins[blockIndex]);

            if (!move.HasValue)
            {
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
        /// True when a raycast target of the UI — a HUD element, the result
        /// panel's backdrop or buttons — lies under <paramref name="screen"/>.
        /// </summary>
        private bool IsOverUi(Vector2 screen)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            if (uiPointerSystem != eventSystem)
            {
                uiPointer = new PointerEventData(eventSystem);
                uiPointerSystem = eventSystem;
            }

            uiPointer.position = screen;
            uiHits.Clear();
            eventSystem.RaycastAll(uiPointer, uiHits);
            var isOverUi = uiHits.Count > 0;
            uiHits.Clear();
            return isOverUi;
        }

        /// <summary>
        /// The fractional grid position under a screen point. The board is
        /// centred on the view's transform, so the world point is taken
        /// relative to it.
        /// </summary>
        private Vector2 ScreenToGrid(Vector2 screen)
        {
            var world = boardCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0f));
            var local = (Vector2)(world - view.transform.position);
            return layout.WorldToGrid(local);
        }
    }
}
