using System;

namespace GateRush.Core
{
    /// <summary>
    /// An opening on a board edge that a matching block can exit through once open.
    /// </summary>
    public sealed class GateDefinition
    {
        public int Id { get; }
        public BoardEdge Edge { get; }
        public int Offset { get; }
        public int Width { get; }
        public BlockColor Color { get; }
        public int? OpenAtClearCount { get; }

        public GateDefinition(
            int id,
            BoardEdge edge,
            int offset,
            int width,
            BlockColor color,
            int? openAtClearCount)
        {
            if (width < 1)
            {
                throw new ArgumentException($"Gate {id} must have a Width of at least 1; got {width}.");
            }

            Id = id;
            Edge = edge;
            Offset = offset;
            Width = width;
            Color = color;
            OpenAtClearCount = openAtClearCount;
        }

        /// <summary>
        /// Whether a gate with this <see cref="OpenAtClearCount"/> is open before
        /// any clear has happened. A missing or non-positive threshold means
        /// open from the start (M2). The single definition of that rule: the
        /// initial <see cref="BoardState"/> and the Level Editor's gate marker
        /// both ask here, so what the editor shows as closed is exactly what the
        /// game starts closed.
        /// </summary>
        public static bool IsOpenAtZeroClears(int? openAtClearCount) =>
            !openAtClearCount.HasValue || openAtClearCount.Value <= 0;
    }
}
