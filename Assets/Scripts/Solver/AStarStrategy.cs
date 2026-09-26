using System;
using System.Collections.Generic;
using System.Diagnostics;
using GateRush.Core;

namespace GateRush.Solver
{
    /// <summary>
    /// A\* search over board states — the optimised <see cref="ISearchStrategy"/>.
    /// Returns the same optimum as <see cref="BreadthFirstStrategy"/> while
    /// expanding fewer states, because it expands in order of
    /// <c>f = g + h</c> rather than depth alone (<c>DECISIONS.md</c> D3).
    /// </summary>
    /// <remarks>
    /// <para><b>Heuristic.</b> <c>h = C − F</c>, computed by
    /// <see cref="EstimateRemainingMoves"/>. <c>C</c> is every colour still to be
    /// cleared: the remaining stack of each living block plus the full stack of
    /// each not-yet-spawned generator or elevator slot. <c>F</c> is the number of
    /// "free clears" still possible: locks that are still locked, whose owner is
    /// alive or not yet spawned, and that either have
    /// <see cref="KeyEffect.ClearOuterColor"/> waiting for a shutter to open
    /// (<see cref="BoardState.WaitingKeyEffect"/>, <c>DECISIONS.md</c> D41) or,
    /// with nothing waiting, are targeted by at least one unconsumed
    /// <see cref="KeyEffect.ClearOuterColor"/> key. A lock with
    /// <see cref="KeyEffect.UnlockMovement"/> waiting is not counted: its
    /// completing key has already decided, and later keys change nothing
    /// (M8).</para>
    ///
    /// <para><b>Free clears.</b> A move clears at most one colour through a
    /// gate (<c>p ≤ 1</c>). The fixpoint loop can add <c>b</c> more, each a
    /// lock firing <see cref="KeyEffect.ClearOuterColor"/> on its owner —
    /// either on completion, or released by a shutter opening (D41), and one
    /// opening can release several. The lock-or-key rule stops each chain
    /// there, because the cleared owner holds a lock and so carries no key
    /// (Module 07). Each lock fires at most once, since firing unlocks it.
    /// <c>F</c> counts a lock as soon as <em>any</em> unconsumed key could
    /// deliver the clear, deliberately: keys for one lock may carry different
    /// effects, and the one that completes the count decides. Overcounting
    /// <c>F</c> only lowers <c>h</c>; undercounting it would overestimate.</para>
    ///
    /// <para><b>Why it is consistent.</b> Every edge costs one move, so
    /// consistency means <c>h</c> drops by at most one per move. Three facts:
    /// (1) each of a move's <c>b</c> free clears belongs to a distinct lock that
    /// the same move unlocks, so that lock leaves <c>F</c>; (2) each such lock
    /// was in <c>F</c> before the move — firing on completion, its completing
    /// <see cref="KeyEffect.ClearOuterColor"/> key was still unconsumed and
    /// nothing was waiting; released, <see cref="KeyEffect.ClearOuterColor"/>
    /// was already waiting; completing and released in the same move, the key
    /// was unconsumed before it; (3) no lock ever joins <c>F</c> — unconsumed
    /// keys only disappear, locks only unlock, and a lock that starts waiting
    /// <see cref="KeyEffect.ClearOuterColor"/> was already counted through the
    /// very key that completed it, so it only switches the reason it is
    /// counted. Hence <c>ΔC = −(p + b)</c> and <c>ΔF = −b − L</c>, where
    /// <c>L ≥ 0</c> counts locks leaving <c>F</c> without firing (a lock's last
    /// <see cref="KeyEffect.ClearOuterColor"/> key consumed without completing
    /// it, an <see cref="KeyEffect.UnlockMovement"/> key completing it, whether
    /// applied or waiting). So <c>Δh = −p + L ≥ −1</c>.</para>
    ///
    /// <para><b>Spawning.</b> A not-yet-spawned block is counted in <c>C</c>,
    /// and in <c>F</c> if it owns a qualifying lock, before it spawns, so a
    /// spawn by itself changes neither. A lock whose keys complete before its
    /// block spawns (<c>DECISIONS.md</c> D42) behaves as under a closed
    /// shutter: its effect waits and it switches from being counted through
    /// its completing key to being counted through the waiting
    /// <see cref="KeyEffect.ClearOuterColor"/>. When the block spawns uncovered
    /// the effect applies — one more way a counted lock fires, so it is one
    /// more <c>b</c>: fact (2) holds because the lock was counted before the
    /// move, whether it began waiting in an earlier move or completed in this
    /// one through a key that was then unconsumed. Several spawns in one move
    /// each release a distinct lock. A block spawning under a closed shutter
    /// keeps waiting, and its opening is the D41 case above.</para>
    ///
    /// <para><b>Why it is admissible.</b> On a solved state <c>C = 0</c> and
    /// every lock's owner is dead, so <c>F = 0</c> and <c>h = 0</c>. A
    /// consistent heuristic that is zero on every goal never overestimates:
    /// summing <c>Δh ≥ −1</c> along any solution gives <c>moves ≥ h</c>.</para>
    ///
    /// <para>Consistency is what
    /// makes a closed set safe: a state is expanded with its shortest
    /// <c>g</c> already known, so a closed state is never reopened. Finding a
    /// strictly shorter path to one means the heuristic has been broken, and the
    /// search throws rather than return an optimum it can no longer vouch
    /// for.</para>
    ///
    /// <para><b>Open set.</b> A binary min-heap ordered by, in turn: lower
    /// <c>f</c>; on equal <c>f</c>, higher <c>g</c>, so the node further along —
    /// a goal in particular — comes out first; on equal <c>g</c>, lower insertion
    /// sequence. The order is total, so which node pops next never depends on
    /// the heap's internal layout, and <see cref="MoveGenerator"/>'s documented
    /// enumeration order fixes the sequence numbers. Searches are therefore
    /// reproducible. <c>System.Collections.Generic.PriorityQueue</c> is not
    /// available at this project's .NET Standard 2.1 API level.</para>
    ///
    /// <para><b>Duplicates.</b> One <see cref="Node"/> per state is current, held
    /// in a <c>Dictionary&lt;BoardState, Node&gt;</c>. A strictly shorter path to
    /// a state still open replaces its node and marks the old one superseded; the
    /// old heap entry is skipped when it pops (lazy deletion) and does not count
    /// as an expansion.</para>
    ///
    /// <para><b>Goal test on pop.</b> Unlike breadth-first search, A\* does not
    /// expand in depth order, so a goal is accepted only when it leaves the open
    /// set. <c>h == 0</c> is not used as the goal test:
    /// <see cref="BoardState.IsSolved"/> is the one definition of solved, and
    /// <c>C − F</c> can reach zero on an unsolved state — a not-yet-spawned
    /// single-colour lock with <see cref="KeyEffect.ClearOuterColor"/> already
    /// waiting (D42) contributes one to each.</para>
    ///
    /// <para><b>Cancellation.</b> <see cref="SearchBudget.Cancellation"/> is
    /// checked before every expansion and throws
    /// <see cref="OperationCanceledException"/>.</para>
    ///
    /// <para><b>Budget.</b> Same cadence as <see cref="BreadthFirstStrategy"/>:
    /// <see cref="SearchBudget.MaxExploredStates"/> before every expansion, the
    /// wall clock every <see cref="SearchBudget.WallClockPollInterval"/>
    /// expansions. A node at <see cref="SearchBudget.MaxDepth"/> is goal-tested
    /// but not expanded. A successor with <c>g + h</c> above
    /// <see cref="SearchBudget.MaxDepth"/> is dropped before it enters the open
    /// set — admissibility means it cannot reach a solution within the limit.
    /// Either cut marks the search truncated, so an emptied open set reports
    /// <see cref="SolveStatus.Indeterminate"/>, never
    /// <see cref="SolveStatus.Unsolvable"/> (D4). One consequence: on a board
    /// with no solution, a dropped successor can make this report
    /// <see cref="SolveStatus.Indeterminate"/> where breadth-first search, having
    /// exhausted that branch below the limit, reports
    /// <see cref="SolveStatus.Unsolvable"/>. Both are truthful; the difference
    /// only appears when the depth limit is close to <c>h</c>.</para>
    ///
    /// <para><b>No stratification.</b> <see cref="BreadthFirstStrategy"/> retires
    /// a progress stratum once the lowest queued progress vector moves past it,
    /// which its first-in, first-out queue makes happen steadily. A\* pops by
    /// <c>f</c> and interleaves strata, so that minimum rarely rises and
    /// retirement would free little. Memory is bounded instead by the states the
    /// search touches, a subset of what breadth-first search touches.
    /// <see cref="SolveResult.PeakRetainedStateCount"/> therefore equals the
    /// number of distinct states reached.</para>
    ///
    /// <para><b>Back-pointers and buffer ownership (D31)</b> follow
    /// <see cref="BreadthFirstStrategy"/>: nodes point at their parent and the
    /// move that produced them, and each <see cref="Search"/> call constructs its
    /// own <see cref="MoveGenerator"/> and <see cref="MoveResolver"/>. Not
    /// thread-safe.</para>
    /// </remarks>
    public sealed class AStarStrategy : ISearchStrategy
    {
        private readonly Func<MoveGenerator> generatorFactory;

