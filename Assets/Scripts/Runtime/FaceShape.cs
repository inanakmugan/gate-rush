using UnityEngine;

namespace GateRush.Runtime
{
    /// <summary>
    /// Where each quarter of a block's face is drawn, and of anything set
    /// into that face (Modules 15 and 20). A block's whole resting drawing
    /// stays inside its footprint's cells: the lip is the footprint's own
    /// quarters, and the face is the same quarters with every side that faces
    /// down out of the block raised by the lip, so the lip shows as a strip
    /// along the block's lower outline, inside the cells. A layered block's
    /// inner shape is the face pulled inward again, by an inset, on every side
    /// that faces out of the block.
    /// </summary>
    /// <remarks>
    /// <para>A quarter sprite is drawn from the cell's centre out, so moving a
    /// rectangle's outward side toward that centre pulls its drawn boundary
    /// inward by about the same amount. Sides toward the cell's own centre
    /// never move, so the quarters of one cell always meet. Which outward
    /// sides move follows the piece:</para>
    /// <list type="bullet">
    /// <item>a fill has no side facing out of the block and is unchanged;</item>
    /// <item>an outer corner moves in by the inset on both of its outward
    /// sides, and a bottom one by the lip too on its bottom side;</item>
    /// <item>a concave corner moves in the same way, which carries its notch
    /// inward, round the inside of the bend; the notch of a bottom one opens
    /// downward, beside a neighbour's bottom side, so it rises by the lip with
    /// that side;</item>
    /// <item>an edge along x moves in by the inset on its y side — a bottom
    /// one by the lip too — and <b>out</b> by the inset along its run;</item>
    /// <item>an edge along y moves in by the inset on its x side and
    /// <b>out</b> by the inset along its run — a top one by the lip
    /// too.</item>
    /// </list>
    /// <para><b>Why an edge reaches along its run.</b> That side of an edge
    /// always faces a cell of the same block, whose quarter there is either
    /// another edge in the same band — the reach then overlaps the same colour
    /// — or a concave corner. A concave corner pulled in leaves a strip
    /// between itself and each neighbouring cell, and the edge across that
    /// strip is the only piece that can cover it. The strip beside it is one
    /// inset wide; the strip beneath a bottom concave corner is one inset and
    /// one lip tall, and the edge that covers it is the top quarter of the
    /// cell below, which is why that one reaches up by the lip as well. With
    /// the reach the quarters cover exactly the face shrunk by the inset;
    /// without it the shape would be slit on both sides of a bend.</para>
    /// </remarks>
    public static class FaceShape
    {
        /// <summary>
        /// The rectangle the quarter for <paramref name="tile"/> covers, in
        /// cell units in the frame of its cells. It never leaves the
        /// footprint's cells.
        /// </summary>
        /// <param name="tile">A quarter of the block's footprint (<see cref="BlockTiling"/>).</param>
        /// <param name="insetCells">How far the shape sits inside the face, in cells; 0 gives the face itself.</param>
        /// <param name="lipCells">How much of the footprint's lower outline the lip takes, in cells; 0 gives a shape that fills the footprint.</param>
        public static Rect QuarterRect(QuarterTile tile, float insetCells, float lipCells)
        {
            var rect = BlockTiling.QuarterRect(tile);
            var isBottom = tile.SignY < 0;
            switch (tile.Piece)
            {
                case QuarterPiece.OuterCorner:
                case QuarterPiece.ConcaveCorner:
                    MoveOutwardX(ref rect, tile.SignX, -insetCells);
                    MoveOutwardY(ref rect, tile.SignY, -(insetCells + (isBottom ? lipCells : 0f)));
                    break;

                case QuarterPiece.EdgeAlongX:
                    MoveOutwardY(ref rect, tile.SignY, -(insetCells + (isBottom ? lipCells : 0f)));
                    MoveOutwardX(ref rect, tile.SignX, insetCells);
                    break;

                case QuarterPiece.EdgeAlongY:
                    MoveOutwardX(ref rect, tile.SignX, -insetCells);
                    MoveOutwardY(ref rect, tile.SignY, insetCells + (isBottom ? 0f : lipCells));
                    break;
            }

            return rect;
        }

        /// <summary>
        /// How far what sits on a block's face — its studs with their gloss,
        /// or its axis arrow — is raised above the footprint's own centres, in
        /// cells: half the lip, which keeps it centred on a face the lip
        /// shortened from below. The one value for the board and for the
        /// introduction cards.
        /// </summary>
        /// <param name="lipCells">How much of the footprint's lower outline the lip takes, in cells.</param>
        public static float ContentRise(float lipCells) => lipCells * 0.5f;

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
