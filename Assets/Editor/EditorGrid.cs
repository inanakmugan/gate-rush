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

        public EditorGridLayout(Rect available, int columns, int rows, float maxCellSize = DefaultMaxCellSize)
        {
            Columns = Math.Max(columns, 1);
            Rows = Math.Max(rows, 1);

            var fit = Mathf.Floor(Mathf.Min(available.width / Columns, available.height / Rows));
            CellSize = Mathf.Min(fit < 1f ? 1f : fit, maxCellSize);

            var w = CellSize * Columns;
            var h = CellSize * Rows;
            Area = new Rect(
                available.x + ((available.width - w) * 0.5f),
                available.y + ((available.height - h) * 0.5f),
                w,
                h);
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
    /// Draws a grid described by a <see cref="EditorGridLayout"/>. Pure rendering: it is
    /// handed a per-cell fill colour and draws it, and it decides nothing about
    /// what a cell contains. Both the main board and a wave's region are drawn by
    /// the same calls.
    /// </summary>
    public static class EditorGrid
    {
        private static readonly Color LineColor = new Color(0f, 0f, 0f, 0.28f);

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
            if (cells == null || cells.Count == 0)
            {
                return;
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

            foreach (var c in cells)
            {
                var col = c.X - minX;
                var row = maxY - c.Y; // +Y up in cell space; GUI space is +Y down — same flip EditorGridLayout.CellRect does
                var rect = new Rect(originX + (col * PreviewCellSize), originY + (row * PreviewCellSize), PreviewCellSize, PreviewCellSize);
                EditorGUI.DrawRect(rect, fill);
            }
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
                    EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), LineColor);
                    EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), LineColor);
                }
            }

            EditorGUI.DrawRect(new Rect(layout.Area.x, layout.Area.yMax - 1f, layout.Area.width, 1f), LineColor);
            EditorGUI.DrawRect(new Rect(layout.Area.xMax - 1f, layout.Area.y, 1f, layout.Area.height), LineColor);
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
