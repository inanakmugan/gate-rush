using GateRush.Core;
using GateRush.Editor;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 20's <see cref="EditorBlockLook"/>: a block that starts
    /// frozen shows as ice with its count and nothing of its colours; a
    /// layered block shows its second colour, and its depth only when deeper
    /// than two. "Frozen at level start" is Core's
    /// <see cref="UnlockConditions.IsThresholdMet"/> at zero clears.
    /// </summary>
    public class EditorBlockLookTests
    {
        private static readonly BlockColor[] OneColour = { BlockColor.Red };
        private static readonly BlockColor[] TwoColours = { BlockColor.Red, BlockColor.Blue };
        private static readonly BlockColor[] ThreeColours = { BlockColor.Red, BlockColor.Blue, BlockColor.Green };

        [Test]
        public void Of_Frozen_IsIceWithItsCountAndNoInset()
        {
            const int UnfreezeAt = 4;

            var look = EditorBlockLook.Of(ThreeColours, UnfreezeAt);

            Assert.IsTrue(look.IsFrozen);
            Assert.AreEqual(UnfreezeAt, look.Number, "the clears that unfreeze it, not its depth");
            Assert.IsNull(look.BeneathColor, "M3 hides a frozen block's colours");
        }

        [Test]
        public void Of_ThresholdZero_IsNotFrozen()
        {
            // The editor asks Core, so it agrees with the initial state and
            // the resolver on both sides of the line: a threshold already met
            // at zero clears never freezes the block, the first one above it
            // does.
            const int MetAtZeroClears = 0;
            const int FirstUnmet = 1;
            Assert.IsTrue(UnlockConditions.IsThresholdMet(0, new int[0], MetAtZeroClears, null), "Core: met at zero clears");
            Assert.IsFalse(UnlockConditions.IsThresholdMet(0, new int[0], FirstUnmet, null), "Core: unmet at zero clears");

            var met = EditorBlockLook.Of(TwoColours, MetAtZeroClears);
            var unmet = EditorBlockLook.Of(TwoColours, FirstUnmet);
            var none = EditorBlockLook.Of(TwoColours, null);

            Assert.IsFalse(met.IsFrozen);
            Assert.AreEqual(BlockColor.Blue, met.BeneathColor, "it shows as the layered block it is");
            Assert.IsNull(met.Number);
            Assert.IsTrue(unmet.IsFrozen);
            Assert.IsFalse(none.IsFrozen);
        }

        [Test]
        public void Of_TwoColours_ShowsTheSecondAndNoDepth()
        {
            var two = EditorBlockLook.Of(TwoColours, null);
            var one = EditorBlockLook.Of(OneColour, null);

            Assert.IsFalse(two.IsFrozen);
            Assert.AreEqual(BlockColor.Blue, two.BeneathColor);
            Assert.IsNull(two.Number, "the two colours shown already tell the depth");
            Assert.IsNull(one.BeneathColor, "a single colour has nothing beneath");
            Assert.IsNull(one.Number);
        }

        [Test]
        public void Of_ThreeColours_ShowsDepthThree()
        {
            var look = EditorBlockLook.Of(ThreeColours, null);

            Assert.AreEqual(BlockColor.Blue, look.BeneathColor);
            Assert.AreEqual(ThreeColours.Length, look.Number);
        }
    }
}
