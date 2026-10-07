using UnityEngine;

namespace GateRush.Runtime
{
    /// <summary>
    /// Where each quarter of a layered block's inner shape is drawn (Module
    /// 20), over a footprint that is filled to its outline. The inner shape is
    /// the block's own quarter pieces drawn again in the colour beneath, each
    /// pulled inward on the sides that face out of the block, so the outer
    /// colour shows as an even rim around one inner piece that follows the
    /// whole footprint — an L shape gets an L-shaped inner piece — and never
    /// as a mark per cell.
    /// </summary>
    /// <remarks>
    /// The rule is <see cref="FaceShape"/>'s, without a lip: what the Level
    /// Editor draws, whose blocks are flat. The board, whose blocks keep a lip
    /// inside their cells, asks <see cref="FaceShape"/> directly.
    /// </remarks>
    public static class LayerInset
    {
        /// <summary>
        /// The rectangle the inner-shape quarter for <paramref name="tile"/>
        /// covers, in cell units in the frame of its cells.
        /// </summary>
        /// <param name="tile">A quarter of the block's footprint (<see cref="BlockTiling"/>).</param>
        /// <param name="insetCells">How far the inner shape sits inside the footprint, in cells; 0 gives the quarter's own rectangle.</param>
        public static Rect QuarterRect(QuarterTile tile, float insetCells) =>
            FaceShape.QuarterRect(tile, insetCells, 0f);
    }
}
