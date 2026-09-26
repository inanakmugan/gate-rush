using System;
using System.Collections.Generic;
using System.Diagnostics;
using GateRush.Core;

namespace GateRush.Solver
{
    /// <summary>
    /// Finds a solution fast by committing to one clear at a time: search the
    /// current stratum for a route to the next clear, play it on the real board,
    /// repeat. Returns a verified solution that is usually, but not always, the
    /// shortest — it reports how good its answer is through
    /// <see cref="SolveResult.LengthLowerBound"/> and
    /// <see cref="SolveResult.ProvenShortestLength"/>, never through
    /// <see cref="SolveStatus"/>.
    /// </summary>
    /// <remarks>
    /// <para><b>Shape.</b> This is enforced hill-climbing (the FF planner's
    /// search): from the current state, search until a strictly better state
    /// turns up — here, one with more clears — commit to it, and never look
    /// back. In general that is incomplete, because a commitment can lead into
    /// a dead end. On a level where <see cref="LevelContext.IsClearMonotone"/>
    /// holds it cannot: a clear never turns a solvable board unsolvable, so any
    /// reachable clear is safe to commit to.</para>
    ///
    /// <para><b>Verdicts.</b> <see cref="SolveStatus.Solvable"/> when the loop
    /// clears the board. When a stratum is searched completely and no clear is
    /// reachable, <see cref="SolveStatus.Unsolvable"/> — but only if
    /// <see cref="LevelContext.IsClearMonotone"/> is true, because only then does
    /// the dead end prove anything about the level; otherwise
    /// <see cref="SolveStatus.Indeterminate"/>. Any budget limit gives
    /// <see cref="SolveStatus.Indeterminate"/>.</para>
    ///
    /// <para><b>Each stratum is searched on a smaller copy.</b> Where it can be
    /// built — no generator or elevator output pending — the search runs on a
    /// <see cref="NextClearAbstraction"/>, whose routes to the next clear are
    /// exactly the real board's and whose state space is far smaller. Otherwise
    /// it runs on the real board. Inside a stratum the search is best-first:
    /// fewest blockers in the way of an exit first
    /// (<see cref="ExitCandidates.FewestBlockers"/>), then fewest moves, then
    /// discovery order. "Nearest" therefore means the first clear that ordering
    /// reaches, not necessarily the fewest moves away. A stratum whose
    /// <see cref="ExitCandidates"/> list is empty is a dead end without being
    /// searched.</para>
    ///
    /// <para><b>Every route is replayed on the real board.</b> Each move the
    /// stratum search returns is applied to the source state through
    /// <see cref="MoveResolver"/>; every move but the last must be accepted
    /// without clearing, and the last must clear. Anything else means the copy
    /// and the real board disagree — a bug — and the search throws rather than
    /// return a solution it cannot vouch for.</para>
    ///
    /// <para><b>Budget.</b> <see cref="SearchBudget.MaxExploredStates"/> and
    /// <see cref="SearchBudget.MaxWallClockMs"/> cover the whole search, across
    /// every stratum, with the wall clock polled every
    /// <see cref="SearchBudget.WallClockPollInterval"/> expansions as in the
    /// other strategies. <see cref="SearchBudget.MaxDepth"/> caps the total
    /// solution length. <see cref="SearchBudget.Mode"/> must be
    /// <see cref="MoveGenMode.Exhaustive"/>: canonical pruning can hide the only
    /// route to a clear, and a dead end found under it would be a false
    /// <see cref="SolveStatus.Unsolvable"/>. <see cref="SearchBudget.Cancellation"/>
    /// is checked before every expansion and throws
    /// <see cref="OperationCanceledException"/>.</para>
    ///
    /// <para><b>Lower bound.</b> <see cref="AStarStrategy.EstimateRemainingMoves"/>
    /// at the initial state, which is admissible; a solution that meets it is
    /// reported proven shortest.</para>
    ///
    /// <para>Deterministic: <see cref="MoveGenerator"/>'s documented order and
    /// the tie-breaks above fix every choice. Each call builds its own generator,
    /// resolver and buffers, so an instance may be reused but is not
    /// thread-safe.</para>
    /// </remarks>
    public sealed class NearestNextClearStrategy : ISearchStrategy
    {
        private readonly Func<MoveGenerator> generatorFactory;

        /// <summary>Creates a search that builds its own <see cref="MoveGenerator"/> per call.</summary>
        public NearestNextClearStrategy()
            : this(() => new MoveGenerator())
        {
        }

