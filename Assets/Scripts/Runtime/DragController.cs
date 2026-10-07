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
    /// <para><b>The gate pull</b> (Module 22, D49). The collision sweep works
    /// on the unpulled position. Each update, a <see cref="PullTarget"/> is
    /// looked for from that position — an origin where the block would exit
    /// by <see cref="BlockReachability.FindExitGate"/>, Core's own arrival
    /// rule — and <see cref="Position"/> is that position drawn a fraction
    /// of the way toward it: all the way within the capture range, where the
    /// block latches onto the origin. Only that fraction is carried from one
    /// update to the next, smoothed; the position is recomputed from the
    /// unpulled one every time, so the pull never feeds back into the
    /// sweep.</para>
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

        // The swept, unpulled position: what the sweep, the corner assist and
        // NearestOrigin work on. Always legal.
        private Vector2 position;
        private Vector2 grabOffset;
        private Vector2 grabPoint;
        private Vector2 smoothedPointer;

        // The position drawn toward the pull target, the target itself and
        // its distance from the unpulled position, all recomputed by every
        // update.
        private Vector2 pulled;
        private PullTarget? pull;
        private float pullDistance;

        // How much of the way to pullOrigin the block is drawn, from 0 to 1,
        // smoothed over time; and the origin it is drawn toward, kept after
        // its target is lost for as long as the fraction decays legally.
        private float pullFraction;
        private Coord pullOrigin;
        private bool hasPullOrigin;

        // Which way the smoothed, projected pointer target last moved on each
        // axis (-1, 0 before it has moved, +1), and the axis it last moved
        // further on. A still pointer keeps its lead; a reversal flips it at
        // once. A forbidden axis never leads: its target coordinate is pinned.
        private Vector2 lastTarget;
        private int leadX;
        private int leadY;
        private int leadingAxis;

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
        /// <see cref="TryBegin"/>. Always legal. While a gate pulls, and while
        /// a lost pull lets go, it lies on the straight, legal line between
        /// the unpulled position and the pull's origin, never past the origin;
        /// within the capture range it settles on the origin itself.
        /// Meaningless when no drag is in progress.
        /// </summary>
        public Vector2 Position => pulled;

        /// <summary>
        /// What an open gate is pulling the block toward, as the last
        /// <see cref="Update"/> found it, or null: none before the first
        /// update, and none when no drag is in progress.
        /// </summary>
        public PullTarget? Pull => IsDragging ? pull : null;

        /// <summary>
        /// The whole-cell origin the block would settle in if released now
        /// with no capture: each axis of the unpulled position rounded to the
        /// nearest whole cell, an exact half toward the start. It is one of
        /// the origins that position overlaps, so it is legal.
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
            pulled = position;
            pull = null;
            pullDistance = 0f;
            pullFraction = 0f;
            hasPullOrigin = false;
            lastTarget = ProjectedTarget();
            leadX = 0;
            leadY = 0;
            leadingAxis = AxisX;
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
        /// <para>Last, the gate pull: <see cref="Pull"/> is found from where
        /// the sweep left the block, and the returned position is drawn toward
        /// it by a fraction that closes on what the pull asks for at
        /// <see cref="DragSettings.PullFollowRate"/>. The sweep itself never
        /// sees the pull.</para>
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
                return pulled;
            }

            if (deltaSeconds > 0f && !float.IsInfinity(deltaSeconds))
            {
                // A frame long enough to close the whole gap lands on the
                // pointer itself: adding the rounded gap could leave it a hair
                // off, and closing that hair next frame would read as the
                // pointer leading the other way.
                var follow = (float)(1.0 - Math.Exp(-(double)settings.FollowRate * deltaSeconds));
                smoothedPointer = follow >= 1f ? pointer : smoothedPointer + (pointer - smoothedPointer) * follow;
            }

            var target = ProjectedTarget();
            TrackLead(target);

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

            ApplyPull(target, deltaSeconds);
            return pulled;
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
        /// <item>There is a <see cref="Pull"/>, its origin is not the start
        /// and it is at most <see cref="DragSettings.CaptureRangeCells"/>
        /// away: <c>Move(block, origin)</c> — the block arrives at its gate
        /// (D49). The origin is reachable: the unpulled position ends a
        /// continuous legal path from the start (D44), and the pull's line
        /// from it to the origin was checked legal, so the path extends to
        /// the origin.</item>
        /// <item>Otherwise, <see cref="NearestOrigin"/> is not the start:
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

            if (pull.HasValue && pull.Value.Origin != start && pullDistance <= settings.CaptureRangeCells)
            {
                result = new Move(blockIndex, pull.Value.Origin);
            }
            else if (settled != start)
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
            pull = null;
            pullDistance = 0f;
        }

        /// <summary>
        /// Where the block is asked to go: the smoothed pointer minus the grab
        /// offset, with the coordinate on an axis the block may not move along
        /// pinned to its start.
        /// </summary>
        private Vector2 ProjectedTarget()
        {
            var target = smoothedPointer - grabOffset;
            if (!permitsX)
            {
                target.x = start.X;
            }

            if (!permitsY)
            {
                target.y = start.Y;
            }

            return target;
        }

        /// <summary>
        /// Records which way <paramref name="target"/> moved since the last
        /// update, per axis, and the axis it moved further on (horizontal on a
        /// tie). An axis it did not move on keeps its lead.
        /// </summary>
        private void TrackLead(Vector2 target)
        {
            var dx = target.x - lastTarget.x;
            var dy = target.y - lastTarget.y;

            if (dx != 0f)
            {
                leadX = Math.Sign(dx);
            }

            if (dy != 0f)
            {
                leadY = Math.Sign(dy);
            }

            if (dx != 0f || dy != 0f)
            {
                leadingAxis = Math.Abs(dx) >= Math.Abs(dy) ? AxisX : AxisY;
            }

            lastTarget = target;
        }

        /// <summary>
        /// Finds the pull from the unpulled position and sets
        /// <see cref="pull"/>, <see cref="pullDistance"/> and
        /// <see cref="pulled"/>. Of the targets the two axes offer, the nearer
        /// wins; a tie goes to the axis the pointer is leading on. With no
        /// target, a block on its start may still be pushed into its gate
        /// (<see cref="PushNudgeAtStart"/>).
        /// </summary>
        /// <remarks>
        /// <para><b>The fraction.</b> The block is drawn
        /// <see cref="pullFraction"/> of the way from the unpulled position to
        /// the origin, both coordinates alike. What the pull asks for is 1
        /// within the capture range — the block latches onto the origin — and
        /// <c>PullAmount × ease(1 − distance / PullRangeCells)</c> outside it,
        /// and 0 with no target. The fraction closes on that by
        /// <c>1 − e^(−PullFollowRate·dt)</c> of the gap per update, so it
        /// slides into the latch and out of it, the same at any frame rate,
        /// and never passes what is asked: it never exceeds 1, so the block
        /// never passes the origin.</para>
        /// <para><b>Legal at every update.</b> With a target, the drawn
        /// position is on the line <see cref="TryFindTarget"/> checked. With
        /// the target gone, the last origin is kept only while every origin
        /// the line from the unpulled position to it can overlap is legal
        /// (<see cref="IsLineToOriginLegal"/>); otherwise the fraction drops to
        /// 0 at once. A target at a new origin starts from 0: a fraction
        /// earned toward one origin is never spent toward another.</para>
        /// </remarks>
        private void ApplyPull(Vector2 target, float deltaSeconds)
        {
            pullDistance = 0f;

            var hasX = TryFindTarget(AxisX, out var originX, out var gateX, out var distanceX);
            var hasY = TryFindTarget(AxisY, out var originY, out var gateY, out var distanceY);
            var asked = 0f;

            if (!hasX && !hasY)
            {
                pull = PushNudgeAtStart(target);
                if (hasPullOrigin && !(pullFraction > 0f && IsLineToOriginLegal(pullOrigin)))
                {
                    hasPullOrigin = false;
                    pullFraction = 0f;
                }
            }
            else
            {
                var useX = hasX
                           && (!hasY || distanceX < distanceY || (distanceX == distanceY && leadingAxis == AxisX));
                var origin = useX ? originX : originY;
                var gateIndex = useX ? gateX : gateY;
                var distance = useX ? distanceX : distanceY;

                if (!hasPullOrigin || pullOrigin != origin)
                {
                    pullOrigin = origin;
                    hasPullOrigin = true;
                    pullFraction = 0f;
                }

                var strength = 1f;
                asked = 1f;
                if (distance > settings.CaptureRangeCells)
                {
                    // Whatever the ease returns, the strength used is within
                    // 0–1; a NaN fails the comparison and reads as none.
                    var eased = settings.PullEase(1f - distance / settings.PullRangeCells);
                    strength = eased >= 0f ? Math.Min(eased, 1f) : 0f;
                    asked = settings.PullAmount * strength;
                }

                pullDistance = distance;
                pull = new PullTarget(
                    origin, gateIndex, strength, distance > 0f ? Vector2.zero : NudgeInto(gateIndex, target));
            }

            if (deltaSeconds > 0f && !float.IsInfinity(deltaSeconds))
            {
                var follow = (float)(1.0 - Math.Exp(-(double)settings.PullFollowRate * deltaSeconds));
                pullFraction += (asked - pullFraction) * follow;
            }

            pulled = hasPullOrigin
                ? new Vector2(
                    Toward(position.x, pullOrigin.X, pullFraction), Toward(position.y, pullOrigin.Y, pullFraction))
                : position;
        }

        /// <summary>
        /// <paramref name="from"/> moved <paramref name="fraction"/> of the way
        /// to <paramref name="to"/>, never outside the two: rounding may not
        /// carry a block drawn all the way to its origin a hair past it, onto
        /// a line that was not checked.
        /// </summary>
        private static float Toward(float from, float to, float fraction)
        {
            var value = from + (to - from) * fraction;
            return Math.Max(Math.Min(from, to), Math.Min(Math.Max(from, to), value));
        }

        /// <summary>
        /// Whether the block may be drawn anywhere on the straight line from
        /// its unpulled position to <paramref name="origin"/>: every
        /// whole-cell origin in the box the two span — floor to ceil of the
        /// position, out to the origin, on each axis — passes
        /// <see cref="BlockReachability.IsFootprintLegal"/>. Every position on
        /// the line overlaps only origins in that box. For a live target this
        /// is the set <see cref="TryFindTarget"/> already checked; it is asked
        /// again only while a lost target's pull decays, when the block may
        /// have moved anywhere.
        /// </summary>
        private bool IsLineToOriginLegal(Coord origin)
        {
            var minX = Math.Min((int)Math.Floor(position.x), origin.X);
            var maxX = Math.Max((int)Math.Ceiling(position.x), origin.X);
            var minY = Math.Min((int)Math.Floor(position.y), origin.Y);
            var maxY = Math.Max((int)Math.Ceiling(position.y), origin.Y);

            for (var x = minX; x <= maxX; x++)
            {
                for (var y = minY; y <= maxY; y++)
                {
                    if (!BlockReachability.IsFootprintLegal(ctx, state, BlockIndex, new Coord(x, y)))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// The pull target along <paramref name="axis"/>, in the direction the
        /// pointer leads on it: the first whole-cell origin ahead, within the
        /// pull range, at which the block would exit by Core's arrival rule
        /// (<see cref="BlockReachability.FindExitGate"/> on the state the drag
        /// began on), reached by a straight legal line. Its coordinate on the
        /// other axis is the one <see cref="NearestOrigin"/> rounds to, so a
        /// block a little off the gate's line is still pulled onto it.
        /// </summary>
        /// <remarks>
        /// <para>The walk goes line by line from the first line ahead that the
        /// block already overlaps — legal, since its position is — and ends at
        /// the first line that is not legal for every row the other coordinate
        /// overlaps (<see cref="IsLineLegal"/>): nothing is pulled through or
        /// around anything. The first line off the board is illegal, so the
        /// walk ends whatever the range. The origin's own row is one of those
        /// rows, so the origin and every position on the line to it overlap
        /// only origins that were checked.</para>
        /// <para>The start is skipped: arriving there is a push in place, which
        /// is D43's to decide, not the pull's.</para>
        /// <para><paramref name="distance"/> is the straight-line distance from
        /// the unpulled position to the origin, which is the distance along
        /// the axis when the other coordinate is already on the origin's.</para>
        /// </remarks>
        private bool TryFindTarget(int axis, out Coord origin, out int gateIndex, out float distance)
        {
            origin = default;
            gateIndex = -1;
            distance = 0f;

            var step = axis == AxisX ? leadX : leadY;
            if (step == 0)
            {
                return false;
            }

            var other = 1 - axis;
            var across = RoundTowardStart(position[other], other == AxisX ? start.X : start.Y);
            var from = position[axis];
            var first = step > 0 ? (int)Math.Ceiling(from) : (int)Math.Floor(from);

            for (var line = first; Math.Abs(line - from) <= settings.PullRangeCells; line += step)
            {
                if (line != first && !IsLineLegal(axis, line))
                {
                    return false;
                }

                var candidate = OriginAt(axis, line, across);
                if (candidate == start)
                {
                    continue;
                }

                var gate = BlockReachability.FindExitGate(ctx, state, BlockIndex, candidate, null);
                if (gate < 0)
                {
                    continue;
                }

                distance = (new Vector2(candidate.X, candidate.Y) - position).magnitude;
                if (distance > settings.PullRangeCells)
                {
                    return false;
                }

                origin = candidate;
                gateIndex = gate;
                return true;
            }

            return false;
        }

        /// <summary>
        /// The nudge of a block standing on its start and pushed toward a gate
        /// it can be cleared through in place: a target at the start, at full
        /// strength, for the gate
        /// <see cref="BlockReachability.FindExitGate"/> finds for the pushed
        /// direction — D43's own rule, so the nudge shows only where a push
        /// that way can clear. Null when the block is off its start, the
        /// pointer does not push, or no gate is there. It never captures:
        /// the release is still D43's, threshold included.
        /// </summary>
        private PullTarget? PushNudgeAtStart(Vector2 target)
        {
            if (position.x != start.X || position.y != start.Y)
            {
                return null;
            }

            var overshoot = DominantDirection(target.x - position.x, target.y - position.y, out var push);
            if (!(overshoot > 0f))
            {
                return null;
            }

            var gateIndex = BlockReachability.FindExitGate(ctx, state, BlockIndex, start, push);
            if (gateIndex < 0)
            {
                return null;
            }

            return new PullTarget(start, gateIndex, 1f, NudgeInto(gateIndex, target));
        }

        /// <summary>
        /// How far into gate <paramref name="gateIndex"/>'s mouth a block
        /// standing on its origin is drawn: along the gate's outward
        /// direction, as far as the pointer target lies past the block that
        /// way, up to <see cref="DragSettings.NudgeMaxCells"/> — the nudge's
        /// own bound, whatever the pull's amount and range are. Zero when the
        /// pointer does not push toward the gate.
        /// </summary>
        private Vector2 NudgeInto(int gateIndex, Vector2 target)
        {
            var outward = GateExit.Outward(ctx.Gates[gateIndex].Edge);
            var overshoot = Vector2.Dot(target - position, outward);
            if (!(overshoot > 0f))
            {
                return Vector2.zero;
            }

            return outward * Math.Min(overshoot, settings.NudgeMaxCells);
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

            return DominantDirection(x, y, out push) >= settings.PushThresholdCells;
        }

        /// <summary>
        /// The direction of a displacement by its dominant axis, horizontal on
        /// a tie, and how far it goes that way. The one reading of "which way
        /// is this push" the release and the nudge share.
        /// </summary>
        private static float DominantDirection(float x, float y, out Direction direction)
        {
            if (Math.Abs(x) >= Math.Abs(y))
            {
                direction = x > 0f ? Direction.Right : Direction.Left;
                return Math.Abs(x);
            }

            direction = y > 0f ? Direction.Up : Direction.Down;
            return Math.Abs(y);
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