        /// <summary>Creates a search that builds its own <see cref="MoveGenerator"/> per call.</summary>
        public AStarStrategy()
            : this(() => new MoveGenerator())
        {
        }

        /// <summary>
        /// Test seam: <paramref name="generatorFactory"/> supplies the generator
        /// each <see cref="Search"/> call uses, so a test can feed the search a
        /// move the resolver rejects.
        /// </summary>
        internal AStarStrategy(Func<MoveGenerator> generatorFactory)
        {
            this.generatorFactory = generatorFactory;
        }

        /// <inheritdoc />
        /// <exception cref="InvalidOperationException">
        /// A strictly shorter path reached a state that was already expanded —
        /// the heuristic has stopped being consistent. Indicates a rule change
        /// the heuristic has not caught up with, not a property of the level.
        /// Also thrown when the resolver rejects a move the generator emitted —
        /// a solver bug, never a property of the level.
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

            var stopwatch = Stopwatch.StartNew();

            if (initial.IsSolved(ctx))
            {
                stopwatch.Stop();
                return SolveResult.FromModeOptimalSearch(
                    SolveStatus.Solvable, Array.Empty<Move>(), budget.Mode, ctx, initial,
                    exploredStateCount: 0, peakFrontierSize: 0,
                    peakRetainedStateCount: 0, elapsedMs: stopwatch.ElapsedMilliseconds);
            }

