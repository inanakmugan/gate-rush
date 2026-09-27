using System.Collections.Generic;
using System.Linq;
using GateRush.Core;
using GateRush.Editor;
using GateRush.Serialization;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="LevelIdIndex"/>: which ids and board signatures the
    /// other level files carry, with the open file left out however its path is
    /// spelled — which also keeps the open file from matching its own board.
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

        [Test]
        public void Build_EmptyExcludePath_KeepsEveryFileWithoutThrowing()
        {
            // A script reload restores a null path field as "".
            var files = new[]
            {
                File("Assets/Resources/Levels/level-1.json", LevelJson(1)),
                File("Assets/Resources/Levels/level-3.json", LevelJson(3)),
            };

            var ids = LevelIdIndex.Build(files, "");

            Assert.AreEqual(2, ids.Count);
        }

        // -- BoardSignature --------------------------------------

        /// <summary>A one-block board with its gate, the block at <paramref name="blockOrigin"/>.</summary>
        private static LevelDraft Board(Coord blockOrigin, int levelId, int goldReward, int timeBudget)
        {
            var draft = LevelDraft.NewEmpty(3, 3);
            draft.LevelId = levelId;
            draft.GoldReward = goldReward;
            draft.SuggestedTimeBudgetSeconds = timeBudget;
            draft.Blocks.Add(new BlockDraft
            {
                Id = 1, Cells = { new Coord(0, 0) }, ColorStack = { BlockColor.Red }, StartOrigin = blockOrigin,
            });
            draft.Gates.Add(new GateDraft { Id = 1, Edge = BoardEdge.Bottom, Offset = 0, Width = 1, Color = BlockColor.Red });
            return draft;
        }

        [Test]
        public void BoardSignature_OnlyMetadataDiffers_Equal()
        {
            var a = Board(new Coord(0, 0), levelId: 2, goldReward: 10, timeBudget: 30);
            var b = Board(new Coord(0, 0), levelId: 3, goldReward: 25, timeBudget: 60);

            var signatureA = LevelIdIndex.BoardSignature(a.ToDto());
            var signatureB = LevelIdIndex.BoardSignature(b.ToDto());

            Assert.AreEqual(signatureA, signatureB);
        }

        [Test]
        public void BoardSignature_BoardDiffers_NotEqual()
        {
            var a = Board(new Coord(0, 0), levelId: 2, goldReward: 10, timeBudget: 30);
            var b = Board(new Coord(1, 1), levelId: 2, goldReward: 10, timeBudget: 30);

            var signatureA = LevelIdIndex.BoardSignature(a.ToDto());
            var signatureB = LevelIdIndex.BoardSignature(b.ToDto());

            Assert.AreNotEqual(signatureA, signatureB);
        }

        [Test]
        public void BoardSignature_AnyDto_LeavesTheDtoUnchanged()
        {
            var dto = Board(new Coord(0, 0), levelId: 2, goldReward: 10, timeBudget: 30).ToDto();
            var before = LevelSerializer.ToJson(dto);

            LevelIdIndex.BoardSignature(dto);

            Assert.AreEqual(before, LevelSerializer.ToJson(dto));
        }

        [Test]
        public void Build_FileSavedFromDraft_SignatureMatchesDraft()
        {
            // The file's signature is taken after a round trip through a draft;
            // it must equal the signature of the draft it was saved from.
            var draft = Board(new Coord(0, 0), levelId: 2, goldReward: 10, timeBudget: 30);
            var files = new[] { File("Assets/Resources/Levels/level-2.json", LevelSerializer.ToJson(draft.ToDto())) };

            var entries = LevelIdIndex.Build(files, null);

            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual(LevelIdIndex.BoardSignature(draft.ToDto()), entries[0].BoardSignature);
        }
    }
}
