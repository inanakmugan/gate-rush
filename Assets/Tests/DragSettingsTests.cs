using System;
using GateRush.Runtime;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="DragSettings"/>: valid values are kept, and every
    /// out-of-range value is both reported by <see cref="DragSettings.Problems"/>
    /// under its field's name and refused by the constructor.
    /// </summary>
    public class DragSettingsTests
    {
        private const float Push = 0.3f;
        private const float Rate = 20f;
        private const float Assist = 0.3f;

        [Test]
        public void Constructor_ValidValues_KeepsThem()
        {
            var settings = new DragSettings(Push, Rate, Assist);

            Assert.AreEqual(Push, settings.PushThresholdCells);
            Assert.AreEqual(Rate, settings.FollowRate);
            Assert.AreEqual(Assist, settings.CornerAssistCells);
        }

        [Test]
        public void Constructor_ZeroCornerAssist_IsValid()
        {
            var problems = DragSettings.Problems(Push, Rate, 0f);

            Assert.IsEmpty(problems);
            Assert.DoesNotThrow(() => new DragSettings(Push, Rate, 0f));
        }

        [TestCase(0f, Rate, Assist, "Push Threshold Cells")]
        [TestCase(-0.1f, Rate, Assist, "Push Threshold Cells")]
        [TestCase(float.NaN, Rate, Assist, "Push Threshold Cells")]
        [TestCase(Push, 0f, Assist, "Follow Rate")]
        [TestCase(Push, float.PositiveInfinity, Assist, "Follow Rate")]
        [TestCase(Push, float.NaN, Assist, "Follow Rate")]
        [TestCase(Push, Rate, -0.1f, "Corner Assist Cells")]
        [TestCase(Push, Rate, DragSettings.MaxCornerAssistCellsExclusive, "Corner Assist Cells")]
        [TestCase(Push, Rate, float.NaN, "Corner Assist Cells")]
        public void Problems_ValueOutOfRange_NamesTheFieldAndTheConstructorThrows(
            float push, float rate, float assist, string field)
        {
            var problems = DragSettings.Problems(push, rate, assist);

            Assert.AreEqual(1, problems.Count);
            StringAssert.StartsWith(field, problems[0]);
            Assert.Throws<ArgumentOutOfRangeException>(() => new DragSettings(push, rate, assist));
        }
    }
}
