using System;
using GateRush.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 17's <see cref="UiBuilder.SlicedPixelsPerUnitMultiplier"/>:
    /// a rounded box's slice border draws as wide as the corner asked for,
    /// capped at half the box's shorter side, the board's rule for sliced
    /// sprites. Module 19's <see cref="UiBuilder.TiledPixelsPerUnitMultiplier"/>:
    /// one tile of a tiled image covers the size asked for.
    /// </summary>
    public class UiBuilderTests
    {
        private const float Tolerance = 1e-4f;
        private const float BorderPixels = 17f;
        private const float SpritePixelsPerUnit = 128f;
        private const float ReferencePixelsPerUnit = 100f;

        [Test]
        public void SlicedPixelsPerUnitMultiplier_CornerFits_DrawsTheBorderAtTheCorner()
        {
            var multiplier = UiBuilder.SlicedPixelsPerUnitMultiplier(
                BorderPixels, SpritePixelsPerUnit, ReferencePixelsPerUnit, 30f, new Vector2(200f, 100f));

            Assert.AreEqual(30f, DrawnBorder(multiplier), Tolerance);
        }

        [Test]
        public void SlicedPixelsPerUnitMultiplier_CornerTooLarge_IsCappedAtHalfTheShorterSide()
        {
            // A pill: the corner asked for is larger than half its height.
            var multiplier = UiBuilder.SlicedPixelsPerUnitMultiplier(
                BorderPixels, SpritePixelsPerUnit, ReferencePixelsPerUnit, 60f, new Vector2(280f, 96f));

            Assert.AreEqual(48f, DrawnBorder(multiplier), Tolerance);
        }

        [Test]
        public void SlicedPixelsPerUnitMultiplier_NoCorner_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => UiBuilder.SlicedPixelsPerUnitMultiplier(
                BorderPixels, SpritePixelsPerUnit, ReferencePixelsPerUnit, 0f, new Vector2(200f, 100f)));
        }

        [Test]
        public void TiledPixelsPerUnitMultiplier_OneTile_CoversTheAskedSize()
        {
            const float tilePixels = 128f;
            const float tileUnits = 170f;

            var multiplier = UiBuilder.TiledPixelsPerUnitMultiplier(
                tilePixels, SpritePixelsPerUnit, ReferencePixelsPerUnit, tileUnits);

            // uGUI draws a sprite's pixels at reference / (sprite × multiplier) units each.
            var drawn = tilePixels * ReferencePixelsPerUnit / (SpritePixelsPerUnit * multiplier);
            Assert.AreEqual(tileUnits, drawn, Tolerance);
        }

        [Test]
        public void TiledPixelsPerUnitMultiplier_NoSize_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => UiBuilder.TiledPixelsPerUnitMultiplier(
                128f, SpritePixelsPerUnit, ReferencePixelsPerUnit, 0f));
        }

        /// <summary>How wide uGUI draws the border at <paramref name="multiplier"/>, in canvas units.</summary>
        private static float DrawnBorder(float multiplier) =>
            BorderPixels * ReferencePixelsPerUnit / (SpritePixelsPerUnit * multiplier);
    }
}
