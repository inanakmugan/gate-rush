using System.Collections.Generic;
using GateRush.Core;
using GateRush.Solver;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Helpers shared by the tests that drive a search: a default
    /// <see cref="SearchBudget"/> loose enough that only a test's own tighter
    /// limits bite, and a replay that applies a solution move by move through
    /// <see cref="MoveResolver"/>, failing the test if any move is rejected.
    /// </summary>
    internal static class Solve
    {
        internal static SearchBudget Budget(
            MoveGenMode mode,
            int maxDepth = 64,
            int maxExplored = 500_000,
            long maxMs = 10_000)
        {
            return new SearchBudget(maxDepth, maxExplored, maxMs, mode);
        }

        internal static BoardState Replay(
            LevelContext ctx, BoardState initial, IReadOnlyList<Move> solution)
        {
            var resolver = new MoveResolver();
            var state = initial;
            foreach (var move in solution)
            {
                Assert.IsTrue(
                    resolver.TryApplyMove(ctx, state, move, out state),
                    $"replay rejected {move}");
            }

            return state;
        }
    }
}
