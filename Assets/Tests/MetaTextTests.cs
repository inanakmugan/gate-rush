using System;
using GateRush.Runtime;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="MetaText"/>: a wait reads as the whole seconds the
    /// countdown would show. The HUD's lives and gold moved to the level's
    /// interface in Module 40, and the result panel's gold line to
    /// <c>LevelPanelTexts</c> in Module 41.
    /// </summary>
    public class MetaTextTests
    {
        [Test]
        public void WholeSeconds_Wait_IsTheSecondTheTextShows()
        {
            var wait = TimeSpan.FromSeconds(59.2);

            var whole = MetaText.WholeSeconds(wait);

            Assert.AreEqual(TimeFormat.WholeSeconds((float)wait.TotalSeconds), whole);
            Assert.AreEqual(60, whole);
        }
    }
}
