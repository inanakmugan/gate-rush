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
    /// ready opening move exists, and — once the solver has run — the explored
    /// state count, the largest stratum, and a suggested time budget (D12).
    /// </summary>
    /// <remarks>
    /// Everything here except the post-solve numbers is a cheap scan, recomputed
    /// on every edit. The branching factor and the ready-opening-move check need
    /// a valid <see cref="LevelContext"/>; when the draft does not form one,
    /// <see cref="OpeningBranchingFactor"/> is <c>-1</c> and
    /// <see cref="HasReadyOpeningMove"/> is false.
    /// </remarks>
    public sealed class DraftMetrics
    {
        /// <summary>Grid cells that are not static walls.</summary>
        public int PlayableCellCount { get; private set; }

        /// <summary>Playable cells a top-level block occupies at level start.</summary>
        public int OccupiedCellCount { get; private set; }

        /// <summary>Playable cells no block occupies at level start (D16 tight packing).</summary>
        public int EmptyCellCount { get; private set; }

        /// <summary>Occupied over playable, 0..1. Zero when there are no playable cells.</summary>
        public float FillRatio { get; private set; }

        /// <summary>
        /// The number of distinct moves the exhaustive generator emits from the
        /// initial state — the real player branching factor. <c>-1</c> when the
        /// draft does not form a valid level.
        /// </summary>
        public int OpeningBranchingFactor { get; private set; }

        /// <summary>Whether some block starts flush against a matching open gate (D16).</summary>
        public bool HasReadyOpeningMove { get; private set; }

        public int? SuggestedTimeBudgetSeconds { get; private set; }
        public int? ExploredStateCount { get; private set; }
        public int? LargestStratum { get; private set; }

        /// <summary>
        /// Computes the metrics. <paramref name="solutionMoveCount"/>,
        /// <paramref name="exploredStateCount"/> and
        /// <paramref name="largestStratum"/> come from the last solve — the
        /// window pulls them out of its <see cref="LevelSolveResult"/> and the
        /// winning stage's <see cref="SolveResult"/>; a suggested time budget is
        /// produced only when a solution length is supplied.
        /// </summary>
        public static DraftMetrics Compute(
            LevelDraft draft,
            TimeBudgetFormula timeBudget,
            int? solutionMoveCount = null,
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

            var occupied = new HashSet<Coord>();
            foreach (var block in draft.Blocks)
            {
                foreach (var cell in block.Cells)
                {
                    var absolute = block.StartOrigin + cell;
                    if (playable.Contains(absolute))
                    {
                        occupied.Add(absolute);
                    }
                }
            }

            metrics.PlayableCellCount = playable.Count;
            metrics.OccupiedCellCount = occupied.Count;
            metrics.EmptyCellCount = playable.Count - occupied.Count;
            metrics.FillRatio = playable.Count == 0 ? 0f : occupied.Count / (float)playable.Count;

            metrics.OpeningBranchingFactor = -1;
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

                metrics.OpeningBranchingFactor =
                    new MoveGenerator().Generate(ctx, initial, MoveGenMode.Exhaustive).Count();

                for (var i = 0; i < ctx.TotalBlockCapacity; i++)
                {
                    if (initial.Alive[i]
                        && BlockReachability.IsAtCompatibleExitGate(ctx, initial, i, initial.Origins[i]))
                    {
                        metrics.HasReadyOpeningMove = true;
                        break;
                    }
                }
            }

            metrics.ExploredStateCount = exploredStateCount;
            metrics.LargestStratum = largestStratum;

            if (solutionMoveCount.HasValue)
            {
                metrics.SuggestedTimeBudgetSeconds =
                    timeBudget.Suggest(solutionMoveCount.Value, TotalTimeBonusSeconds(draft));
            }

            return metrics;
        }

        /// <summary>
        /// Every M10 bonus in the level — top-level and spawned. A solved level
        /// destroys every block, so every bonus eventually lands.
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
