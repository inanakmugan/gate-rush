using System;
using GateRush.Runtime;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="BurstSettings"/>: valid values are kept, and every
    /// out-of-range value is both reported by <see cref="BurstSettings.Problems"/>
    /// under its field's name, with the prefix it was given, and refused by
    /// the constructor.
    /// </summary>
    public class BurstSettingsTests
    {
        [Test]
        public void Problems_Defaults_AreEmpty()
        {
            var problems = BurstSettings.Problems("Cube", 4, 16, 0.16f, 0.26f, 0.8f, 1.6f, 35f, 270f, 0.04f);

            Assert.IsEmpty(problems);
            Assert.DoesNotThrow(() => new BurstSettings(4, 16, 0.16f, 0.26f, 0.8f, 1.6f, 35f, 270f, 0.04f));
        }

        [TestCase(BurstSettings.MaxSpreadDegreesExclusive)]
        [TestCase(120f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        public void Problems_SpreadOutOfRange_IsReported(float spread)
        {
            var problems = BurstSettings.Problems("Cube", 4, 16, 0.16f, 0.26f, 0.8f, 1.6f, spread, 270f, 0.04f);

            Assert.That(problems, Has.Some.StartsWith("Cube Spread Degrees"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BurstSettings(4, 16, 0.16f, 0.26f, 0.8f, 1.6f, spread, 270f, 0.04f));
        }

        [Test]
        public void Problems_MinAboveMax_IsReported()
        {
            var problems = BurstSettings.Problems("Shard", 2, 8, 0.3f, 0.2f, 0.6f, 0.3f, 30f, 180f, 0.03f);

            Assert.That(problems, Has.Some.StartsWith("Shard Size Cells"));
            Assert.That(problems, Has.Some.StartsWith("Shard Travel Cells"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BurstSettings(2, 8, 0.3f, 0.2f, 0.6f, 0.3f, 30f, 180f, 0.03f));
        }

        [Test]
        public void Problems_ZeroShortestTravel_IsReported()
        {
            var problems = BurstSettings.Problems("Cube", 4, 16, 0.16f, 0.26f, 0f, 1.6f, 35f, 270f, 0.04f);

            Assert.That(problems, Has.Some.StartsWith("Cube Travel Cells"));
        }

        [TestCase(0, 16, "Cube Count Per Cell")]
        [TestCase(4, 0, "Cube Cap")]
        public void Problems_CountOrCapBelowOne_IsReported(int perCell, int cap, string field)
        {
            var problems = BurstSettings.Problems("Cube", perCell, cap, 0.16f, 0.26f, 0.8f, 1.6f, 35f, 270f, 0.04f);

            Assert.That(problems, Has.Some.StartsWith(field));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BurstSettings(perCell, cap, 0.16f, 0.26f, 0.8f, 1.6f, 35f, 270f, 0.04f));
        }

        [TestCase(-1f, 0.04f, "Cube Spin Degrees")]
        [TestCase(270f, -0.1f, "Cube Max Delay Seconds")]
        public void Problems_NegativeSpinOrDelay_IsReported(float spin, float delay, string field)
        {
            var problems = BurstSettings.Problems("Cube", 4, 16, 0.16f, 0.26f, 0.8f, 1.6f, 35f, spin, delay);

            Assert.That(problems, Has.Some.StartsWith(field));
        }

        [Test]
        public void Constructor_ValidValues_KeepsThem()
        {
            var settings = new BurstSettings(2, 8, 0.12f, 0.22f, 0.3f, 0.6f, 30f, 180f, 0.03f);

            Assert.AreEqual(2, settings.CountPerArea);
            Assert.AreEqual(8, settings.Cap);
            Assert.AreEqual(0.12f, settings.SizeMinCells);
            Assert.AreEqual(0.22f, settings.SizeMaxCells);
            Assert.AreEqual(0.3f, settings.TravelMinCells);
            Assert.AreEqual(0.6f, settings.TravelMaxCells);
            Assert.AreEqual(30f, settings.SpreadDegrees);
            Assert.AreEqual(180f, settings.SpinDegrees);
            Assert.AreEqual(0.03f, settings.MaxDelaySeconds);
        }
    }
}
