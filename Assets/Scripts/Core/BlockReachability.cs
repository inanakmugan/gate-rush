using System;
using System.Collections.Generic;

namespace GateRush.Core
{
    /// <summary>
    /// The one implementation of "where can this block go" and "would this block
    /// exit here". Movement is reachability, not straight-line sliding
    /// (<c>DECISIONS.md</c> D27): a block reaches any origin connected to its
    /// current one by a path of single-cell orthogonal steps — in the directions
    /// its <see cref="MovementAxis"/> permits — along which the block's whole
    /// footprint is legal at every step. <c>MoveResolver</c> validates a single
    /// player move against this; <c>MoveGenerator</c> (in <c>GateRush.Solver</c>)
    /// enumerates its full output once per expanded search node. Both call here so
    /// the two can never disagree about what is reachable or about which landing
    /// positions clear a block at a gate.
    /// </summary>
    /// <remarks>
    /// <para><b>Reused buffers.</b> The breadth-first traversal reuses a frontier
    /// queue and a visited map across calls so a search that scans millions of
    /// nodes does not re-allocate them. The visited map is an <see cref="int"/>
    /// array indexed by origin cell (<c>y * width + x</c>), stamped with a
    /// per-scan generation number rather than cleared — cheap because
    /// <c>DECISIONS.md</c> D30 makes a block's origin one of its occupied grid
    /// cells, so every legal origin indexes the array directly with no hashing.
    /// </para>
    /// <para><b>Never share an instance.</b> Every <see cref="IsReachable"/> and
    /// <see cref="ReachableOrigins"/> call overwrites those buffers, and
    /// <see cref="ReachableOrigins"/> returns the live buffer by reference. A
    /// second caller — or a nested call, which <c>ISearchStrategy</c> (Module 05)
    /// may well introduce — that starts a scan while an earlier enumeration is
    /// still being read corrupts that enumeration mid-flight. <c>MoveResolver</c>
    /// and <c>MoveGenerator</c> therefore each construct and keep their own
    /// private instance; none is ever passed between them. Not thread-safe, for
    /// the same reason — acceptable because the solver is single-threaded and
    /// WebGL has no threads.</para>
    /// </remarks>
    public sealed class BlockReachability
    {
        // Step directions in Direction enum order (Up, Down, Left, Right). The
        // deterministic order MoveGenerator emits moves in is defined by this
        // order; reordering these arrays silently changes every generated
        // enumeration, which is why a MoveGenerator test pins a concrete output
        // sequence rather than only comparing two runs.
        private static readonly Coord[] FreeSteps =
        {
            new Coord(0, 1), new Coord(0, -1), new Coord(-1, 0), new Coord(1, 0)
        };

        private static readonly Coord[] HorizontalSteps =
        {
            new Coord(-1, 0), new Coord(1, 0)
        };

        private static readonly Coord[] VerticalSteps =
        {
            new Coord(0, 1), new Coord(0, -1)
        };

        private readonly Queue<Coord> frontier = new Queue<Coord>();
        private readonly List<Coord> reached = new List<Coord>();

        private int[] stamps = Array.Empty<int>();
        private int scanGeneration;

        /// <summary>
        /// True when <paramref name="target"/> is connected to
        /// <paramref name="from"/> by a corner-turning path of single-cell
        /// orthogonal steps along which block <paramref name="blockIndex"/>'s
        /// whole footprint is legal at every intermediate position. A multi-cell
        /// or L-shaped block may fail a corner a 1x1 block manages.
        /// </summary>
        public bool IsReachable(
            LevelContext ctx, BoardState state, int blockIndex, Coord from, Coord target)
        {
            if (from == target)
            {
                return true;
            }

            Scan(ctx, state, blockIndex, from, hasTarget: true, target, out var targetReached);
            return targetReached;
        }

