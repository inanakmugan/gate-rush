using System;
using GateRush.Runtime;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="DragSettings"/>: valid values are kept, and every
    /// out-of-range value is both reported by <see cref="DragSettings.Problems"/>
    /// under its field's name and refused by the constructor. Module 22 adds
    /// the gate pull's range, amount, capture range, follow rate, nudge bound
    /// and ease.
    /// </summary>
    public class DragSettingsTests
    {
        private const float Push = 0.3f;
        private const float Rate = 20f;
        private const float Assist = 0.3f;
        private const float Range = 0.8f;
        private const float Amount = 0.35f;
        private const float Capture = 0.6f;
        private const float PullRate = 25f;
        private const float Nudge = 0.15f;

        private static float Linear(float strength) => strength;

        [Test]
        public void Constructor_ValidValues_KeepsThem()
        {
            Func<float, float> ease = Linear;

            var settings = new DragSettings(Push, Rate, Assist, Range, Amount, Capture, PullRate, Nudge, ease);

            Assert.AreEqual(Push, settings.PushThresholdCells);
            Assert.AreEqual(Rate, settings.FollowRate);
            Assert.AreEqual(Assist, settings.CornerAssistCells);
            Assert.AreEqual(Range, settings.PullRangeCells);
            Assert.AreEqual(Amount, settings.PullAmount);
            Assert.AreEqual(Capture, settings.CaptureRangeCells);
            Assert.AreEqual(PullRate, settings.PullFollowRate);
            Assert.AreEqual(Nudge, settings.NudgeMaxCells);
            Assert.AreSame(ease, settings.PullEase);
        }

        [Test]
        public void Constructor_ZeroCornerAssist_IsValid()
        {
            var problems = DragSettings.Problems(Push, Rate, 0f, Range, Amount, Capture, PullRate, Nudge);

            Assert.IsEmpty(problems);
            Assert.DoesNotThrow(() => new DragSettings(Push, Rate, 0f, Range, Amount, Capture, PullRate, Nudge, Linear));
        }

        [Test]
        public void Constructor_CaptureRangeEqualToThePullRange_IsValid()
        {
            var problems = DragSettings.Problems(Push, Rate, Assist, Range, Amount, Range, PullRate, Nudge);

            Assert.IsEmpty(problems);
            Assert.DoesNotThrow(() => new DragSettings(Push, Rate, Assist, Range, Amount, Range, PullRate, Nudge, Linear));
        }

        [Test]
        public void Constructor_PullAmountOfOne_IsValid()
        {
            var problems = DragSettings.Problems(Push, Rate, Assist, Range, 1f, Capture, PullRate, Nudge);

            Assert.IsEmpty(problems);
            Assert.DoesNotThrow(() => new DragSettings(Push, Rate, Assist, Range, 1f, Capture, PullRate, Nudge, Linear));
        }

        [Test]
        public void Constructor_ZeroNudge_IsValid()
        {
            var problems = DragSettings.Problems(Push, Rate, Assist, Range, Amount, Capture, PullRate, 0f);

            Assert.IsEmpty(problems);
            Assert.DoesNotThrow(() => new DragSettings(Push, Rate, Assist, Range, Amount, Capture, PullRate, 0f, Linear));
        }

        [Test]
        public void Constructor_NullPullEase_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new DragSettings(Push, Rate, Assist, Range, Amount, Capture, PullRate, Nudge, null));
        }

        [TestCase(0f, Rate, Assist, Range, Amount, Capture, PullRate, Nudge, "Push Threshold Cells")]
        [TestCase(-0.1f, Rate, Assist, Range, Amount, Capture, PullRate, Nudge, "Push Threshold Cells")]
        [TestCase(float.NaN, Rate, Assist, Range, Amount, Capture, PullRate, Nudge, "Push Threshold Cells")]
        [TestCase(Push, 0f, Assist, Range, Amount, Capture, PullRate, Nudge, "Follow Rate")]
        [TestCase(Push, float.PositiveInfinity, Assist, Range, Amount, Capture, PullRate, Nudge, "Follow Rate")]
        [TestCase(Push, float.NaN, Assist, Range, Amount, Capture, PullRate, Nudge, "Follow Rate")]
        [TestCase(Push, Rate, -0.1f, Range, Amount, Capture, PullRate, Nudge, "Corner Assist Cells")]
        [TestCase(Push, Rate, DragSettings.MaxCornerAssistCellsExclusive, Range, Amount, Capture, PullRate, Nudge, "Corner Assist Cells")]
        [TestCase(Push, Rate, float.NaN, Range, Amount, Capture, PullRate, Nudge, "Corner Assist Cells")]
        [TestCase(Push, Rate, Assist, 0f, Amount, Capture, PullRate, Nudge, "Pull Range Cells")]
        [TestCase(Push, Rate, Assist, -0.1f, Amount, Capture, PullRate, Nudge, "Pull Range Cells")]
        [TestCase(Push, Rate, Assist, float.PositiveInfinity, Amount, Capture, PullRate, Nudge, "Pull Range Cells")]
        [TestCase(Push, Rate, Assist, float.NaN, Amount, Capture, PullRate, Nudge, "Pull Range Cells")]
        [TestCase(Push, Rate, Assist, Range, 0f, Capture, PullRate, Nudge, "Pull Amount")]
        [TestCase(Push, Rate, Assist, Range, 1.1f, Capture, PullRate, Nudge, "Pull Amount")]
        [TestCase(Push, Rate, Assist, Range, float.NaN, Capture, PullRate, Nudge, "Pull Amount")]
        [TestCase(Push, Rate, Assist, Range, Amount, 0f, PullRate, Nudge, "Capture Range Cells")]
        [TestCase(Push, Rate, Assist, Range, Amount, 0.9f, PullRate, Nudge, "Capture Range Cells")]
        [TestCase(Push, Rate, Assist, Range, Amount, float.PositiveInfinity, PullRate, Nudge, "Capture Range Cells")]
        [TestCase(Push, Rate, Assist, Range, Amount, float.NaN, PullRate, Nudge, "Capture Range Cells")]
        [TestCase(Push, Rate, Assist, Range, Amount, Capture, 0f, Nudge, "Pull Follow Rate")]
        [TestCase(Push, Rate, Assist, Range, Amount, Capture, float.PositiveInfinity, Nudge, "Pull Follow Rate")]
        [TestCase(Push, Rate, Assist, Range, Amount, Capture, float.NaN, Nudge, "Pull Follow Rate")]
        [TestCase(Push, Rate, Assist, Range, Amount, Capture, PullRate, -0.1f, "Nudge Max Cells")]
        [TestCase(Push, Rate, Assist, Range, Amount, Capture, PullRate, DragSettings.MaxNudgeCellsExclusive, "Nudge Max Cells")]
        [TestCase(Push, Rate, Assist, Range, Amount, Capture, PullRate, float.NaN, "Nudge Max Cells")]
        public void Problems_ValueOutOfRange_NamesTheFieldAndTheConstructorThrows(
            float push, float rate, float assist, float range, float amount, float capture, float pullRate, float nudge,
            string field)
        {
            var problems = DragSettings.Problems(push, rate, assist, range, amount, capture, pullRate, nudge);

            Assert.AreEqual(1, problems.Count, string.Join(" | ", problems));
            StringAssert.StartsWith(field, problems[0]);
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new DragSettings(push, rate, assist, range, amount, capture, pullRate, nudge, Linear));
        }
    }
}