            var generator = generatorFactory();
            var resolver = new MoveResolver();

            var nodesByState = new Dictionary<BoardState, Node>();
            var open = new OpenSet();
            long nextSequence = 0;

            var explored = 0;

            // Set when a budget limit prevented full exploration, so an emptied
            // open set is reported Indeterminate rather than Unsolvable (D4).
            var truncated = false;

            var root = new Node(
                initial, parent: null, move: default, g: 0,
                h: EstimateRemainingMoves(ctx, initial), sequence: nextSequence++);
            nodesByState.Add(initial, root);
            open.Push(root);
            var peakFrontier = 1;

            while (open.Count > 0)
            {
                var node = open.Pop();

                if (node.IsSuperseded)
                {
                    continue;
                }

                if (node.State.IsSolved(ctx))
                {
                    stopwatch.Stop();
                    return SolveResult.FromModeOptimalSearch(
                        SolveStatus.Solvable, Reconstruct(node), budget.Mode, ctx, initial,
                        explored, peakFrontier, nodesByState.Count, stopwatch.ElapsedMilliseconds);
                }

                // Checked after the stale-entry skip, so the wall clock is polled
                // once per expansion count rather than once per stale pop.
                if (explored >= budget.MaxExploredStates
                    || (explored > 0 && explored % SearchBudget.WallClockPollInterval == 0
                        && stopwatch.ElapsedMilliseconds > budget.MaxWallClockMs))
                {
                    truncated = true;
                    break;
                }

                budget.Cancellation.ThrowIfCancellationRequested();
                node.IsClosed = true;
                explored++;

                if (node.G >= budget.MaxDepth)
                {
                    truncated = true;
                    continue;
                }

                var childG = node.G + 1;

                foreach (var move in generator.Generate(ctx, node.State, budget.Mode))
                {
                    // Time bonuses (M10) are outside the search space (D12); the
                    // solver discards them.
                    if (!resolver.TryApplyMove(ctx, node.State, move, out var successor, out _))
                    {
                        throw RejectedMove.Error(ctx, move);
                    }

                    int h;
                    if (nodesByState.TryGetValue(successor, out var existing))
                    {
                        if (existing.G <= childG)
                        {
                            continue;
                        }

                        if (existing.IsClosed)
                        {
                            throw new InvalidOperationException(
                                $"A* reached an already-expanded state in {childG} moves after expanding it at " +
                                $"{existing.G} (level {ctx.LevelId}). The heuristic is no longer consistent, so " +
                                "the search cannot vouch for a shortest solution.");
                        }

                        // h is a function of the state alone, so the existing
                        // node's value is reused rather than recomputed.
                        h = existing.H;
                    }
                    else
                    {
                        h = EstimateRemainingMoves(ctx, successor);
                    }

                    if (childG + h > budget.MaxDepth)
                    {
                        truncated = true;
                        continue;
                    }

                    if (existing != null)
                    {
                        existing.IsSuperseded = true;
                    }

                    var child = new Node(successor, node, move, childG, h, nextSequence++);
                    nodesByState[successor] = child;
                    open.Push(child);

                    if (open.Count > peakFrontier)
                    {
                        peakFrontier = open.Count;
                    }
                }
            }

