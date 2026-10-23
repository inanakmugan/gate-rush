using System;
using System.Collections.Generic;
using UnityEngine;

namespace GateRush.Runtime
{
    /// <summary>
    /// The generated sprite sets a quarter tile can be drawn with (Module 46).
    /// A plain set has a sprite per <see cref="QuarterSpriteKind"/>; the lit
    /// sets have one per quarter's place — a corner of its cell, or the side an
    /// edge faces — since the light has a direction. All are posed the same
    /// way (<see cref="QuarterTile"/>).
    /// </summary>
    public enum QuarterLayer
    {
        /// <summary>The shape's silhouette, white and tinted: a grabbed block's outline, a preview.</summary>
        Plain,

        /// <summary>The shape's soft shadow on the floor, untinted black, on a canvas widened past the cell (<see cref="FaceShape.ShadowQuarterRect"/>).</summary>
        Shadow,

        /// <summary>The lit body: its shade under the light, tinted, drawn with the lit material.</summary>
        Lit,

        /// <summary>
        /// The lit body the other way round, for a recess — a layered block's
        /// window: each quarter takes the lit piece of the opposite corner, or
        /// of the opposite side, so the slope there faces inward and the light
        /// reverses as it would on a sunken rim.
        /// </summary>
        ReversedLit,

        /// <summary>The lit body's white highlight, untinted, added by the specular material.</summary>
        Body
    }

    /// <summary>
    /// Which quarters of a shape carry its floor shadow, and how far a quarter
    /// may overlap its neighbours (Module 46, D79). Pure rules over
    /// <see cref="BlockTiling"/>'s or <see cref="FrameTiling"/>'s quarters;
    /// where and how they are drawn is the caller's.
    /// </summary>
    public static class QuarterLayers
    {
        /// <summary>
        /// The quarters of <paramref name="tiles"/> with an edge — every one
        /// but a fill — in their order: those that cast a shadow past the
        /// shape. A fill lies wholly under the shape's own body.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="tiles"/> is null.</exception>
        public static IReadOnlyList<QuarterTile> Edged(IReadOnlyList<QuarterTile> tiles)
        {
            if (tiles == null)
            {
                throw new ArgumentNullException(nameof(tiles));
            }

            var edged = new List<QuarterTile>(tiles.Count);
            for (var i = 0; i < tiles.Count; i++)
            {
                if (tiles[i].Piece != QuarterPiece.Fill)
                {
                    edged.Add(tiles[i]);
                }
            }

            return edged;
        }

        /// <summary>
        /// How much a quarter of <paramref name="layer"/> drawn in
        /// <paramref name="color"/> is enlarged on every side to hide
        /// sub-pixel cracks at its seams: all of <paramref name="overlapCells"/>
        /// when it is opaque, where two neighbours covering the same strip
        /// look like one; none when it is translucent or added — a shadow, a
        /// highlight, or anything drawn or fading below full alpha — where that
        /// strip would be blended twice and show as a line along every seam.
        /// </summary>
        public static float SeamOverlap(QuarterLayer layer, Color color, float overlapCells) =>
            layer != QuarterLayer.Shadow && layer != QuarterLayer.Body && color.a >= 1f ? overlapCells : 0f;
    }
}
