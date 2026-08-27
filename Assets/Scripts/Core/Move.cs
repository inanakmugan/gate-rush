using System;

namespace GateRush.Core
{
    /// <summary>
    /// A single player action: carry block <see cref="BlockIndex"/> to
    /// <see cref="TargetOrigin"/>. The move records the drag's endpoints, not a
    /// direction and distance — movement is reachability-based and may turn
    /// corners (see <c>DECISIONS.md</c> D27).
    /// </summary>
    /// <remarks>
    /// <see cref="TargetOrigin"/> <b>may equal the block's current origin</b>.
    /// That is a zero-distance move: the deliberate push that clears a block
    /// already flush against a compatible open gate, and typically a level's
    /// opening move (see <c>DECISIONS.md</c> D25).
    /// </remarks>
    public readonly struct Move : IEquatable<Move>
    {
        /// <summary>Flat index of the block to move, as used by
        /// <see cref="LevelContext.SpecAt"/> and <c>BoardState</c>'s per-block
        /// arrays.</summary>
        public int BlockIndex { get; }

        /// <summary>Where the block's origin should end up. May equal its
        /// current origin (a zero-distance move).</summary>
        public Coord TargetOrigin { get; }

        public Move(int blockIndex, Coord targetOrigin)
        {
            BlockIndex = blockIndex;
            TargetOrigin = targetOrigin;
        }

        public bool Equals(Move other) =>
            BlockIndex == other.BlockIndex && TargetOrigin == other.TargetOrigin;

        public override bool Equals(object obj) => obj is Move other && Equals(other);

        public override int GetHashCode() =>
            unchecked((BlockIndex * 397) ^ TargetOrigin.GetHashCode());

        public static bool operator ==(Move a, Move b) => a.Equals(b);

        public static bool operator !=(Move a, Move b) => !a.Equals(b);

        public override string ToString() => $"Move(block {BlockIndex} -> {TargetOrigin})";
    }
}