            stopwatch.Stop();
            return SolveResult.FromModeOptimalSearch(
                truncated ? SolveStatus.Indeterminate : SolveStatus.Unsolvable,
                Array.Empty<Move>(), budget.Mode, ctx, initial,
                explored, peakFrontier, nodesByState.Count, stopwatch.ElapsedMilliseconds);
        }

        /// <summary>
        /// The heuristic: a lower bound on the moves still needed to solve
        /// <paramref name="state"/>, <c>C − F</c>. See the type remarks for the
        /// definitions and the admissibility and consistency argument.
        /// </summary>
        /// <remarks>
        /// <c>internal</c> so Edit Mode tests can check the admissibility and
        /// consistency claims directly against the corpus, not only through the
        /// search's answers. Never reads time bonuses (D12).
        /// </remarks>
        internal static int EstimateRemainingMoves(LevelContext ctx, BoardState state)
        {
            var coloursRemaining = 0;
            var freeClears = 0;

            for (var i = 0; i < ctx.TotalBlockCapacity; i++)
            {
                // A slot that is not alive is either destroyed or not spawned yet;
                // only the second still has colours to clear (BoardState's index
                // scheme).
                if (!state.Alive[i] && state.Origins[i] != BoardState.UnspawnedOrigin)
                {
                    continue;
                }

                var spec = ctx.SpecAt(i);
                coloursRemaining += spec.ColorStack.Count - state.ClearedColors[i];

                if (spec.LockId.HasValue
                    && !state.Unlocked[i]
                    && CanStillClearForFree(ctx, state, i, spec.LockId.Value))
                {
                    freeClears++;
                }
            }

            return coloursRemaining - freeClears;
        }

