using GateRush.Runtime;
using GateRush.UI;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers the UI polish pass's <see cref="ResultPanelShapes"/>: a campaign
    /// win with gold or the closing message keeps the reward panel; a win with
    /// nothing in its middle — every free-play win, and a campaign win whose
    /// gold could not be reported — takes the compact panel; a loss takes the
    /// loss panel, and the lost-life panel when it cost a life.
    /// </summary>
    public class ResultPanelShapesTests
    {
        [Test]
        public void ShapeOf_CampaignWinWithGold_IsReward()
        {
            var shape = ResultPanelShapes.ShapeOf(LevelResult.CampaignWin(50, true));

            Assert.AreEqual(ResultPanelShape.Reward, shape);
        }

        [Test]
        public void ShapeOf_LastCampaignWinWithoutGold_IsRewardForTheClosingMessage()
        {
            var shape = ResultPanelShapes.ShapeOf(LevelResult.CampaignWin(null, false));

            Assert.AreEqual(ResultPanelShape.Reward, shape);
        }

        [Test]
        public void ShapeOf_CampaignWinNotReportedWithALevelToFollow_IsCompact()
        {
            var shape = ResultPanelShapes.ShapeOf(LevelResult.CampaignWin(null, true));

            Assert.AreEqual(ResultPanelShape.Compact, shape);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ShapeOf_FreePlayWin_IsCompact(bool hasNext)
        {
            var shape = ResultPanelShapes.ShapeOf(LevelResult.FreePlayWin(hasNext));

            Assert.AreEqual(ResultPanelShape.Compact, shape);
        }

        [Test]
        public void ShapeOf_CampaignLoss_IsLifeLost()
        {
            var shape = ResultPanelShapes.ShapeOf(LevelResult.CampaignLoss());

            Assert.AreEqual(ResultPanelShape.LifeLost, shape);
        }

        [Test]
        public void ShapeOf_FreePlayLoss_IsLoss()
        {
            var shape = ResultPanelShapes.ShapeOf(LevelResult.FreePlayLoss());

            Assert.AreEqual(ResultPanelShape.Loss, shape);
        }
    }
}
