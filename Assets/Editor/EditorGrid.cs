using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using GateRush.Core;

namespace GateRush.Editor
{
    /// <summary>
    /// The pixel layout of a square-cell grid: where it sits on screen, how big
    /// a cell is, and which cell a point falls in. One <see cref="EditorGridLayout"/>
    /// describes the main board; another, with different
    /// <see cref="Columns"/>/<see cref="Rows"/>, describes an elevator wave's
    /// region. The maths is identical, which is the point — the two grids are one
    /// code path with different bounds, so they cannot drift.
    /// </summary>
    public readonly struct EditorGridLayout
    {
        /// <summary>
        /// The default ceiling on cell size (revision A5). A cell past this buys
        /// nothing — a 64px target is already easy to click — and a huge cell
        /// holding small text looks wrong.
        /// </summary>
        public const float DefaultMaxCellSize = 64f;

        /// <summary>The rectangle the cells actually occupy, centred within the area offered.</summary>
        public Rect Area { get; }
        public int Columns { get; }
        public int Rows { get; }
        public float CellSize { get; }

        /// <summary>
        /// The smallest cell either construction path yields, whatever it is
        /// offered or configured with: below one pixel a cell cannot be drawn,
        /// and a zero size would divide by zero in <see cref="TryPick"/>.
        /// </summary>
        private const float SmallestDrawableCellSize = 1f;

        public EditorGridLayout(Rect available, int columns, int rows, float maxCellSize = DefaultMaxCellSize)
            : this(
                Math.Max(columns, 1),
                Math.Max(rows, 1),
                Mathf.Min(
                    Mathf.Max(FitCellSize(available, Math.Max(columns, 1), Math.Max(rows, 1), 0f), SmallestDrawableCellSize),
                    maxCellSize),
                available)
        {
        }

        /// <summary>A grid of exactly <paramref name="cellSize"/> cells, centred in <paramref name="available"/>. Both public paths end here once they have chosen a size.</summary>
        private EditorGridLayout(int columns, int rows, float cellSize, Rect available)
        {
            Columns = columns;
            Rows = rows;
            CellSize = cellSize;

            var w = CellSize * Columns;
            var h = CellSize * Rows;
            Area = new Rect(
                available.x + ((available.width - w) * 0.5f),
                available.y + ((available.height - h) * 0.5f),
                w,
                h);
        }

        /// <summary>
        /// The main board's layout: the largest whole-pixel cell that fits
        /// <paramref name="canvas"/> with <see cref="GridFit.MarginCells"/> cells
        /// of clearance on every side, clamped to <paramref name="fit"/>'s
        /// minimum and maximum, and centred in the canvas. The margin is counted
        /// in cells rather than pixels because the edge markers that live in it
        /// are sized in cells — a pixel margin clips them once cells grow.
        /// Every hit test and drag reads the returned layout, so they cannot
        /// disagree with what is drawn. When the minimum wins, the grid is larger
        /// than the fit; a caller that sizes its canvas to
        /// <see cref="GridFit.RequiredCanvasSize"/> never sees that overflow.
        /// </summary>
        public static EditorGridLayout Fit(Rect canvas, int columns, int rows, GridFit fit)
        {
            var c = Math.Max(columns, 1);
            var r = Math.Max(rows, 1);
            var cell = FitCellSize(canvas, c, r, fit.MarginCells);
            cell = Mathf.Min(Mathf.Max(cell, fit.MinCellSize), fit.MaxCellSize);
            cell = Mathf.Max(cell, SmallestDrawableCellSize);
            return new EditorGridLayout(c, r, cell, canvas);
        }

        /// <summary>The largest whole-pixel cell for which <paramref name="columns"/> × <paramref name="rows"/> cells plus <paramref name="marginCells"/> on each side fit <paramref name="available"/>; unclamped.</summary>
        private static float FitCellSize(Rect available, int columns, int rows, float marginCells)
        {
            var margin = Mathf.Max(marginCells, 0f) * 2f;
            return Mathf.Floor(Mathf.Min(available.width / (columns + margin), available.height / (rows + margin)));
        }

        /// <summary>
        /// The screen rect of grid cell <paramref name="c"/>. The grid's origin
        /// is bottom-left (+Y up); GUI space is top-left (+Y down), so the row is
        /// flipped here and nowhere else.
        /// </summary>
        public Rect CellRect(Coord c)
        {
            var screenRow = Rows - 1 - c.Y;
            return new Rect(Area.x + (c.X * CellSize), Area.y + (screenRow * CellSize), CellSize, CellSize);
        }

        /// <summary>
        /// The grid cell under <paramref name="point"/>, extrapolated beyond the
        /// grid's own bounds rather than failing like <see cref="TryPick"/>. A
        /// drag can carry the pointer outside the grid while a grabbed block's
        /// footprint is still entirely legal (a grab offset near a block's far
        /// edge), so the candidate must keep tracking the pointer instead of
        /// clamping or freezing at the edge.
        /// </summary>
        public Coord CellAtUnclamped(Vector2 point)
        {
            var col = Mathf.FloorToInt((point.x - Area.x) / CellSize);
            var screenRow = Mathf.FloorToInt((point.y - Area.y) / CellSize);
            return new Coord(col, Rows - 1 - screenRow);
        }

        /// <summary>
        /// The cell space of a block whose origin is at grid cell
        /// <paramref name="origin"/>: where anything measured in cells from
        /// that origin lands on screen, clipped to the grid. The same row flip
        /// as <see cref="CellRect"/>, for positions between cells.
        /// </summary>
        public EditorCellSpace CellSpaceAt(Coord origin) =>
            new EditorCellSpace(
                new Vector2(Area.x + (origin.X * CellSize), Area.y + ((Rows - origin.Y) * CellSize)),
                CellSize,
                Area);

        public bool TryPick(Vector2 point, out Coord cell)
        {
            cell = default;
            if (!Area.Contains(point))
            {
                return false;
            }

            var col = Mathf.FloorToInt((point.x - Area.x) / CellSize);
            var screenRow = Mathf.FloorToInt((point.y - Area.y) / CellSize);
            if (col < 0 || col >= Columns || screenRow < 0 || screenRow >= Rows)
            {
                return false;
            }

            cell = new Coord(col, Rows - 1 - screenRow);
            return true;
        }
    }

