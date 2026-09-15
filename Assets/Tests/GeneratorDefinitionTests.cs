using System;
using GateRush.Core;
using NUnit.Framework;
using static GateRush.Tests.Fixture;

namespace GateRush.Tests
{
    public class GeneratorDefinitionTests
    {
        [Test]
        public void Constructor_WidthLessThanOne_Throws()
        {
            Assert.Throws<ArgumentException>(() => Spawner(1, BoardEdge.Top, offset: 0, width: 0));
        }

        [Test]
        public void Constructor_WidthAboveMaxWidth_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                Spawner(1, BoardEdge.Top, offset: 0, width: GeneratorDefinition.MaxWidth + 1));
        }

        [Test]
        public void Constructor_WidthOfOne_Succeeds()
        {
            var generator = Spawner(1, BoardEdge.Top, offset: 0, width: 1);

            Assert.AreEqual(1, generator.Width);
        }

        [Test]
        public void Constructor_WidthOfMaxWidth_Succeeds()
        {
            var generator = Spawner(1, BoardEdge.Top, offset: 0, width: GeneratorDefinition.MaxWidth);

            Assert.AreEqual(GeneratorDefinition.MaxWidth, generator.Width);
        }

        /// <summary>
        /// The cap is a rule of the game (M6, D34), not a tuning value — a level
        /// wider than this is a data error rather than a hard level. Pinned so
        /// that raising it is a deliberate edit to a stated rule and not a quiet
        /// side effect of some other change.
        /// </summary>
        [Test]
        public void MaxWidth_IsTwoCells()
        {
            Assert.AreEqual(2, GeneratorDefinition.MaxWidth);
        }
    }
}
