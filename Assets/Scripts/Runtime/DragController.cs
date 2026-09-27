using System;
using GateRush.Core;
using UnityEngine;

namespace GateRush.Runtime
{
    /// <summary>
    /// Turns one pointer drag into at most one <see cref="Move"/>, following the
    /// drag model of M1 and D27: the grabbed block follows the finger one cell
    /// at a time, stops against obstacles, and the release decides the move.
    /// </summary>
    /// <remarks>
    /// <para><b>Core is the only authority.</b> Every step is checked with
    /// <see cref="BlockReachability.IsFootprintLegal"/> against the state the
    /// drag began on, and a push in place with
    /// <see cref="BlockReachability.CanClearInPlace(LevelContext, BoardState, int, Direction)"/>.
    /// Nothing here decides legality on its own, so every move
    /// <see cref="End"/> returns is one <see cref="MoveResolver"/> accepts.</para>
    /// <para><b>Pointer positions</b> are fractional grid positions in cell
    /// units: the integer part (floored) is the cell, the fraction gives a
    /// displacement finer than a cell, which a push needs.</para>
    /// <para><b>No route-finding.</b> The block walks toward the pointer greedily
    /// and waits when both candidate steps are blocked; the player steers it
    /// around an obstacle. Every origin it reaches is legal and connected to its
    /// start by legal single steps, so the released move is reachable by
    /// construction.</para>
    /// </remarks>
    public sealed class DragController
    {
        private readonly float pushThresholdCells;

        private LevelContext ctx;
        private BoardState state;

        // 1 on each axis the grabbed block's MovementAxis permits, 0 on the
        // other. The one projection both the walk target and the push
        // displacement go through, so the two can never disagree about which
        // pointer movement a restricted block ignores.
        private int permitsX;
        private int permitsY;

        private Coord start;
        private Coord origin;
        private Coord grabOffset;
        private Vector2 grabPoint;

        /// <summary>A controller with no drag in progress.</summary>
        /// <param name="pushThresholdCells">
        /// How far, in cells, the pointer must travel from where it grabbed
        /// before a release at the start reads as a push. Comes from
        /// <c>RuntimeConfig</c>.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="pushThresholdCells"/> is not positive: with no
        /// threshold, a tap with no displacement at all would have no direction.
        /// </exception>
        public DragController(float pushThresholdCells)
        {
            if (!(pushThresholdCells > 0f))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(pushThresholdCells), pushThresholdCells, "The push threshold must be positive.");
            }

