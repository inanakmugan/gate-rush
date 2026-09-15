using System;
using System.Collections.Generic;

namespace GateRush.Core
{
    /// <summary>
    /// A board-edge source that pushes an explicit, ordered sequence of blocks
    /// inward — the inverse of a gate (M6).
    /// </summary>
    /// <remarks>
    /// Being a gate's inverse, it has a gate's geometry: an edge, an offset and a
    /// <see cref="Width"/>. A queued block's projection onto the edge may not
    /// exceed that width — the same projection rule a gate measures an exiting
    /// block by — while its extent into the board is unconstrained. That
    /// compatibility is a level-design question the editor warns about; the
    /// <see cref="Width"/> bound itself is a rule of the game and is enforced
    /// here (see <c>DECISIONS.md</c> D34).
    /// </remarks>
    public sealed class GeneratorDefinition
    {
        /// <summary>
        /// The widest a generator may be, in cells along its edge. A rule of the
        /// game rather than a tuning value: the reference game never shows a
        /// wider one, and the bound is what makes a queue entry's projection
        /// checkable at all (M6, <c>DECISIONS.md</c> D34). It costs almost
        /// nothing — of the shape presets only the three-long one *along* the
        /// edge is excluded, since the width bounds the projection, not the depth.
        /// </summary>
        public const int MaxWidth = 2;

        public int Id { get; }
        public BoardEdge Edge { get; }
        public int Offset { get; }

        /// <summary>
        /// How many cells of <see cref="Edge"/> this generator spans, within
        /// <c>[1, <see cref="MaxWidth"/>]</c>. A spawning block aligns to
        /// <see cref="Offset"/>, so a narrower block sits at the low end of the
        /// span rather than anywhere within it — the simplest deterministic rule,
        /// and one that needs no per-entry field (D34).
        /// </summary>
        public int Width { get; }

        public IReadOnlyList<SpawnedBlock> Queue { get; }

        public GeneratorDefinition(
            int id, BoardEdge edge, int offset, int width, IReadOnlyList<SpawnedBlock> queue)
        {
            if (width < 1 || width > MaxWidth)
            {
                throw new ArgumentException(
                    $"Generator {id} must have a Width of 1 to {MaxWidth} cells; got {width}.");
            }

            Id = id;
            Edge = edge;
            Offset = offset;
            Width = width;
            Queue = new List<SpawnedBlock>(queue ?? Array.Empty<SpawnedBlock>()).AsReadOnly();

            for (var i = 0; i < Queue.Count; i++)
            {
                if (Queue[i].RegionOrigin.HasValue)
                {
                    throw new ArgumentException(
                        $"Generator {id} queue entry {i} carries a RegionOrigin. Generator output has no " +
                        "authored position — it derives from the generator's edge and offset — so the value " +
                        "would do nothing; it belongs only on elevator wave blocks (M9).");
                }
            }
        }
    }
}
