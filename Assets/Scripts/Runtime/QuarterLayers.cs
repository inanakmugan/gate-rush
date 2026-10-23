using System;
using System.Collections.Generic;
using UnityEngine;

namespace GateRush.Runtime
{
    /// <summary>
    /// The generated sprite sets a quarter tile can be drawn with (Module 46).
    /// Each set has a sprite per <see cref="QuarterSpriteKind"/>, or, for the
    /// light, per quarter's place, and is posed the same way
    /// (<see cref="QuarterTile"/>).
    /// </summary>
    public enum QuarterLayer
    {
        /// <summary>The face: the shape inside its outline, white and tinted, used for lips and faces.</summary>
        Face,

        /// <summary>The whole silhouette, outline included, drawn untinted in the outline colour under the face.</summary>
        Contour,

        /// <summary>
        /// The body over a block's, a gate's or the frame's face, untinted:
        /// its bevel ring, its top band and gloss streak, and the reflection
        /// line above its lip, one sprite per corner of a cell and per side of
        /// an edge.
        /// </summary>
        Body,

        /// <summary>
        /// The matte light the other way round, for a recess — a layered
        /// block's window: each quarter takes the light piece of the opposite
        /// corner, or of the opposite side, so the bands along the edges
        /// facing the light are shaded and the others lit.
        /// </summary>
        ReversedLight
    }

    /// <summary>
    /// Which quarters of a shape carry its contour and its light (Module 46,
    /// D79), and how far a quarter may overlap its neighbours. Pure rules over
    /// <see cref="BlockTiling"/>'s or <see cref="FrameTiling"/>'s quarters;
    /// where and how they are drawn is the caller's.
    /// </summary>
    /// <remarks>
    /// Every quarter but a fill carries both: a fill lies wholly inside the
    /// shape, under a face fill that covers it exactly, and has no edge to
    /// light.
    /// </remarks>
    public static class QuarterLayers
    {
        /// <summary>The quarters of <paramref name="tiles"/> that carry the contour and the light, in their order.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="tiles"/> is null.</exception>
        public static IReadOnlyList<QuarterTile> Contour(IReadOnlyList<QuarterTile> tiles)
        {
            if (tiles == null)
            {
                throw new ArgumentNullException(nameof(tiles));
            }

            var contour = new List<QuarterTile>(tiles.Count);
            for (var i = 0; i < tiles.Count; i++)
            {
                if (tiles[i].Piece != QuarterPiece.Fill)
                {
                    contour.Add(tiles[i]);
                }
            }

            return contour;
        }

        /// <summary>
        /// How much a quarter of <paramref name="layer"/> drawn in
        /// <paramref name="color"/> is enlarged on every side to hide
        /// sub-pixel cracks at its seams: all of <paramref name="overlapCells"/>
        /// when it is opaque, where two neighbours covering the same strip
        /// look like one; none when it is translucent — a light piece, whose
        /// bands are translucent in the sprite itself, or anything drawn or
        /// fading below full alpha — where that strip would be blended twice
        /// and show as a line along every seam.
        /// </summary>
        public static float SeamOverlap(QuarterLayer layer, Color color, float overlapCells) =>
            layer != QuarterLayer.Body && layer != QuarterLayer.ReversedLight && color.a >= 1f ? overlapCells : 0f;
    }
}
