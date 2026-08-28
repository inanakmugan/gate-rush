using System;
using System.Collections.Generic;
using System.Diagnostics;
using GateRush.Core;

namespace GateRush.Solver
{
    /// <summary>
    /// Breadth-first search over board states — the reference
    /// <see cref="ISearchStrategy"/>. A plain FIFO frontier makes the first
    /// solution the search reaches a shortest one; progress-vector stratification
    /// (<c>DECISIONS.md</c> D6) is layered on top without touching that queue,
    /// scoping and releasing the visited set only.
    /// </summary>
    /// <remarks>
    /// <para><b>Back-pointers, not paths.</b> Each <see cref="Node"/> holds a
    /// reference to its parent node and the move that produced it; the solution
    /// is rebuilt by walking parents. A move list per node would multiply memory
    /// by solution length, and a global <c>BoardState -&gt; BoardState</c>
    /// back-pointer map would pin every visited state for the whole search,
    /// cancelling the stratification benefit. Parent references instead let the
    /// garbage collector reclaim a retired stratum's nodes except the few still
    /// pinned as ancestors of live frontier nodes.</para>
    ///
    /// <para><b>Stratification.</b> The visited set is
    /// <c>SortedDictionary&lt;ProgressVector, Dictionary&lt;BoardState, Node&gt;&gt;</c>.
    /// A stratum's inner dictionary is retired — dropped whole — once every node
    /// still in the frontier queue sits at a strictly greater
    /// <see cref="ProgressVector"/>. Monotonicity (D6) then guarantees no state of
    /// that stratum can ever be generated again, because every future successor
    /// descends from a queued node whose vector already exceeds the retired one.
    /// Dedup correctness is therefore untouched and results are identical to a
    /// non-stratified run — only peak visited-set memory differs. Pass
    /// <c>stratifyVisitedSet: false</c> to keep every stratum; that is the
    /// baseline the equivalence regression test compares against.</para>
    ///
    /// <para><b>Buffer ownership (D31).</b> Each <see cref="Search"/> call
    /// constructs its own <see cref="MoveGenerator"/> and <see cref="MoveResolver"/>
    /// — each of which owns a private <c>BlockReachability</c> — as locals.
    /// <see cref="MoveGenerator.Generate"/> returns a fully materialised list, so
    /// iterating it while the resolver runs its own flood fill never aliases a
    /// live scan buffer, and no <c>BlockReachability</c> is ever shared or nested.
    /// Not thread-safe; the solver is single-threaded and WebGL has no threads
    /// anyway.</para>
    /// </remarks>
    public sealed class BreadthFirstStrategy : ISearchStrategy
    {
        private readonly bool stratifyVisitedSet;

        /// <param name="stratifyVisitedSet">
        /// When true (the default), retire a progress stratum's visited entries
        /// once the frontier has moved entirely past it, bounding peak memory to
        /// roughly the largest single stratum. When false, keep every stratum —
        /// the plain global visited set, used as the equivalence baseline.
        /// </param>
        public BreadthFirstStrategy(bool stratifyVisitedSet = true)
        {
            this.stratifyVisitedSet = stratifyVisitedSet;
        }

        /// <summary>
        /// How often the wall-clock budget is polled — once every this many
        /// expansions rather than every one. <c>Stopwatch.ElapsedMilliseconds</c>
        /// queries the OS timer on each read, and this is the hottest loop in the
        /// project; the budget is a coarse safety ceiling, so a few hundred extra
        /// expansions before it trips do not matter.
        /// </summary>
        private const int WallClockPollInterval = 1024;

        /// <inheritdoc />
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
                return new SolveResult(
                    SolveStatus.Solvable, Array.Empty<Move>(),
                    exploredStateCount: 0, peakFrontierSize: 0,
                    peakRetainedStateCount: 0, elapsedMs: stopwatch.ElapsedMilliseconds);
            }

            var generator = new MoveGenerator();
            var resolver = new MoveResolver();

            var visited = new SortedDictionary<ProgressVector, Dictionary<BoardState, Node>>();
            var queue = new Queue<Node>();
            var queuedByVector = new SortedDictionary<ProgressVector, int>();

            var explored = 0;
            var peakFrontier = 0;
            var retainedCount = 0;
            var peakRetained = 0;

            // Set when a budget limit prevented full exploration, so a drained
            // queue is reported Indeterminate rather than Unsolvable (D4).
            var truncated = false;

            var rootNode = new Node(initial, parent: null, move: default, depth: 0, vector: initial.ProgressVector);
            AddVisited(visited, rootNode, ref retainedCount);
            queue.Enqueue(rootNode);
            Increment(queuedByVector, rootNode.Vector);
            peakFrontier = 1;
            peakRetained = retainedCount;

