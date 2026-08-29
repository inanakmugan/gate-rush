using System.Linq;
using GateRush.Core;
using GateRush.Editor;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="LevelDraft.PreviewResize"/> and
    /// <see cref="LevelDraft.ApplyResize"/>: growing changes nothing else,
    /// shrinking reports exactly what escapes before anything is removed, and
    /// confirming removes that set and nothing more (Module 09, no irreversible
    /// edit without confirmation).
    /// </summary>
    public class GridResizeTests
    {
        private static LevelDraft FiveByFive()
        {
            var draft = LevelDraft.NewEmpty(5, 5);

            draft.Blocks.Add(new BlockDraft
            {
                Id = 1, Cells = { new Coord(0, 0) }, ColorStack = { BlockColor.Red }, StartOrigin = new Coord(0, 0),
            });
            draft.Blocks.Add(new BlockDraft
            {
                Id = 2, Cells = { new Coord(0, 0) }, ColorStack = { BlockColor.Red }, StartOrigin = new Coord(4, 4),
            });
            draft.Gates.Add(new GateDraft
            {
                Id = 1, Edge = BoardEdge.Right, Offset = 4, Width = 1, Color = BlockColor.Red,
            });
            draft.StaticWalls.Add(new Coord(4, 0));

            return draft;
        }

        [Test]
        public void PreviewResize_Grow_IsLossless()
        {
            var impact = FiveByFive().PreviewResize(7, 7);

            Assert.IsTrue(impact.IsLossless);
        }

        [Test]
        public void ApplyResize_Grow_ChangesOnlyTheDimensions()
        {
            var draft = FiveByFive();

            draft.ApplyResize(7, 7);

            Assert.AreEqual(7, draft.Width);
            Assert.AreEqual(7, draft.Height);
            Assert.AreEqual(2, draft.Blocks.Count);
            Assert.AreEqual(1, draft.Gates.Count);
            Assert.AreEqual(1, draft.StaticWalls.Count);
        }

        [Test]
        public void PreviewResize_Shrink_ReportsExactlyWhatEscapes_AndDoesNotMutate()
        {
            var draft = FiveByFive();

            var impact = draft.PreviewResize(4, 4);

            CollectionAssert.AreEquivalent(new[] { 2 }, impact.RemovedBlockIds);
            CollectionAssert.AreEquivalent(new[] { 1 }, impact.RemovedGateIds);
            CollectionAssert.AreEquivalent(new[] { new Coord(4, 0) }, impact.RemovedStaticWalls);

            // Nothing removed yet.
            Assert.AreEqual(2, draft.Blocks.Count);
            Assert.AreEqual(1, draft.Gates.Count);
            Assert.AreEqual(1, draft.StaticWalls.Count);
        }

        [Test]
        public void ApplyResize_Shrink_RemovesExactlyThePreviewedSet()
        {
            var draft = FiveByFive();

            draft.ApplyResize(4, 4);

            Assert.AreEqual(new[] { 1 }, draft.Blocks.Select(b => b.Id).ToArray());
            Assert.AreEqual(0, draft.Gates.Count);
            Assert.AreEqual(0, draft.StaticWalls.Count);
            Assert.AreEqual(4, draft.Width);
            Assert.AreEqual(4, draft.Height);
        }
    }
}
