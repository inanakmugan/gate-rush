using System;
using GateRush.Meta;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 31's <see cref="CampaignProgress"/>: a new player is at
    /// the first level, each advance moves on by one and stops at
    /// <see cref="int.MaxValue"/>, and a negative position throws.
    /// </summary>
    public class CampaignProgressTests
    {
        [Test]
        public void Starting_IsZero()
        {
            Assert.AreEqual(0, CampaignProgress.Starting().Position);
        }

        [Test]
        public void Advance_Any_RaisesByOne()
        {
            var campaign = new CampaignProgress(4);

            campaign.Advance();

            Assert.AreEqual(5, campaign.Position);
        }

        [Test]
        public void Advance_AtIntMax_StopsAtIntMax()
        {
            var campaign = new CampaignProgress(int.MaxValue);

            campaign.Advance();

            Assert.AreEqual(int.MaxValue, campaign.Position);
        }

        [Test]
        public void Constructor_Negative_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CampaignProgress(-1));
        }
    }
}
