using System;
using GateRush.Editor;
using GateRush.Meta;
using GateRush.Runtime;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 49's <see cref="DevCampaignMove"/>, the editor shortcut to
    /// the end of the campaign: the position it writes is the last level's
    /// index in the level order, by the order and not the ids; with no usable
    /// order there is none; and the save it writes changes the campaign
    /// position and nothing else.
    /// </summary>
    /// <remarks>
    /// The catalog's ids are 0, 1, 3 and 4, given out of order: the last level
    /// is the fourth by id, at position 3.
    /// </remarks>
    public class DevCampaignMoveTests
    {
        private static readonly (string name, int levelId)[] Levels =
        {
            ("level-d", 4), ("level-a", 0), ("level-c", 3), ("level-b", 1)
        };

        private const int LastPosition = 3;
        private const string LastLevel = "level-d";

        [Test]
        public void TryGetLastLevelPosition_UsableOrder_IsTheLastLevelsIndex()
        {
            var catalog = new LevelCatalog(Levels);

            var found = DevCampaignMove.TryGetLastLevelPosition(catalog, out var position);

            Assert.IsTrue(found);
            Assert.AreEqual(LastPosition, position);
            Assert.IsTrue(CampaignLevels.TryGetLevel(catalog, position, out var name), "the position names a level to play");
            Assert.AreEqual(LastLevel, name);
            Assert.IsFalse(CampaignEnd.IsOver(catalog, position), "the last level is still to be played");
            Assert.IsTrue(CampaignEnd.IsOver(catalog, position + 1), "and its win finishes the campaign");
        }

        [Test]
        public void TryGetLastLevelPosition_NoCatalog_IsFalse()
        {
            var found = DevCampaignMove.TryGetLastLevelPosition(null, out _);

            Assert.IsFalse(found);
        }

        [Test]
        public void TryGetLastLevelPosition_CatalogWithNoLevels_IsFalse()
        {
            var found = DevCampaignMove.TryGetLastLevelPosition(new LevelCatalog(Array.Empty<(string, int)>()), out _);

            Assert.IsFalse(found);
        }

        [Test]
        public void WithCampaignAt_SaveWithEveryField_ChangesThePositionAndNothingElse()
        {
            var save = SaveFixture.EveryField();
            var expected = SaveFixture.EveryField();

            var moved = DevCampaignMove.WithCampaignAt(save, LastPosition);

            Assert.AreEqual(LastPosition, moved.Campaign.Position);
            Assert.AreNotEqual(expected.Campaign.Position, LastPosition, "the fixture's position differs, so the move shows");
            AssertSameExceptCampaign(expected, moved);
        }

        [Test]
        public void WithCampaignAt_NoSave_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => DevCampaignMove.WithCampaignAt(null, LastPosition));
        }

        /// <summary>
        /// <see cref="SaveFixture.AssertSameSave"/> with <paramref name="actual"/>'s
        /// campaign taken as <paramref name="expected"/>'s, so every other
        /// section is compared field by field.
        /// </summary>
        private static void AssertSameExceptCampaign(SaveData expected, SaveData actual)
        {
            var sameCampaign = new SaveData(
                actual.Completed,
                actual.Sound,
                actual.Wallet,
                actual.Lives,
                actual.Jokers,
                actual.Streak,
                new CampaignProgress(expected.Campaign.Position),
                actual.Profile,
                actual.Haptics);
            SaveFixture.AssertSameSave(expected, sameCampaign);
        }
    }
}