    /// <summary>
    /// Where a block's own cell units land on screen: positions measured in
    /// cells from the block's origin, +Y up, as <c>Core</c> and the board's
    /// layout rules give them, to GUI pixels, +Y down. It carries positions
    /// between cells — an inset rectangle, a mark's centre — which
    /// <see cref="EditorGridLayout.CellRect"/>, whole cells only, cannot. One
    /// type serves the main grid, a wave's region and a shape preview, so a
    /// block's marks are drawn by one code path on all three.
    /// </summary>
    public readonly struct EditorCellSpace
    {
        private readonly Vector2 origin;

        /// <summary>A cell space.</summary>
        /// <param name="origin">The GUI position of the lower-left corner of cell (0, 0).</param>
        /// <param name="cellSize">Pixels per cell.</param>
        /// <param name="clip">The GUI rectangle drawing stays inside.</param>
        public EditorCellSpace(Vector2 origin, float cellSize, Rect clip)
        {
            this.origin = origin;
            CellSize = cellSize;
            Clip = clip;
        }

        /// <summary>Pixels per cell.</summary>
        public float CellSize { get; }

        /// <summary>The GUI rectangle drawing stays inside: the grid, so a block partly off it draws only its on-grid part.</summary>
        public Rect Clip { get; }

        /// <summary>The GUI position of <paramref name="cells"/>, a point in cell units.</summary>
        public Vector2 ToGui(Vector2 cells) =>
            new Vector2(origin.x + (cells.x * CellSize), origin.y - (cells.y * CellSize));

        /// <summary>
        /// The GUI rectangle of <paramref name="cells"/>, a rectangle in cell
        /// units, cut to <see cref="Clip"/> and then snapped to whole pixels;
        /// false when nothing of it is left inside.
        /// </summary>
        /// <remarks>
        /// Every edge is snapped by the one rule, <see cref="Mathf.Round(float)"/>,
        /// after the cut. <c>EditorGUI.DrawRect</c> rounds each rectangle on
        /// its own, so two rectangles sharing an edge at a fractional pixel —
        /// a half-cell seam on an odd cell size — could round apart and leave
        /// a one-pixel gap showing what is underneath. Snapped here, a shared
        /// edge is one value in and so one pixel out, for both rectangles.
        /// </remarks>
        public bool TryToGui(Rect cells, out Rect gui)
        {
            var xMin = Mathf.Round(Mathf.Max(origin.x + (cells.xMin * CellSize), Clip.xMin));
            var xMax = Mathf.Round(Mathf.Min(origin.x + (cells.xMax * CellSize), Clip.xMax));
            var yMin = Mathf.Round(Mathf.Max(origin.y - (cells.yMax * CellSize), Clip.yMin));
            var yMax = Mathf.Round(Mathf.Min(origin.y - (cells.yMin * CellSize), Clip.yMax));
            gui = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            return xMax > xMin && yMax > yMin;
        }
    }

