using System;
using System.Collections.Generic;
using GateRush.Core;
using UnityEngine;

namespace GateRush.Runtime
{
    /// <summary>
    /// Splits a block's footprint into quarter tiles so it draws as one rounded
    /// piece (Module 15): the outer pieces make the rounded outline and the gap
    /// to the next block, edges and fills join the cells, and a concave corner
    /// closes the inside of an L.
    /// </summary>
    public static class BlockTiling
    {
        /// <summary>
        /// Four quarters per footprint cell, in the order of
        /// <paramref name="cells"/> and then by <see cref="Quarter"/>. Tiles keep
        /// the footprint's own coordinates.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="cells"/> is null.</exception>
        public static IReadOnlyList<QuarterTile> Compute(IReadOnlyList<Coord> cells)
        {
            if (cells == null)
            {
                throw new ArgumentNullException(nameof(cells));
            }

            var members = new HashSet<Coord>(cells);
            return QuarterTiling.For(cells, members.Contains);
        }

        /// <summary>
        /// The rectangle <paramref name="tile"/> covers, in cell units in the
        /// frame of its cells: half a cell on each side, on the half of its cell
        /// its corner names.
        /// </summary>
        public static Rect QuarterRect(QuarterTile tile) =>
            new Rect(
                tile.Cell.X + (tile.SignX > 0 ? 0.5f : 0f),
                tile.Cell.Y + (tile.SignY > 0 ? 0.5f : 0f),
                0.5f,
                0.5f);
    }
}
