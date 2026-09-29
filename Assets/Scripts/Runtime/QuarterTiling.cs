using System;
using System.Collections.Generic;
using GateRush.Core;

namespace GateRush.Runtime
{
    /// <summary>
    /// The quarter rule, written once for both <see cref="BlockTiling"/> and
    /// <see cref="FrameTiling"/>: each quarter looks at its two orthogonal
    /// neighbours and its diagonal neighbour on that corner, and picks one of
    /// the five <see cref="QuarterPiece"/>s.
    /// </summary>
    internal static class QuarterTiling
    {
        /// <summary>The order quarters are listed in within one cell.</summary>
        private static readonly Quarter[] Corners =
        {
            Quarter.BottomLeft, Quarter.BottomRight, Quarter.TopLeft, Quarter.TopRight
        };

        /// <summary>
        /// Four quarters per cell of <paramref name="cells"/>, in cell order and
        /// then in <see cref="Quarter"/> order. <paramref name="isMember"/> says
        /// which cells belong to the shape; every cell in
        /// <paramref name="cells"/> must.
        /// </summary>
        internal static IReadOnlyList<QuarterTile> For(IReadOnlyList<Coord> cells, Func<Coord, bool> isMember)
        {
            var tiles = new QuarterTile[cells.Count * Corners.Length];
            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                for (var c = 0; c < Corners.Length; c++)
                {
                    var corner = Corners[c];
                    var sx = QuarterTile.SignXOf(corner);
                    var sy = QuarterTile.SignYOf(corner);
                    var piece = PieceFor(
                        isMember(cell + new Coord(sx, 0)),
                        isMember(cell + new Coord(0, sy)),
                        isMember(cell + new Coord(sx, sy)));
                    tiles[i * Corners.Length + c] = new QuarterTile(cell, corner, piece);
                }
            }

            return tiles;
        }

        private static QuarterPiece PieceFor(bool hasAlongX, bool hasAlongY, bool hasDiagonal)
        {
            if (!hasAlongX && !hasAlongY)
            {
                return QuarterPiece.OuterCorner;
            }

            if (!hasAlongY)
            {
                return QuarterPiece.EdgeAlongX;
            }

            if (!hasAlongX)
            {
                return QuarterPiece.EdgeAlongY;
            }

            return hasDiagonal ? QuarterPiece.Fill : QuarterPiece.ConcaveCorner;
        }
    }
}