            while (queue.Count > 0)
            {
                // Checked before every expansion (the spec's note): one stratum
                // can exceed a budget on its own, so a per-level check would miss
                // it. The explored-state count is a cheap int compare; the
                // wall clock is polled only every WallClockPollInterval.
                if (explored >= budget.MaxExploredStates
                    || (explored > 0 && explored % WallClockPollInterval == 0
                        && stopwatch.ElapsedMilliseconds > budget.MaxWallClockMs))
                {
                    truncated = true;
                    break;
                }

                var node = queue.Dequeue();
                Decrement(queuedByVector, node.Vector);
                explored++;

                if (node.Depth >= budget.MaxDepth)
                {
                    // A node at MaxDepth is dequeued, marked truncated, and
                    // skipped — never expanded. A solution of exactly MaxDepth
                    // moves is still found: it is caught below as a *child* of a
                    // depth-(MaxDepth - 1) node during that node's expansion, not
                    // by expanding the depth-MaxDepth node itself. Draining
                    // continues because a shallower branch may still hold a
                    // solution within the limit.
                    truncated = true;
                    RetireStrata(visited, queuedByVector, ref retainedCount);
                    continue;
                }

                foreach (var move in generator.Generate(ctx, node.State, budget.Mode))
                {
                    // Time bonuses (M10) are outside the search space (D12); the
                    // solver discards them.
                    if (!resolver.TryApplyMove(ctx, node.State, move, out var successor, out _))
                    {
                        // The canonical and exhaustive move sets are both
                        // subsets of what the resolver accepts (Module 04's
                        // tests pin this), so this branch should not be taken;
                        // skipping rather than throwing keeps a generator bug
                        // from crashing an editor session.
                        continue;
                    }

                    var vector = successor.ProgressVector;
                    if (IsVisited(visited, vector, successor))
                    {
                        continue;
                    }

                    var child = new Node(successor, node, move, node.Depth + 1, vector);
                    AddVisited(visited, child, ref retainedCount);
                    if (retainedCount > peakRetained)
                    {
                        peakRetained = retainedCount;
                    }

                    if (successor.IsSolved(ctx))
                    {
                        stopwatch.Stop();
                        return new SolveResult(
                            SolveStatus.Solvable, Reconstruct(child),
                            explored, peakFrontier, peakRetained, stopwatch.ElapsedMilliseconds);
                    }

                    queue.Enqueue(child);
                    Increment(queuedByVector, vector);

                    // Updated per enqueue, not once after the loop, so the
                    // expansion that returns a solution still reports the peak
                    // the queue reached while its earlier children went in.
                    if (queue.Count > peakFrontier)
                    {
                        peakFrontier = queue.Count;
                    }
                }

                RetireStrata(visited, queuedByVector, ref retainedCount);
            }

            stopwatch.Stop();
            return new SolveResult(
                truncated ? SolveStatus.Indeterminate : SolveStatus.Unsolvable,
                Array.Empty<Move>(),
                explored, peakFrontier, peakRetained, stopwatch.ElapsedMilliseconds);
        }

        /// <summary>
        /// Drops every visited stratum whose vector is strictly below the lowest
        /// vector still represented in the frontier queue. Safe because a future
        /// successor can only descend from a queued node, and every queued node's
        /// vector is at least that lowest one, so nothing lexicographically —
        /// hence nothing componentwise — at or below a dropped stratum can be
        /// produced again (D6).
        /// </summary>
        private void RetireStrata(
            SortedDictionary<ProgressVector, Dictionary<BoardState, Node>> visited,
            SortedDictionary<ProgressVector, int> queuedByVector,
            ref int retainedCount)
        {
            if (!stratifyVisitedSet || queuedByVector.Count == 0)
            {
                return;
            }

            var minQueued = FirstKey(queuedByVector);

            while (visited.Count > 0)
            {
                var lowest = FirstKey(visited);
                if (lowest.CompareTo(minQueued) >= 0)
                {
                    break;
                }

                retainedCount -= visited[lowest].Count;
                visited.Remove(lowest);
            }
        }

        private static bool IsVisited(
            SortedDictionary<ProgressVector, Dictionary<BoardState, Node>> visited,
            ProgressVector vector, BoardState state)
        {
            return visited.TryGetValue(vector, out var stratum) && stratum.ContainsKey(state);
        }

        private static void AddVisited(
            SortedDictionary<ProgressVector, Dictionary<BoardState, Node>> visited,
            Node node, ref int retainedCount)
        {
            if (!visited.TryGetValue(node.Vector, out var stratum))
            {
                stratum = new Dictionary<BoardState, Node>();
                visited.Add(node.Vector, stratum);
            }

            stratum.Add(node.State, node);
            retainedCount++;
        }

        private static void Increment(SortedDictionary<ProgressVector, int> counts, ProgressVector vector)
        {
            counts.TryGetValue(vector, out var current);
            counts[vector] = current + 1;
        }

        private static void Decrement(SortedDictionary<ProgressVector, int> counts, ProgressVector vector)
        {
            var remaining = counts[vector] - 1;
            if (remaining == 0)
            {
                counts.Remove(vector);
            }
            else
            {
                counts[vector] = remaining;
            }
        }

        /// <summary>
        /// The smallest key of a non-empty <see cref="SortedDictionary{TKey,TValue}"/>,
        /// read through the struct enumerator so no allocation or LINQ is
        /// involved on this per-expansion path.
        /// </summary>
        private static ProgressVector FirstKey<TValue>(SortedDictionary<ProgressVector, TValue> dict)
        {
            foreach (var key in dict.Keys)
            {
                return key;
            }

            throw new InvalidOperationException("FirstKey called on an empty dictionary.");
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
        /// One breadth-first frontier entry. <see cref="Parent"/> is the node's
        /// back-pointer — see the type remarks on why this, not a stored path or
        /// a global map.
        /// </summary>
        private sealed class Node
        {
            public Node(BoardState state, Node parent, Move move, int depth, ProgressVector vector)
            {
                State = state;
                Parent = parent;
                Move = move;
                Depth = depth;
                Vector = vector;
            }

            public BoardState State { get; }

            public Node Parent { get; }

            public Move Move { get; }

            public int Depth { get; }

            public ProgressVector Vector { get; }
        }
    }
}
