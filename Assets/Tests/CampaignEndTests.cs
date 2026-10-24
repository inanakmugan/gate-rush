using System;
using GateRush.Meta;
using GateRush.Runtime;
using GateRush.Serialization;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 49's <see cref="CampaignEnd"/>: the campaign is over
    /// only past the last level of a usable order; only a campaign win that
    /// left it over finished it, so a free-play win of the last level or a
    /// loss does not; Restart plays the first level of the order in free
    /// play. The flow test runs Play and Restart on a finished campaign over
    /// a save with every field set: Play is over and starts nothing, Restart
    /// gives level 1 in free play, and the save is the same text afterwards,
    /// so it fails if Restart ever resets progress or starts a campaign
    /// attempt.
    /// </summary>
    /// <remarks>
    /// The catalog's ids are 0, 1, 3 and 4, given out of order, so the first
    /// level is the first by id, not the first given.
    /// </remarks>
    public class CampaignEndTests
    {
        private static readonly (string name, int levelId)[] Levels =
        {
            ("level-d", 4), ("level-a", 0), ("level-c", 3), ("level-b", 1)
        };

        private const string FirstLevel = "level-a";
        private const int LevelCount = 4;

        // The moment SaveFixture.EveryField's lives were saved at: read then,
        // no life comes back, so reading them changes nothing in the save.
        private static readonly DateTime LivesSavedAt = new DateTime(2026, 10, 15, 12, 34, 56, DateTimeKind.Utc);

        [Test]
        public void IsOver_PastTheLastLevel_IsTrue()
        {
            Assert.IsTrue(CampaignEnd.IsOver(Catalog(), LevelCount));
        }

        [Test]
        public void IsOver_AtTheLastLevel_IsFalse()
        {
            Assert.IsFalse(CampaignEnd.IsOver(Catalog(), LevelCount - 1), "the last level is still to be played");
        }

        [Test]
        public void IsOver_NoCatalog_IsFalse()
        {
            Assert.IsFalse(CampaignEnd.IsOver(null, LevelCount), "with no level order there is no campaign to have finished");
        }

        [Test]
        public void IsOver_CatalogWithNoLevels_IsFalse()
        {
            var empty = new LevelCatalog(Array.Empty<(string, int)>());

            Assert.IsFalse(CampaignEnd.IsOver(empty, 0));
        }

        [Test]
        public void IsOver_NegativePosition_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CampaignEnd.IsOver(Catalog(), -1));
        }

        [Test]
        public void IsJustFinished_CampaignWinThatLeftTheCampaignOver_IsTrue()
        {
            Assert.IsTrue(CampaignEnd.IsJustFinished(PlayMode.Campaign, LevelOutcome.Won, Catalog(), LevelCount));
        }

        [Test]
        public void IsJustFinished_CampaignWinWithLevelsLeft_IsFalse()
        {
            Assert.IsFalse(CampaignEnd.IsJustFinished(PlayMode.Campaign, LevelOutcome.Won, Catalog(), LevelCount - 1));
        }

        [Test]
        public void IsJustFinished_FreePlayWinWithTheCampaignOver_IsFalse()
        {
            // Level 1 or the last level replayed after the end: Menu opens a plain Home.
            Assert.IsFalse(CampaignEnd.IsJustFinished(PlayMode.FreePlay, LevelOutcome.Won, Catalog(), LevelCount));
        }

        [TestCase(LevelOutcome.Lost)]
        [TestCase(LevelOutcome.None)]
        public void IsJustFinished_CampaignLevelNotWon_IsFalse(LevelOutcome outcome)
        {
            Assert.IsFalse(CampaignEnd.IsJustFinished(PlayMode.Campaign, outcome, Catalog(), LevelCount));
        }

        [Test]
        public void TryGetRestartLevel_UsableOrder_IsTheFirstLevelInFreePlay()
        {
            var found = CampaignEnd.TryGetRestartLevel(Catalog(), out var name);

            Assert.IsTrue(found);
            Assert.AreEqual(FirstLevel, name);
            Assert.AreEqual(PlayMode.FreePlay, CampaignEnd.RestartMode, "never a campaign attempt (D58)");
        }

        [Test]
        public void TryGetRestartLevel_NoCatalog_IsFalse()
        {
            var found = CampaignEnd.TryGetRestartLevel(null, out var name);

            Assert.IsFalse(found);
            Assert.IsNull(name);
        }

        [Test]
        public void PlayThenRestart_CampaignFinished_StartsLevelOneInFreePlayAndLeavesTheSaveUnchanged()
        {
            var catalog = Catalog();
            var meta = FinishedCampaign(catalog);
            var codec = new JsonSaveCodec();
            var before = codec.Encode(meta.Save);

            var play = CampaignLevels.ChooseStart(catalog, meta, out var playName);
            var isRestartFound = CampaignEnd.TryGetRestartLevel(catalog, out var restartName);
            var playAfter = CampaignLevels.ChooseStart(catalog, meta, out _);

            Assert.AreEqual(CampaignStart.Over, play, "Play opens End of Content");
            Assert.IsNull(playName, "and starts nothing");
            Assert.IsTrue(isRestartFound);
            Assert.AreEqual(FirstLevel, restartName, "Restart plays level 1");
            Assert.AreEqual(PlayMode.FreePlay, CampaignEnd.RestartMode, "in free play, not as a campaign attempt");
            Assert.AreEqual(CampaignStart.Over, playAfter, "the campaign is still finished");
            Assert.AreEqual(before, codec.Encode(meta.Save), "completed levels, position, gold, lives, jokers, streak and profile unchanged");
            SaveFixture.AssertSameSave(FinishedSave(catalog), meta.Save);
        }

        [Test]
        public void Close_CampaignFinished_LeavesTheSaveUnchanged()
        {
            // Close is the front end's own: it reaches neither the bootstrap
            // nor the meta game, so Play afterwards finds the same save.
            var catalog = Catalog();
            var meta = FinishedCampaign(catalog);
            var before = new JsonSaveCodec().Encode(meta.Save);

            var playAfterClose = CampaignLevels.ChooseStart(catalog, meta, out _);

            Assert.AreEqual(CampaignStart.Over, playAfterClose);
            Assert.AreEqual(before, new JsonSaveCodec().Encode(meta.Save));
        }

        private static LevelCatalog Catalog() => new LevelCatalog(Levels);

        /// <summary>
        /// <see cref="SaveFixture.EveryField"/> with the campaign past the last
        /// level of <paramref name="catalog"/>: every other field away from
        /// its default, so a reset of any of them shows.
        /// </summary>
        private static SaveData FinishedSave(LevelCatalog catalog)
        {
            var every = SaveFixture.EveryField();
            return new SaveData(
                every.Completed,
                every.Sound,
                every.Wallet,
                every.Lives,
                every.Jokers,
                every.Streak,
                new CampaignProgress(catalog.Names.Count),
                every.Profile,
                every.Haptics);
        }

        private static MetaGame FinishedCampaign(LevelCatalog catalog) =>
            new MetaGame(FinishedSave(catalog), SaveFixture.Rules, new ManualClock(LivesSavedAt));
    }
}
