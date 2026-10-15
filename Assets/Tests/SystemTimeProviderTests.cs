using System;
using GateRush.Runtime;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 30's <see cref="SystemTimeProvider"/>: it answers in UTC,
    /// as every member of <c>GateRush.Meta.Lives</c> requires.
    /// </summary>
    public class SystemTimeProviderTests
    {
        [Test]
        public void UtcNow_Always_IsUtcKind()
        {
            var now = new SystemTimeProvider().UtcNow;

            Assert.AreEqual(DateTimeKind.Utc, now.Kind);
        }
    }
}
