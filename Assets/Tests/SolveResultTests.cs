using System;
using GateRush.Core;
using GateRush.Solver;
using NUnit.Framework;
using static GateRush.Tests.SearchCorpus;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="SolveResult"/>'s split between existence and quality:
    /// a solution is proven shortest exactly when the lower bound reaches its
    /// length, no other path marks it proven, the constructor refuses bounds no
    /// correct strategy could produce, and the bound a mode-optimal search
    /// claims depends on whether its move set is the player's.
    /// </summary>
    public class SolveResultTests
    {
        private static Move[] Moves(int count)
        {
            var moves = new Move[count];
            for (var i = 0; i < count; i++)
            {
                moves[i] = new Move(0, new Coord(i, 0));
            }

            return moves;
        }

        private static SolveResult Result(SolveStatus status, int solutionLength, int lowerBound) =>
            new SolveResult(status, Moves(solutionLength), lowerBound, 0, 0, 0, 0);

        // ----- Proven shortest is derived from the bound -----------------

        [Test]
        public void ProvenShortestLength_SolvableWithBoundEqualToLength_IsTheLength()
        {
            var result = Result(SolveStatus.Solvable, solutionLength: 3, lowerBound: 3);

            Assert.AreEqual(3, result.ProvenShortestLength);
        }

        [Test]
        public void ProvenShortestLength_SolvableWithBoundBelowLength_IsNull()
        {
            var result = Result(SolveStatus.Solvable, solutionLength: 3, lowerBound: 2);

            Assert.IsNull(result.ProvenShortestLength);
            Assert.AreEqual(2, result.LengthLowerBound);
        }

        [Test]
        public void ProvenShortestLength_AlreadySolvedInitialState_IsZero()
        {
            var result = Result(SolveStatus.Solvable, solutionLength: 0, lowerBound: 0);

            Assert.AreEqual(0, result.ProvenShortestLength);
        }

        [Test]
        public void ProvenShortestLength_IndeterminateOrUnsolvable_IsNull()
        {
            var indeterminate = Result(SolveStatus.Indeterminate, solutionLength: 0, lowerBound: 0);
            var unsolvable = Result(SolveStatus.Unsolvable, solutionLength: 0, lowerBound: 0);

            Assert.IsNull(indeterminate.ProvenShortestLength);
            Assert.IsNull(unsolvable.ProvenShortestLength);
        }

        // ----- Bounds no correct strategy produces -----------------------

        [Test]
        public void Constructor_BoundAboveSolutionLength_Throws()
        {
            Assert.Throws<ArgumentException>(() => Result(SolveStatus.Solvable, solutionLength: 2, lowerBound: 3));
        }

        [Test]
        public void Constructor_NegativeBound_Throws()
        {
            Assert.Throws<ArgumentException>(() => Result(SolveStatus.Indeterminate, solutionLength: 0, lowerBound: -1));
        }

        [Test]
        public void Constructor_UnsolvableWithNonZeroBound_Throws()
        {
            Assert.Throws<ArgumentException>(() => Result(SolveStatus.Unsolvable, solutionLength: 0, lowerBound: 1));
        }

        // ----- The bound a mode-optimal search claims ---------------------

        [Test]
        public void LowerBoundForModeOptimalSearch_ExhaustiveSolution_IsItsOwnLength()
        {
            var ctx = StepAsideBeforeFirstClearBoard();

            var bound = SolveResult.LowerBoundForModeOptimalSearch(
                SolveStatus.Solvable, 3, MoveGenMode.Exhaustive, ctx, BoardState.CreateInitial(ctx));

            Assert.AreEqual(3, bound);
        }

        [Test]
        public void LowerBoundForModeOptimalSearch_CanonicalSolution_FallsBackToTheHeuristic()
        {
            var ctx = StepAsideBeforeFirstClearBoard();
            var initial = BoardState.CreateInitial(ctx);

            var bound = SolveResult.LowerBoundForModeOptimalSearch(
                SolveStatus.Solvable, 3, MoveGenMode.Canonical, ctx, initial);

            Assert.AreEqual(AStarStrategy.EstimateRemainingMoves(ctx, initial), bound);
            Assert.AreEqual(2, bound);
        }

        [Test]
        public void LowerBoundForModeOptimalSearch_Indeterminate_FallsBackToTheHeuristicInEitherMode()
        {
            var ctx = StepAsideBeforeFirstClearBoard();
            var initial = BoardState.CreateInitial(ctx);

            foreach (var mode in new[] { MoveGenMode.Canonical, MoveGenMode.Exhaustive })
            {
                var bound = SolveResult.LowerBoundForModeOptimalSearch(
                    SolveStatus.Indeterminate, 0, mode, ctx, initial);

                Assert.AreEqual(2, bound, mode.ToString());
            }
        }

        [Test]
        public void LowerBoundForModeOptimalSearch_Unsolvable_IsZero()
        {
            var ctx = StepAsideBeforeFirstClearBoard();

            var bound = SolveResult.LowerBoundForModeOptimalSearch(
                SolveStatus.Unsolvable, 0, MoveGenMode.Exhaustive, ctx, BoardState.CreateInitial(ctx));

            Assert.AreEqual(0, bound);
        }
    }
}
