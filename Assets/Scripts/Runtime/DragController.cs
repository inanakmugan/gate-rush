using System;
using GateRush.Core;
using UnityEngine;

namespace GateRush.Runtime
{
    /// <summary>
    /// Turns one pointer drag into at most one <see cref="Move"/>, following the
    /// free drag of Module 13 and D44: the grabbed block floats under the
    /// finger at a continuous position, stops flush against whatever is in its
    /// way, and the release settles it into the nearest cell, which is the move.
    /// </summary>
    /// <remarks>
    /// <para><b>Positions</b> are continuous block origins in cell units. Along
    /// each axis a coordinate overlaps the whole cells <c>floor</c> and
    /// <c>ceil</c> of it — one when it is whole, two otherwise — so a position
    /// overlaps at most four whole-cell origins.</para>
    /// <para><b>Core is the only authority.</b> A position is legal exactly when
    /// every whole-cell origin it overlaps passes
    /// <see cref="BlockReachability.IsFootprintLegal"/> against the state the
    /// drag began on, and every position <see cref="Update"/> produces is
    /// legal. A push in place is decided by
    /// <see cref="BlockReachability.CanClearInPlace(LevelContext, BoardState, int, Direction)"/>.
    /// A continuous path through legal positions implies a path of legal
    /// single-cell steps (D44), so every move <see cref="End(Vector2)"/>
    /// returns is one <see cref="MoveResolver"/> accepts.</para>
    /// <para><b>Pointer positions</b> are fractional grid positions in cell
    /// units. The block follows a smoothed copy of the pointer; the collision
    /// sweep runs toward that smoothed pointer, never the other way round, so
    /// no easing can carry the block across the corner of a wall.</para>
    /// <para><b>No route-finding.</b> The block goes where the finger leads and
    /// waits when it is blocked; the player steers it around an obstacle.</para>
    /// </remarks>
    public sealed class DragController
    {
        private const int AxisX = 0;
        private const int AxisY = 1;

        private readonly DragSettings settings;

        private LevelContext ctx;
        private BoardState state;

        // Whether the grabbed block's MovementAxis permits each axis. The one
        // projection the target, the corner assist and the push displacement
        // all go through, so they can never disagree about which pointer
        // movement a restricted block ignores.
        private bool permitsX;
        private bool permitsY;

        private Coord start;
        private Vector2 position;
        private Vector2 grabOffset;
        private Vector2 grabPoint;
        private Vector2 smoothedPointer;

        /// <summary>A controller with no drag in progress.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="settings"/> is null.</exception>
        public DragController(DragSettings settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>The block being dragged, or -1 when no drag is in progress.</summary>
        public int BlockIndex { get; private set; } = -1;

        /// <summary>True between a successful <see cref="TryBegin"/> and <see cref="End(Vector2)"/> or <see cref="Cancel"/>.</summary>
        public bool IsDragging => BlockIndex >= 0;

        /// <summary>
        /// The dragged block's continuous origin, in cell units: the value the
        /// last <see cref="Update"/> returned, or the block's start right after
        /// <see cref="TryBegin"/>. Always legal. Meaningless when no drag is in
        /// progress.
        /// </summary>
        public Vector2 Position => position;

        /// <summary>
        /// The whole-cell origin the block would settle in if released now:
        /// each axis of <see cref="Position"/> rounded to the nearest whole
        /// cell, an exact half toward the start. It is one of the origins the
        /// position overlaps, so it is legal.
        /// </summary>
        public Coord NearestOrigin => new Coord(
            RoundTowardStart(position.x, start.X), RoundTowardStart(position.y, start.Y));

        /// <summary>
        /// Grabs the living block covering the cell under
        /// <paramref name="pointer"/>, if <see cref="BoardState.CanMove"/> allows
        /// it — a frozen, locked, shuttered or dead block cannot be grabbed.
        /// The pointer's continuous offset from the block's origin is kept, so
        /// the block does not jump at the grab. A drag already in progress is
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

            if (!IsFinite(pointer))
            {
                return false;
            }

            var blockIndex = BlockAt(ctx, state, BoardLayout.CellOf(pointer));
            if (blockIndex < 0 || !state.CanMove(ctx, blockIndex))
            {
                return false;
            }

            this.ctx = ctx;
            this.state = state;
            var axis = ctx.SpecAt(blockIndex).Axis;
            permitsX = axis != MovementAxis.VerticalOnly;
            permitsY = axis != MovementAxis.HorizontalOnly;
            start = state.Origins[blockIndex];
            position = new Vector2(start.X, start.Y);
            grabOffset = pointer - position;
            grabPoint = pointer;
            smoothedPointer = pointer;
            BlockIndex = blockIndex;
            return true;
        }

