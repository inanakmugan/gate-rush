using System;
using GateRush.Runtime;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 41's <see cref="LevelResult"/>: a campaign win with a
    /// level to follow offers Next and its gold; the last campaign level
    /// offers the closing message instead of Next; neither offers Restart
    /// (D58); a win that could not be reported shows no gold; a free-play win
    /// offers Restart and Next only when a level follows, with no gold and no
    /// message; a loss offers Restart alone, and only a campaign loss costs
    /// a life (the Module 45 follow-up).
    /// </summary>
    public class LevelResultTests
    {
        [Test]
        public void CampaignWin_WithALevelToFollow_OffersNextAndTheGold()
        {
            var result = LevelResult.CampaignWin(50, true);

            Assert.IsTrue(result.IsWin);
            Assert.AreEqual(50, result.GoldEarned);
            Assert.IsTrue(result.HasNext);
            Assert.IsFalse(result.HasRestart, "a won campaign level is never replayed");
            Assert.IsFalse(result.HasClosingMessage);
            Assert.IsFalse(result.LostLife);
        }

        [Test]
        public void CampaignWin_LastLevel_OffersTheClosingMessageAndNoNext()
        {
            var result = LevelResult.CampaignWin(50, false);

            Assert.IsTrue(result.IsWin);
            Assert.AreEqual(50, result.GoldEarned);
            Assert.IsFalse(result.HasNext);
            Assert.IsFalse(result.HasRestart);
            Assert.IsTrue(result.HasClosingMessage);
        }

        [Test]
        public void CampaignWin_NotReported_ShowsNoGold()
        {
            var result = LevelResult.CampaignWin(null, true);

            Assert.IsNull(result.GoldEarned);
            Assert.IsTrue(result.HasNext);
        }

        [Test]
        public void CampaignWin_NegativeGold_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => LevelResult.CampaignWin(-1, true));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void FreePlayWin_AnyNext_OffersRestartAndNoGoldOrMessage(bool hasNext)
        {
            var result = LevelResult.FreePlayWin(hasNext);

            Assert.IsTrue(result.IsWin);
            Assert.IsNull(result.GoldEarned, "free play pays nothing");
            Assert.AreEqual(hasNext, result.HasNext);
            Assert.IsTrue(result.HasRestart);
            Assert.IsFalse(result.HasClosingMessage, "the closing message is the campaign's alone");
            Assert.IsFalse(result.LostLife);
        }

        [Test]
        public void CampaignLoss_LosesALifeAndOffersRestart()
        {
            var result = LevelResult.CampaignLoss();

            Assert.IsFalse(result.IsWin);
            Assert.IsNull(result.GoldEarned);
            Assert.IsFalse(result.HasNext);
            Assert.IsTrue(result.HasRestart);
            Assert.IsFalse(result.HasClosingMessage);
            Assert.IsTrue(result.LostLife, "a campaign loss costs a life (D57)");
        }

        [Test]
        public void FreePlayLoss_LosesNothingAndOffersRestart()
        {
            var result = LevelResult.FreePlayLoss();

            Assert.IsFalse(result.IsWin);
            Assert.IsNull(result.GoldEarned);
            Assert.IsFalse(result.HasNext);
            Assert.IsTrue(result.HasRestart);
            Assert.IsFalse(result.HasClosingMessage);
            Assert.IsFalse(result.LostLife, "free play costs no life (D58)");
        }
    }
}
