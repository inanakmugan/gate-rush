using System;
using System.Collections.Generic;
using GateRush.Core;

namespace GateRush.Solver
{
    /// <summary>
    /// A smaller copy of one board state that answers exactly one question the
    /// same way the real board does: which move sequences reach the next clear,
    /// and how short the shortest one is. Built fresh at each clear point, it is
    /// discarded once that clear happens.
    /// </summary>
    /// <remarks>
    /// <para><b>Why it is exact until the next clear.</b> Every rule a
    /// non-clearing move can touch is fixed between clears: gate, shutter,
    /// frozen and lock states only change when a clear raises a counter or
    /// consumes a key, and those are the only triggers (spawns aside — see
    /// below). So the copy freezes them as they stand in the source state:</para>
    /// <list type="bullet">
    /// <item>Only gates open now are kept, and kept permanently open; a closed
    /// gate cannot open before the next clear, so it plays no part.</item>
    /// <item>Only shutters closed now are kept, and kept permanently closed.</item>
    /// <item>A block that cannot move now — frozen, locked, or under a closed
    /// shutter — stays unable to move: frozen and locked blocks become frozen
    /// until the first clear, and a shuttered block stays under its shutter.
    /// A lock and a freeze are identical to movement, and keys only fire when a
    /// block is destroyed, which is a clear.</item>
    /// <item>A block keeps its current colour only if it can move and some open
    /// gate has that colour. Every other block gets one shared colour no open
    /// gate has. Such a block can never be the one that clears next, so its
    /// colour cannot change which sequences reach a clear — but merging lets
    /// <see cref="BlockSymmetry"/> (<c>DECISIONS.md</c> D35) treat those blocks
    /// as interchangeable, which is where the saving comes from.</item>
    /// <item>A layered block is reduced to its current colour: only the outer
    /// colour can clear next.</item>
    /// </list>
    /// <para>Movement never depends on colour, so the copy and the real board
    /// accept exactly the same non-clearing moves, and a move clears in one
    /// exactly when it clears in the other. After the first clear the copy's
    /// frozen gates and thresholds are stale, which is why it is rebuilt.</para>
    /// <para><b>Spawns are not modelled.</b> A generator or elevator can place
    /// blocks between clears, which none of the above accounts for, so
    /// <see cref="Of"/> refuses a state with generator or elevator output still
    /// pending.</para>
    /// <para><b>Mapping back.</b> The copy holds one block per block alive in
    /// the source state, in ascending source index order. <see cref="BoardState"/>
    /// keeps block rows in literal index order (D35 sorts only for hashing and
    /// equality), so a move on the copy names the same block on the real board
    /// through <see cref="ToSourceMove"/>.</para>
    /// </remarks>
    public sealed class NextClearAbstraction
    {
        private readonly int[] sourceIndexByAbstractIndex;

        private NextClearAbstraction(LevelContext context, BoardState initial, int[] sourceIndexByAbstractIndex)
        {
            Context = context;
            Initial = initial;
            this.sourceIndexByAbstractIndex = sourceIndexByAbstractIndex;
        }

        /// <summary>The smaller level. Its gates are always open and its shutters always closed.</summary>
        public LevelContext Context { get; }

        /// <summary>The copy's starting state: every block at its source position.</summary>
        public BoardState Initial { get; }

