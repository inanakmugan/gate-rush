using System;

namespace GateRush.Core
{
    /// <summary>
    /// The one event every removal in the game emits — a block pushed into a
    /// matching gate, a key effect, a rocket, a broom (see <c>DECISIONS.md</c>
    /// D7). Every progress counter and, from phase 1.7, every unlock condition
    /// listens to this and nothing else.
    /// </summary>
    /// <remarks>
    /// The event does not distinguish a block that died from a layered block
    /// that merely shed its outer colour: <see cref="BlockIndex"/> plus
    /// <see cref="Color"/> is all a counter needs, and treating both cases
    /// identically is exactly why layered blocks require no special handling
    /// elsewhere.
    /// </remarks>
    public readonly struct ColorClearedEvent : IEquatable<ColorClearedEvent>
    {
        /// <summary>The block whose colour was removed.</summary>
        public int BlockIndex { get; }

        /// <summary>The colour that was removed — the one counters credit,
        /// regardless of what (if anything) lies beneath it.</summary>
        public BlockColor Color { get; }

        public ColorClearedEvent(int blockIndex, BlockColor color)
        {
            BlockIndex = blockIndex;
            Color = color;
        }

        public bool Equals(ColorClearedEvent other) =>
            BlockIndex == other.BlockIndex && Color == other.Color;

        public override bool Equals(object obj) => obj is ColorClearedEvent other && Equals(other);

        public override int GetHashCode() => unchecked((BlockIndex * 397) ^ (int)Color);

        public override string ToString() => $"ColorCleared(block {BlockIndex}, {Color})";
    }
}