        /// <summary>
        /// Eases the smoothed pointer toward <paramref name="pointer"/> by
        /// <c>1 − e^(−FollowRate·deltaSeconds)</c> of the gap, then moves the
        /// block toward the smoothed pointer minus the grab offset, and returns
        /// its new <see cref="Position"/>.
        /// </summary>
        /// <remarks>
        /// <para>The block sweeps one axis at a time — the one with the larger
        /// remaining distance first, horizontal on a tie — each as far as it
        /// can go while staying legal, and stops exactly flush against what
        /// blocks it. Every whole cell between the block and its target is
        /// checked in order, so no jump of the pointer, however large, carries
        /// it through a wall or another block.</para>
        /// <para>Then the corner assist: when the first axis that was blocked
        /// could continue from a whole cell within
        /// <see cref="DragSettings.CornerAssistCells"/> on the other axis, the
        /// other coordinate is nudged toward that cell, by no more than the
        /// blocked axis's remaining distance, and on reaching it the blocked
        /// axis is swept again.</para>
        /// <para>A non-finite pointer is ignored, and a non-positive or
        /// non-finite <paramref name="deltaSeconds"/> leaves the smoothed
        /// pointer where it is.</para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">No drag is in progress.</exception>
        public Vector2 Update(Vector2 pointer, float deltaSeconds)
        {
            RequireDragging();

            if (!IsFinite(pointer))
            {
                return position;
            }

            if (deltaSeconds > 0f && !float.IsInfinity(deltaSeconds))
            {
                var follow = (float)(1.0 - Math.Exp(-(double)settings.FollowRate * deltaSeconds));
                smoothedPointer += (pointer - smoothedPointer) * follow;
            }

            var target = smoothedPointer - grabOffset;
            if (!permitsX)
            {
                target.x = start.X;
            }

            if (!permitsY)
            {
                target.y = start.Y;
            }

            var first = Math.Abs(target.x - position.x) >= Math.Abs(target.y - position.y) ? AxisX : AxisY;
            var second = 1 - first;

            var isFirstBlocked = Sweep(first, target[first]);
            var isSecondBlocked = Sweep(second, target[second]);

            if (isFirstBlocked)
            {
                AssistAroundCorner(first, target[first]);
            }
            else if (isSecondBlocked)
            {
                AssistAroundCorner(second, target[second]);
            }

            return position;
        }

        /// <summary>
        /// Ends the drag and returns the move to apply, or null for none.
        /// The overload with a push direction explains which.
        /// </summary>
        /// <exception cref="InvalidOperationException">No drag is in progress.</exception>
        public Move? End(Vector2 pointer) => End(pointer, out _);

