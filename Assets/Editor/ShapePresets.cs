using System;
using System.Collections.Generic;
using GateRush.Core;

namespace GateRush.Editor
{
    /// <summary>
    /// The block-shape palette entries. A preset is nothing but "fill these cells
    /// in one click" — presets and free drawing produce the same thing, a set of
    /// cells (Module 09). <see cref="Free"/> means the window collects cells one
    /// by one instead.
    /// </summary>
    public enum ShapePreset
    {
        Single,
        Horizontal2,
        Vertical2,
        Horizontal3,
        Vertical3,
        Square2,
        LNorthEast,
        LNorthWest,
        LSouthEast,
        LSouthWest,
        Free,
    }

    public static class ShapePresets
    {
        /// <summary>
        /// The cell set for a preset, relative to the clicked cell. Throws for
        /// <see cref="ShapePreset.Free"/>, which has no fixed set.
        /// </summary>
        public static IReadOnlyList<Coord> Cells(ShapePreset preset)
        {
            switch (preset)
            {
                case ShapePreset.Single: return new[] { C(0, 0) };
                case ShapePreset.Horizontal2: return new[] { C(0, 0), C(1, 0) };
                case ShapePreset.Vertical2: return new[] { C(0, 0), C(0, 1) };
                case ShapePreset.Horizontal3: return new[] { C(0, 0), C(1, 0), C(2, 0) };
                case ShapePreset.Vertical3: return new[] { C(0, 0), C(0, 1), C(0, 2) };
                case ShapePreset.Square2: return new[] { C(0, 0), C(1, 0), C(0, 1), C(1, 1) };
                case ShapePreset.LNorthEast: return new[] { C(0, 0), C(0, 1), C(1, 1) };
                case ShapePreset.LNorthWest: return new[] { C(1, 0), C(1, 1), C(0, 1) };
                case ShapePreset.LSouthEast: return new[] { C(0, 0), C(1, 0), C(0, 1) };
                case ShapePreset.LSouthWest: return new[] { C(0, 0), C(1, 0), C(1, 1) };
                default:
                    throw new ArgumentException($"{preset} has no fixed cell set.", nameof(preset));
            }
        }

        private static Coord C(int x, int y) => new Coord(x, y);
    }
}
