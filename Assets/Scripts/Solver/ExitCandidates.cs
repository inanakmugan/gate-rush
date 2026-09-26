using System;
using System.Collections.Generic;
using GateRush.Core;

namespace GateRush.Solver
{
    /// <summary>
    /// Every place a block could exit during one stratum — a movable block, and
    /// an origin that sits it flush and aligned against an open gate of its
    /// colour, reachable from where the block stands — built once per stratum.
    /// <see cref="FewestBlockers"/> then scores any state of that stratum by how
    /// many other blocks stand in the way of the easiest exit, on the exit
    /// itself or anywhere along the route to it.
    /// </summary>
    /// <remarks>
    /// <para><b>Why one list serves the whole stratum.</b> Which blocks can
    /// move, their colours, which gates are open, and where walls, closed
    /// shutters and immovable blocks sit all stay fixed until the next clear.
    /// Call those fixed obstacles. The candidate exits, and whether each can be
    /// reached at all, depend only on them, so they are the same in every state
    /// of the stratum; only the movable blocks in the way change.</para>
    /// <para><b>Feasibility.</b> A candidate exit is kept only if its
    /// footprint stays inside the grid and clear of fixed obstacles, and the
    /// block can get there from its current origin through positions that are
    /// also clear of fixed obstacles — treating every movable block as
    /// something that could step aside. Nothing the block does within the
    /// stratum changes that answer: it only ever moves within the set of
    /// positions that route reaches. If no candidate survives,
    /// <see cref="IsEmpty"/> is true and no clear is reachable from any state
    /// in the stratum.</para>
    /// <para><b>The start origin.</b> The origin a block stands on when the
    /// stratum begins is the one place it can be without having arrived by a
    /// move, so reaching an exit there takes a push in place, which its axis
    /// may forbid (<c>DECISIONS.md</c> D39). It is an exit when
    /// <see cref="BlockReachability.CanClearInPlace"/> holds, and also when the
    /// block can step off it at all — then it can come back, and arriving
    /// clears on any edge. See <see cref="Of"/> for why that second case must
    /// count.</para>
    /// <para><b>The score.</b> For each candidate block, a cheapest-route
    /// search over its positions, where stepping to a position costs the number
    /// of other blocks its footprint newly runs into there. The score is the
    /// cheapest route to any of its exits, minimised over candidate blocks: the
    /// fewest blocks that would have to step aside before some block could
    /// exit. A block counts once per time the route runs into it, so a route
    /// that meets the same block twice counts it twice; the cheapest route
    /// rarely does. The score orders a search and decides nothing on its
    /// own.</para>
    /// <para>Instances reuse internal buffers across calls and are not
    /// thread-safe — one per search, like <see cref="BlockReachability"/>.</para>
    /// </remarks>
    internal sealed class ExitCandidates
    {
        private readonly Candidate[] candidates;
        private readonly int width;
        private readonly int height;
        private readonly bool[] fixedObstacle;

        private readonly int[] occupancy;
        private readonly int[] cost;
        private readonly bool[] settled;
        private readonly int[] fromBlockers;
        private readonly int[] toBlockers;

        private ExitCandidates(Candidate[] candidates, int width, int height, bool[] fixedObstacle, int maxFootprint)
        {
            this.candidates = candidates;
            this.width = width;
            this.height = height;
            this.fixedObstacle = fixedObstacle;

            var cellCount = width * height;
            occupancy = new int[cellCount];
            cost = new int[cellCount];
            settled = new bool[cellCount];
            fromBlockers = new int[maxFootprint];
            toBlockers = new int[maxFootprint];
        }

        /// <summary>No exit is possible anywhere in the stratum.</summary>
        public bool IsEmpty => candidates.Length == 0;

        /// <summary>The candidate exits for the stratum <paramref name="state"/> is in.</summary>
        public static ExitCandidates Of(LevelContext ctx, BoardState state)
        {
            if (ctx == null)
            {
                throw new ArgumentNullException(nameof(ctx));
            }

            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var width = ctx.Width;
            var height = ctx.Height;
            var fixedObstacle = new bool[width * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var cell = new Coord(x, y);
                    fixedObstacle[y * width + x] = ctx.IsStaticWall(cell) || IsUnderClosedShutter(ctx, state, cell);
                }
            }

            for (var j = 0; j < ctx.TotalBlockCapacity; j++)
            {
                if (state.Alive[j] && !state.CanMove(ctx, j))
                {
                    foreach (var cell in state.OccupiedCells(ctx, j))
                    {
                        fixedObstacle[cell.Y * width + cell.X] = true;
                    }
                }
            }