        /// <summary>
        /// Ends the drag and returns the move to apply, or null for none. The
        /// block is not moved toward <paramref name="pointer"/> first: it
        /// settles from where the player sees it.
        /// <list type="bullet">
        /// <item><see cref="NearestOrigin"/> is not the start:
        /// <c>Move(block, NearestOrigin)</c>. Whether it clears is the
        /// resolver's call (D25).</item>
        /// <item><see cref="NearestOrigin"/> is the start: a push candidate. The
        /// real pointer's displacement from where it grabbed, projected onto
        /// the axes the block may move along, gives the direction by its
        /// dominant axis (horizontal on a tie) — if it is at least the push
        /// threshold. <c>Move(block, start)</c> is returned only when the block
        /// can be cleared in place by a push that way, through a gate on the
        /// edge it is pushed toward (D43).</item>
        /// </list>
        /// <paramref name="pushDirection"/> is the direction of that push when
        /// the move is one, for presentation to show the block leaving that
        /// way; null for a move that arrives somewhere, and when no move is
        /// returned.
        /// </summary>
        /// <exception cref="InvalidOperationException">No drag is in progress.</exception>
        public Move? End(Vector2 pointer, out Direction? pushDirection)
        {
            RequireDragging();

            var blockIndex = BlockIndex;
            var settled = NearestOrigin;
            Move? result = null;
            pushDirection = null;

            if (settled != start)
            {
                result = new Move(blockIndex, settled);
            }
            else if (TryPushDirection(pointer - grabPoint, out var push)
                     && BlockReachability.CanClearInPlace(ctx, state, blockIndex, push))
            {
                result = new Move(blockIndex, start);
                pushDirection = push;
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
        /// Moves the block along <paramref name="axis"/> toward
        /// <paramref name="target"/> as far as it stays legal. The other
        /// coordinate is fixed meanwhile, so the whole cells it overlaps do not
        /// change and each whole cell crossed along the axis adds one line of
        /// at most two origins to check. Lines the block already overlaps are
        /// legal, since its position is. True when a line stopped it short of
        /// the target, in which case it sits exactly flush: a whole number.
        /// The first line off the board is illegal, so the walk ends whatever
        /// the target.
        /// </summary>
        private bool Sweep(int axis, float target)
        {
            var from = position[axis];

            if (target > from)
            {
                var reached = (int)Math.Ceiling(from);
                while (target > reached)
                {
                    if (!IsLineLegal(axis, reached + 1))
                    {
                        position[axis] = reached;
                        return true;
                    }

                    reached++;
                }
            }
            else if (target < from)
            {
                var reached = (int)Math.Floor(from);
                while (target < reached)
                {
                    if (!IsLineLegal(axis, reached - 1))
                    {
                        position[axis] = reached;
                        return true;
                    }

                    reached--;
                }
            }

            position[axis] = target;
            return false;
        }

        /// <summary>
        /// The corner assist for a block stopped flush on
        /// <paramref name="blockedAxis"/>. Of the two whole cells either side
        /// of the other coordinate, the nearest one within the assist distance
        /// from which the next line along the blocked axis is legal is the
        /// goal; the other coordinate moves toward it by at most the blocked
        /// axis's remaining distance, and on reaching it the blocked axis is
        /// swept again.
        /// </summary>
        /// <remarks>
        /// Always legal: the nudge keeps the other coordinate between its own
        /// floor and ceil, so the position overlaps a subset of the origins it
        /// already overlapped, and those were legal. The second sweep is an
        /// ordinary checked sweep.
        /// </remarks>
        private void AssistAroundCorner(int blockedAxis, float target)
        {
            var other = 1 - blockedAxis;
            if (!(settings.CornerAssistCells > 0f) || !Permits(other))
            {
                return;
            }

            var across = position[other];
            var below = (int)Math.Floor(across);
            var above = (int)Math.Ceiling(across);
            if (below == above)
            {
                return;
            }

            // Blocked means flush, so the blocked coordinate is whole.
            var along = position[blockedAxis];
            var remaining = Math.Abs(target - along);
            var nextLine = (int)along + Math.Sign(target - along);

            var goal = across;
            var goalDistance = float.PositiveInfinity;
            foreach (var candidate in new[] { below, above })
            {
                var distance = Math.Abs(across - candidate);
                if (distance <= settings.CornerAssistCells
                    && distance < goalDistance
                    && BlockReachability.IsFootprintLegal(ctx, state, BlockIndex, OriginAt(blockedAxis, nextLine, candidate)))
                {
                    goal = candidate;
                    goalDistance = distance;
                }
            }

            if (float.IsPositiveInfinity(goalDistance))
            {
                return;
            }

            if (goalDistance <= remaining)
            {
                position[other] = goal;
                Sweep(blockedAxis, target);
            }
            else
            {
                position[other] = across + Math.Sign(goal - across) * remaining;
            }
        }

        /// <summary>
        /// Whether the block may overlap line <paramref name="line"/> along
        /// <paramref name="axis"/>: every whole-cell origin on that line which
        /// the other coordinate overlaps is legal.
        /// </summary>
        private bool IsLineLegal(int axis, int line)
        {
            var across = position[1 - axis];
            var below = (int)Math.Floor(across);
            var above = (int)Math.Ceiling(across);

            return BlockReachability.IsFootprintLegal(ctx, state, BlockIndex, OriginAt(axis, line, below))
                   && (above == below
                       || BlockReachability.IsFootprintLegal(ctx, state, BlockIndex, OriginAt(axis, line, above)));
        }

        private static Coord OriginAt(int axis, int along, int across) =>
            axis == AxisX ? new Coord(along, across) : new Coord(across, along);

        private bool Permits(int axis) => axis == AxisX ? permitsX : permitsY;

        /// <summary>
        /// <paramref name="value"/> rounded to the nearest whole number, an
        /// exact half toward <paramref name="toward"/>. With <c>d</c> the
        /// offset from <paramref name="toward"/>, the result lies
        /// <c>ceil(|d| − ½)</c> whole cells from it in d's direction: a
        /// distance of exactly ½ gives 0, anything more gives 1.
        /// </summary>
        private static int RoundTowardStart(float value, int toward)
        {
            var offset = (double)value - toward;
            var steps = (int)Math.Ceiling(Math.Abs(offset) - 0.5);
            return toward + Math.Sign(offset) * steps;
        }

        private static bool IsFinite(Vector2 v) =>
            !float.IsNaN(v.x) && !float.IsInfinity(v.x) && !float.IsNaN(v.y) && !float.IsInfinity(v.y);

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
        /// The push direction of a pointer displacement: projected onto the
        /// block's permitted axes, then its dominant axis. False when the
        /// projected displacement is below the threshold.
        /// </summary>
        private bool TryPushDirection(Vector2 displacement, out Direction push)
        {
            var x = permitsX ? displacement.x : 0f;
            var y = permitsY ? displacement.y : 0f;

            if (Math.Abs(x) >= Math.Abs(y))
            {
                push = x > 0f ? Direction.Right : Direction.Left;
                return Math.Abs(x) >= settings.PushThresholdCells;
            }

            push = y > 0f ? Direction.Up : Direction.Down;
            return Math.Abs(y) >= settings.PushThresholdCells;
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
