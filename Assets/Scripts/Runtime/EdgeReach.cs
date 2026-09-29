using System;

namespace GateRush.Runtime
{
    /// <summary>
    /// How far something drawn outside the board's frame reaches beyond it on
    /// each side, in cells: the room the camera fit keeps for generator
    /// machines (D48). The default value reaches nowhere.
    /// </summary>
    public readonly struct EdgeReach
    {
        /// <summary>A reach per side, each at least 0.</summary>
        /// <exception cref="ArgumentOutOfRangeException">A side is negative or not finite.</exception>
        public EdgeReach(float left, float right, float bottom, float top)
        {
            Left = Check(left, nameof(left));
            Right = Check(right, nameof(right));
            Bottom = Check(bottom, nameof(bottom));
            Top = Check(top, nameof(top));
        }

        /// <summary>Cells beyond the frame on the left.</summary>
        public float Left { get; }

        /// <summary>Cells beyond the frame on the right.</summary>
        public float Right { get; }

        /// <summary>Cells beyond the frame at the bottom.</summary>
        public float Bottom { get; }

        /// <summary>Cells beyond the frame at the top.</summary>
        public float Top { get; }

        private static float Check(float cells, string side)
        {
            if (!(cells >= 0f) || float.IsInfinity(cells))
            {
                throw new ArgumentOutOfRangeException(side, cells, "A reach beyond the frame is at least 0 cells and finite.");
            }

            return cells;
        }
    }
}
