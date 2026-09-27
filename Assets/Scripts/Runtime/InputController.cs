using UnityEngine;
using UnityEngine.InputSystem;

namespace GateRush.Runtime
{
    /// <summary>
    /// Wires the Input System to the drag model: pointer presses, moves and
    /// releases go to <see cref="DragController"/>, the move it produces goes to
    /// <see cref="LevelSession"/>, and the displayed origin goes to
    /// <see cref="BoardView"/>. <b>R</b> restarts the level. Decides nothing
    /// itself.
    /// </summary>
    /// <remarks>
    /// Mouse and touch share one path through <see cref="Pointer.current"/>:
    /// a touchscreen's primary touch is a pointer like a mouse is. A move the
    /// session rejects is a bug — every drag produces a legal move by
    /// construction — so it is logged as an error, never swallowed.
    /// </remarks>
    public sealed class InputController : MonoBehaviour
    {
        private LevelSession session;
        private DragController drag;
        private BoardLayout layout;
        private BoardView view;
        private Camera boardCamera;

        /// <summary>Binds the controller to one level. Until this is called it ignores input.</summary>
        public void Initialize(
            LevelSession session, DragController drag, BoardLayout layout, BoardView view, Camera boardCamera)
        {
            this.session = session;
            this.drag = drag;
            this.layout = layout;
            this.view = view;
            this.boardCamera = boardCamera;
        }

        private void OnDisable()
        {
            if (drag != null && drag.IsDragging)
            {
                CancelDrag();
            }
        }

        private void Update()
        {
            if (session == null)
            {
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                drag.Cancel();
                session.Restart();
                return;
            }

            var pointer = Pointer.current;
            if (pointer == null)
            {
                return;
            }

            var grid = PointerGridPosition(pointer);

            if (pointer.press.wasPressedThisFrame)
            {
                drag.TryBegin(session.Context, session.State, grid);
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
                view.ShowDragOrigin(drag.BlockIndex, drag.Update(grid));
            }
        }

        private void Release(Vector2 grid)
        {
            var blockIndex = drag.BlockIndex;
            var move = drag.End(grid);

            if (move.HasValue && session.TryApply(move.Value))
            {
                // StateChanged has redrawn the board.
                return;
            }

            if (move.HasValue)
            {
                Debug.LogError(
                    $"Level {session.Context.LevelId}: the resolver rejected {move.Value}, which the drag produced as legal. " +
                    "The block is shown back at its origin.",
                    this);
            }

            view.ShowDragOrigin(blockIndex, session.State.Origins[blockIndex]);
        }

        private void CancelDrag()
        {
            var blockIndex = drag.BlockIndex;
            drag.Cancel();
            view.ShowDragOrigin(blockIndex, session.State.Origins[blockIndex]);
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
