using System;
using System.Collections.Generic;
using GateRush.Core;
using GateRush.Runtime;
using UnityEngine;

namespace GateRush.Editor
{
    /// <summary>
    /// Where the Level Editor draws a layered block's second colour (Module
    /// 20): the footprint pulled inward as one piece, by the board's own rule.
    /// Pure geometry over a cell list, no <c>UnityEditor</c>, so the window
    /// only turns the result into pixels.
    /// </summary>
    /// <remarks>
    /// The rectangles are <see cref="LayerInset"/>'s, over
    /// <see cref="BlockTiling"/>'s quarters — the rule the game draws a
    /// layered block's inner shape with — so the editor and the board cannot
    /// come to disagree about what "one piece through the bend" means. The
    /// editor fills them flat, so together they cover exactly the footprint
    /// shrunk by the inset on every outer side.
    /// </remarks>
    public static class EditorLayerInset
    {
        /// <summary>
        /// The inset rectangles of <paramref name="cells"/>, four per cell, in
        /// cell units relative to the footprint's origin with +Y up; empty for
        /// a block of fewer than two colours, which has no colour beneath to
        /// show, and for an empty footprint.
        /// </summary>
        /// <param name="cells">The block's footprint, relative to its origin.</param>
        /// <param name="colourCount">How many colours the block's stack holds.</param>
        /// <param name="insetCells">How far the second colour sits inside the footprint, in cells.</param>
        public static IReadOnlyList<Rect> Rects(IReadOnlyList<Coord> cells, int colourCount, float insetCells)
        {
            if (cells == null || cells.Count == 0 || colourCount < 2)
            {
                return Array.Empty<Rect>();
            }

            var tiles = BlockTiling.Compute(cells);
            var rects = new Rect[tiles.Count];
            for (var i = 0; i < tiles.Count; i++)
            {
                rects[i] = LayerInset.QuarterRect(tiles[i], insetCells);
            }

            return rects;
        }
    }
}