            this.pushThresholdCells = pushThresholdCells;
        }

        /// <summary>
        /// Raised with the new origin after every single-cell step the block
        /// takes, in order — one <see cref="Update"/> may take several.
        /// Presentation that animates cell by cell listens here.
        /// </summary>
        public event Action<Coord> Stepped;

        /// <summary>The block being dragged, or -1 when no drag is in progress.</summary>
        public int BlockIndex { get; private set; } = -1;

        /// <summary>True between a successful <see cref="TryBegin"/> and <see cref="End"/> or <see cref="Cancel"/>.</summary>
        public bool IsDragging => BlockIndex >= 0;

        /// <summary>
        /// Grabs the living block covering the cell under
        /// <paramref name="pointer"/>, if <see cref="BoardState.CanMove"/> allows
        /// it — a frozen, locked, shuttered or dead block cannot be grabbed.
        /// The offset between that cell and the block's origin is kept, so the
        /// block does not jump to the pointer. A drag already in progress is
        /// abandoned first, whatever the outcome.
        /// </summary>
        public bool TryBegin(LevelContext ctx, BoardState state, Vector2 pointer)
        {
            if (ctx == null)
            {
                throw new ArgumentNullException(nameof(ctx));
            }

            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            Cancel();

            var cell = BoardLayout.CellOf(pointer);
            var blockIndex = BlockAt(ctx, state, cell);
            if (blockIndex < 0 || !state.CanMove(ctx, blockIndex))
            {
                return false;
            }

            this.ctx = ctx;
            this.state = state;
            var axis = ctx.SpecAt(blockIndex).Axis;
            permitsX = axis == MovementAxis.VerticalOnly ? 0 : 1;
            permitsY = axis == MovementAxis.HorizontalOnly ? 0 : 1;
            start = state.Origins[blockIndex];
            origin = start;
            grabOffset = cell - start;
            grabPoint = pointer;
            BlockIndex = blockIndex;
            return true;
        }

        /// <summary>
        /// Walks the block toward the pointer's cell minus the grab offset and
        /// returns its displayed origin. Each step is one cell along an axis the
        /// block's <see cref="MovementAxis"/> permits, taken only if the new
        /// footprint is legal; the axis with the larger remaining distance is
        /// tried first (horizontal on a tie), the other when that is blocked.
        /// When neither is legal the block stops and waits. Every step shortens
        /// the remaining distance by one, so the walk always ends.
        /// </summary>
        /// <exception cref="InvalidOperationException">No drag is in progress.</exception>
        public Coord Update(Vector2 pointer)
        {
            RequireDragging();

            var offset = BoardLayout.CellOf(pointer) - grabOffset - start;
            var target = start + new Coord(offset.X * permitsX, offset.Y * permitsY);

            while (origin != target)
            {
                var dx = target.X - origin.X;
                var dy = target.Y - origin.Y;
                var horizontal = new Coord(Math.Sign(dx), 0);
                var vertical = new Coord(0, Math.Sign(dy));
                var horizontalFirst = Math.Abs(dx) >= Math.Abs(dy);

                if (TryStep(horizontalFirst ? horizontal : vertical)
                    || TryStep(horizontalFirst ? vertical : horizontal))
                {
                    continue;
                }

                break;
            }

            return origin;
        }

        /// <summary>
        /// Ends the drag and returns the move to apply, or null for none.
        /// <list type="bullet">
        /// <item>The block moved: <c>Move(block, displayed origin)</c>. Whether it
        /// clears is the resolver's call (D25).</item>
        /// <item>The block is back at its start: a push candidate. The pointer's
        /// displacement from where it grabbed, projected onto the axes the
        /// block may move along, gives the direction by its dominant axis
        /// (horizontal on a tie) — if it is at least the push threshold.
        /// <c>Move(block, start)</c> is returned only when the block can be
        /// cleared in place by a push that way, through a gate on the edge it
        /// is pushed toward; a push into a wall, a block, or an edge without a
        /// usable gate returns null.</item>
        /// </list>
        /// </summary>
        /// <exception cref="InvalidOperationException">No drag is in progress.</exception>
        public Move? End(Vector2 pointer)
        {
            Update(pointer);

            var blockIndex = BlockIndex;
            Move? result = null;

            if (origin != start)
            {
                result = new Move(blockIndex, origin);
            }
            else if (TryPushDirection(pointer - grabPoint, out var push)
                     && BlockReachability.CanClearInPlace(ctx, state, blockIndex, push))
            {
                result = new Move(blockIndex, start);
            }

            Cancel();
            return result;
        }

        /// <summary>Abandons any drag in progress without producing a move.</summary>
        public void Cancel()
        {
            BlockIndex = -1;
            ctx = null;
            state = null;
        }

        /// <summary>
        /// The living block covering <paramref name="cell"/>, or -1. A linear
        /// scan over the block slots: it runs once per pointer press, not in a
        /// search.
        /// </summary>
        private static int BlockAt(LevelContext ctx, BoardState state, Coord cell)
        {
            for (var i = 0; i < ctx.TotalBlockCapacity; i++)
            {
                if (!state.Alive[i])
                {
                    continue;
                }

                var blockOrigin = state.Origins[i];
                var cells = ctx.SpecAt(i).Cells;
                for (var c = 0; c < cells.Count; c++)
                {
                    if (blockOrigin + cells[c] == cell)
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        /// <summary>
        /// Takes one step if it is a real step (non-zero) and the footprint there
        /// is legal. The caller only ever offers steps toward a target whose
        /// forbidden-axis component was projected away, so an axis-restricted
        /// block is offered only zero steps in a forbidden direction.
        /// </summary>
        private bool TryStep(Coord step)
        {
            if (step.X == 0 && step.Y == 0)
            {
                return false;
            }

            var next = origin + step;
            if (!BlockReachability.IsFootprintLegal(ctx, state, BlockIndex, next))
            {
                return false;
            }

            origin = next;
            Stepped?.Invoke(origin);
            return true;
        }

        /// <summary>
        /// The push direction of a pointer displacement: projected onto the
        /// block's permitted axes like every step, then its dominant axis.
        /// False when the projected displacement is below the threshold.
        /// </summary>
        private bool TryPushDirection(Vector2 displacement, out Direction push)
        {
            var x = displacement.x * permitsX;
            var y = displacement.y * permitsY;

            if (Math.Abs(x) >= Math.Abs(y))
            {
                push = x > 0f ? Direction.Right : Direction.Left;
                return Math.Abs(x) >= pushThresholdCells;
            }

            push = y > 0f ? Direction.Up : Direction.Down;
            return Math.Abs(y) >= pushThresholdCells;
        }

        private void RequireDragging()
        {
            if (!IsDragging)
            {
                throw new InvalidOperationException("No drag is in progress.");
            }
        }
    }
}
