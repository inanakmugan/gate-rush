using UnityEngine;

namespace GateRush.Runtime
{
    /// <summary>
    /// Where each quarter of a layered block's inner shape is drawn (Module
    /// 20). The inner shape is the block's own quarter pieces drawn again in
    /// the colour beneath, each pulled inward on the sides that face out of
    /// the block, so the outer colour shows as an even rim around one inner
    /// piece that follows the whole footprint — an L shape gets an L-shaped
    /// inner piece — and never as a mark per cell.
    /// </summary>
    /// <remarks>
    /// <para>A quarter sprite is drawn from the cell's centre out, so moving a
    /// rectangle's outward side toward that centre pulls its drawn boundary
    /// inward by about the inset. Sides toward the cell's own centre never
    /// move, so the quarters of one cell always meet. Which outward sides move
    /// follows the piece:</para>
    /// <list type="bullet">
    /// <item>a fill has no side facing out of the block and is unchanged;</item>
    /// <item>an outer corner moves in on both of its outward sides;</item>
    /// <item>a concave corner moves in on both too, which carries its notch
    /// inward, round the inside of the bend;</item>
    /// <item>an edge moves in on the side that faces out of the block, and
    /// <b>out</b> by the inset on its other outward side — along its own
    /// run.</item>
    /// </list>
    /// <para><b>Why an edge reaches along its run.</b> That side of an edge
    /// always faces a cell of the same block, whose quarter there is either
    /// another edge in the same band — the reach then overlaps the same colour
    /// — or a concave corner. A concave corner pulled in leaves a strip one
    /// inset wide between itself and each neighbouring cell, and the edge
    /// across that strip is the only piece that can cover it. With the reach
    /// the quarters cover exactly the footprint shrunk by the inset; without
    /// it an L's inner piece would be slit on both sides of its bend.</para>
    /// </remarks>
    public static class LayerInset
    {
        /// <summary>
        /// The rectangle the inner-shape quarter for <paramref name="tile"/>
        /// covers, in cell units in the frame of its cells.
        /// </summary>
        /// <param name="tile">A quarter of the block's footprint (<see cref="BlockTiling"/>).</param>
        /// <param name="insetCells">How far the inner shape sits inside the footprint, in cells; 0 gives the quarter's own rectangle.</param>
        public static Rect QuarterRect(QuarterTile tile, float insetCells)
        {
            var rect = BlockTiling.QuarterRect(tile);
            switch (tile.Piece)
            {
                case QuarterPiece.OuterCorner:
                case QuarterPiece.ConcaveCorner:
                    MoveOutwardX(ref rect, tile.SignX, -insetCells);
                    MoveOutwardY(ref rect, tile.SignY, -insetCells);
                    break;

                case QuarterPiece.EdgeAlongX:
                    MoveOutwardY(ref rect, tile.SignY, -insetCells);
                    MoveOutwardX(ref rect, tile.SignX, insetCells);
                    break;

                case QuarterPiece.EdgeAlongY:
                    MoveOutwardX(ref rect, tile.SignX, -insetCells);
                    MoveOutwardY(ref rect, tile.SignY, insetCells);
                    break;
            }

            return rect;
        }

        /// <summary>Moves the side of <paramref name="rect"/> away from its cell's centre on x by <paramref name="by"/>, outward when positive.</summary>
        private static void MoveOutwardX(ref Rect rect, int signX, float by)
        {
            if (signX > 0)
            {
                rect.xMax += by;
            }
            else
            {
                rect.xMin -= by;
            }
        }

        /// <summary>Moves the side of <paramref name="rect"/> away from its cell's centre on y by <paramref name="by"/>, outward when positive.</summary>
        private static void MoveOutwardY(ref Rect rect, int signY, float by)
        {
            if (signY > 0)
            {
                rect.yMax += by;
            }
            else
            {
                rect.yMin -= by;
            }
        }
    }
}
