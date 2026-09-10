using GateRush.Editor;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="ProportionalSize"/> (docs/Modules/09a follow-up, item 4):
    /// the arithmetic that replaced a guessed pixel constant for the properties
    /// column's width and the warnings list's height.
    /// </summary>
    public class ProportionalSizeTests
    {
        [Test]
        public void Resolve_WideWindow_TheRatioWins()
        {
            var size = new ProportionalSize(ratio: 0.2f, floor: 300f);

            Assert.AreEqual(400f, size.Resolve(2000f)); // 2000 * 0.2 = 400 > 300
        }

        [Test]
        public void Resolve_NarrowWindow_TheFloorWins()
        {
            var size = new ProportionalSize(ratio: 0.2f, floor: 300f);

            Assert.AreEqual(300f, size.Resolve(1000f)); // 1000 * 0.2 = 200 < 300
        }

        [Test]
        public void Resolve_FloorExceedsTheWindowItself_StillReturnsTheFloor()
        {
            var size = new ProportionalSize(ratio: 0.2f, floor: 300f);

            Assert.AreEqual(300f, size.Resolve(100f)); // 100 * 0.2 = 20; the floor (300) exceeds the window (100) and still wins
        }
    }
}
