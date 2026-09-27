using System.Collections.Generic;
using System.Linq;
using GateRush.Editor;
using GateRush.Serialization;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="LevelIdIndex"/>: which ids the other level files
    /// claim, with the open file left out however its path is spelled.
    /// </summary>
    public class LevelIdIndexTests
    {
        private static string LevelJson(int levelId) =>
            $"{{\"formatVersion\":{LevelSerializer.FormatVersion},\"levelId\":{levelId}}}";

        private static KeyValuePair<string, string> File(string path, string json) =>
            new KeyValuePair<string, string>(path, json);

        [Test]
        public void Build_ReadableFiles_CollectsEachFileNameAndId()
        {
            var files = new[]
            {
                File("Assets/Resources/Levels/level-1.json", LevelJson(1)),
                File("Assets/Resources/Levels/level-3.json", LevelJson(3)),
            };

            var ids = LevelIdIndex.Build(files, null);

            CollectionAssert.AreEqual(new[] { "level-1.json", "level-3.json" }, ids.Select(i => i.FileName).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 3 }, ids.Select(i => i.LevelId).ToArray());
        }

        [Test]
        public void Build_UnreadableFiles_SkippedWithoutThrowing()
        {
            var files = new[]
            {
                File("Assets/Resources/Levels/empty.json", ""),
                File("Assets/Resources/Levels/old.json", "{\"formatVersion\":1,\"levelId\":7}"),
                File("Assets/Resources/Levels/level-2.json", LevelJson(2)),
            };

            var ids = LevelIdIndex.Build(files, null);

            Assert.AreEqual(1, ids.Count);
            Assert.AreEqual("level-2.json", ids[0].FileName);
        }

        [Test]
        public void Build_ExcludedPathSpelledWithOtherSeparatorAndCase_LeftOut()
        {
            // Directory.GetFiles on Windows returns a backslash before the file
            // name; Unity's save panel returns forward slashes throughout.
            var files = new[]
            {
                File("Assets/Resources/Levels\\level-1.json", LevelJson(1)),
                File("Assets/Resources/Levels\\level-3.json", LevelJson(3)),
            };

            var ids = LevelIdIndex.Build(files, "Assets/Resources/levels/Level-1.json");

            Assert.AreEqual(1, ids.Count);
            Assert.AreEqual("level-3.json", ids[0].FileName);
        }

        [Test]
        public void Build_NullExcludePath_KeepsEveryFile()
        {
            var files = new[]
            {
                File("Assets/Resources/Levels/level-1.json", LevelJson(1)),
                File("Assets/Resources/Levels/level-3.json", LevelJson(3)),
            };

            var ids = LevelIdIndex.Build(files, null);

            Assert.AreEqual(2, ids.Count);
        }
    }
}
