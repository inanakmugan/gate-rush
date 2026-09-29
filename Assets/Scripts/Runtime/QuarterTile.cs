using System;
using GateRush.Core;

namespace GateRush.Runtime
{
    /// <summary>One quarter of a cell, named by the corner it touches.</summary>
    public enum Quarter
    {
        BottomLeft,
        BottomRight,
        TopLeft,
        TopRight
    }

    /// <summary>
    /// Which of the five pieces a quarter shows, decided by its two orthogonal
    /// neighbours and its diagonal neighbour on that corner (Module 15).
    /// </summary>
    public enum QuarterPiece
    {
        /// <summary>Neither orthogonal neighbour is in the shape: a rounded corner.</summary>
        OuterCorner,

        /// <summary>Only the neighbour along x is in the shape: an edge running along x.</summary>
        EdgeAlongX,

        /// <summary>Only the neighbour along y is in the shape: an edge running along y.</summary>
        EdgeAlongY,

        /// <summary>Both orthogonal neighbours and the diagonal are in the shape: solid face.</summary>
        Fill,

        /// <summary>Both orthogonal neighbours are in the shape but the diagonal is not: the inside of an L.</summary>
        ConcaveCorner
    }

    /// <summary>
    /// The four sprites the generator draws. <see cref="QuarterPiece.EdgeAlongX"/>
    /// and <see cref="QuarterPiece.EdgeAlongY"/> share <see cref="Edge"/>, the
    /// second shown rotated.
    /// </summary>
    public enum QuarterSpriteKind
    {
        Outer,
        Edge,
        Fill,
        Concave
    }

    /// <summary>
    /// One quarter tile of a shape drawn from quarters: a block's footprint
    /// (<see cref="BlockTiling"/>) or the board frame (<see cref="FrameTiling"/>).
    /// Also says how the one drawn sprite for its piece is posed onto this
    /// quarter.
    /// </summary>
    /// <remarks>
    /// <para>Every sprite is drawn once, for the <see cref="Quarter.TopRight"/>
    /// quarter; <see cref="QuarterSpriteKind.Edge"/> is drawn with its boundary
    /// along the top, as <see cref="QuarterPiece.EdgeAlongX"/> needs it there.
    /// The generator shades every piece symmetrically — tone depends only on the
    /// distance to the shape's boundary — so mirroring and rotating a piece
    /// never turns its lighting the wrong way.</para>
    /// <para><b>Pose.</b> A quarter at corner signs <c>(sx, sy)</c> mirrors the
    /// sprite by those signs. <see cref="QuarterPiece.EdgeAlongY"/> also turns
    /// the sprite by <see cref="EdgeAlongYRotationDegrees"/>, moving the top
    /// boundary to the right. A <c>SpriteRenderer</c> flips in sprite space,
    /// before its transform's rotation, so a rotated piece swaps which sign
    /// drives which flip.</para>
    /// </remarks>
    public readonly struct QuarterTile : IEquatable<QuarterTile>
    {
        /// <summary>
        /// The rotation, in degrees about z, that turns the edge sprite's top
        /// boundary onto its right side: a quarter turn clockwise.
        /// </summary>
        public const float EdgeAlongYRotationDegrees = -90f;

        /// <summary>A quarter tile.</summary>
        public QuarterTile(Coord cell, Quarter corner, QuarterPiece piece)
        {
            Cell = cell;
            Corner = corner;
            Piece = piece;
        }

        /// <summary>The cell the quarter belongs to, in the frame of the tiled cells.</summary>
        public Coord Cell { get; }

        /// <summary>Which corner of <see cref="Cell"/> the quarter covers.</summary>
        public Quarter Corner { get; }

        /// <summary>The piece it shows.</summary>
        public QuarterPiece Piece { get; }

        /// <summary>+1 when the quarter is on the right half of its cell, −1 on the left.</summary>
        public int SignX => SignXOf(Corner);

        /// <summary>+1 when the quarter is on the top half of its cell, −1 on the bottom.</summary>
        public int SignY => SignYOf(Corner);

        /// <summary>The drawn sprite this quarter shows.</summary>
        public QuarterSpriteKind SpriteKind
        {
            get
            {
                switch (Piece)
                {
                    case QuarterPiece.OuterCorner:
                        return QuarterSpriteKind.Outer;
                    case QuarterPiece.EdgeAlongX:
                    case QuarterPiece.EdgeAlongY:
                        return QuarterSpriteKind.Edge;
                    case QuarterPiece.Fill:
                        return QuarterSpriteKind.Fill;
                    default:
                        return QuarterSpriteKind.Concave;
                }
            }
        }

        /// <summary>True when the sprite is turned by <see cref="EdgeAlongYRotationDegrees"/>.</summary>
        public bool IsRotated => Piece == QuarterPiece.EdgeAlongY;

        /// <summary>The sprite's <c>flipX</c>, applied in sprite space before the rotation.</summary>
        public bool FlipX => (IsRotated ? SignY : SignX) < 0;

        /// <summary>The sprite's <c>flipY</c>, applied in sprite space before the rotation.</summary>
        public bool FlipY => (IsRotated ? SignX : SignY) < 0;

        /// <summary>+1 for a right-hand corner, −1 for a left-hand one.</summary>
        public static int SignXOf(Quarter corner) =>
            corner == Quarter.BottomRight || corner == Quarter.TopRight ? 1 : -1;

        /// <summary>+1 for a top corner, −1 for a bottom one.</summary>
        public static int SignYOf(Quarter corner) =>
            corner == Quarter.TopLeft || corner == Quarter.TopRight ? 1 : -1;

        /// <inheritdoc />
        public bool Equals(QuarterTile other) =>
            Cell == other.Cell && Corner == other.Corner && Piece == other.Piece;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is QuarterTile other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => (Cell.GetHashCode() * 397) ^ ((int)Corner * 31) ^ (int)Piece;

        /// <inheritdoc />
        public override string ToString() => $"{Cell} {Corner}: {Piece}";
    }
}
