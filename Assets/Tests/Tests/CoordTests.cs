using GateRush.Core;
using NUnit.Framework;

namespace GateRush.Tests
{
    public class CoordTests
    {
        [Test]
        public void Equals_SameXY_ReturnsTrue()
        {
            var a = new Coord(2, 3);
            var b = new Coord(2, 3);

            Assert.IsTrue(a.Equals(b));
            Assert.IsTrue(a == b);
        }

        [Test]
        public void Equals_DifferentXY_ReturnsFalse()
        {
            var a = new Coord(2, 3);
            var b = new Coord(3, 2);

            Assert.IsFalse(a.Equals(b));
            Assert.IsTrue(a != b);
        }

        [Test]
        public void GetHashCode_EqualCoords_ReturnsSameHash()
        {
            var a = new Coord(5, -1);
            var b = new Coord(5, -1);

            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void OperatorPlus_AddsComponentwise()
        {
            var a = new Coord(1, 2);
            var b = new Coord(3, 4);

            var result = a + b;

            Assert.AreEqual(new Coord(4, 6), result);
        }

        [Test]
        public void OperatorMinus_SubtractsComponentwise()
        {
            var a = new Coord(5, 5);
            var b = new Coord(2, 1);

            var result = a - b;

            Assert.AreEqual(new Coord(3, 4), result);
        }
    }
}