        /// <summary>
        /// Test seam: <paramref name="generatorFactory"/> supplies the generator
        /// each <see cref="Search"/> call uses, so a test can feed the search a
        /// move the resolver rejects.
        /// </summary>
        internal NearestNextClearStrategy(Func<MoveGenerator> generatorFactory)
        {
            this.generatorFactory = generatorFactory;
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentException"><paramref name="budget"/> is not exhaustive.</exception>
        /// <exception cref="InvalidOperationException">
        /// A route found on the smaller copy did not replay on the real board —
        /// a bug in <see cref="NextClearAbstraction"/> — or the resolver rejected
        /// a move the generator emitted. Either is a solver bug, never a property
        /// of the level.
        /// </exception>
        public SolveResult Search(LevelContext ctx, BoardState initial, SearchBudget budget)
        {
            if (ctx == null)
            {
                throw new ArgumentNullException(nameof(ctx));
            }

            if (initial == null)
            {
                throw new ArgumentNullException(nameof(initial));
            }

            if (budget == null)
            {
                throw new ArgumentNullException(nameof(budget));
            }

            if (budget.Mode != MoveGenMode.Exhaustive)
            {
                throw new ArgumentException(
                    "NearestNextClearStrategy needs an exhaustive budget: canonical pruning can hide the only " +
                    "route to a clear, and a dead end found under it would be a false Unsolvable.",
                    nameof(budget));
            }

            var run = new Run(ctx, budget, generatorFactory());
            var solution = new List<Move>();
            var state = initial;

            while (!state.IsSolved(ctx))
            {
                var route = run.FindRouteToNextClear(state, out var outcome);
                if (outcome == StratumOutcome.OutOfBudget)
                {
                    return run.Result(SolveStatus.Indeterminate, solution: null, initial);
                }

                if (outcome == StratumOutcome.DeadEnd)
                {
                    return ctx.IsClearMonotone
                        ? run.Result(SolveStatus.Unsolvable, solution: null, initial)
                        : run.Result(SolveStatus.Indeterminate, solution: null, initial);
                }

                if (solution.Count + route.Count > budget.MaxDepth)
                {
                    return run.Result(SolveStatus.Indeterminate, solution: null, initial);
                }

                state = run.ReplayOnSource(state, route);
                solution.AddRange(route);
            }

            return run.Result(SolveStatus.Solvable, solution, initial);
        }

        private enum StratumOutcome
        {
            FoundClear,
            DeadEnd,
            OutOfBudget,
        }

        /// <summary>The state of one <see cref="Search"/> call: its budget counters, statistics and reusable tools.</summary>
        private sealed class Run
        {
            private readonly LevelContext ctx;
            private readonly SearchBudget budget;
            private readonly Stopwatch stopwatch = Stopwatch.StartNew();
            private readonly MoveGenerator generator;
            private readonly MoveResolver resolver = new MoveResolver();

            private int explored;
            private int peakFrontier;
            private int peakRetained;

            public Run(LevelContext ctx, SearchBudget budget, MoveGenerator generator)
            {
                this.ctx = ctx;
                this.budget = budget;
                this.generator = generator;
            }

            /// <summary>
            /// Searches the stratum <paramref name="source"/> is in for a route to
            /// the next clear, as moves on the real board. Null unless
            /// <paramref name="outcome"/> is <see cref="StratumOutcome.FoundClear"/>.
            /// </summary>
            public IReadOnlyList<Move> FindRouteToNextClear(BoardState source, out StratumOutcome outcome)
            {
                // The copy cannot model spawns; with output pending, search the
                // real board instead, ordered by moves alone.
                NextClearAbstraction abstraction = null;
                if (NextClearAbstraction.CanModel(ctx, source))
                {
                    abstraction = NextClearAbstraction.Of(ctx, source);
                }

                var searchCtx = abstraction?.Context ?? ctx;
                var root = abstraction?.Initial ?? source;

                ExitCandidates candidates = null;
                if (abstraction != null)
                {
                    candidates = ExitCandidates.Of(searchCtx, root);
                    if (candidates.IsEmpty)
                    {
                        outcome = StratumOutcome.DeadEnd;
                        return null;
                    }
                }

                var parents = new Dictionary<BoardState, (BoardState from, Move move)> { [root] = (null, default) };
                var open = new OpenSet();
                long sequence = 0;
                open.Push(new Entry(root, 0, 0, sequence++));

                while (open.Count > 0)
                {
                    if (explored >= budget.MaxExploredStates
                        || (explored > 0 && explored % SearchBudget.WallClockPollInterval == 0
                            && stopwatch.ElapsedMilliseconds > budget.MaxWallClockMs))
                    {
                        outcome = StratumOutcome.OutOfBudget;
                        return null;
                    }

                    budget.Cancellation.ThrowIfCancellationRequested();
                    var entry = open.Pop();
                    explored++;

                    foreach (var move in generator.Generate(searchCtx, entry.State, MoveGenMode.Exhaustive))
                    {
                        if (!resolver.TryApplyMove(searchCtx, entry.State, move, out var next, out _))
                        {
                            throw RejectedMove.Error(searchCtx, move);
                        }

                        if (next.TotalClearCount > root.TotalClearCount)
                        {
                            outcome = StratumOutcome.FoundClear;
                            return ToSourceRoute(abstraction, parents, entry.State, move);
                        }

                        if (parents.ContainsKey(next))
                        {
                            continue;
                        }

                        parents[next] = (entry.State, move);
                        var blockers = candidates?.FewestBlockers(searchCtx, next) ?? 0;
                        open.Push(new Entry(next, blockers, entry.Depth + 1, sequence++));

                        peakFrontier = Math.Max(peakFrontier, open.Count);
                        peakRetained = Math.Max(peakRetained, parents.Count);
                    }
                }

                outcome = StratumOutcome.DeadEnd;
                return null;
            }

            /// <summary>
            /// Applies <paramref name="route"/> to <paramref name="source"/> on the
            /// real board and returns the state after the clear.
            /// </summary>
            /// <exception cref="InvalidOperationException">
            /// A move is rejected, clears before the last step, or the last step
            /// does not clear.
            /// </exception>
            public BoardState ReplayOnSource(BoardState source, IReadOnlyList<Move> route)
            {
                var state = source;
                for (var i = 0; i < route.Count; i++)
                {
                    if (!resolver.TryApplyMove(ctx, state, route[i], out var next, out _))
                    {
                        throw new InvalidOperationException(
                            $"Level {ctx.LevelId}: the real board rejected {route[i]}, step {i + 1} of a " +
                            $"{route.Count}-move route to the next clear found on the smaller copy.");
                    }

                    var cleared = next.TotalClearCount > source.TotalClearCount;
                    var isLast = i == route.Count - 1;
                    if (cleared != isLast)
                    {
                        throw new InvalidOperationException(
                            $"Level {ctx.LevelId}: step {i + 1} of a {route.Count}-move route to the next clear " +
                            (cleared ? "cleared early" : "did not clear") + " on the real board.");
                    }

                    state = next;
                }

                return state;
            }

            public SolveResult Result(SolveStatus status, IReadOnlyList<Move> solution, BoardState initial)
            {
                var lowerBound = status == SolveStatus.Unsolvable ? 0 : AStarStrategy.EstimateRemainingMoves(ctx, initial);
                return new SolveResult(
                    status, solution, lowerBound, explored, peakFrontier, peakRetained, stopwatch.ElapsedMilliseconds);
            }

            private static IReadOnlyList<Move> ToSourceRoute(
                NextClearAbstraction abstraction,
                Dictionary<BoardState, (BoardState from, Move move)> parents,
                BoardState beforeClear,
                Move clearingMove)
            {
                var route = new List<Move> { clearingMove };
                for (var at = beforeClear; parents[at].from != null; at = parents[at].from)
                {
                    route.Add(parents[at].move);
                }

                route.Reverse();

                if (abstraction != null)
                {
                    for (var i = 0; i < route.Count; i++)
                    {
                        route[i] = abstraction.ToSourceMove(route[i]);
                    }
                }

                return route;
            }
        }

        private readonly struct Entry
        {
            public Entry(BoardState state, int blockers, int depth, long sequence)
            {
                State = state;
                Blockers = blockers;
                Depth = depth;
                Sequence = sequence;
            }

            public BoardState State { get; }

            public int Blockers { get; }

            public int Depth { get; }

            public long Sequence { get; }

            /// <summary>Fewer blockers first, then fewer moves, then earlier discovery — a total order.</summary>
            public bool Precedes(Entry other)
            {
                if (Blockers != other.Blockers)
                {
                    return Blockers < other.Blockers;
                }

                if (Depth != other.Depth)
                {
                    return Depth < other.Depth;
                }

                return Sequence < other.Sequence;
            }
        }

        /// <summary>
        /// Array-backed binary min-heap of <see cref="Entry"/> values in
        /// <see cref="Entry.Precedes"/> order.
        /// </summary>
        private sealed class OpenSet
        {
            /// <summary>Starting array length; the array doubles when full. A growth seed, not a limit.</summary>
            private const int InitialCapacity = 64;

            private Entry[] items = new Entry[InitialCapacity];

            public int Count { get; private set; }

            public void Push(Entry entry)
            {
                if (Count == items.Length)
                {
                    Array.Resize(ref items, items.Length * 2);
                }

                var index = Count++;
                while (index > 0)
                {
                    var parent = (index - 1) / 2;
                    if (!entry.Precedes(items[parent]))
                    {
                        break;
                    }

                    items[index] = items[parent];
                    index = parent;
                }

                items[index] = entry;
            }

            public Entry Pop()
            {
                var top = items[0];
                Count--;
                var last = items[Count];
                items[Count] = default;

                if (Count > 0)
                {
                    var index = 0;
                    while (true)
                    {
                        var child = 2 * index + 1;
                        if (child >= Count)
                        {
                            break;
                        }

                        var right = child + 1;
                        if (right < Count && items[right].Precedes(items[child]))
                        {
                            child = right;
                        }

                        if (!items[child].Precedes(last))
                        {
                            break;
                        }

                        items[index] = items[child];
                        index = child;
                    }

                    items[index] = last;
                }

                return top;
            }
        }
    }
}
