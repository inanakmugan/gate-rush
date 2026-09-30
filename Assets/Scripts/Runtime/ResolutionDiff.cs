using System;
using System.Collections.Generic;
using GateRush.Core;

namespace GateRush.Runtime
{
    /// <summary>
    /// A block whose outer colour a move removed at a gate: what the clear
    /// effect needs to show it leaving.
    /// </summary>
    public readonly struct ClearedBlock
    {
        /// <summary>A cleared block. <paramref name="exposedColor"/> is null exactly when it was destroyed.</summary>
        public ClearedBlock(int blockIndex, BlockColor removedColor, BlockColor? exposedColor, int gateIndex, BoardEdge gateEdge)
        {
            BlockIndex = blockIndex;
            RemovedColor = removedColor;
            ExposedColor = exposedColor;
            GateIndex = gateIndex;
            GateEdge = gateEdge;
        }

        /// <summary>The block's slot in <c>LevelContext</c>.</summary>
        public int BlockIndex { get; }

        /// <summary>The colour the clear removed: the block's outer colour before it.</summary>
        public BlockColor RemovedColor { get; }

        /// <summary>The colour now outermost on a layered block that survived (M4); null when the block was destroyed.</summary>
        public BlockColor? ExposedColor { get; }

        /// <summary>True when the removed colour was the block's last and the block is gone.</summary>
        public bool IsDestroyed => !ExposedColor.HasValue;

        /// <summary>The gate it was cleared through, as an index into <c>LevelContext.Gates</c>.</summary>
        public int GateIndex { get; }

        /// <summary>The edge that gate sits on: the way a destroyed block leaves.</summary>
        public BoardEdge GateEdge { get; }
    }

    /// <summary>
    /// Compares the states before and after a player move and reports the
    /// clear the move made, for presentation to animate before the board is
    /// redrawn.
    /// </summary>
    /// <remarks>
    /// Only the moved block is examined. In play the only clear a move makes
    /// is the moved block's, at the gate it was pushed into or arrived at;
    /// every other change in the state — unlocks, spawns, openings — is
    /// <see cref="MoveChanges"/>'s, which takes its clears from here. The gate is found by
    /// <see cref="BlockReachability.FindExitGate"/>, the same scan the resolver
    /// clears by, evaluated on the state the move started from.
    /// </remarks>
    public static class ResolutionDiff
    {
        /// <summary>
        /// One <see cref="ClearedBlock"/> when <paramref name="move"/> removed
        /// its block's outer colour; otherwise empty.
        /// </summary>
        /// <param name="ctx">The level.</param>
        /// <param name="before">The state <paramref name="move"/> was applied to.</param>
        /// <param name="after">The fully resolved state the move produced.</param>
        /// <param name="move">The move applied.</param>
        /// <param name="push">
        /// The push direction when <paramref name="move"/> was a push in place
        /// (<see cref="DragController"/>'s <c>End</c> reports it); null for a
        /// move that arrived. It decides which gate a
        /// block in a corner was pushed through.
        /// </param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The move's block index is not a slot of <paramref name="ctx"/>.</exception>
        /// <exception cref="InvalidOperationException">
        /// The block lost its colour but no gate it could have exited through
        /// exists — the states and the move do not belong together.
        /// </exception>
        public static IReadOnlyList<ClearedBlock> Between(
            LevelContext ctx, BoardState before, BoardState after, Move move, Direction? push)
        {
            if (ctx == null)
            {
                throw new ArgumentNullException(nameof(ctx));
            }

            if (before == null)
            {
                throw new ArgumentNullException(nameof(before));
            }

            if (after == null)
            {
                throw new ArgumentNullException(nameof(after));
            }

            var blockIndex = move.BlockIndex;
            if (blockIndex < 0 || blockIndex >= ctx.TotalBlockCapacity)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(move), blockIndex, $"Level {ctx.LevelId} has no block slot {blockIndex}.");
            }

            var clearedBefore = before.ClearedColors[blockIndex];
            if (after.ClearedColors[blockIndex] <= clearedBefore)
            {
                return Array.Empty<ClearedBlock>();
            }

            var gateIndex = BlockReachability.FindExitGate(ctx, before, blockIndex, move.TargetOrigin, push);
            if (gateIndex < 0)
            {
                throw new InvalidOperationException(
                    $"Level {ctx.LevelId}: block slot {blockIndex} lost its colour on {move}, " +
                    "but no gate it could exit through is there.");
            }

            var stack = ctx.SpecAt(blockIndex).ColorStack;
            var exposedIndex = clearedBefore + 1;
            BlockColor? exposed = exposedIndex < stack.Count ? stack[exposedIndex] : (BlockColor?)null;

            return new[]
            {
                new ClearedBlock(blockIndex, stack[clearedBefore], exposed, gateIndex, ctx.Gates[gateIndex].Edge)
            };
        }
    }
}
