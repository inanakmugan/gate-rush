using System;
using System.Collections.Generic;
using System.Linq;
using GateRush.Core;
using GateRush.Solver;

namespace GateRush.Editor
{
    /// <summary>
    /// The numbers the editor's footer panel shows about a draft: how tightly it
    /// is packed (D16), the branching factor at the opening position, whether a
    /// ready opening move exists, the total of its time bonuses, and — once the
    /// solver has run — the explored state count, the largest stratum, and a
    /// suggested time budget (D12) that leaves those bonuses out (D54).
    /// </summary>
    /// <remarks>
    /// Everything here except the post-solve numbers is a cheap scan, recomputed
    /// on every edit. The packing numbers, the branching factor and the
    /// ready-opening-move check read the level's starting board — the initial
    /// <see cref="BoardState"/> the solver and the game start from — and so
    /// need a valid <see cref="LevelContext"/>; when the draft does not form
    /// one, <see cref="HasStartingBoard"/> is false,
    /// <see cref="OccupiedCellCount"/>, <see cref="EmptyCellCount"/> and
    /// <see cref="OpeningBranchingFactor"/> are <see cref="NotAvailable"/>,
    /// and <see cref="HasReadyOpeningMove"/> is false.
    /// </remarks>
    public sealed class DraftMetrics
    {
        /// <summary>
        /// What a count that reads the starting board holds when the draft
        /// does not form a valid level, so there is no board to read.
        /// </summary>
        public const int NotAvailable = -1;

        /// <summary>
        /// Whether the draft forms a valid level, and so has a starting board
        /// for the packing numbers and the opening checks to read.
        /// </summary>
        public bool HasStartingBoard { get; private set; }

        /// <summary>Grid cells that are not static walls.</summary>
        public int PlayableCellCount { get; private set; }

        /// <summary>
        /// Cells a block occupies when the level starts, as the player first
        /// meets it: authored blocks and every block spawned before the first
        /// move (D42). Counted on the initial <see cref="BoardState"/>, never
        /// on the draft's block list, so it cannot drift from the rules.
        /// <see cref="NotAvailable"/> without a starting board.
        /// </summary>
        public int OccupiedCellCount { get; private set; }

        /// <summary>
        /// Playable cells no block occupies when the level starts (D16 tight
        /// packing). <see cref="NotAvailable"/> without a starting board.
        /// </summary>
        public int EmptyCellCount { get; private set; }

        /// <summary>
        /// Occupied over playable, 0..1. Zero when there are no playable
        /// cells, and without a starting board.
        /// </summary>
        public float FillRatio { get; private set; }

        /// <summary>
        /// The number of distinct moves the exhaustive generator emits from the
        /// initial state — the real player branching factor.
        /// <see cref="NotAvailable"/> when the draft does not form a valid
        /// level.
        /// </summary>
        public int OpeningBranchingFactor { get; private set; }

        /// <summary>Whether some block starts flush against a matching open gate (D16).</summary>
        public bool HasReadyOpeningMove { get; private set; }

        /// <summary>
        /// The total of every M10 time bonus in the draft: placed blocks,
        /// elevator wave blocks and generator queue entries. A won level has
        /// destroyed every block, so the player collects all of it. Shown
        /// beside <see cref="SuggestedTimeBudgetSeconds"/> as information and
        /// never added to it (D54). Read from the draft alone, so it is
        /// available whether or not the draft forms a level or has been solved.
        /// </summary>
        public int AvailableTimeBonusSeconds { get; private set; }

        /// <summary>
        /// <c>base + perMove * moves</c>, rounded up, from the last solve's
        /// length; null when no length was supplied. Time bonuses are not
        /// part of it (D54).
        /// </summary>
        public int? SuggestedTimeBudgetSeconds { get; private set; }

        /// <summary>
        /// True when <see cref="SuggestedTimeBudgetSeconds"/> was computed from a
        /// solution not proven shortest. Such a length can only overstate the
        /// optimum, so the budget errs generous; the editor labels it so it is
        /// never mistaken for the designed budget.
        /// </summary>
        public bool IsTimeBudgetFromUnprovenLength { get; private set; }

        public int? ExploredStateCount { get; private set; }
        public int? LargestStratum { get; private set; }

