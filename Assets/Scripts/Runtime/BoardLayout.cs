using System;
using GateRush.Core;
using UnityEngine;

namespace GateRush.Runtime
{
    /// <summary>
    /// Maps between grid positions and world positions, and sizes an
    /// orthographic camera to fit the board. The board is centred on the world
    /// origin, and grid y grows upward like world y, so <see cref="BoardEdge.Top"/>
    /// (row <c>Height - 1</c>) is drawn at the top of the screen.
    /// </summary>
    /// <remarks>
    /// A <em>grid position</em> is a fractional position in cell units: cell
    /// <c>(x, y)</c> covers <c>[x, x + 1) × [y, y + 1)</c>, so the floor of a
    /// grid position is its cell. This is the unit <see cref="DragController"/>
    /// takes pointers in.
    /// </remarks>
    public sealed class BoardLayout
    {
        /// <summary>A layout for a <paramref name="width"/> x <paramref name="height"/> board.</summary>
        /// <param name="cellSize">World units per cell. Comes from <c>RuntimeConfig</c>.</param>
        /// <exception cref="ArgumentOutOfRangeException">A dimension or the cell size is not positive.</exception>
        public BoardLayout(int width, int height, float cellSize)
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

            Width = width;
            Height = height;
            CellSize = cellSize;
        }

        /// <summary>Board width in cells.</summary>
        public int Width { get; }

        /// <summary>Board height in cells.</summary>
        public int Height { get; }

        /// <summary>World units per cell.</summary>
        public float CellSize { get; }

        /// <summary>The cell containing a grid position: its floor on both axes.</summary>
        public static Coord CellOf(Vector2 gridPosition) =>
            new Coord(Mathf.FloorToInt(gridPosition.x), Mathf.FloorToInt(gridPosition.y));

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
        /// which the whole board plus <paramref name="marginCells"/> on every
        /// side fits a screen of <paramref name="aspect"/> (width / height) in
        /// both directions: whichever of height and width is the tighter fit
        /// decides.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="aspect"/> is not positive, or
        /// <paramref name="marginCells"/> is negative.
        /// </exception>
        public float FitOrthographicSize(float aspect, float marginCells)
        {
            if (!(aspect > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(aspect), aspect, "The aspect ratio must be positive.");
            }

            if (marginCells < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(marginCells), marginCells, "The margin may not be negative.");
            }

            var halfHeight = (Height * 0.5f + marginCells) * CellSize;
            var halfWidth = (Width * 0.5f + marginCells) * CellSize;
            return Math.Max(halfHeight, halfWidth / aspect);
        }
    }
}
