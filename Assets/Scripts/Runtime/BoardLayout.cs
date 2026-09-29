using System;
using System.Collections.Generic;
using GateRush.Core;
using UnityEngine;

namespace GateRush.Runtime
{
    /// <summary>
    /// Maps between grid positions and world positions, and sizes and places an
    /// orthographic camera so the board plus its frame fits the screen with the
    /// HUD bands kept free. The board is centred on the world origin, and grid
    /// y grows upward like world y, so <see cref="BoardEdge.Top"/> (row
    /// <c>Height - 1</c>) is drawn at the top of the screen.
    /// </summary>
    /// <remarks>
    /// <para>A <em>grid position</em> is a fractional position in cell units:
    /// cell <c>(x, y)</c> covers <c>[x, x + 1) × [y, y + 1)</c>, so the floor of
    /// a grid position is its cell. This is the unit <see cref="DragController"/>
    /// takes pointers in.</para>
    /// <para><b>Camera fit.</b> The side margin is in cells, like the board.
    /// The top and bottom bands are fractions of the screen's height: the HUD
    /// is a screen-space canvas, so a band given in cells would grow with the
    /// board's on-screen cell size and either eat a small board's screen or let
    /// the HUD overlap a large one. Generator machines sit outside the frame
    /// (D48), so the fit also counts <see cref="Reach"/> on each side that has
    /// one. <see cref="Problems"/> is the single
    /// statement of the valid values; the constructor and
    /// <see cref="FitOrthographicSize"/> throw on what it reports.</para>
    /// </remarks>
    public sealed class BoardLayout
    {
        private const string FrameThicknessMessage = "Frame Thickness Cells must be above 0 and at most 1.";
        private const string SideMarginMessage = "Side Margin Cells may not be negative.";
        private const string BandsMessage =
            "Top Band Screen Fraction and Bottom Band Screen Fraction may not be negative, and their sum must be below 1.";

        /// <summary>A layout for a <paramref name="width"/> x <paramref name="height"/> board.</summary>
        /// <param name="cellSize">World units per cell. Comes from <c>RuntimeConfig</c>.</param>
        /// <param name="frameThicknessCells">The frame's thickness around the grid, in cells: above 0, at most 1.</param>
        /// <param name="reach">
        /// How far anything drawn outside the frame reaches beyond it on each
        /// side — generator machines (<see cref="GeneratorMachine.Reach"/>) — for
        /// the camera fit to keep in view. The default reaches nowhere.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">A dimension or the cell size is not positive, or the frame thickness is out of range.</exception>
        public BoardLayout(int width, int height, float cellSize, float frameThicknessCells, EdgeReach reach = default)
        {
            if (width < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(width), width, "The board width must be positive.");
            }

            if (height < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(height), height, "The board height must be positive.");
            }