            var candidates = new List<Candidate>();
            var maxFootprint = 1;
            for (var i = 0; i < ctx.TotalBlockCapacity; i++)
            {
                if (!state.CanMove(ctx, i))
                {
                    continue;
                }

                var spec = ctx.SpecAt(i);
                var start = state.Origins[i];
                var startIndex = start.Y * width + start.X;
                var reachable = ReachableOrigins(spec.Cells, BlockReachability.PermittedSteps(spec.Axis), start, fixedObstacle, width, height);

                var canLeaveStart = false;
                for (var origin = 0; origin < reachable.Length && !canLeaveStart; origin++)
                {
                    canLeaveStart = reachable[origin] && origin != startIndex;
                }

                var clearsInPlace = BlockReachability.CanClearInPlace(ctx, state, i);

                var isExit = new bool[width * height];
                var hasExit = false;
                for (var origin = 0; origin < isExit.Length; origin++)
                {
                    if (!reachable[origin]
                        || !BlockReachability.IsAtCompatibleExitGate(ctx, state, i, new Coord(origin % width, origin / width)))
                    {
                        continue;
                    }

                    // The start origin is reached by a push in place or by
                    // arriving back after stepping off. It must stay an exit
                    // whenever the block can step off at all, even if the push
                    // is forbidden: IsEmpty has to over-approximate the clears
                    // reachable in this stratum, because an empty list becomes a
                    // DeadEnd, and on a clear-monotone level a DeadEnd becomes a
                    // proven Unsolvable. Stepping off in the relaxed flood only
                    // needs movable blocks to make way, which they can.
                    if (origin == startIndex && !clearsInPlace && !canLeaveStart)
                    {
                        continue;
                    }

                    isExit[origin] = true;
                    hasExit = true;
                }

                if (hasExit)
                {
                    candidates.Add(new Candidate(
                        i, spec.Cells, BlockReachability.PermittedSteps(spec.Axis), isExit,
                        inPlaceExit: clearsInPlace ? startIndex : -1));
                    maxFootprint = Math.Max(maxFootprint, spec.Cells.Count);
                }
            }

            return new ExitCandidates(candidates.ToArray(), width, height, fixedObstacle, maxFootprint);
        }

        /// <summary>
        /// The fewest blocks that would have to step aside before some block
        /// could exit, in <paramref name="state"/> — see the type remarks. Zero
        /// when some block has a clear route to an exit. <paramref name="state"/>
        /// must belong to the stratum this instance was built for.
        /// </summary>
        /// <exception cref="InvalidOperationException">The stratum has no candidate exit (<see cref="IsEmpty"/>).</exception>
        public int FewestBlockers(LevelContext ctx, BoardState state)
        {
            if (IsEmpty)
            {
                throw new InvalidOperationException("No candidate exit exists in this stratum; check IsEmpty first.");
            }

            FillOccupancy(ctx, state);

            var fewest = int.MaxValue;
            for (var k = 0; k < candidates.Length && fewest > 0; k++)
            {
                var candidate = candidates[k];
                var start = state.Origins[candidate.BlockIndex];
                fewest = Math.Min(fewest, CheapestRouteToAnExit(candidate, start.Y * width + start.X, fewest));
            }

            return fewest;
        }

        /// <summary>
        /// Dijkstra over the candidate's origins. Stops at the first exit
        /// settled, or once nothing cheaper than the best route found so far —
        /// initially <paramref name="toBeat"/> — is left. Positions number at
        /// most a few dozen, so the unvisited minimum is found by a plain scan.
        /// <para>The block's current origin is special: standing there is not
        /// arriving there. It counts as an exit at cost zero only when it is the
        /// candidate's <see cref="Candidate.InPlaceExit"/>; otherwise the route
        /// has to step off and come back, which is recorded when an edge leads
        /// back into it — it is settled by then, so it can never be settled as an
        /// exit.</para>
        /// </summary>
        private int CheapestRouteToAnExit(Candidate candidate, int start, int toBeat)
        {
            if (start == candidate.InPlaceExit)
            {
                return 0;
            }

            for (var i = 0; i < cost.Length; i++)
            {
                cost[i] = int.MaxValue;
                settled[i] = false;
            }

            cost[start] = 0;
            var best = toBeat;

            while (true)
            {
                var current = -1;
                for (var i = 0; i < cost.Length; i++)
                {
                    if (!settled[i] && cost[i] != int.MaxValue && (current < 0 || cost[i] < cost[current]))
                    {
                        current = i;
                    }
                }

                if (current < 0 || cost[current] >= best)
                {
                    return best;
                }

                if (candidate.IsExit[current] && current != start)
                {
                    return cost[current];
                }

                settled[current] = true;
                var origin = new Coord(current % width, current / width);
                var fromCount = BlockersAt(candidate, origin, fromBlockers);

                for (var s = 0; s < candidate.Steps.Count; s++)
                {
                    var next = origin + candidate.Steps[s];
                    if (!IsOpenPosition(candidate.Cells, next))
                    {
                        continue;
                    }

                    var index = next.Y * width + next.X;
                    var arrivesBackAtStart = index == start && candidate.IsExit[start];
                    if (settled[index] && !arrivesBackAtStart)
                    {
                        continue;
                    }

                    var toCount = BlockersAt(candidate, next, toBlockers);
                    var newlyMet = 0;
                    for (var t = 0; t < toCount; t++)
                    {
                        if (Array.IndexOf(fromBlockers, toBlockers[t], 0, fromCount) < 0)
                        {
                            newlyMet++;
                        }
                    }

                    var total = cost[current] + newlyMet;
                    if (arrivesBackAtStart)
                    {
                        best = Math.Min(best, total);
                        continue;
                    }

                    if (total < cost[index])
                    {
                        cost[index] = total;
                    }
                }
            }
        }

