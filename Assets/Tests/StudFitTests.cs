using System.Collections.Generic;
using GateRush.Core;
using GateRush.Editor;
using GateRush.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GateRush.Tests
{
    /// <summary>
    /// Checks that a 1×1 block's grid of studs — each its contact shadow, to
    /// where it fades out, its wall and its top — sits inside its lit body
    /// (Module 46): clear of the body's edge on every side and of every
    /// rounded corner by at least <see cref="MinFacePixels"/>. The fit depends
    /// on two assets that cannot see each other — the <see cref="ArtRecipe"/>
    /// draws the studs, the gap and the corners, and <see cref="RuntimeConfig"/>
    /// sets a layered block's window — so it is checked here, on the code
    /// defaults and on the project's assets, with the runtime's own rectangles
    /// (<see cref="FaceShape"/>).
    /// </summary>
    /// <remarks>
    /// A quarter sprite is drawn from its cell's centre out, and the body's
    /// edge lies at the share <c>(cell/2 − gap/2) / (cell/2)</c> of the
    /// quarter's extent, wherever the runtime stretches that quarter. All
    /// lengths here are in cells.
    /// </remarks>
    public class StudFitTests
    {
        /// <summary>The body that must show between a stud and the body's edge.</summary>
        private const float MinFacePixels = 1f;

        private static readonly Coord[] Single = { new Coord(0, 0) };
        private static readonly Coord[] Square2 = { new Coord(0, 0), new Coord(1, 0), new Coord(0, 1), new Coord(1, 1) };

        [TestCase(false, TestName = "Studs_SingleCellAtTheCodeDefaults_SitInsideTheFace")]
        [TestCase(true, TestName = "Studs_SingleCellAtTheProjectsAssets_SitInsideTheFace")]
        public void Studs_SingleCell_SitInsideTheFace(bool fromAssets)
        {
            WithAssets(fromAssets, (recipe, config) => AssertStudsFit(recipe));
        }

        private static void AssertStudsFit(ArtRecipe recipe)
        {
            float cell = recipe.CellPixels;
            var quarter = cell / 2f;
            var bodyShare = (quarter - recipe.BlockGapPixels / 2f) / quarter;
            var margin = MinFacePixels / cell;

            // The body's edges, from the 1×1 block's own quarters as the board
            // poses them; each quarter's cell-centre side is its inner side.
            var tiles = BlockTiling.Compute(Single);
            var topRight = BlockTiling.QuarterRect(TileAt(tiles, Quarter.TopRight));
            var bottomLeft = BlockTiling.QuarterRect(TileAt(tiles, Quarter.BottomLeft));
            var top = topRight.yMin + bodyShare * topRight.height;
            var right = topRight.xMin + bodyShare * topRight.width;
            var bottom = bottomLeft.yMax - bodyShare * bottomLeft.height;
            var left = bottomLeft.xMax - bodyShare * bottomLeft.width;

            // The grid is Stud Spacing Pixels apart, centred on the cell.
            var perSide = recipe.StudsPerCellSide;
            var spacing = recipe.StudSpacingPixels / cell;
            var first = 0.5f - (perSide - 1) * spacing / 2f;
            var last = 1f - first;

            var reach = StudReach.Of(recipe);
            Assert.GreaterOrEqual(top - (last + reach.Up), margin, "body above the top studs");
            Assert.GreaterOrEqual(first - reach.Down - bottom, margin, "body below the bottom studs");
            Assert.GreaterOrEqual(right - (last + reach.Right), margin, "body right of the right-hand studs");
            Assert.GreaterOrEqual(first - reach.Left - left, margin, "body left of the left-hand studs");

            // The rounded corners: every stud's every circle stays inside each
            // corner's arc.
            var points = new List<Vector2>();
            for (var row = 0; row < perSide; row++)
            {
                for (var column = 0; column < perSide; column++)
                {
                    points.Add(new Vector2(first + column * spacing, first + row * spacing));
                }
            }

            var corners = new List<(QuarterTile, Rect)>();
            foreach (var tile in tiles)
            {
                corners.Add((tile, BlockTiling.QuarterRect(tile)));
            }

            AssertInsideRoundedCorners(recipe, corners, points);
        }

        [TestCase(false, TestName = "LayeredInnerStuds_TwoByTwoAtTheCodeDefaults_AreThreeByThreeInsideTheInnerFace")]
        [TestCase(true, TestName = "LayeredInnerStuds_TwoByTwoAtTheProjectsAssets_AreThreeByThreeInsideTheInnerFace")]
        public void LayeredInnerStuds_TwoByTwo_AreThreeByThreeInsideTheInnerFace(bool fromAssets)
        {
            WithAssets(fromAssets, (recipe, config) =>
            {
                var centers = InnerCenters(config, Square2);

                Assert.AreEqual(9, centers.Count, "3×3 inner studs");
                AssertInsideInnerFace(recipe, config, Square2, centers);
            });
        }

        [TestCase(false, TestName = "LayeredInnerStuds_SingleCellAtTheCodeDefaults_AreNone")]
        [TestCase(true, TestName = "LayeredInnerStuds_SingleCellAtTheProjectsAssets_AreNone")]
        public void LayeredInnerStuds_SingleCell_AreNone(bool fromAssets)
        {
            WithAssets(fromAssets, (recipe, config) => Assert.IsEmpty(InnerCenters(config, Single), "a plain square"));
        }

        private static IReadOnlyList<Vector2> InnerCenters(RuntimeConfig config, Coord[] cells) =>
            LayerStuds.Centers(cells, config.LayerInnerInsetCells, config.LayerStudSpacingCells, config.LayerStudClearanceCells);

        /// <summary>
        /// Asserts every stud at <paramref name="centers"/> leaves
        /// <see cref="MinFacePixels"/> of the inner block's body on every
        /// side, and that none lies past a corner's rounding, where the
        /// straight edges would not bound it.
        /// </summary>
        private static void AssertInsideInnerFace(
            ArtRecipe recipe, RuntimeConfig config, Coord[] cells, IReadOnlyList<Vector2> centers)
        {
            float cell = recipe.CellPixels;
            var quarter = cell / 2f;
            var bodyShare = (quarter - recipe.BlockGapPixels / 2f) / quarter;
            var arcShare = (quarter - recipe.BlockGapPixels / 2f - recipe.CornerRadiusPixels) / quarter;
            var reach = StudReach.Of(recipe);
            var margin = MinFacePixels / cell;

            MarkLayout.Bounds(cells, out var minX, out var maxX, out var minY, out var maxY);
            var corners = new[]
            {
                InnerQuarter(config, cells, new Coord(minX, minY), Quarter.BottomLeft),
                InnerQuarter(config, cells, new Coord(maxX, minY), Quarter.BottomRight),
                InnerQuarter(config, cells, new Coord(minX, maxY), Quarter.TopLeft),
                InnerQuarter(config, cells, new Coord(maxX, maxY), Quarter.TopRight),
            };

            var left = corners[0].Rect.xMax - bodyShare * corners[0].Rect.width;
            var bottom = corners[0].Rect.yMax - bodyShare * corners[0].Rect.height;
            var right = corners[3].Rect.xMin + bodyShare * corners[3].Rect.width;
            var top = corners[3].Rect.yMin + bodyShare * corners[3].Rect.height;

            foreach (var c in centers)
            {
                Assert.GreaterOrEqual(c.x - reach.Left - left, margin, $"body left of the stud at {c}");
                Assert.GreaterOrEqual(right - (c.x + reach.Right), margin, $"body right of the stud at {c}");
                Assert.GreaterOrEqual(top - (c.y + reach.Up), margin, $"body above the stud at {c}");
                Assert.GreaterOrEqual(c.y - reach.Down - bottom, margin, $"body below the stud at {c}");

                foreach (var (tile, rect) in corners)
                {
                    var arcX = tile.SignX > 0 ? rect.xMin + arcShare * rect.width : rect.xMax - arcShare * rect.width;
                    var arcY = tile.SignY > 0 ? rect.yMin + arcShare * rect.height : rect.yMax - arcShare * rect.height;
                    var isPastCorner = tile.SignX * (c.x - arcX) > 0f && tile.SignY * (c.y - arcY) > 0f;
                    Assert.IsFalse(isPastCorner, $"the stud at {c} lies past the {tile.Corner} corner's rounding");
                }
            }
        }

        /// <summary>
        /// Asserts that every circle of every stud at one of
        /// <paramref name="points"/> — its contact shadow, its wall and its
        /// top — keeps <see cref="MinFacePixels"/> inside the body's arc at
        /// each of <paramref name="corners"/>, wherever it lies past that arc's
        /// centre on both axes. A corner quarter is posed over its rectangle
        /// from the cell's centre out, so its arc is an ellipse when the
        /// rectangle is not square. Each circle is sampled every degree.
        /// </summary>
        private static void AssertInsideRoundedCorners(
            ArtRecipe recipe, IEnumerable<(QuarterTile Tile, Rect Rect)> corners, IReadOnlyList<Vector2> points)
        {
            const int Samples = 360;
            float cell = recipe.CellPixels;
            var quarter = cell / 2f;
            var arcFromInner = quarter - recipe.BlockGapPixels / 2f - recipe.CornerRadiusPixels;
            var margin = MinFacePixels / cell;
            var reach = StudReach.Of(recipe);
            var checkedPoints = 0;

            foreach (var (tile, rect) in corners)
            {
                if (tile.Piece != QuarterPiece.OuterCorner)
                {
                    continue;
                }

                // Board cells per sprite pixel, along each axis.
                var perPixelX = rect.width / quarter;
                var perPixelY = rect.height / quarter;
                var arcX = (tile.SignX > 0 ? rect.xMin : rect.xMax) + tile.SignX * arcFromInner * perPixelX;
                var arcY = (tile.SignY > 0 ? rect.yMin : rect.yMax) + tile.SignY * arcFromInner * perPixelY;
                var radiusX = recipe.CornerRadiusPixels * perPixelX - margin;
                var radiusY = recipe.CornerRadiusPixels * perPixelY - margin;

                foreach (var point in points)
                {
                    foreach (var (offset, radius) in reach.Circles)
                    {
                        for (var k = 0; k < Samples; k++)
                        {
                            var angle = 2.0 * System.Math.PI * k / Samples;
                            var x = point.x + offset.x + radius * (float)System.Math.Cos(angle);
                            var y = point.y + offset.y + radius * (float)System.Math.Sin(angle);
                            if (tile.SignX * (x - arcX) <= 0f || tile.SignY * (y - arcY) <= 0f)
                            {
                                continue;
                            }

                            var dx = (x - arcX) / radiusX;
                            var dy = (y - arcY) / radiusY;
                            Assert.LessOrEqual(dx * dx + dy * dy, 1f, $"({x}, {y}), of the stud at {point}, inside the {tile.Corner} corner's arc");
                            checkedPoints++;
                        }
                    }
                }
            }

            Assert.Greater(checkedPoints, 0, "some stud reaches past a corner's arc centre, so the arcs were checked");
        }

        /// <summary>
        /// The circles a stud covers, from its grid point, in cells: its
        /// contact shadow out to where it fades, its wall and its top, each
        /// with its offset; and how far they reach along each axis.
        /// </summary>
        private readonly struct StudReach
        {
            private StudReach((Vector2 Offset, float Radius)[] circles)
            {
                Circles = circles;
                float up = 0f, down = 0f, right = 0f, left = 0f;
                foreach (var (offset, radius) in circles)
                {
                    up = Mathf.Max(up, offset.y + radius);
                    down = Mathf.Max(down, radius - offset.y);
                    right = Mathf.Max(right, offset.x + radius);
                    left = Mathf.Max(left, radius - offset.x);
                }

                Up = up;
                Down = down;
                Right = right;
                Left = left;
            }

            public IReadOnlyList<(Vector2 Offset, float Radius)> Circles { get; }
            public float Up { get; }
            public float Down { get; }
            public float Right { get; }
            public float Left { get; }

            public static StudReach Of(ArtRecipe recipe)
            {
                float cell = recipe.CellPixels;
                var radius = recipe.StudDiameterPixels / 2f;
                var disc = (radius + recipe.StudEdgePixels) / cell;
                return new StudReach(new[]
                {
                    (recipe.StudContactOffsetPixels / cell, (radius + recipe.StudContactOuterPixels) / cell),
                    (recipe.StudWallOffsetPixels / cell, disc),
                    (recipe.StudTopOffsetPixels / cell, disc),
                });
            }
        }

        private static QuarterTile TileAt(IReadOnlyList<QuarterTile> tiles, Quarter corner)
        {
            foreach (var tile in tiles)
            {
                if (tile.Corner == corner)
                {
                    return tile;
                }
            }

            Assert.Fail($"the footprint has a {corner} quarter");
            return default;
        }

        /// <summary>The inner block's quarter at <paramref name="corner"/> of <paramref name="cell"/>, posed as the board poses it.</summary>
        private static (QuarterTile Tile, Rect Rect) InnerQuarter(RuntimeConfig config, Coord[] cells, Coord cell, Quarter corner)
        {
            foreach (var tile in BlockTiling.Compute(cells))
            {
                if (tile.Cell == cell && tile.Corner == corner)
                {
                    return (tile, FaceShape.QuarterRect(tile, config.LayerInnerInsetCells));
                }
            }

            Assert.Fail($"the footprint has a {corner} quarter at {cell}");
            return default;
        }

        /// <summary>
        /// Runs <paramref name="check"/> on the project's two assets, or on
        /// fresh instances at their code defaults, destroyed afterwards.
        /// </summary>
        private static void WithAssets(bool fromAssets, System.Action<ArtRecipe, RuntimeConfig> check)
        {
            var recipe = fromAssets ? LoadOnly<ArtRecipe>() : ScriptableObject.CreateInstance<ArtRecipe>();
            var config = fromAssets ? LoadOnly<RuntimeConfig>() : ScriptableObject.CreateInstance<RuntimeConfig>();
            try
            {
                check(recipe, config);
            }
            finally
            {
                if (!fromAssets)
                {
                    Object.DestroyImmediate(recipe);
                    Object.DestroyImmediate(config);
                }
            }
        }

        /// <summary>The project's only asset of type <typeparamref name="T"/>.</summary>
        private static T LoadOnly<T>()
            where T : Object
        {
            var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            Assert.AreEqual(1, guids.Length, $"the project has one {typeof(T).Name}");
            return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }
}