    /// <summary>
    /// How the main board fits its canvas (<see cref="EditorGridLayout.Fit"/>):
    /// a clearance around the grid, in cells, and the range a cell's pixel size
    /// is clamped to. The values come from <see cref="LevelEditorSettings"/>;
    /// this type only carries them and answers how much canvas they need.
    /// </summary>
    public readonly struct GridFit
    {
        /// <summary>Clearance on each side of the grid, in cells — room for the edge markers, which are sized in cells.</summary>
        public float MarginCells { get; }
        public float MinCellSize { get; }
        public float MaxCellSize { get; }

        public GridFit(float marginCells, float minCellSize, float maxCellSize)
        {
            MarginCells = marginCells;
            MinCellSize = minCellSize;
            MaxCellSize = maxCellSize;
        }

        /// <summary>
        /// The smallest canvas that holds a <paramref name="columns"/> ×
        /// <paramref name="rows"/> grid and its margin at
        /// <see cref="MinCellSize"/>. A canvas requested at least this large
        /// never makes <see cref="EditorGridLayout.Fit"/> overflow it, so an
        /// enclosing scroll view scrolls instead of the grid drawing over
        /// whatever sits below it.
        /// </summary>
        public Vector2 RequiredCanvasSize(int columns, int rows)
        {
            var margin = Mathf.Max(MarginCells, 0f) * 2f;
            return new Vector2(
                (Math.Max(columns, 1) + margin) * MinCellSize,
                (Math.Max(rows, 1) + margin) * MinCellSize);
        }
    }

    /// <summary>
    /// Draws a grid described by a <see cref="EditorGridLayout"/>. Pure rendering: it is
    /// handed a per-cell fill colour and draws it, and it decides nothing about
    /// what a cell contains. Both the main board and a wave's region are drawn by
    /// the same calls.
    /// </summary>
    public static class EditorGrid
    {
        private static readonly Color LineColor = new Color(0f, 0f, 0f, 0.28f);

        /// <summary>
        /// How thick the rule between two cells is. Public because a caller that
        /// paints a grid line out again — the Level Editor does, where a seam
        /// falls inside one block — has to cover exactly what was drawn here.
        /// </summary>
        public const float LineThickness = 1f;

        /// <summary>The fixed cell size <see cref="DrawCellPreview"/> renders at — small on purpose, since a preview is read at a glance, not clicked precisely (the queue-entry free-draw grid is the exception; it reuses <see cref="DrawCells"/> instead, since it needs real click targets).</summary>
        public const float PreviewCellSize = 12f;

        /// <summary>
        /// Renders <paramref name="cells"/> as a miniature, non-interactive
        /// shape preview inside <paramref name="area"/>: a filled rect per
        /// occupied cell at <see cref="PreviewCellSize"/>, laid out from the
        /// shape's own bounding box and centred in <paramref name="area"/> —
        /// not from any grid origin, so the absolute coordinates of
        /// <paramref name="cells"/> do not matter, only their shape. Draws
        /// nothing for an empty list. No <c>LevelDraft</c> or selection
        /// knowledge — cells in, pixels out — so a shape preset's preview and a
        /// generator queue entry's current shape are both just a call here.
        /// </summary>
        public static void DrawCellPreview(Rect area, IReadOnlyList<Coord> cells, Color fill)
        {
            if (!TryPreviewCellSpace(area, cells, out var space))
            {
                return;
            }

            foreach (var c in cells)
            {
                if (space.TryToGui(new Rect(c.X, c.Y, 1f, 1f), out var rect))
                {
                    EditorGUI.DrawRect(rect, fill);
                }
            }
        }