        /// <summary>Writes the distinct other blocks the candidate's footprint overlaps at <paramref name="origin"/> into <paramref name="buffer"/>; returns how many.</summary>
        private int BlockersAt(Candidate candidate, Coord origin, int[] buffer)
        {
            var count = 0;
            for (var c = 0; c < candidate.Cells.Count; c++)
            {
                var cell = origin + candidate.Cells[c];
                var occupant = occupancy[cell.Y * width + cell.X];
                if (occupant >= 0 && occupant != candidate.BlockIndex && Array.IndexOf(buffer, occupant, 0, count) < 0)
                {
                    buffer[count++] = occupant;
                }
            }

            return count;
        }

        private bool IsOpenPosition(IReadOnlyList<Coord> cells, Coord origin) =>
            IsOpenPosition(cells, origin, fixedObstacle, width, height);

        /// <summary>The footprint at <paramref name="origin"/> is inside the grid and clear of fixed obstacles.</summary>
        private static bool IsOpenPosition(IReadOnlyList<Coord> cells, Coord origin, bool[] fixedObstacle, int width, int height)
        {
            for (var c = 0; c < cells.Count; c++)
            {
                var cell = origin + cells[c];
                if (cell.X < 0 || cell.X >= width || cell.Y < 0 || cell.Y >= height || fixedObstacle[cell.Y * width + cell.X])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Every origin reachable from <paramref name="start"/> through open
        /// positions, movable blocks treated as able to step aside.
        /// </summary>
        private static bool[] ReachableOrigins(
            IReadOnlyList<Coord> cells, IReadOnlyList<Coord> steps, Coord start, bool[] fixedObstacle, int width, int height)
        {
            var reached = new bool[width * height];
            reached[start.Y * width + start.X] = true;
            var frontier = new Queue<Coord>();
            frontier.Enqueue(start);

            while (frontier.Count > 0)
            {
                var origin = frontier.Dequeue();
                for (var s = 0; s < steps.Count; s++)
                {
                    var next = origin + steps[s];
                    if (!IsOpenPosition(cells, next, fixedObstacle, width, height))
                    {
                        continue;
                    }

                    var index = next.Y * width + next.X;
                    if (!reached[index])
                    {
                        reached[index] = true;
                        frontier.Enqueue(next);
                    }
                }
            }

            return reached;
        }

        private void FillOccupancy(LevelContext ctx, BoardState state)
        {
            for (var i = 0; i < occupancy.Length; i++)
            {
                occupancy[i] = -1;
            }

            for (var j = 0; j < ctx.TotalBlockCapacity; j++)
            {
                if (!state.Alive[j])
                {
                    continue;
                }

                var cells = ctx.SpecAt(j).Cells;
                var origin = state.Origins[j];
                for (var c = 0; c < cells.Count; c++)
                {
                    var cell = origin + cells[c];
                    occupancy[cell.Y * width + cell.X] = j;
                }
            }
        }

        private static bool IsUnderClosedShutter(LevelContext ctx, BoardState state, Coord cell)
        {
            var shutter = ctx.ShutterPositionAt(cell);
            return shutter.HasValue && !state.ShutterOpen[shutter.Value];
        }

        /// <summary>One movable block with at least one reachable exit, and which of its origins are exits.</summary>
        private sealed class Candidate
        {
            public Candidate(int blockIndex, IReadOnlyList<Coord> cells, IReadOnlyList<Coord> steps, bool[] isExit, int inPlaceExit)
            {
                BlockIndex = blockIndex;
                Cells = cells;
                Steps = steps;
                IsExit = isExit;
                InPlaceExit = inPlaceExit;
            }

            public int BlockIndex { get; }

            public IReadOnlyList<Coord> Cells { get; }

            public IReadOnlyList<Coord> Steps { get; }

            /// <summary>Indexed by origin cell (<c>y * width + x</c>).</summary>
            public bool[] IsExit { get; }

            /// <summary>
            /// The stratum's start origin (<c>y * width + x</c>) when the block
            /// can be pushed in place into a gate there
            /// (<see cref="BlockReachability.CanClearInPlace"/>); -1 otherwise.
            /// </summary>
            public int InPlaceExit { get; }
        }
    }
}
