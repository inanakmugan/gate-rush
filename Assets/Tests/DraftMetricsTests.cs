using System.Linq;
using GateRush.Core;
using GateRush.Editor;
using GateRush.Solver;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="DraftMetrics"/>: the packing numbers on a known board,
    /// the opening branching factor against <see cref="MoveGenerator"/>'s own
    /// output, ready-opening-move detection, and that the suggested time budget
    /// rises with solution length and with available M10 bonuses (D12).
    /// </summary>
    public class DraftMetricsTests
    {
        private static readonly TimeBudgetFormula Formula = new TimeBudgetFormula(10, 3, 5);

        private static BlockDraft RedBlock(int id, Coord origin, int timeBonusSeconds = 0) =>
            new BlockDraft
            {
                Id = id,
                Cells = { new Coord(0, 0) },
                ColorStack = { BlockColor.Red },
                StartOrigin = origin,
                TimeBonusSeconds = timeBonusSeconds,
            };

        [Test]
        public void Compute_EmptyCellsAndFillRatio_OnAKnownBoard()
        {
            var draft = LevelDraft.NewEmpty(3, 3);
            draft.StaticWalls.Add(new Coord(2, 2));
            draft.Blocks.Add(new BlockDraft
            {
                Id = 1, Cells = { new Coord(0, 0), new Coord(1, 0) }, ColorStack = { BlockColor.Red },
                StartOrigin = new Coord(0, 0),
            });

            var metrics = DraftMetrics.Compute(draft, Formula);

            Assert.AreEqual(8, metrics.PlayableCellCount);
            Assert.AreEqual(2, metrics.OccupiedCellCount);
            Assert.AreEqual(6, metrics.EmptyCellCount);
            Assert.AreEqual(0.25f, metrics.FillRatio, 0.0001f);
        }

        [Test]
        public void Compute_OpeningBranchingFactor_MatchesMoveGeneratorOutput()
        {
            var draft = LevelDraft.NewEmpty(3, 3);
            draft.Blocks.Add(RedBlock(1, new Coord(1, 1)));

            var ctx = draft.ToContext();
            var expected = new MoveGenerator()
                .Generate(ctx, BoardState.CreateInitial(ctx), MoveGenMode.Exhaustive)
                .Count();

            var metrics = DraftMetrics.Compute(draft, Formula);

            Assert.AreEqual(expected, metrics.OpeningBranchingFactor);
        }

        [Test]
        public void Compute_OpeningBranchingFactor_MinusOneWhenDraftIsNotAValidLevel()
        {
            var draft = LevelDraft.NewEmpty(3, 3);
            draft.Blocks.Add(RedBlock(1, new Coord(9, 9)));

            var metrics = DraftMetrics.Compute(draft, Formula);

            Assert.AreEqual(-1, metrics.OpeningBranchingFactor);
        }

        [Test]
        public void Compute_HasReadyOpeningMove_PositiveAndNegative()
        {
            var withReady = LevelDraft.NewEmpty(3, 3);
            withReady.Blocks.Add(RedBlock(1, new Coord(1, 0)));
            withReady.Gates.Add(new GateDraft
            {
                Id = 1, Edge = BoardEdge.Bottom, Offset = 1, Width = 1, Color = BlockColor.Red,
            });

            var withoutReady = LevelDraft.NewEmpty(3, 3);
            withoutReady.Blocks.Add(RedBlock(1, new Coord(1, 1)));
            withoutReady.Gates.Add(new GateDraft
            {
                Id = 1, Edge = BoardEdge.Bottom, Offset = 1, Width = 1, Color = BlockColor.Red,
            });

            Assert.IsTrue(DraftMetrics.Compute(withReady, Formula).HasReadyOpeningMove);
            Assert.IsFalse(DraftMetrics.Compute(withoutReady, Formula).HasReadyOpeningMove);
        }

        [Test]
        public void Compute_SuggestedTimeBudget_RisesWithSolutionLengthAndWithBonuses()
        {
            var plain = LevelDraft.NewEmpty(3, 3);
            plain.Blocks.Add(RedBlock(1, new Coord(0, 0)));

            var withBonus = LevelDraft.NewEmpty(3, 3);
            withBonus.Blocks.Add(RedBlock(1, new Coord(0, 0), timeBonusSeconds: 12));

            var shortSolve = DraftMetrics.Compute(plain, Formula, solutionMoveCount: 2).SuggestedTimeBudgetSeconds;
            var longSolve = DraftMetrics.Compute(plain, Formula, solutionMoveCount: 8).SuggestedTimeBudgetSeconds;
            var withBonusSolve = DraftMetrics.Compute(withBonus, Formula, solutionMoveCount: 2).SuggestedTimeBudgetSeconds;

            Assert.Greater(longSolve.Value, shortSolve.Value);
            Assert.Greater(withBonusSolve.Value, shortSolve.Value);
        }

        [Test]
        public void Compute_SuggestedTimeBudget_NullWhenNoSolutionLengthGiven()
        {
            var draft = LevelDraft.NewEmpty(3, 3);
            draft.Blocks.Add(RedBlock(1, new Coord(0, 0)));

            Assert.IsNull(DraftMetrics.Compute(draft, Formula).SuggestedTimeBudgetSeconds);
        }
    }
}
