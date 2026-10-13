using System;
using System.Collections.Generic;
using GateRush.Core;

namespace GateRush.Runtime
{
    /// <summary>
    /// Decides which generators preview their next block on the board while a
    /// block is held, and on which cells (Module 26, D53): every generator
    /// whose next block would land on a cell the held block stands on. It
    /// answers the player's question on grabbing that block — what comes in if
    /// I move it. Presentation only: it reads state and changes no rule (M6).
    /// </summary>
    /// <remarks>
    /// Nothing here is derived a second time. Whether a generator is still
    /// shown, what its next block is and whether that block would spawn frozen
    /// are <see cref="VisibilityLayer.Generator"/>'s, so the preview never
    /// disagrees with the machine's screen (D48); where the block lands is
    /// <see cref="LevelContext.GeneratorSpawnOrigin"/>, the placement the
    /// resolver spawns by, so the preview lands exactly where the block will.
    /// </remarks>
    public static class GeneratorPreview
    {
        /// <summary>
        /// The previews for holding block <paramref name="heldBlockIndex"/>,
        /// in generator order: one for every generator still shown whose next
        /// block's cells, at its spawn origin, include a cell the held block
        /// covers at its origin in <paramref name="state"/>. Empty when none
        /// does, and for a block that is not alive, which covers no cell.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="ctx"/> or <paramref name="state"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="heldBlockIndex"/> is outside <c>[0, TotalBlockCapacity)</c>.
        /// </exception>
        public static IReadOnlyList<GeneratorPreviewTarget> For(LevelContext ctx, BoardState state, int heldBlockIndex)
        {
            if (ctx == null)
            {
                throw new ArgumentNullException(nameof(ctx));
            }

            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            // Before the liveness test, so an index out of range throws rather
            // than reading as a block that is not there.
            var heldCells = ctx.SpecAt(heldBlockIndex).Cells;
            if (!state.Alive[heldBlockIndex])
            {
                return Array.Empty<GeneratorPreviewTarget>();
            }

            var held = new HashSet<Coord>();
            var heldOrigin = state.Origins[heldBlockIndex];
            for (var i = 0; i < heldCells.Count; i++)
            {
                held.Add(heldOrigin + heldCells[i]);
            }

            var visibility = new VisibilityLayer(ctx);
            List<GeneratorPreviewTarget> targets = null;
            for (var g = 0; g < ctx.Generators.Count; g++)
            {
                var visual = visibility.Generator(state, g);
                if (!visual.IsShown)
                {
                    continue;
                }

                // A shown generator has an entry at its current queue index.
                var origin = ctx.GeneratorSpawnOrigin(g, state.GeneratorIndex[g]);
                var cells = new Coord[visual.NextCells.Count];
                var isUnderHeld = false;
                for (var i = 0; i < cells.Length; i++)
                {
                    cells[i] = origin + visual.NextCells[i];
                    isUnderHeld |= held.Contains(cells[i]);
                }

                if (!isUnderHeld)
                {
                    continue;
                }

                targets = targets ?? new List<GeneratorPreviewTarget>();
                targets.Add(new GeneratorPreviewTarget(g, cells, visual.NextColor, visual.IsNextFrozen));
            }

            return targets != null ? (IReadOnlyList<GeneratorPreviewTarget>)targets : Array.Empty<GeneratorPreviewTarget>();
        }
    }

    /// <summary>
    /// One generator's next block as the board previews it (D53): where it
    /// will land and in what colour.
    /// </summary>
    public readonly struct GeneratorPreviewTarget
    {
        /// <summary>A preview target with every field given.</summary>
        public GeneratorPreviewTarget(int generatorIndex, IReadOnlyList<Coord> cells, BlockColor? color, bool isFrozen)
        {
            GeneratorIndex = generatorIndex;
            Cells = cells;
            Color = color;
            IsFrozen = isFrozen;
        }

        /// <summary>The generator's position in <see cref="LevelContext.Generators"/>.</summary>
        public int GeneratorIndex { get; }

        /// <summary>The board cells the next block will occupy when it spawns.</summary>
        public IReadOnlyList<Coord> Cells { get; }

        /// <summary>The next block's outer colour; null when it would spawn frozen (M3).</summary>
        public BlockColor? Color { get; }

        /// <summary>True when the next block would spawn frozen at the current counts: previewed in ice.</summary>
        public bool IsFrozen { get; }
    }
}
