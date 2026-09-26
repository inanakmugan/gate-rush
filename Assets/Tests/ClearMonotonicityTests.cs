using System;
using System.Collections.Generic;
using GateRush.Core;
using GateRush.Solver;
using NUnit.Framework;
using static GateRush.Tests.Solve;

namespace GateRush.Tests
{
    /// <summary>
    /// Checks the claim <see cref="LevelContext.IsClearMonotone"/> makes —
    /// that on such a level a clear never turns a solvable board unsolvable —
    /// directly, independent of any strategy that relies on it: over seeded
    /// random clear-monotone boards A\* proves solvable, every clear reachable
    /// from the first stratum must lead to a board A\* does not prove
    /// unsolvable. Boards the flag excludes are skipped, and one of them — a
    /// mixed-effect lock — is shown to break the claim, which is why the flag
    /// excludes it.
    /// </summary>
    /// <remarks>
    /// If a future rule change quietly breaks the claim — a clear that closes
    /// something, a move that cannot be undone — this is the test that should
    /// fail first, before the nearest-next-clear strategy starts reporting
    /// solvable levels as unsolvable.
    /// </remarks>
    public class ClearMonotonicityTests
    {
        private const int Seed = 20260929;
        private const int BoardCount = 300;
        private const int MaxSide = 3;
        private const int StratumCap = 3_000;
        private const int PostClearSamplesPerBoard = 6;
        private const int MinConfirmedPostClearStates = 100;
        private const int MinBoardsPerMechanic = 5;

        private static SearchBudget GroundTruth() =>
            Budget(MoveGenMode.Exhaustive, maxDepth: 200, maxExplored: 10_000, maxMs: 60_000);

        [Test]
        public void EveryReachableClear_FromASolvableBoard_LeavesItSolvable_OverSeededRandomBoards()
        {
            var rng = new Random(Seed);
            var generator = new MoveGenerator();
            var resolver = new MoveResolver();
            var confirmed = 0;
            var boardsWithLock = 0;
            var boardsWithShutter = 0;

            for (var b = 0; b < BoardCount; b++)
            {
                var ctx = RandomBoards.Next(rng, MaxSide);
                if (!ctx.IsClearMonotone)
                {
                    // The claim is not made for these; see the counterexample below.
                    continue;
                }

                var initial = BoardState.CreateInitial(ctx);
                if (new AStarStrategy().Search(ctx, initial, GroundTruth()).Status != SolveStatus.Solvable)
                {
                    continue;
                }

                var postClear = FirstStratumClears(ctx, initial, generator, resolver);
                if (postClear == null)
                {
                    continue;
                }

                boardsWithLock += HasLock(ctx) ? 1 : 0;
                boardsWithShutter += ctx.Shutters.Count > 0 ? 1 : 0;

                for (var k = 0; k < Math.Min(PostClearSamplesPerBoard, postClear.Count); k++)
                {
                    var after = postClear[rng.Next(postClear.Count)];
                    if (after.IsSolved(ctx))
                    {
                        continue;
                    }

                    var truth = new AStarStrategy().Search(ctx, after, GroundTruth());
                    Assert.AreNotEqual(
                        SolveStatus.Unsolvable, truth.Status,
                        $"board {b} (seed {Seed}): a clear reachable from a solvable start left the board unsolvable");
                    confirmed += truth.Status == SolveStatus.Solvable ? 1 : 0;
                }
            }

            Assert.GreaterOrEqual(confirmed, MinConfirmedPostClearStates, "post-clear states confirmed solvable");
            Assert.GreaterOrEqual(boardsWithLock, MinBoardsPerMechanic, "boards with a lock");
            Assert.GreaterOrEqual(boardsWithShutter, MinBoardsPerMechanic, "boards with a shutter");
        }

        [Test]
        public void MixedEffectLock_AClearCanTurnASolvableBoardUnsolvable_SoTheLevelIsNotClearMonotone()
        {
            // Why IsClearMonotone must be false for a lock whose keys carry
            // different effects: clearing the pre-aligned ClearOuterColor key
            // first wastes its effect, and the red owner can no longer leave.
            var ctx = SearchCorpus.WastedClearKeyTrapBoard();
            var initial = BoardState.CreateInitial(ctx);
            Assert.AreEqual(SolveStatus.Solvable, new AStarStrategy().Search(ctx, initial, GroundTruth()).Status);

            Assert.IsTrue(new MoveResolver().TryApplyMove(ctx, initial, new Move(0, new Coord(0, 0)), out var afterClear, out _));
            Assert.AreEqual(1, afterClear.TotalClearCount);

            Assert.AreEqual(SolveStatus.Unsolvable, new AStarStrategy().Search(ctx, afterClear, GroundTruth()).Status);
            Assert.IsFalse(ctx.IsClearMonotone);
        }

        /// <summary>
        /// Every distinct state reached by a clearing move from any state of the
        /// initial stratum, in discovery order; null when the stratum is larger
        /// than <see cref="StratumCap"/>.
        /// </summary>
        private static List<BoardState> FirstStratumClears(
            LevelContext ctx, BoardState initial, MoveGenerator generator, MoveResolver resolver)
        {
            var seen = new HashSet<BoardState> { initial };
            var queue = new Queue<BoardState>();
            queue.Enqueue(initial);
            var postClear = new List<BoardState>();
            var postClearSeen = new HashSet<BoardState>();

            while (queue.Count > 0)
            {
                var state = queue.Dequeue();
                foreach (var move in generator.Generate(ctx, state, MoveGenMode.Exhaustive))
                {
                    if (!resolver.TryApplyMove(ctx, state, move, out var next, out _))
                    {
                        continue;
                    }

                    if (next.TotalClearCount > initial.TotalClearCount)
                    {
                        if (postClearSeen.Add(next))
                        {
                            postClear.Add(next);
                        }

                        continue;
                    }

                    if (seen.Add(next))
                    {
                        if (seen.Count > StratumCap)
                        {
                            return null;
                        }

                        queue.Enqueue(next);
                    }
                }
            }

            return postClear;
        }

        private static bool HasLock(LevelContext ctx)
        {
            foreach (var block in ctx.Blocks)
            {
                if (block.LockId.HasValue)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