        /// <summary>
        /// Whether a still-locked lock may yet fire
        /// <see cref="KeyEffect.ClearOuterColor"/>: decided by the waiting effect
        /// when its keys completed under a closed shutter (D41), otherwise by
        /// whether any unconsumed key could still deliver it.
        /// </summary>
        private static bool CanStillClearForFree(LevelContext ctx, BoardState state, int ownerIndex, int lockId)
        {
            var waiting = state.WaitingKeyEffect[ownerIndex];
            if (waiting.HasValue)
            {
                return waiting.Value == KeyEffect.ClearOuterColor;
            }

            return HasUnconsumedClearOuterColorKey(ctx, state, lockId);
        }

        private static bool HasUnconsumedClearOuterColorKey(LevelContext ctx, BoardState state, int lockId)
        {
            var keyIndices = ctx.KeyIndicesForLock(lockId);
            for (var k = 0; k < keyIndices.Count; k++)
            {
                var keyIndex = keyIndices[k];
                if (!state.KeyConsumed[keyIndex] && ctx.SpecAt(keyIndex).KeyEffect == KeyEffect.ClearOuterColor)
                {
                    return true;
                }
            }

            return false;
        }

        private static IReadOnlyList<Move> Reconstruct(Node solved)
        {
            var moves = new List<Move>();
            for (var current = solved; current.Parent != null; current = current.Parent)
            {
                moves.Add(current.Move);
            }

            moves.Reverse();
            return moves;
        }

        /// <summary>
        /// One search node. <see cref="Parent"/> is the back-pointer the solution
        /// is rebuilt from. <see cref="IsClosed"/> and <see cref="IsSuperseded"/>
        /// are the only mutable fields: the search's own bookkeeping, never
        /// visible outside it.
        /// </summary>
        private sealed class Node
        {
            public Node(BoardState state, Node parent, Move move, int g, int h, long sequence)
            {
                State = state;
                Parent = parent;
                Move = move;
                G = g;
                H = h;
                Sequence = sequence;
            }

            public BoardState State { get; }

            public Node Parent { get; }

            public Move Move { get; }

            /// <summary>Moves from the initial state along this node's path.</summary>
            public int G { get; }

            /// <summary><see cref="EstimateRemainingMoves"/> for <see cref="State"/>.</summary>
            public int H { get; }

            /// <summary>Insertion order into the open set; the final tie-break.</summary>
            public long Sequence { get; }

            /// <summary>True once the node has been expanded.</summary>
            public bool IsClosed { get; set; }

            /// <summary>
            /// True once a strictly shorter path to the same state replaced this
            /// node; its heap entry is skipped when popped.
            /// </summary>
            public bool IsSuperseded { get; set; }
        }

        /// <summary>
        /// Array-backed binary min-heap of <see cref="Node"/>s in the order the
        /// type remarks define.
        /// </summary>
        private sealed class OpenSet
        {
            /// <summary>
            /// Starting array length; the array doubles when full. A growth seed,
            /// not a limit — the budget bounds the search, not this.
            /// </summary>
            private const int InitialCapacity = 64;

            private Node[] items = new Node[InitialCapacity];

            public int Count { get; private set; }

            public void Push(Node node)
            {
                if (Count == items.Length)
                {
                    Array.Resize(ref items, items.Length * 2);
                }

                var index = Count++;
                while (index > 0)
                {
                    var parent = (index - 1) / 2;
                    if (!Precedes(node, items[parent]))
                    {
                        break;
                    }

                    items[index] = items[parent];
                    index = parent;
                }

                items[index] = node;
            }

            public Node Pop()
            {
                var top = items[0];
                Count--;
                var last = items[Count];
                items[Count] = null;

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
                        if (right < Count && Precedes(items[right], items[child]))
                        {
                            child = right;
                        }

                        if (!Precedes(items[child], last))
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

            private static bool Precedes(Node a, Node b)
            {
                var fa = a.G + a.H;
                var fb = b.G + b.H;
                if (fa != fb)
                {
                    return fa < fb;
                }

                if (a.G != b.G)
                {
                    return a.G > b.G;
                }

                return a.Sequence < b.Sequence;
            }
        }
    }
}
