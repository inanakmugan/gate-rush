using System;
using GateRush.Runtime;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 35's <see cref="FormatStrings.IsUsable"/>, the check every
    /// configuration runs on its formats: a format is usable when its
    /// placeholders are within the numbers it is given and its braces are
    /// balanced.
    /// </summary>
    public class FormatStringsTests
    {
        [TestCase("Level {0}", 1)]
        [TestCase("{1} {0}", 2)]
        [TestCase("{0}:{1:00}", 2)]
        [TestCase("Full", 0)]
        public void IsUsable_PlaceholdersWithinTheArguments_IsTrue(string format, int argumentCount)
        {
            Assert.IsTrue(FormatStrings.IsUsable(format, argumentCount));
        }

        [TestCase("{1}", 1)]
        [TestCase("{0}/{2}", 2)]
        public void IsUsable_PlaceholderBeyondTheArguments_IsFalse(string format, int argumentCount)
        {
            Assert.IsFalse(FormatStrings.IsUsable(format, argumentCount));
        }

        [Test]
        public void IsUsable_UnbalancedBrace_IsFalse()
        {
            Assert.IsFalse(FormatStrings.IsUsable("Gold {0", 1));
        }

        [Test]
        public void IsUsable_Null_IsFalse()
        {
            Assert.IsFalse(FormatStrings.IsUsable(null, 1));
        }

        [Test]
        public void IsUsable_NegativeArgumentCount_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => FormatStrings.IsUsable("{0}", -1));
        }
    }
}
