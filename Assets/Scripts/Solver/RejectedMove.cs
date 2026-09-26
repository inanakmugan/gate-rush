using System;
using GateRush.Core;

namespace GateRush.Solver
{
    /// <summary>
    /// The one error every search strategy raises when <see cref="MoveResolver"/>
    /// rejects a move <see cref="MoveGenerator"/> emitted. Both read the same
    /// <see cref="BlockReachability"/> (<c>DECISIONS.md</c> D31), so a rejection
    /// means they disagree — a solver bug. Skipping the move instead would
    /// silently shrink the move set and could turn a solvable level into a false
    /// <see cref="SolveStatus.Unsolvable"/>; the A\* cross-check (D37) cannot
    /// catch that, because it shares the same generator and resolver.
    /// </summary>
    internal static class RejectedMove
    {
        /// <summary>The exception to throw for <paramref name="move"/>, naming its block and target.</summary>
        internal static InvalidOperationException Error(LevelContext ctx, Move move) =>
            new InvalidOperationException(
                $"Level {ctx.LevelId}: the resolver rejected a generated move — block {move.BlockIndex} " +
                $"to {move.TargetOrigin}. The move generator and resolver disagree; this is a solver bug, " +
                "and skipping the move could report a solvable level as unsolvable.");
    }
}
