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
    /// Checks that a 1×1 block's grid of studs, with their feet, sits inside
    /// its face (Module 46): clear of the outline at the top and the sides and
    /// of the lip at the bottom, by at least <see cref="MinFacePixels"/> of
    /// face. The fit depends on two assets that cannot see each other — the
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
            var radius = recipe.StudDiameterPixels / 2f / cell;
            var drop = recipe.StudFootDropPixels / cell;
            var rise = FaceShape.ContentRise(lip);
            var lowest = 0.5f / perSide + rise;
            var highest = (perSide - 0.5f) / perSide + rise;
            var outermost = (perSide - 0.5f) / perSide;

            // A stud is a disc raised off its grid point over a crescent
            // hanging the rest of its depth below it.
            var raise = recipe.StudDiscRaisePixels / cell;
            Assert.GreaterOrEqual(faceTop - (highest + raise + radius), margin, "face above the top studs");
            Assert.GreaterOrEqual(lowest + raise - drop - radius - faceBottom, margin, "face below the bottom studs' crescents");
            Assert.GreaterOrEqual(faceRight - (outermost + radius), margin, "face beside the outer studs");

            // A face's rounded corner only reaches a stud whose centre lies
            // past the corner's centre on both axes; the outer studs' column
            // must not, or the bottom corner, squashed by the lip, would need
            // its own check.
            var cornerCenter = 0.5f + (quarter - recipe.BlockGapPixels / 2f - recipe.CornerRadiusPixels) / cell;
            Assert.LessOrEqual(outermost, cornerCenter, "the outer studs stay beside the face's rounded corners");
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
            var radius = recipe.StudDiameterPixels / 2f / cell;
            var drop = recipe.StudFootDropPixels / cell;
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
                Assert.GreaterOrEqual(c.x - radius - left, margin, $"face left of the stud at {c}");
                Assert.GreaterOrEqual(right - (c.x + radius), margin, $"face right of the stud at {c}");
                Assert.GreaterOrEqual(top - (c.y + raise + radius), margin, $"face above the stud at {c}");
                Assert.GreaterOrEqual(c.y + raise - drop - radius - bottom, margin, $"face below the stud's crescent at {c}");

                foreach (var (tile, rect) in corners)
                {
                    var arcX = tile.SignX > 0 ? rect.xMin + arcShare * rect.width : rect.xMax - arcShare * rect.width;
                    var arcY = tile.SignY > 0 ? rect.yMin + arcShare * rect.height : rect.yMax - arcShare * rect.height;
                    var isPastCorner = tile.SignX * (c.x - arcX) > 0f && tile.SignY * (c.y + raise - arcY) > 0f;
                    Assert.IsFalse(isPastCorner, $"the stud at {c} lies past the {tile.Corner} corner's rounding");
                }
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