        /// <summary>
        /// Builds the copy of <paramref name="state"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// A generator or elevator in <paramref name="state"/> still has output
        /// pending; spawns are not modelled (see the type remarks).
        /// </exception>
        public static NextClearAbstraction Of(LevelContext ctx, BoardState state)
        {
            if (ctx == null)
            {
                throw new ArgumentNullException(nameof(ctx));
            }

            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var pendingSpawn = DescribePendingSpawn(ctx, state);
            if (pendingSpawn != null)
            {
                throw new InvalidOperationException($"{pendingSpawn}; a NextClearAbstraction does not model spawns.");
            }

            var openGateColors = new HashSet<BlockColor>();
            var gates = new List<GateDefinition>();
            for (var g = 0; g < ctx.Gates.Count; g++)
            {
                if (!state.GateOpen[g])
                {
                    continue;
                }

                var gate = ctx.Gates[g];
                openGateColors.Add(gate.Color);
                gates.Add(new GateDefinition(gate.Id, gate.Edge, gate.Offset, gate.Width, gate.Color, openAtClearCount: null));
            }

            // Kept closed until the first clear, which the copy never reaches.
            const int ClosedUntilFirstClear = 1;

            var shutters = new List<ShutterDefinition>();
            for (var s = 0; s < ctx.Shutters.Count; s++)
            {
                if (state.ShutterOpen[s])
                {
                    continue;
                }

                var shutter = ctx.Shutters[s];
                shutters.Add(new ShutterDefinition(shutter.Id, shutter.Min, shutter.Max, ClosedUntilFirstClear, requiredColor: null));
            }

            var mergedColor = FirstColorWithoutAnOpenGate(openGateColors);

            var blocks = new List<BlockDefinition>();
            var sourceIndices = new List<int>();
            for (var i = 0; i < ctx.TotalBlockCapacity; i++)
            {
                if (!state.Alive[i])
                {
                    continue;
                }

                var spec = ctx.SpecAt(i);
                var color = state.CurrentColorOf(ctx, i);

                // A frozen or locked block stays frozen until the first clear. A
                // block under a closed shutter needs nothing extra: the shutter is
                // kept, and it keeps the block from moving exactly as before.
                var isHeld = !state.Unfrozen[i] || !state.Unlocked[i];
                var canClearNext = state.CanMove(ctx, i) && openGateColors.Contains(color);

                blocks.Add(new BlockDefinition(
                    id: blocks.Count + 1,
                    cells: spec.Cells,
                    colorStack: new[] { canClearNext || !mergedColor.HasValue ? color : mergedColor.Value },
                    startOrigin: state.Origins[i],
                    axis: spec.Axis,
                    unfreezeAtClearCount: isHeld ? ClosedUntilFirstClear : (int?)null,
                    lockId: null,
                    requiredKeyCount: 0,
                    keyTargetLockId: null,
                    keyEffect: KeyEffect.UnlockMovement,
                    timeBonusSeconds: 0));
                sourceIndices.Add(i);
            }

            var context = new LevelContext(
                ctx.LevelId,
                ctx.Width,
                ctx.Height,
                ctx.StaticWalls,
                blocks,
                gates,
                shutters,
                generators: null,
                elevators: null,
                suggestedTimeBudgetSeconds: 0,
                goldReward: 0);

            return new NextClearAbstraction(context, BoardState.CreateInitial(context), sourceIndices.ToArray());
        }

        /// <summary>
        /// The move on the source board that <paramref name="abstractMove"/>
        /// stands for: same target, the block renamed to its source index. Valid
        /// for any move made from a state of this copy before its first clear.
        /// </summary>
        public Move ToSourceMove(Move abstractMove)
        {
            var index = abstractMove.BlockIndex;
            if (index < 0 || index >= sourceIndexByAbstractIndex.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(abstractMove),
                    $"Block index {index} is not a block of this abstraction ({sourceIndexByAbstractIndex.Length} blocks).");
            }

            return new Move(sourceIndexByAbstractIndex[index], abstractMove.TargetOrigin);
        }

        /// <summary>
        /// True when <see cref="Of"/> can build a copy of <paramref name="state"/>:
        /// no generator or elevator in it still has output pending.
        /// </summary>
        public static bool CanModel(LevelContext ctx, BoardState state)
        {
            if (ctx == null)
            {
                throw new ArgumentNullException(nameof(ctx));
            }

            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            return DescribePendingSpawn(ctx, state) == null;
        }

        /// <summary>The first generator or elevator with output pending, described; null when there is none.</summary>
        private static string DescribePendingSpawn(LevelContext ctx, BoardState state)
        {
            for (var g = 0; g < ctx.Generators.Count; g++)
            {
                if (state.GeneratorIndex[g] < ctx.Generators[g].Queue.Count)
                {
                    return $"Generator {ctx.Generators[g].Id} still has output pending";
                }
            }

            for (var e = 0; e < ctx.Elevators.Count; e++)
            {
                if (state.ElevatorWaveIndex[e] < ctx.Elevators[e].Waves.Count)
                {
                    return $"Elevator {ctx.Elevators[e].Id} still has waves pending";
                }
            }

            return null;
        }

        /// <summary>
        /// The first colour, in enum order, that no open gate has — the one
        /// merged colour. Null when every colour has an open gate, in which case
        /// nothing can be merged and every block keeps its own colour.
        /// </summary>
        private static BlockColor? FirstColorWithoutAnOpenGate(HashSet<BlockColor> openGateColors)
        {
            foreach (BlockColor color in Enum.GetValues(typeof(BlockColor)))
            {
                if (!openGateColors.Contains(color))
                {
                    return color;
                }
            }

            return null;
        }
    }
}
