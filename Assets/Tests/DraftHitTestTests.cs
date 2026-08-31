using GateRush.Core;
using GateRush.Editor;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="DraftHitTest"/> (revision A1) — the pure cell-to-thing
    /// resolution the window uses to decide "did I click something". Precedence:
    /// block over region over wall, and nothing for an empty cell.
    /// </summary>
    public class DraftHitTestTests
    {
        private static LevelDraft Draft()
        {
            var draft = LevelDraft.NewEmpty(6, 6);

            draft.Blocks.Add(new BlockDraft
            {
                Id = 1,
                Cells = { new Coord(0, 0) },
                ColorStack = { BlockColor.Red },
                StartOrigin = new Coord(1, 1),
            });
            draft.Shutters.Add(new ShutterDraft { Id = 1, Min = new Coord(0, 0), Max = new Coord(3, 3) });
            draft.Elevators.Add(new ElevatorDraft { Id = 1, Min = new Coord(4, 0), Max = new Coord(5, 2) });
            draft.StaticWalls.Add(new Coord(0, 5));

            return draft;
        }

        [Test]
        public void PickAt_CellWithABlockAndAShutter_ReturnsTheBlock()
        {
            var draft = Draft();

            var hit = DraftHitTest.PickAt(draft, new Coord(1, 1)); // block sits inside the shutter region

            Assert.AreEqual(DraftHitKind.Block, hit.Kind);
            Assert.AreSame(draft.Blocks[0], hit.Target);
        }

        [Test]
        public void PickAt_ShutterCellWithNoBlock_ReturnsTheShutter()
        {
            var draft = Draft();

            var hit = DraftHitTest.PickAt(draft, new Coord(3, 3));

            Assert.AreEqual(DraftHitKind.Shutter, hit.Kind);
            Assert.AreSame(draft.Shutters[0], hit.Target);
        }

        [Test]
        public void PickAt_ElevatorCellOutsideAnyShutter_ReturnsTheElevator()
        {
            var draft = Draft();

            var hit = DraftHitTest.PickAt(draft, new Coord(5, 2));

            Assert.AreEqual(DraftHitKind.Elevator, hit.Kind);
            Assert.AreSame(draft.Elevators[0], hit.Target);
        }

        [Test]
        public void PickAt_WallCellWithNothingElse_ReturnsWallWithNoTarget()
        {
            var draft = Draft();

            var hit = DraftHitTest.PickAt(draft, new Coord(0, 5));

            Assert.AreEqual(DraftHitKind.Wall, hit.Kind);
            Assert.IsNull(hit.Target);
        }

        [Test]
        public void PickAt_EmptyCell_ReturnsNone()
        {
            var draft = Draft();

            var hit = DraftHitTest.PickAt(draft, new Coord(2, 5));

            Assert.IsTrue(hit.IsEmpty);
            Assert.AreEqual(DraftHitKind.None, hit.Kind);
        }
    }
}
