using System;
using System.Collections.Generic;
using UnityEngine;

namespace GateRush.Runtime
{
    /// <summary>
    /// The generated sprite sets a quarter tile can be drawn with (Module 46).
    /// Each set has a sprite per <see cref="QuarterSpriteKind"/> and is posed
    /// the same way (<see cref="QuarterTile"/>).
    /// </summary>
    public enum QuarterLayer
    {
        /// <summary>The face: the shape inside its outline, white and tinted, used for lips and faces.</summary>
        Face,

        /// <summary>The whole silhouette, outline included, drawn untinted in the outline colour under the face.</summary>
        Contour,

        /// <summary>The gloss band just inside the face's edge, as strong as the edge faces up, drawn untinted over the face.</summary>
        Shine
    }

    /// <summary>
    /// Which quarters of a shape carry its contour and its shine (Module 46,
    /// D79). Pure selection over <see cref="BlockTiling"/>'s or
    /// <see cref="FrameTiling"/>'s quarters; where and how they are drawn is
    /// the caller's.
    /// </summary>
    /// <remarks>
    /// <para><b>Contour.</b> Every quarter but a fill: a fill lies wholly
    /// inside the shape, under a face fill that covers it exactly.</para>
    /// <para><b>Shine.</b> The shine sprite lights an edge as much as it faces
    /// the sprite's top and fades to nothing where it turns to a side, so it
    /// goes only on quarters whose mirroring keeps "top" meaning one way: an
    /// outer corner, an edge along x and a concave corner. A quarter on the
    /// top half of its cell lights its top edge; one on the bottom half,
    /// mirrored, its bottom edge. An edge along y is a side, turned onto the
    /// sprite's top, and would be lit fully: it gets none, and the corners
    /// beside it have faded to nothing where they meet it. A fill has no
    /// edge.</para>
    /// </remarks>
    public static class QuarterLayers
    {
        /// <summary>The quarters of <paramref name="tiles"/> that carry the contour, in their order.</summary>
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
        /// The quarters of <paramref name="tiles"/> that carry the shine on the
        /// shape's top edges (<paramref name="isTop"/>) or its bottom edges, in
        /// their order.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="tiles"/> is null.</exception>
        public static IReadOnlyList<QuarterTile> Shine(IReadOnlyList<QuarterTile> tiles, bool isTop)
        {
            if (tiles == null)
            {
                throw new ArgumentNullException(nameof(tiles));
            }

            var shine = new List<QuarterTile>();
            for (var i = 0; i < tiles.Count; i++)
            {
                var tile = tiles[i];
                if ((tile.SignY > 0) == isTop && CarriesShine(tile.Piece))
                {
                    shine.Add(tile);
                }
            }

            return shine;
        }

        /// <summary>
        /// How much a quarter drawn in <paramref name="color"/> is enlarged on
        /// every side to hide sub-pixel cracks at its seams: all of
        /// <paramref name="overlapCells"/> when the colour is opaque, where two
        /// neighbours covering the same strip look like one; none when it is
        /// translucent — a shine, a bevel line, anything fading — where that
        /// strip would be blended twice and show as a line along every seam.
        /// </summary>
        public static float SeamOverlap(Color color, float overlapCells) => color.a >= 1f ? overlapCells : 0f;

        private static bool CarriesShine(QuarterPiece piece) =>
            piece == QuarterPiece.OuterCorner || piece == QuarterPiece.EdgeAlongX || piece == QuarterPiece.ConcaveCorner;
    }
}
