using System;
using GateRush.Core;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="UnlockConditions.IsThresholdMet"/> — the one predicate
    /// behind M2 (gates), M3 (frozen blocks) and M5 (shutters). The two forms:
    /// the total-count form (<c>requiredColor</c> null) and the colour-bound
    /// form.
    /// </summary>
    public class UnlockConditionsTests
    {
        private static int[] NoClears() => new int[Enum.GetValues(typeof(BlockColor)).Length];

        [Test]
        public void IsThresholdMet_TotalForm_IsMetAtTheThresholdNotOneBefore()
        {
            var clears = NoClears();

            Assert.IsFalse(UnlockConditions.IsThresholdMet(2, clears, 3, null));
            Assert.IsTrue(UnlockConditions.IsThresholdMet(3, clears, 3, null));
            Assert.IsTrue(UnlockConditions.IsThresholdMet(4, clears, 3, null));
        }

        [Test]
        public void IsThresholdMet_ColourBoundForm_CountsOnlyItsOwnColour()
        {
            var clears = NoClears();
            clears[(int)BlockColor.Red] = 4;

            Assert.IsFalse(
                UnlockConditions.IsThresholdMet(4, clears, 3, BlockColor.Yellow),
                "four red clears must not satisfy a threshold of three yellow");
        }

        [Test]
        public void IsThresholdMet_ColourBoundForm_IsSatisfiedByItsColourAloneRegardlessOfOthers()
        {
            var clears = NoClears();
            clears[(int)BlockColor.Yellow] = 3;
            clears[(int)BlockColor.Red] = 0;

            Assert.IsTrue(
                UnlockConditions.IsThresholdMet(3, clears, 3, BlockColor.Yellow),
                "three yellow clears satisfy a threshold of three yellow no matter what else was cleared");
        }
    }
}
