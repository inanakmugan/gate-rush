using UnityEngine;

namespace GateRush.Runtime
{
    /// <summary>
    /// Where each quarter of a grabbed block's white outline is drawn (Module
    /// 18). The outline is the block's own quarter pieces drawn again, white,
    /// each on its quarter grown outward on the sides that face out of the
    /// block, so the outline follows the whole footprint — an L shape gets an
    /// L-shaped outline — and sits behind the lip and face.
    /// </summary>
    /// <remarks>
    /// <para>A quarter sprite is drawn from the cell's centre out, so
    /// stretching its rectangle away from that inner corner pushes its drawn
    /// boundary outward by about the growth. Which sides grow follows the
    /// piece: an outer corner on both of its outer sides, an edge along x only
    /// on its y side, an edge along y only on its x side, a concave corner
    /// toward its corner (pushing its notch outward, round the inside of the
    /// bend), and a fill not at all. Sides that face the block's own cells
    /// never grow, except the concave corner's, whose growth falls under the
    /// neighbouring cells' faces.</para>
    /// <para>The lip shows below the face, so a bottom side grows by the lip's
    /// drop as well, and the outline wraps the lip instead of hiding under
    /// it.</para>
    /// </remarks>
    public static class LiftOutline
    {
        /// <summary>
        /// The rectangle the outline quarter for <paramref name="tile"/> covers,
        /// in cell units in the frame of its cells.
        /// </summary>
        /// <param name="tile">A quarter of the block's footprint (<see cref="BlockTiling"/>).</param>
        /// <param name="widthCells">How far the outline reaches past the face, in cells.</param>
        /// <param name="lipDropCells">How far the lip shows below the face, in cells; added on bottom sides.</param>
        public static Rect QuarterRect(QuarterTile tile, float widthCells, float lipDropCells)
        {
            var rect = BlockTiling.QuarterRect(tile);
            var growsX = tile.Piece == QuarterPiece.OuterCorner || tile.Piece == QuarterPiece.EdgeAlongY
                         || tile.Piece == QuarterPiece.ConcaveCorner;
            var growsY = tile.Piece == QuarterPiece.OuterCorner || tile.Piece == QuarterPiece.EdgeAlongX
                         || tile.Piece == QuarterPiece.ConcaveCorner;

            if (growsX)
            {
                if (tile.SignX > 0)
                {
                    rect.xMax += widthCells;
                }
                else
                {
                    rect.xMin -= widthCells;
                }
            }

            if (growsY)
            {
                if (tile.SignY > 0)
                {
                    rect.yMax += widthCells;
                }
                else
                {
                    rect.yMin -= widthCells + lipDropCells;
                }
            }

            return rect;
        }
    }
}
