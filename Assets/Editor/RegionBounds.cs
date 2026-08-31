using System;
using GateRush.Core;

namespace GateRush.Editor
{
    /// <summary>
    /// Keeps a shutter or elevator region's <c>Min</c>/<c>Max</c> valid as the
    /// designer types into the numeric bound fields (revision A2): every corner
    /// inside the grid, and <c>Min ≤ Max</c> on each axis — corrected, not
    /// stored wrong. A pure function, tested beside the other logic.
    /// </summary>
    public static class RegionBounds
    {
        public static (Coord Min, Coord Max) Clamped(Coord min, Coord max, int width, int height)
        {
            var maxX = Math.Max(width, 1) - 1;
            var maxY = Math.Max(height, 1) - 1;

            var x0 = Clamp(min.X, 0, maxX);
            var y0 = Clamp(min.Y, 0, maxY);
            var x1 = Clamp(max.X, 0, maxX);
            var y1 = Clamp(max.Y, 0, maxY);

            return (
                new Coord(Math.Min(x0, x1), Math.Min(y0, y1)),
                new Coord(Math.Max(x0, x1), Math.Max(y0, y1)));
        }

        private static int Clamp(int value, int lo, int hi) => value < lo ? lo : value > hi ? hi : value;
    }
}
