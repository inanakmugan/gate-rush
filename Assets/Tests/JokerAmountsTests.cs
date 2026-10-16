using System;
using GateRush.Meta;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 31's <see cref="JokerAmounts"/>: each kind gives its own
    /// value, and an undefined kind throws.
    /// </summary>
    public class JokerAmountsTests
    {
        [Test]
        public void Of_EachKind_GivesItsValue()
        {
            var amounts = new JokerAmounts(5, 7, 9);

            Assert.AreEqual(5, amounts.Of(JokerKind.Clock));
            Assert.AreEqual(7, amounts.Of(JokerKind.Rocket));
            Assert.AreEqual(9, amounts.Of(JokerKind.Broom));
        }

        [Test]
        public void Of_UndefinedKind_Throws()
        {
            var amounts = new JokerAmounts(5, 7, 9);

            Assert.Throws<ArgumentOutOfRangeException>(() => amounts.Of((JokerKind)99));
        }
    }
}
