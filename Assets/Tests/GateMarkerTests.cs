using GateRush.Editor;
using NUnit.Framework;
using UnityEngine;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="GateMarker"/>: only a gate with a positive clear-count
    /// threshold reads as gated and is filled with the fixed Frost colour,
    /// hiding its own; every other gate
    /// renders with its palette colour untouched.
    /// </summary>
    public class GateMarkerTests
    {
        private static readonly Color Base = new Color(0.29f, 0.49f, 0.88f);

        [Test]
        public void IsThresholdGated_Null_ReturnsFalse()
        {
            var gated = GateMarker.IsThresholdGated(null);

            Assert.IsFalse(gated);
        }

        [Test]
        public void IsThresholdGated_Zero_ReturnsFalse()
        {
            var gated = GateMarker.IsThresholdGated(0);

            Assert.IsFalse(gated);
        }

        [Test]
        public void IsThresholdGated_Negative_ReturnsFalse()
        {
            var gated = GateMarker.IsThresholdGated(-1);

            Assert.IsFalse(gated);
        }

        [Test]
        public void IsThresholdGated_Positive_ReturnsTrue()
        {
            var gated = GateMarker.IsThresholdGated(3);

            Assert.IsTrue(gated);
        }

        [Test]
        public void Fill_NullThreshold_ReturnsBaseColourUnchanged()
        {
            var fill = GateMarker.Fill(Base, null);

            Assert.AreEqual(Base, fill);
        }

        [Test]
        public void Fill_ZeroThreshold_ReturnsBaseColourUnchanged()
        {
            var fill = GateMarker.Fill(Base, 0);

            Assert.AreEqual(Base, fill);
        }

        [Test]
        public void Fill_PositiveThreshold_ReturnsFrostRegardlessOfBaseColour()
        {
            // Two very different bases, so the assertion cannot pass by one
            // base happening to sit near Frost.
            var other = new Color(0.88f, 0.29f, 0.29f);

            var fillFromBase = GateMarker.Fill(Base, 3);
            var fillFromOther = GateMarker.Fill(other, 3);

            Assert.AreEqual(GateMarker.Frost, fillFromBase);
            Assert.AreEqual(GateMarker.Frost, fillFromOther);
        }
    }
}