        /// <summary>
        /// Computes the metrics. The solution lengths,
        /// <paramref name="exploredStateCount"/> and
        /// <paramref name="largestStratum"/> come from the last solve — the
        /// window pulls them out of its <see cref="ValidationResult"/> and the
        /// winning stage's <see cref="SolveResult"/>.
        /// </summary>
        /// <param name="provenShortestLength">
        /// The last solve's <see cref="ValidationResult.ProvenShortestLength"/>.
        /// Preferred for the suggested time budget whenever present.
        /// </param>
        /// <param name="unprovenSolutionLength">
        /// The length of a solution the last solve found but did not prove
        /// shortest. Used for the budget only when no proven length is supplied,
        /// and then flagged through <see cref="IsTimeBudgetFromUnprovenLength"/>.
        /// A suggested time budget is produced only when one of the two lengths
        /// is supplied.
        /// </param>
        public static DraftMetrics Compute(
            LevelDraft draft,
            TimeBudgetFormula timeBudget,
            int? provenShortestLength = null,
            int? unprovenSolutionLength = null,
            int? exploredStateCount = null,
            int? largestStratum = null)
        {
            var metrics = new DraftMetrics();

            var playable = new HashSet<Coord>();
            for (var x = 0; x < draft.Width; x++)
            {
                for (var y = 0; y < draft.Height; y++)
                {
                    playable.Add(new Coord(x, y));
                }
            }

            foreach (var wall in draft.StaticWalls)
            {
                playable.Remove(wall);
            }

            metrics.PlayableCellCount = playable.Count;
            metrics.AvailableTimeBonusSeconds = TotalTimeBonusSeconds(draft);

            metrics.HasStartingBoard = false;
            metrics.OccupiedCellCount = NotAvailable;
            metrics.EmptyCellCount = NotAvailable;
            metrics.FillRatio = 0f;
            metrics.OpeningBranchingFactor = NotAvailable;
            metrics.HasReadyOpeningMove = false;

            LevelContext ctx = null;
            try
            {
                ctx = draft.ToContext();
            }
            catch (Exception)
            {
                ctx = null;
            }

            if (ctx != null)
            {
                var initial = BoardState.CreateInitial(ctx);

                // The starting board, not the draft's block list: it already
                // holds whatever spawned before the first move (D42), and a
                // valid level keeps every living block on playable cells.
                var occupied = new HashSet<Coord>();
                for (var i = 0; i < ctx.TotalBlockCapacity; i++)
                {
                    foreach (var cell in initial.OccupiedCells(ctx, i))
                    {
                        occupied.Add(cell);
                    }
                }

                metrics.HasStartingBoard = true;
                metrics.OccupiedCellCount = occupied.Count;
                metrics.EmptyCellCount = playable.Count - occupied.Count;
                metrics.FillRatio = playable.Count == 0 ? 0f : occupied.Count / (float)playable.Count;

                metrics.OpeningBranchingFactor =
                    new MoveGenerator().Generate(ctx, initial, MoveGenMode.Exhaustive).Count();

                for (var i = 0; i < ctx.TotalBlockCapacity; i++)
                {
                    if (BlockReachability.CanClearInPlace(ctx, initial, i))
                    {
                        metrics.HasReadyOpeningMove = true;
                        break;
                    }
                }
            }

            metrics.ExploredStateCount = exploredStateCount;
            metrics.LargestStratum = largestStratum;

            if (provenShortestLength.HasValue)
            {
                metrics.SuggestedTimeBudgetSeconds = timeBudget.Suggest(provenShortestLength.Value);
            }
            else if (unprovenSolutionLength.HasValue)
            {
                metrics.SuggestedTimeBudgetSeconds = timeBudget.Suggest(unprovenSolutionLength.Value);
                metrics.IsTimeBudgetFromUnprovenLength = true;
            }

            return metrics;
        }

        /// <summary>
        /// Every M10 bonus in the level — top-level and spawned
        /// (<see cref="AvailableTimeBonusSeconds"/>).
        /// </summary>
        private static int TotalTimeBonusSeconds(LevelDraft draft)
        {
            var total = 0;

            foreach (var block in draft.Blocks)
            {
                total += block.TimeBonusSeconds;
            }

            foreach (var generator in draft.Generators)
            {
                foreach (var spawned in generator.Queue)
                {
                    total += spawned.TimeBonusSeconds;
                }
            }

            foreach (var elevator in draft.Elevators)
            {
                foreach (var wave in elevator.Waves)
                {
                    foreach (var spawned in wave.Blocks)
                    {
                        total += spawned.TimeBonusSeconds;
                    }
                }
            }

            return total;
        }
    }
}
