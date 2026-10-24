using System.Collections.Generic;
using GateRush.Core;
using UnityEngine;

namespace GateRush.Runtime
{
    /// <summary>
    /// Where each quarter of a block's lit body is drawn, and of anything set
    /// into it or cast by it (Modules 15, 20 and 46). A block's body is its
    /// footprint's own quarters; a layered block's inner shape is the same
    /// quarters pulled inward, by an inset, on every side that faces out of
    /// the block; and its floor shadow is the same quarters widened outward,
    /// on those sides, by the shadow's reach.
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
    /// sides;</item>
    /// <item>a concave corner moves in the same way, which carries its notch
    /// inward, round the inside of the bend;</item>
    /// <item>an edge moves in by the inset on its outward side and
    /// <b>out</b> by the inset along its run.</item>
    /// </list>
    /// <para><b>Why an edge reaches along its run.</b> That side of an edge
    /// always faces a cell of the same block, whose quarter there is either
    /// another edge in the same band — the reach then overlaps the same colour
    /// — or a concave corner. A concave corner pulled in leaves a strip one
    /// inset wide between itself and each neighbouring cell, and the edge
    /// across that strip is the only piece that can cover it. With the reach
    /// the quarters cover exactly the body shrunk by the inset; without it the
    /// shape would be slit on both sides of a bend.</para>
    /// </remarks>
    public static class FaceShape
    {
        /// <summary>
        /// The rectangle the quarter for <paramref name="tile"/> covers, in
        /// cell units in the frame of its cells. It never leaves the
        /// footprint's cells.
        /// </summary>
        /// <param name="tile">A quarter of the block's footprint (<see cref="BlockTiling"/>).</param>
        /// <param name="insetCells">How far the shape sits inside the body, in cells; 0 gives the body itself.</param>
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

        /// <summary>
        /// The rectangle a translucent or added quarter for
        /// <paramref name="tile"/> covers — the highlight on a layered block's
        /// inner shape, which the peel re-poses — in cell units in the frame of
        /// its cells: <see cref="QuarterRect"/>'s, except that an edge keeps to
        /// its own quarter along its run unless the next quarter along it is a
        /// concave corner. The reach along a run covers the strip beside a
        /// pulled-in concave corner; on a straight run it only overlaps the
        /// neighbouring edge, which an opaque face hides but a translucent or
        /// added piece would blend twice, making it stronger wherever two edges
        /// overlap (Module 46).
        /// </summary>
        /// <param name="tile">A quarter of the block's footprint.</param>
        /// <param name="tiles">Every quarter of that footprint (<see cref="BlockTiling"/>), to find the next quarter along the run.</param>
        /// <param name="insetCells">How far the shape sits inside the body, in cells.</param>
        public static Rect LightQuarterRect(QuarterTile tile, IReadOnlyList<QuarterTile> tiles, float insetCells)
        {
            var rect = QuarterRect(tile, insetCells);
            switch (tile.Piece)
            {
                case QuarterPiece.EdgeAlongX:
                    if (!IsNextAlongRunConcave(tile, tiles, new Coord(tile.SignX, 0)))
                    {
                        MoveOutwardX(ref rect, tile.SignX, -insetCells);
                    }

                    break;

                case QuarterPiece.EdgeAlongY:
                    if (!IsNextAlongRunConcave(tile, tiles, new Coord(0, tile.SignY)))
                    {
                        MoveOutwardY(ref rect, tile.SignY, -insetCells);
                    }

                    break;
            }

            return rect;
        }

        /// <summary>
        /// The rectangle a floor-shadow quarter for <paramref name="tile"/>
        /// covers: <paramref name="quarterRect"/>, where the quarter itself is
        /// drawn, widened by <paramref name="marginCells"/> on the sides that
        /// face out of the shape — both for an outer corner, the outward one
        /// for an edge, none for a concave corner or a fill — so the shadow
        /// can fall past the shape, and no two quarters of one shape cover the
        /// same floor (Module 46). The shadow sprites are painted on canvases
        /// widened the same way.
        /// </summary>
        /// <param name="quarterRect">Where the quarter is drawn, in any frame whose axes match its cells'.</param>
        /// <param name="tile">The quarter (<see cref="BlockTiling"/> or <see cref="FrameTiling"/>).</param>
        /// <param name="marginCells">How far past the cell the shadow reaches, in the frame's units.</param>
        public static Rect ShadowQuarterRect(Rect quarterRect, QuarterTile tile, float marginCells)
        {
            var rect = quarterRect;
            switch (tile.Piece)
            {
                case QuarterPiece.OuterCorner:
                    MoveOutwardX(ref rect, tile.SignX, marginCells);
                    MoveOutwardY(ref rect, tile.SignY, marginCells);
                    break;

                case QuarterPiece.EdgeAlongX:
                    MoveOutwardY(ref rect, tile.SignY, marginCells);
                    break;

                case QuarterPiece.EdgeAlongY:
                    MoveOutwardX(ref rect, tile.SignX, marginCells);
                    break;
            }

            return rect;
        }

        /// <summary>
        /// True when the quarter beside <paramref name="tile"/> across its
        /// outward side along its run — in the cell <paramref name="step"/>
        /// away, mirrored on that axis — is a concave corner.
        /// </summary>
        private static bool IsNextAlongRunConcave(QuarterTile tile, IReadOnlyList<QuarterTile> tiles, Coord step)
        {
            var nextCell = tile.Cell + step;
            var signX = step.X != 0 ? -tile.SignX : tile.SignX;
            var signY = step.Y != 0 ? -tile.SignY : tile.SignY;
            for (var i = 0; i < tiles.Count; i++)
            {
                var other = tiles[i];
                if (other.Cell == nextCell && other.SignX == signX && other.SignY == signY)
                {
                    return other.Piece == QuarterPiece.ConcaveCorner;
                }
            }

            return false;
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
