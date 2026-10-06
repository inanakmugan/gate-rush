using GateRush.Editor;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 21's <see cref="PlayableLevelFile"/>: the Level Editor
    /// can play only a level saved to a file in the folder the game loads
    /// levels from, and says why for any other.
    /// </summary>
    public class PlayableLevelFileTests
    {
        private const string LevelsFolder = "Assets/Resources/Levels";

        [Test]
        public void TryGetLevelName_NullPath_SaysNeverSaved()
        {
            var playable = PlayableLevelFile.TryGetLevelName(null, LevelsFolder, out var levelName, out var reason);

            Assert.IsFalse(playable);
            Assert.IsNull(levelName);
            StringAssert.Contains("never been saved", reason);
        }

        [Test]
        public void TryGetLevelName_OutsideLevelsFolder_SaysWhy()
        {
            var playable = PlayableLevelFile.TryGetLevelName(
                "Assets/Drafts/level-6.json", LevelsFolder, out var levelName, out var reason);

            Assert.IsFalse(playable);
            Assert.IsNull(levelName);
            StringAssert.Contains("level-6.json", reason);
            StringAssert.Contains(LevelsFolder, reason);
        }

        [Test]
        public void TryGetLevelName_FolderWhoseNameOnlyStartsTheSame_IsOutside()
        {
            var playable = PlayableLevelFile.TryGetLevelName(
                "Assets/Resources/LevelsOld/level-6.json", LevelsFolder, out _, out var reason);

            Assert.IsFalse(playable);
            Assert.IsNotNull(reason);
        }

        [Test]
        public void TryGetLevelName_InLevelsFolder_GivesFileNameWithoutExtension()
        {
            var playable = PlayableLevelFile.TryGetLevelName(
                "Assets/Resources/Levels/level-6.json", LevelsFolder, out var levelName, out var reason);

            Assert.IsTrue(playable);
            Assert.AreEqual("level-6", levelName);
            Assert.IsNull(reason);
        }

        [Test]
        public void TryGetLevelName_BackslashPath_IsStillInside()
        {
            // Directory.GetFiles on Windows returns a backslash before the file name.
            var playable = PlayableLevelFile.TryGetLevelName(
                "Assets/Resources/Levels\\level-6.json", LevelsFolder, out var levelName, out _);

            Assert.IsTrue(playable);
            Assert.AreEqual("level-6", levelName);
        }
    }
}
