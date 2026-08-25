using System;
using GateRush.Core;
using NUnit.Framework;

namespace GateRush.Tests
{
    public class GateDefinitionTests
    {
        [Test]
        public void Constructor_WidthLessThanOne_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new GateDefinition(1, BoardEdge.Top, offset: 0, width: 0, color: BlockColor.Red, openAtClearCount: null));
        }

        [Test]
        public void Constructor_WidthOfOne_Succeeds()
        {
            var gate = new GateDefinition(1, BoardEdge.Top, offset: 0, width: 1, color: BlockColor.Red, openAtClearCount: null);

            Assert.AreEqual(1, gate.Width);
        }
    }
}
