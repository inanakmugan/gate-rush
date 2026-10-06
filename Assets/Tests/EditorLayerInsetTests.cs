using System.Collections.Generic;
using System.Linq;
using GateRush.Core;
using GateRush.Editor;
using NUnit.Framework;
using UnityEngine;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 20's <see cref="EditorLayerInset"/>: the Level Editor's
    /// inset rectangles for a layered block form one region over its
    /// footprint, through an L's bend too, and a single-colour block gets
    /// none.
    /// </summary>
    public class EditorLayerInsetTests
    {
        private const float Inset = 0.2f;
        private const int TwoColours = 2;

        private static readonly Coord[] Square2x2 = { new Coord(0, 0), new Coord(1, 0), new Coord(0, 1), new Coord(1, 1) };
        private static readonly Coord[] LShape = { new Coord(0, 0), new Coord(1, 0), new Coord(0, 1) };

        [Test]
        public void Rects_TwoByTwoLayered_FormOneRegion()
        {
            var rects = EditorLayerInset.Rects(Square2x2, TwoColours, Inset);

            // One region, and exactly the footprint shrunk by the inset on
            // every outer side: every corner of that square is covered and
            // nothing reaches past it.
            Assert.AreEqual(Square2x2.Length * 4, rects.Count, "four quarters per cell");
            Assert.IsTrue(IsOneRegion(Square2x2, rects));
            Assert.AreEqual(Inset, rects.Min(r => r.xMin), 1e-5f);
            Assert.AreEqual(Inset, rects.Min(r => r.yMin), 1e-5f);
            Assert.AreEqual(2f - Inset, rects.Max(r => r.xMax), 1e-5f);
            Assert.AreEqual(2f - Inset, rects.Max(r => r.yMax), 1e-5f);
            Assert.IsTrue(rects.Any(r => r.Contains(new Vector2(1f, 1f))), "where the four cells meet");
        }

        [Test]
        public void Rects_LShapeLayered_AreConnectedThroughTheBend()
        {
            var rects = EditorLayerInset.Rects(LShape, TwoColours, Inset);

            Assert.IsTrue(IsOneRegion(LShape, rects));
            Assert.IsTrue(rects.Any(r => r.Contains(new Vector2(1f - Inset * 0.5f, 0.75f))), "beside the bend, toward the right arm");
            Assert.IsTrue(rects.Any(r => r.Contains(new Vector2(0.75f, 1f - Inset * 0.5f))), "beside the bend, toward the upper arm");
            Assert.IsFalse(
                rects.Any(r => r.Contains(new Vector2(1f - Inset * 0.5f, 1f - Inset * 0.5f))),
                "the bend's own corner stays in the outer colour");
        }

        [Test]
        public void Rects_SingleColour_IsEmpty()
        {
            var single = EditorLayerInset.Rects(Square2x2, 1, Inset);
            var layered = EditorLayerInset.Rects(Square2x2, TwoColours, Inset);

            Assert.IsEmpty(single);
            Assert.IsNotEmpty(layered, "the same footprint with a second colour gets its inset");
        }

        /// <summary>
        /// True when the covered points of a fine grid over the footprint form
        /// one 4-connected region: a flood fill from one covered point reaches
        /// every other.
        /// </summary>
        private static bool IsOneRegion(Coord[] cells, IReadOnlyList<Rect> rects)
        {
            const int PerCell = 20;
            var width = (cells.Max(c => c.X) + 1) * PerCell;
            var height = (cells.Max(c => c.Y) + 1) * PerCell;
            var covered = new bool[width, height];
            var total = 0;
            var start = (X: -1, Y: -1);

            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < height; y++)
                {
                    var point = new Vector2((x + 0.5f) / PerCell, (y + 0.5f) / PerCell);
                    covered[x, y] = rects.Any(r => r.Contains(point));
                    if (covered[x, y])
                    {
                        total++;
                        start = (x, y);
                    }
                }
            }

            Assert.Greater(total, 0, "the inset covers something");

            var reached = 0;
            var pending = new Stack<(int X, int Y)>();
            pending.Push(start);
            covered[start.X, start.Y] = false;
            while (pending.Count > 0)
            {
                var at = pending.Pop();
                reached++;
                foreach (var step in new[] { (X: 1, Y: 0), (X: -1, Y: 0), (X: 0, Y: 1), (X: 0, Y: -1) })
                {
                    var x = at.X + step.X;
                    var y = at.Y + step.Y;
                    if (x >= 0 && x < width && y >= 0 && y < height && covered[x, y])
                    {
                        covered[x, y] = false;
                        pending.Push((x, y));
                    }
                }
            }

            return reached == total;
        }
    }
}
