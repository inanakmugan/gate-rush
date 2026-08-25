using System;
using GateRush.Core;
using NUnit.Framework;

namespace GateRush.Tests
{
    public class ShutterDefinitionTests
    {
        [Test]
        public void Constructor_NegativeThreshold_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new ShutterDefinition(1, new Coord(0, 0), new Coord(1, 1), threshold: -1, requiredColor: null));
        }

        [Test]
        public void Constructor_ZeroThreshold_Succeeds()
        {
            var shutter = new ShutterDefinition(1, new Coord(0, 0), new Coord(1, 1), threshold: 0, requiredColor: null);

            Assert.AreEqual(0, shutter.Threshold);
        }
    }
}