            if (!(cellSize > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize), cellSize, "The cell size must be positive.");
            }

            if (!IsValidFrameThickness(frameThicknessCells))
            {
                throw new ArgumentOutOfRangeException(nameof(frameThicknessCells), frameThicknessCells, FrameThicknessMessage);
            }

            Width = width;
            Height = height;
            CellSize = cellSize;
            FrameThicknessCells = frameThicknessCells;
            Reach = reach;
        }

        /// <summary>Board width in cells.</summary>
        public int Width { get; }

        /// <summary>Board height in cells.</summary>
        public int Height { get; }

        /// <summary>World units per cell.</summary>
        public float CellSize { get; }

        /// <summary>The frame's thickness around the grid, in cells.</summary>
        public float FrameThicknessCells { get; }

        /// <summary>How far drawing outside the frame reaches beyond it on each side, in cells.</summary>
        public EdgeReach Reach { get; }

        /// <summary>The cell containing a grid position: its floor on both axes.</summary>
        public static Coord CellOf(Vector2 gridPosition) =>
            new Coord(Mathf.FloorToInt(gridPosition.x), Mathf.FloorToInt(gridPosition.y));

        /// <summary>
        /// Every reason these layout values cannot fit a board, one message per
        /// field, named as the config's inspector shows it; empty when valid.
        /// </summary>
        public static IReadOnlyList<string> Problems(
            float frameThicknessCells, float sideMarginCells, float topBandScreenFraction, float bottomBandScreenFraction)
        {
            var problems = new List<string>();

            if (!IsValidFrameThickness(frameThicknessCells))
            {
                problems.Add(FrameThicknessMessage);
            }

            if (!IsValidSideMargin(sideMarginCells))
            {
                problems.Add(SideMarginMessage);
            }

            if (!AreValidBands(topBandScreenFraction, bottomBandScreenFraction))
            {
                problems.Add(BandsMessage);
            }

            return problems;
        }

        /// <summary>The world position of a grid position. Inverse of <see cref="WorldToGrid"/>.</summary>
        public Vector2 GridToWorld(Vector2 gridPosition) =>
            new Vector2(
                (gridPosition.x - Width * 0.5f) * CellSize,
                (gridPosition.y - Height * 0.5f) * CellSize);

        /// <summary>The grid position of a world position. Inverse of <see cref="GridToWorld"/>.</summary>
        public Vector2 WorldToGrid(Vector2 world) =>
            new Vector2(
                world.x / CellSize + Width * 0.5f,
                world.y / CellSize + Height * 0.5f);

        /// <summary>The world position of the centre of <paramref name="cell"/>.</summary>
        public Vector2 CellCenter(Coord cell) =>
            GridToWorld(new Vector2(cell.X + 0.5f, cell.Y + 0.5f));

        /// <summary>The cell under a world position. May lie outside the board.</summary>
        public Coord WorldToCell(Vector2 world) => CellOf(WorldToGrid(world));

        /// <summary>
        /// The orthographic size (half the visible height, in world units) at
        /// which the board plus its frame and <see cref="Reach"/> fits a screen
        /// of <paramref name="aspect"/> (width / height): the full width less
        /// <paramref name="sideMarginCells"/> on each side, and the height
        /// between a top and a bottom band given as fractions of the screen's
        /// height. The width decides on a portrait screen; on a landscape or
        /// very short one the height does, and the whole board stays visible.
        /// Place the camera with <see cref="CameraCenterOffset"/>.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="aspect"/> is not positive, or a margin or band is one
        /// <see cref="Problems"/> reports.
        /// </exception>
        public float FitOrthographicSize(
            float aspect, float sideMarginCells, float topBandScreenFraction, float bottomBandScreenFraction)
        {
            if (!(aspect > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(aspect), aspect, "The aspect ratio must be positive.");
            }

            if (!IsValidSideMargin(sideMarginCells))
            {
                throw new ArgumentOutOfRangeException(nameof(sideMarginCells), sideMarginCells, SideMarginMessage);
            }

            if (!AreValidBands(topBandScreenFraction, bottomBandScreenFraction))
            {
                throw new ArgumentOutOfRangeException(nameof(topBandScreenFraction), topBandScreenFraction, BandsMessage);
            }

            var framedWidth = Width + 2f * FrameThicknessCells + Reach.Left + Reach.Right;
            var framedHeight = Height + 2f * FrameThicknessCells + Reach.Bottom + Reach.Top;
            var widthFit = (framedWidth * 0.5f + sideMarginCells) * CellSize / aspect;
            var heightFit = framedHeight * CellSize * 0.5f / (1f - topBandScreenFraction - bottomBandScreenFraction);
            return Math.Max(widthFit, heightFit);
        }

        /// <summary>
        /// Where the camera sits relative to the board's centre, in world units:
        /// on the middle of the framed board plus its <see cref="Reach"/>, and
        /// raised so that middle is centred between the top and bottom bands of
        /// a camera of <paramref name="orthographicSize"/>. Only the camera
        /// moves, so the mapping between grid and world stays centred on the
        /// board.
        /// </summary>
        public Vector2 CameraCenterOffset(float orthographicSize, float topBandScreenFraction, float bottomBandScreenFraction) =>
            new Vector2(
                (Reach.Right - Reach.Left) * 0.5f * CellSize,
                (Reach.Top - Reach.Bottom) * 0.5f * CellSize
                + (topBandScreenFraction - bottomBandScreenFraction) * orthographicSize);

        private static bool IsValidFrameThickness(float cells) => cells > 0f && cells <= 1f;

        private static bool IsValidSideMargin(float cells) => cells >= 0f && !float.IsInfinity(cells);

        private static bool AreValidBands(float top, float bottom) => top >= 0f && bottom >= 0f && top + bottom < 1f;
    }
}
