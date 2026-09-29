using System.Collections.Generic;
using System.Linq;
using GateRush.Core;
using GateRush.Runtime;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="QuarterTile"/>'s pose: each sprite is drawn once, for
    /// the top-right quarter, and the flips and rotation must carry its drawn
    /// boundary onto exactly the sides of the quarter that face outside the
    /// shape. Modelled the way a <c>SpriteRenderer</c> draws: flip in sprite
    /// space, then turn.
    /// </summary>
    public class QuarterTileTests
    {
        private static readonly Quarter[] Corners =
        {
            Quarter.BottomLeft, Quarter.BottomRight, Quarter.TopLeft, Quarter.TopRight
        };

        private static readonly QuarterPiece[] Pieces =
        {
            QuarterPiece.OuterCorner, QuarterPiece.EdgeAlongX, QuarterPiece.EdgeAlongY,
            QuarterPiece.Fill, QuarterPiece.ConcaveCorner
        };

        [Test]
        public void Pose_EveryPieceOnEveryCorner_CarriesTheDrawnBoundaryOntoTheOutwardSides()
        {
            Assert.AreEqual(-90f, QuarterTile.EdgeAlongYRotationDegrees, "the model below turns a quarter clockwise");
            var checkedPoses = 0;

            foreach (var corner in Corners)
            {
                foreach (var piece in Pieces)
                {
                    var tile = new QuarterTile(new Coord(0, 0), corner, piece);
                    var sx = tile.SignX;
                    var sy = tile.SignY;

                    var posedSides = DrawnBoundarySides(tile.SpriteKind).Select(side => Pose(tile, side)).ToList();
                    var posedCorner = HasDrawnCorner(tile.SpriteKind) ? Pose(tile, (1, 1)) : ((int, int)?)null;

                    CollectionAssert.AreEquivalent(OutwardSides(piece, sx, sy), posedSides, $"{tile}: boundary sides");
                    Assert.AreEqual(HasDrawnCorner(tile.SpriteKind) ? (sx, sy) : ((int, int)?)null, posedCorner, $"{tile}: rounded corner");
                    checkedPoses++;
                }
            }

            Assert.AreEqual(Corners.Length * Pieces.Length, checkedPoses);
        }

        /// <summary>The sides of the top-right quarter each drawn sprite shows as boundary, as outward unit steps.</summary>
        private static IEnumerable<(int, int)> DrawnBoundarySides(QuarterSpriteKind kind)
        {
            switch (kind)
            {
                case QuarterSpriteKind.Outer:
                    return new[] { (1, 0), (0, 1) };
                case QuarterSpriteKind.Edge:
                    return new[] { (0, 1) };
                default:
                    return new (int, int)[0];
            }
        }

        /// <summary>The outer and concave sprites round the quarter's (1, 1) corner.</summary>
        private static bool HasDrawnCorner(QuarterSpriteKind kind) =>
            kind == QuarterSpriteKind.Outer || kind == QuarterSpriteKind.Concave;

        /// <summary>The sides of a quarter at signs (sx, sy) that face outside the shape, for its piece.</summary>
        private static IEnumerable<(int, int)> OutwardSides(QuarterPiece piece, int sx, int sy)
        {
            switch (piece)
            {
                case QuarterPiece.OuterCorner:
                    return new[] { (sx, 0), (0, sy) };
                case QuarterPiece.EdgeAlongX:
                    return new[] { (0, sy) };
                case QuarterPiece.EdgeAlongY:
                    return new[] { (sx, 0) };
                default:
                    return new (int, int)[0];
            }
        }

        /// <summary>Where a sprite-space direction ends up: flipped in sprite space, then turned a quarter clockwise when rotated.</summary>
        private static (int, int) Pose(QuarterTile tile, (int x, int y) direction)
        {
            var x = tile.FlipX ? -direction.x : direction.x;
            var y = tile.FlipY ? -direction.y : direction.y;
            return tile.IsRotated ? (y, -x) : (x, y);
        }
    }
}
