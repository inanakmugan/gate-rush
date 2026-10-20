using GateRush.UI;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 39's <see cref="LeaderboardScroll"/>: the player's row
    /// is centred in the viewport when it can be, and the offset stays
    /// between the list's top and the furthest it scrolls.
    /// </summary>
    public class LeaderboardScrollTests
    {
        private const float RowHeight = 100f;
        private const float ContentHeight = 2000f;
        private const float ViewportHeight = 600f;

        [Test]
        public void CenteredOffset_RowInTheMiddle_CentresIt()
        {
            var offset = LeaderboardScroll.CenteredOffset(1000f, RowHeight, ContentHeight, ViewportHeight);

            // The row's centre, 1050 down, at the viewport's centre, 300 down.
            Assert.AreEqual(750f, offset);
        }

        [Test]
        public void CenteredOffset_RowNearTheTop_StaysAtTheTop()
        {
            var offset = LeaderboardScroll.CenteredOffset(100f, RowHeight, ContentHeight, ViewportHeight);

            Assert.AreEqual(0f, offset);
        }

        [Test]
        public void CenteredOffset_RowNearTheBottom_StopsAtTheEnd()
        {
            var offset = LeaderboardScroll.CenteredOffset(1900f, RowHeight, ContentHeight, ViewportHeight);

            Assert.AreEqual(ContentHeight - ViewportHeight, offset);
        }

        [Test]
        public void CenteredOffset_ContentShorterThanViewport_DoesNotScroll()
        {
            var offset = LeaderboardScroll.CenteredOffset(400f, RowHeight, 500f, ViewportHeight);

            Assert.AreEqual(0f, offset);
        }
    }
}