        /// <summary>
        /// The cell space <see cref="DrawCellPreview"/> lays
        /// <paramref name="cells"/> out in: <see cref="PreviewCellSize"/>
        /// cells, the shape's bounding box centred in <paramref name="area"/>.
        /// A caller drawing more on a preview than its cells — a block's
        /// marks — draws through this, so it lands on the cells drawn. False
        /// for an empty shape, which draws nothing.
        /// </summary>
        public static bool TryPreviewCellSpace(Rect area, IReadOnlyList<Coord> cells, out EditorCellSpace space)
        {
            space = default;
            if (cells == null || cells.Count == 0)
            {
                return false;
            }

            var minX = cells[0].X;
            var maxX = cells[0].X;
            var minY = cells[0].Y;
            var maxY = cells[0].Y;
            for (var i = 1; i < cells.Count; i++)
            {
                var c = cells[i];
                if (c.X < minX) minX = c.X;
                if (c.X > maxX) maxX = c.X;
                if (c.Y < minY) minY = c.Y;
                if (c.Y > maxY) maxY = c.Y;
            }

            var w = (maxX - minX + 1) * PreviewCellSize;
            var h = (maxY - minY + 1) * PreviewCellSize;
            var originX = area.x + ((area.width - w) * 0.5f);
            var originY = area.y + ((area.height - h) * 0.5f);

            // +Y up in cell space; GUI space is +Y down — the same flip
            // EditorGridLayout.CellRect does. The bounding box's top-left is
            // (originX, originY), so cell (0, 0)'s lower-left corner sits minX
            // cells to its left and maxY + 1 cells below it. The clip is the
            // bounding box itself: a preview draws all of its shape.
            space = new EditorCellSpace(
                new Vector2(originX - (minX * PreviewCellSize), originY + ((maxY + 1) * PreviewCellSize)),
                PreviewCellSize,
                new Rect(originX, originY, w, h));
            return true;
        }

        public static void DrawCells(EditorGridLayout layout, Func<Coord, Color> fillOf)
        {
            for (var y = 0; y < layout.Rows; y++)
            {
                for (var x = 0; x < layout.Columns; x++)
                {
                    var cell = new Coord(x, y);
                    var rect = layout.CellRect(cell);
                    EditorGUI.DrawRect(rect, fillOf(cell));
                    EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, LineThickness), LineColor);
                    EditorGUI.DrawRect(new Rect(rect.x, rect.y, LineThickness, rect.height), LineColor);
                }
            }

            EditorGUI.DrawRect(
                new Rect(layout.Area.x, layout.Area.yMax - LineThickness, layout.Area.width, LineThickness), LineColor);
            EditorGUI.DrawRect(
                new Rect(layout.Area.xMax - LineThickness, layout.Area.y, LineThickness, layout.Area.height), LineColor);
        }

        public static void DrawOutline(Rect rect, Color color, float thickness = 2f)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        /// <summary>
        /// The screen rect of a marker hanging off <paramref name="edge"/> over
        /// <paramref name="cells"/> cells from <paramref name="offset"/>, sitting
        /// <paramref name="depth"/> pixels outside the grid — a gate bar or a
        /// generator's triangle bounds. The run is clamped to the edge so a gate
        /// or generator that does not fit still draws on-screen rather than as a
        /// garbage rect; matching that clamped marker to the invalid data is a
        /// warning's job, not the drawing's.
        /// </summary>
        public static Rect EdgeMarker(EditorGridLayout layout, BoardEdge edge, int offset, int cells, float depth)
        {
            var horizontal = edge == BoardEdge.Top || edge == BoardEdge.Bottom;
            var edgeLength = horizontal ? layout.Columns : layout.Rows;

            var start = Mathf.Clamp(offset, 0, Mathf.Max(0, edgeLength - 1));
            var run = Mathf.Clamp(cells, 1, edgeLength - start);
            var span = run * layout.CellSize;

            switch (edge)
            {
                case BoardEdge.Bottom:
                {
                    var c = layout.CellRect(new Coord(start, 0));
                    return new Rect(c.x, layout.Area.yMax, span, depth);
                }
                case BoardEdge.Top:
                {
                    var c = layout.CellRect(new Coord(start, layout.Rows - 1));
                    return new Rect(c.x, layout.Area.y - depth, span, depth);
                }
                case BoardEdge.Left:
                {
                    var c = layout.CellRect(new Coord(0, start + run - 1));
                    return new Rect(layout.Area.x - depth, c.y, depth, span);
                }
                default:
                {
                    var c = layout.CellRect(new Coord(layout.Columns - 1, start + run - 1));
                    return new Rect(layout.Area.xMax, c.y, depth, span);
                }
            }
        }
    }
}
