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
    /// Checks that a 1×1 block's grid of studs, with their crescents and
    /// shadows falling away from the light, sits inside its face (Module 46):
    /// clear of the outline at the top and the sides, of the lip at the
    /// bottom, and of every rounded corner — the bottom ones squashed by the
    /// lip — by at least <see cref="MinFacePixels"/> of face. The fit depends on two assets that cannot see each other — the
    /// <see cref="ArtRecipe"/> draws the studs, the gap and the outline, and
    /// <see cref="RuntimeConfig"/> sets the lip that shortens the face and
    /// raises the studs — so it is checked here, on the code defaults and on
    /// the project's assets, with the runtime's own face rectangles
    /// (<see cref="FaceShape"/>).
    /// </summary>
    /// <remarks>
    /// A quarter sprite is drawn from its cell's centre out, and its face's
    /// edge lies at the share <c>(cell/2 − gap/2 − outline) / (cell/2)</c> of
    /// the quarter's extent, wherever the runtime stretches that quarter. All
    /// lengths here are in cells.
    /// </remarks>
    public class StudFitTests
    {
        /// <summary>The face that must show between a stud, or its foot, and the face's edge.</summary>
        private const float MinFacePixels = 1f;

        private static readonly Coord[] Single = { new Coord(0, 0) };
        private static readonly Coord[] Square2 = { new Coord(0, 0), new Coord(1, 0), new Coord(0, 1), new Coord(1, 1) };

        [TestCase(false, TestName = "Studs_SingleCellAtTheCodeDefaults_SitInsideTheFace")]
        [TestCase(true, TestName = "Studs_SingleCellAtTheProjectsAssets_SitInsideTheFace")]
        public void Studs_SingleCell_SitInsideTheFace(bool fromAssets)
        {
            WithAssets(fromAssets, AssertStudsFit);
        }

        private static void AssertStudsFit(ArtRecipe recipe, RuntimeConfig config)
        {
            float cell = recipe.CellPixels;
            var quarter = cell / 2f;
            var faceShare = (quarter - recipe.BlockGapPixels / 2f - recipe.PieceOutlinePixels) / quarter;
            var lip = config.BlockLipCells;
            var margin = MinFacePixels / cell;

            // The face's edges, from the 1×1 block's own quarters as the board
            // poses them; each quarter's cell-centre side is its inner side.
            var tiles = BlockTiling.Compute(Single);
            Rect RectAt(Quarter corner)
            {
                foreach (var tile in tiles)
                {
                    if (tile.Corner == corner)
                    {
                        return FaceShape.QuarterRect(tile, 0f, lip);
                    }
                }

                Assert.Fail($"a single cell has a {corner} quarter");
                return default;
            }

            var topRight = RectAt(Quarter.TopRight);
            var bottomRight = RectAt(Quarter.BottomRight);
            var faceTop = topRight.yMin + faceShare * topRight.height;
            var faceRight = topRight.xMin + faceShare * topRight.width;
            var faceBottom = bottomRight.yMax - faceShare * bottomRight.height;

            var perSide = recipe.StudsPerCellSide;
            var rise = FaceShape.ContentRise(lip);
            var lowest = 0.5f / perSide + rise;
            var highest = (perSide - 0.5f) / perSide + rise;
            var outermost = (perSide - 0.5f) / perSide;
            var innermost = 0.5f / perSide;

            // A stud is a disc raised off its grid point, with its crescent
            // and shadow falling away from the light (StudReach).
            var raise = recipe.StudDiscRaisePixels / cell;
            var reach = StudReach.Of(recipe);
            Assert.GreaterOrEqual(faceTop - (highest + raise + reach.Up), margin, "face above the top studs");
            Assert.GreaterOrEqual(lowest + raise - reach.Down - faceBottom, margin, "face below the bottom studs, crescent and shadow");
            Assert.GreaterOrEqual(faceRight - (outermost + reach.Right), margin, "face right of the outer studs");
            Assert.GreaterOrEqual(innermost - reach.Left - (1f - faceRight), margin, "face left of the outer studs");

            // The rounded corners, the bottom ones squashed by the lip: every
            // stud's every circle stays inside each corner's arc.
            var discs = new List<Vector2>();
            for (var row = 0; row < perSide; row++)
            {
                for (var column = 0; column < perSide; column++)
                {
                    discs.Add(new Vector2((column + 0.5f) / perSide, (row + 0.5f) / perSide + rise + raise));
                }
            }

            var corners = new List<(QuarterTile, Rect)>();
            foreach (var tile in tiles)
            {
                corners.Add((tile, FaceShape.QuarterRect(tile, 0f, lip)));
            }

            AssertInsideRoundedCorners(recipe, corners, discs);
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
            LayerStuds.Centers(
                cells, config.LayerInnerInsetCells, config.BlockLipCells, config.LayerStudSpacingCells,
                config.LayerStudClearanceCells);

        /// <summary>
        /// Asserts every stud at <paramref name="centers"/>, with its foot,
        /// leaves <see cref="MinFacePixels"/> of the inner block's face on every
        /// side, and that none lies past a corner's rounding, where the
        /// straight edges would not bound it.
        /// </summary>
        private static void AssertInsideInnerFace(
            ArtRecipe recipe, RuntimeConfig config, Coord[] cells, IReadOnlyList<Vector2> centers)
        {
            float cell = recipe.CellPixels;
            var quarter = cell / 2f;
            var faceShare = (quarter - recipe.BlockGapPixels / 2f - recipe.PieceOutlinePixels) / quarter;
            var arcShare = (quarter - recipe.BlockGapPixels / 2f - recipe.CornerRadiusPixels) / quarter;
            var reach = StudReach.Of(recipe);
            var raise = recipe.StudDiscRaisePixels / cell;
            var margin = MinFacePixels / cell;

            MarkLayout.Bounds(cells, out var minX, out var maxX, out var minY, out var maxY);
            var corners = new[]
            {
                InnerQuarter(config, cells, new Coord(minX, minY), Quarter.BottomLeft),
                InnerQuarter(config, cells, new Coord(maxX, minY), Quarter.BottomRight),
                InnerQuarter(config, cells, new Coord(minX, maxY), Quarter.TopLeft),
                InnerQuarter(config, cells, new Coord(maxX, maxY), Quarter.TopRight),
            };

            var left = corners[0].Rect.xMax - faceShare * corners[0].Rect.width;
            var bottom = corners[0].Rect.yMax - faceShare * corners[0].Rect.height;
            var right = corners[3].Rect.xMin + faceShare * corners[3].Rect.width;
            var top = corners[3].Rect.yMin + faceShare * corners[3].Rect.height;

            foreach (var c in centers)
            {
                Assert.GreaterOrEqual(c.x - reach.Left - left, margin, $"face left of the stud at {c}");
                Assert.GreaterOrEqual(right - (c.x + reach.Right), margin, $"face right of the stud at {c}");
                Assert.GreaterOrEqual(top - (c.y + raise + reach.Up), margin, $"face above the stud at {c}");
                Assert.GreaterOrEqual(c.y + raise - reach.Down - bottom, margin, $"face below the stud's crescent and shadow at {c}");

                foreach (var (tile, rect) in corners)
                {
                    var arcX = tile.SignX > 0 ? rect.xMin + arcShare * rect.width : rect.xMax - arcShare * rect.width;
                    var arcY = tile.SignY > 0 ? rect.yMin + arcShare * rect.height : rect.yMax - arcShare * rect.height;
                    var isPastCorner = tile.SignX * (c.x - arcX) > 0f && tile.SignY * (c.y + raise - arcY) > 0f;
                    Assert.IsFalse(isPastCorner, $"the stud at {c} lies past the {tile.Corner} corner's rounding");
                }
            }
        }

        /// <summary>
        /// Asserts that every circle of every stud whose disc is centred at
        /// one of <paramref name="discs"/> — the disc, its crescent and its
        /// shadow — keeps <see cref="MinFacePixels"/> inside the face's arc at
        /// each of <paramref name="corners"/>, wherever it lies past that arc's
        /// centre on both axes. A corner quarter is posed over its rectangle
        /// from the cell's centre out, so its arc is an ellipse when the lip
        /// squashes it. Each circle is sampled every degree.
        /// </summary>
        private static void AssertInsideRoundedCorners(
            ArtRecipe recipe, IEnumerable<(QuarterTile Tile, Rect Rect)> corners, IReadOnlyList<Vector2> discs)
        {
            const int Samples = 360;
            float cell = recipe.CellPixels;
            var quarter = cell / 2f;
            var arcFromInner = quarter - recipe.BlockGapPixels / 2f - recipe.CornerRadiusPixels;
            var faceRadius = recipe.CornerRadiusPixels - recipe.PieceOutlinePixels;
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
                var radiusX = faceRadius * perPixelX - margin;
                var radiusY = faceRadius * perPixelY - margin;

                foreach (var disc in discs)
                {
                    foreach (var offset in reach.CircleOffsets)
                    {
                        for (var k = 0; k < Samples; k++)
                        {
                            var angle = 2.0 * System.Math.PI * k / Samples;
                            var x = disc.x + offset.x + reach.Radius * (float)System.Math.Cos(angle);
                            var y = disc.y + offset.y + reach.Radius * (float)System.Math.Sin(angle);
                            if (tile.SignX * (x - arcX) <= 0f || tile.SignY * (y - arcY) <= 0f)
                            {
                                continue;
                            }

                            var dx = (x - arcX) / radiusX;
                            var dy = (y - arcY) / radiusY;
                            Assert.LessOrEqual(dx * dx + dy * dy, 1f, $"({x}, {y}), of the stud at {disc}, inside the {tile.Corner} corner's arc");
                            checkedPoints++;
                        }
                    }
                }
            }

            Assert.Greater(checkedPoints, 0, "some stud reaches past a corner's arc centre, so the arcs were checked");
        }

        /// <summary>
        /// How far a stud reaches from its disc's centre, in cells: the disc's
        /// radius, and beyond it on the side away from the light the crescent
        /// and then the shadow, circles as large as the disc carried by the
        /// crescent's depth and the shadow's offset. The shadow's softness
        /// lies inside its outline and adds nothing.
        /// </summary>
        private readonly struct StudReach
        {
            private StudReach(float radius, Vector2 fall, Vector2[] circleOffsets)
            {
                Radius = radius;
                Up = radius + System.Math.Max(0f, fall.y);
                Down = radius + System.Math.Max(0f, -fall.y);
                Right = radius + System.Math.Max(0f, fall.x);
                Left = radius + System.Math.Max(0f, -fall.x);
                CircleOffsets = circleOffsets;
            }

            public float Radius { get; }
            public float Up { get; }
            public float Down { get; }
            public float Right { get; }
            public float Left { get; }

            /// <summary>The centres of the disc, the crescent and the shadow's two circles, from the disc's centre.</summary>
            public IReadOnlyList<Vector2> CircleOffsets { get; }

            public static StudReach Of(ArtRecipe recipe)
            {
                float cell = recipe.CellPixels;
                var radians = recipe.LightAngleDegrees * System.Math.PI / 180.0;
                var away = new Vector2((float)-System.Math.Cos(radians), (float)-System.Math.Sin(radians));
                var drop = recipe.StudFootDropPixels / cell;
                var shadow = recipe.StudShadowOffsetPixels / cell;
                var offsets = new[] { Vector2.zero, away * drop, away * shadow, away * (drop + shadow) };
                return new StudReach(recipe.StudDiameterPixels / 2f / cell, away * (drop + shadow), offsets);
            }
        }

        /// <summary>The inner block's quarter at <paramref name="corner"/> of <paramref name="cell"/>, posed as the board poses it.</summary>
        private static (QuarterTile Tile, Rect Rect) InnerQuarter(RuntimeConfig config, Coord[] cells, Coord cell, Quarter corner)
        {
            foreach (var tile in BlockTiling.Compute(cells))
            {
                if (tile.Cell == cell && tile.Corner == corner)
                {
                    return (tile, FaceShape.QuarterRect(tile, config.LayerInnerInsetCells, config.BlockLipCells));
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
