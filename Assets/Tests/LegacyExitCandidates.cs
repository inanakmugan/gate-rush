using System;
using System.Collections.Generic;
using GateRush.Core;

namespace GateRush.Tests
{
    /// <summary>
    /// <c>ExitCandidates</c> as it was before Module 28 gave its route search a
    /// heap: the same candidates, the same costs, and a search that finds the
    /// next origin to settle by scanning every cell. Kept only so
    /// <c>ExitCandidatesTests</c> can hold the faster search to the answers of
    /// the one it replaced. It is a frozen copy: a later change to what
    /// <c>ExitCandidates</c> computes is made there and argued there, not
    /// mirrored here.
    /// </summary>
    internal sealed class LegacyExitCandidates
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

        private LegacyExitCandidates(Candidate[] candidates, int width, int height, bool[] fixedObstacle, int maxFootprint)
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

        /// <summary>How many candidate blocks the stratum has.</summary>
        public int CandidateCount => candidates.Length;

        /// <summary>The candidate exits for the stratum <paramref name="state"/> is in.</summary>
        public static LegacyExitCandidates Of(LevelContext ctx, BoardState state)
        {
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

            return new LegacyExitCandidates(candidates.ToArray(), width, height, fixedObstacle, maxFootprint);
        }

        /// <summary>The fewest blocks that would have to step aside before some block could exit.</summary>
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
        /// The cheapest route of candidate <paramref name="candidateIndex"/>
        /// alone to any of its exits, searched to the end;
        /// <see cref="int.MaxValue"/> when it has none.
        /// </summary>
        public int RouteCost(LevelContext ctx, BoardState state, int candidateIndex)
        {
            FillOccupancy(ctx, state);

            var candidate = candidates[candidateIndex];
            var start = state.Origins[candidate.BlockIndex];
            return CheapestRouteToAnExit(candidate, start.Y * width + start.X, int.MaxValue);
        }

        /// <summary>The search as it was: the cheapest unsettled origin is found by scanning every cell.</summary>
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
                    if (!IsOpenPosition(candidate.Cells, next, fixedObstacle, width, height))
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

            public bool[] IsExit { get; }

            public int InPlaceExit { get; }
        }
    }
}