        /// <summary>
        /// Every origin block <paramref name="blockIndex"/> can reach from
        /// <paramref name="from"/>, excluding <paramref name="from"/> itself, in a
        /// deterministic breadth-first order: ascending path length, ties broken
        /// by the <see cref="Direction"/>-enum order of the step that first
        /// reached the cell.
        /// <para><b>The returned list is a reused internal buffer.</b> It is valid
        /// only until the next call on this instance; copy anything that must
        /// outlive that. See the type remarks on why an instance must not be
        /// shared.</para>
        /// </summary>
        public IReadOnlyList<Coord> ReachableOrigins(
            LevelContext ctx, BoardState state, int blockIndex, Coord from)
        {
            Scan(ctx, state, blockIndex, from, hasTarget: false, default, out _);
            return reached;
        }

        /// <summary>
        /// True when block <paramref name="blockIndex"/>'s whole footprint is
        /// legal with its origin at <paramref name="candidateOrigin"/>: every
        /// occupied cell inside the grid, clear of static walls, closed shutters,
        /// and other living blocks.
        /// </summary>
        public static bool IsFootprintLegal(
            LevelContext ctx, BoardState state, int blockIndex, Coord candidateOrigin)
        {
            var cells = ctx.SpecAt(blockIndex).Cells;

            for (var i = 0; i < cells.Count; i++)
            {
                if (!state.IsCellFree(ctx, candidateOrigin + cells[i], blockIndex))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// True when block <paramref name="blockIndex"/> would exit through some
        /// open gate if its origin were <paramref name="origin"/>: the footprint
        /// flush against that gate's edge, the gate's colour equal to the block's
        /// current colour, the gate at least as wide as the footprint's
        /// projection span onto that edge, and that projection entirely within
        /// the opening. Non-rectangular footprints project their whole bounding
        /// extent, not just the cells touching the wall (M1).
        /// </summary>
        public static bool IsAtCompatibleExitGate(
            LevelContext ctx, BoardState state, int blockIndex, Coord origin)
        {
            var cells = ctx.SpecAt(blockIndex).Cells;

            var minX = int.MaxValue;
            var maxX = int.MinValue;
            var minY = int.MaxValue;
            var maxY = int.MinValue;

            for (var i = 0; i < cells.Count; i++)
            {
                var cell = origin + cells[i];
                if (cell.X < minX)
                {
                    minX = cell.X;
                }

                if (cell.X > maxX)
                {
                    maxX = cell.X;
                }

                if (cell.Y < minY)
                {
                    minY = cell.Y;
                }

                if (cell.Y > maxY)
                {
                    maxY = cell.Y;
                }
            }

            var currentColor = state.CurrentColorOf(ctx, blockIndex);

            for (var g = 0; g < ctx.Gates.Count; g++)
            {
                if (!state.GateOpen[g])
                {
                    continue;
                }

                var gate = ctx.Gates[g];
                if (gate.Color != currentColor)
                {
                    continue;
                }

                bool flush;
                int spanMin;
                int spanMax;

                switch (gate.Edge)
                {
                    case BoardEdge.Bottom:
                        flush = minY == 0;
                        spanMin = minX;
                        spanMax = maxX;
                        break;
                    case BoardEdge.Top:
                        flush = maxY == ctx.Height - 1;
                        spanMin = minX;
                        spanMax = maxX;
                        break;
                    case BoardEdge.Left:
                        flush = minX == 0;
                        spanMin = minY;
                        spanMax = maxY;
                        break;
                    case BoardEdge.Right:
                        flush = maxX == ctx.Width - 1;
                        spanMin = minY;
                        spanMax = maxY;
                        break;
                    default:
                        continue;
                }

                if (!flush)
                {
                    continue;
                }

                if (spanMax - spanMin + 1 > gate.Width)
                {
                    continue;
                }

                if (spanMin < gate.Offset || spanMax > gate.Offset + gate.Width - 1)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// True when the block at <paramref name="origin"/> is stopped by a solid
        /// obstruction — a static wall, a closed shutter, or another living block
        /// — in at least one of the directions its <see cref="MovementAxis"/>
        /// permits. The board edge alone does not count: a position flush against
        /// the grid boundary (every gate-aligned position among them) is not
        /// "resting against an obstacle" here, so <c>MoveGenerator</c>'s canonical
        /// filter keeps such positions through its gate criterion rather than
        /// folding them in through this one. Used only by move generation; the
        /// resolver has no need for it.
        /// </summary>
        public static bool IsRestingAgainstObstacle(
            LevelContext ctx, BoardState state, int blockIndex, Coord origin)
        {
            var spec = ctx.SpecAt(blockIndex);
            var steps = StepsFor(spec.Axis);
            var cells = spec.Cells;

            for (var s = 0; s < steps.Length; s++)
            {
                if (StepStoppedByObstacle(ctx, state, blockIndex, cells, origin, steps[s]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool StepStoppedByObstacle(
            LevelContext ctx, BoardState state, int blockIndex,
            IReadOnlyList<Coord> cells, Coord origin, Coord step)
        {
            for (var i = 0; i < cells.Count; i++)
            {
                var ahead = origin + cells[i] + step;

                if (!ctx.IsInsideGrid(ahead))
                {
                    // Off the board: the edge is not an obstacle for this test.
                    continue;
                }

                if (!state.IsCellFree(ctx, ahead, blockIndex))
                {
                    // A cell inside the grid that is not free — static wall, a
                    // closed shutter, or another living block. A genuine
                    // obstruction; the rest of the footprint need not be checked.
                    // This runs per reachable position per permitted direction
                    // during canonical filtering, so the early return matters.
                    return true;
                }
            }

            return false;
        }

        private void Scan(
            LevelContext ctx, BoardState state, int blockIndex,
            Coord from, bool hasTarget, Coord target, out bool targetReached)
        {
            targetReached = false;

            var steps = StepsFor(ctx.SpecAt(blockIndex).Axis);
            var width = ctx.Width;
            var height = ctx.Height;

            EnsureStampCapacity(width * height);
            AdvanceScanGeneration();

            reached.Clear();
            frontier.Clear();

            stamps[CellIndex(from, width)] = scanGeneration;
            frontier.Enqueue(from);

            while (frontier.Count > 0)
            {
                var origin = frontier.Dequeue();

                for (var s = 0; s < steps.Length; s++)
                {
                    var next = origin + steps[s];

                    if (next.X < 0 || next.X >= width || next.Y < 0 || next.Y >= height)
                    {
                        // Any legal origin is inside the grid (D30), so an
                        // out-of-range candidate can never be reachable; skip it
                        // before it would index the stamp array out of bounds.
                        continue;
                    }

                    var cell = CellIndex(next, width);
                    if (stamps[cell] == scanGeneration)
                    {
                        continue;
                    }

                    stamps[cell] = scanGeneration;

                    if (!IsFootprintLegal(ctx, state, blockIndex, next))
                    {
                        continue;
                    }

                    reached.Add(next);

                    if (hasTarget && next == target)
                    {
                        targetReached = true;
                        return;
                    }

                    frontier.Enqueue(next);
                }
            }
        }

        private void EnsureStampCapacity(int cellCount)
        {
            if (stamps.Length < cellCount)
            {
                stamps = new int[cellCount];
            }
        }

        /// <summary>
        /// Moves to the next per-scan generation number. On the one increment that
        /// would wrap <see cref="int.MaxValue"/> back to 0 — where it would alias
        /// the <see cref="stamps"/> array's initial zeros and make every cell read
        /// as already visited, so the flood fill would silently find nothing — the
        /// array is cleared and the count restarts at 1 instead. Unreachable in
        /// practice (billions of scans), but the failure mode is close to
        /// undiagnosable, so it is handled rather than left to chance.
        /// </summary>
        private void AdvanceScanGeneration()
        {
            if (scanGeneration == int.MaxValue)
            {
                Array.Clear(stamps, 0, stamps.Length);
                scanGeneration = 0;
            }

            scanGeneration++;
        }

        private static int CellIndex(Coord c, int width) => (c.Y * width) + c.X;

        private static Coord[] StepsFor(MovementAxis axis)
        {
            switch (axis)
            {
                case MovementAxis.HorizontalOnly:
                    return HorizontalSteps;
                case MovementAxis.VerticalOnly:
                    return VerticalSteps;
                default:
                    return FreeSteps;
            }
        }
    }
}
