using System;
using System.Collections.Generic;
using GateRush.Core;

namespace GateRush.Solver
{
    /// <summary>
    /// Enumerates the moves available from a board state, in one of two modes
    /// (<see cref="MoveGenMode"/>). The solver's move-enumeration front end: it
    /// decides what the search may branch on, applies nothing, and mutates no
    /// state.
    /// </summary>
    /// <remarks>
    /// <para><b>Shared reachability.</b> The corner-turning flood fill this
    /// enumerates, and the gate-projection geometry that decides which landings
    /// clear a block, are <see cref="BlockReachability"/> — the very same code
    /// <c>MoveResolver</c> validates a single player move against, so an
    /// enumerated move and the resolver's verdict on it can never disagree
    /// (<c>DECISIONS.md</c> D27, D5). This generator keeps its own private
    /// <see cref="BlockReachability"/> instance and never shares it; the buffers
    /// inside are overwritten per scan.</para>
    ///
    /// <para><b>Enumeration order</b> (both modes): ascending block index; within
    /// a block, the zero-distance move first (when the block is already flush
    /// against a compatible open gate), then reachable positions in the
    /// breadth-first order <see cref="BlockReachability.ReachableOrigins"/>
    /// produces — ascending path length, ties broken by <see cref="Direction"/>
    /// enum order. Only blocks for which <see cref="BoardState.CanMove"/> is true
    /// are enumerated; dead, unspawned, frozen, locked, and shuttered blocks
    /// yield nothing (jokers are not moves — D10).</para>
    ///
    /// <para><b>Allocation.</b> <see cref="Generate"/> builds and returns a fresh
    /// <see cref="List{Move}"/> each call. The expensive buffers — the grid-sized
    /// visited map and the frontier queue — are reused across calls inside the
    /// <see cref="BlockReachability"/> instance. A fresh result list per call is
    /// deliberate: the search (Module 05) may hold or partially drain the
    /// returned sequence, and a reused list would change under it.</para>
    /// </remarks>
    public sealed class MoveGenerator
    {
        private readonly BlockReachability reachability = new BlockReachability();

        /// <summary>
        /// The moves available from <paramref name="state"/> under
        /// <paramref name="mode"/>. See the type remarks for order and mode
        /// semantics.
        /// </summary>
        public IEnumerable<Move> Generate(LevelContext ctx, BoardState state, MoveGenMode mode)
        {
            if (ctx == null)
            {
                throw new ArgumentNullException(nameof(ctx));
            }

            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var result = new List<Move>();

            for (var blockIndex = 0; blockIndex < ctx.TotalBlockCapacity; blockIndex++)
            {
                if (!state.CanMove(ctx, blockIndex))
                {
                    continue;
                }

                AppendMovesForBlock(ctx, state, blockIndex, mode, result);
            }

            return result;
        }

        private void AppendMovesForBlock(
            LevelContext ctx, BoardState state, int blockIndex, MoveGenMode mode, List<Move> result)
        {
            var origin = state.Origins[blockIndex];

            // Zero-distance move: the deliberate push that clears a block already
            // flush against a compatible open gate (D25). Emitted first for the
            // block, in both modes.
            if (BlockReachability.IsAtCompatibleExitGate(ctx, state, blockIndex, origin))
            {
                result.Add(new Move(blockIndex, origin));
            }

            // ReachableOrigins excludes the block's own origin, so a positional
            // move can never duplicate the zero-distance move above.
            var reachable = reachability.ReachableOrigins(ctx, state, blockIndex, origin);

            for (var i = 0; i < reachable.Count; i++)
            {
                var target = reachable[i];

                if (mode == MoveGenMode.Exhaustive
                    || IsCanonicalTarget(ctx, state, blockIndex, origin, target))
                {
                    result.Add(new Move(blockIndex, target));
                }
            }
        }

        /// <summary>
        /// True when <paramref name="target"/> is worth branching on: it lands the
        /// block flush and aligned with a compatible gate, or it changes an
        /// elevator region's occupancy by this block, or it leaves the block
        /// resting against an obstacle. The zero-distance clear — the fifth
        /// canonical criterion — is handled by the caller before this runs.
        /// </summary>
        private bool IsCanonicalTarget(
            LevelContext ctx, BoardState state, int blockIndex, Coord fromOrigin, Coord target)
        {
            // 1. Lands flush and aligned with a compatible open gate: the move
            //    would clear the block. Same test the resolver uses.
            if (BlockReachability.IsAtCompatibleExitGate(ctx, state, blockIndex, target))
            {
                return true;
            }

            // 2. EXTENSION POINT — phase 1.13 (M6 generators). A generator's spawn
            //    footprint is the projection of its edge and offset through the
            //    incoming block's shape; that placement algorithm is Module 03's
            //    and does not exist yet, so a position that vacates or occupies a
            //    generator's spawn cells cannot be recognised here.
            //
            //    Consequence, per D5's safety argument: until 1.13, canonical
            //    mode produces MORE false negatives on levels with generators
            //    than it eventually will, so the editor falls through to
            //    Exhaustive on those levels more often. That is expected — a
            //    smaller canonical set is always safe (it is a subset of the
            //    player's moves) — not a defect.
            if (FlipsGeneratorRegionOccupancy(ctx, state, blockIndex, fromOrigin, target))
            {
                return true;
            }

            // 3. Vacates or occupies an elevator region: the move can trigger the
            //    next wave, or free the region for one.
            if (FlipsElevatorRegionOccupancy(ctx, state, blockIndex, fromOrigin, target))
            {
                return true;
            }

            // 4. Comes to rest against an obstacle — a static wall, a closed
            //    shutter, or another living block — in at least one axis-permitted
            //    direction. Replaces the old "maximum slide" criterion, which is
            //    meaningless once a block can turn corners.
            if (BlockReachability.IsRestingAgainstObstacle(ctx, state, blockIndex, target))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// EXTENSION POINT — phase 1.13. Always false while generators do not
        /// exist in level data; see the call site for why that is safe.
        /// </summary>
        private static bool FlipsGeneratorRegionOccupancy(
            LevelContext ctx, BoardState state, int blockIndex, Coord fromOrigin, Coord target) => false;

        private static bool FlipsElevatorRegionOccupancy(
            LevelContext ctx, BoardState state, int blockIndex, Coord fromOrigin, Coord target)
        {
            var cells = ctx.SpecAt(blockIndex).Cells;

            for (var e = 0; e < ctx.Elevators.Count; e++)
            {
                var elevator = ctx.Elevators[e];
                var atTarget = FootprintOverlapsRegion(cells, target, elevator.Min, elevator.Max);
                var atFrom = FootprintOverlapsRegion(cells, fromOrigin, elevator.Min, elevator.Max);

                if (atTarget != atFrom)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool FootprintOverlapsRegion(
            IReadOnlyList<Coord> cells, Coord origin, Coord regionMin, Coord regionMax)
        {
            for (var i = 0; i < cells.Count; i++)
            {
                var cell = origin + cells[i];
                if (cell.X >= regionMin.X && cell.X <= regionMax.X
                    && cell.Y >= regionMin.Y && cell.Y <= regionMax.Y)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
