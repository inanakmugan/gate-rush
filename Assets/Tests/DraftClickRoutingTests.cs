using GateRush.Core;
using GateRush.Editor;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="DraftClickRouting"/> (docs/Modules/09a, Session B, Part 1):
    /// which tools treat a block on the clicked cell as a selection candidate, and
    /// which always proceed to their own placement.
    /// </summary>
    public class DraftClickRoutingTests
    {
        private static LevelDraft Draft()
        {
            var draft = LevelDraft.NewEmpty(6, 6);

            draft.Blocks.Add(new BlockDraft
            {
                Id = 1,
                Cells = { new Coord(0, 0) },
                ColorStack = { BlockColor.Red },
                StartOrigin = new Coord(2, 2),
            });
            draft.Shutters.Add(new ShutterDraft { Id = 1, Min = new Coord(2, 2), Max = new Coord(2, 2) });
            draft.Elevators.Add(new ElevatorDraft { Id = 1, Min = new Coord(4, 4), Max = new Coord(4, 4) });

            // D16: boards are packed, so a block sitting on a cell no region
            // covers is the normal case, not an edge case — the Shutter and
            // Elevator tools have to be able to create there.
            draft.Blocks.Add(new BlockDraft
            {
                Id = 2,
                Cells = { new Coord(0, 0) },
                ColorStack = { BlockColor.Blue },
                StartOrigin = new Coord(0, 0),
            });

            return draft;
        }

        [TestCase(EditorTool.Select)]
        [TestCase(EditorTool.Block)]
        [TestCase(EditorTool.Wall)]
        public void Route_BlockCell_SelectAndBlockAndWallSelectTheBlock(EditorTool tool)
        {
            var draft = Draft();

            var routing = DraftClickRouting.Route(draft, new Coord(2, 2), tool);

            Assert.IsTrue(routing.SelectsExisting);
            Assert.AreSame(draft.Blocks[0], routing.Target);
        }

        [TestCase(EditorTool.Gate)]
        [TestCase(EditorTool.Generator)]
        public void Route_BlockCell_GateAndGeneratorNeverSelectTheBlock(EditorTool tool)
        {
            var draft = Draft();

            var routing = DraftClickRouting.Route(draft, new Coord(2, 2), tool);

            Assert.IsFalse(routing.SelectsExisting);
        }

        [Test]
        public void Route_ShutterCellAlsoHoldingABlock_ShutterToolSelectsTheShutterNotTheBlock()
        {
            var draft = Draft(); // the block sits inside the shutter's one-cell region

            var routing = DraftClickRouting.Route(draft, new Coord(2, 2), EditorTool.Shutter);

            Assert.IsTrue(routing.SelectsExisting);
            Assert.AreSame(draft.Shutters[0], routing.Target);
        }

        [Test]
        public void Route_ElevatorCellWithNoBlock_ElevatorToolSelectsTheElevator()
        {
            var draft = Draft();

            var routing = DraftClickRouting.Route(draft, new Coord(4, 4), EditorTool.Elevator);

            Assert.IsTrue(routing.SelectsExisting);
            Assert.AreSame(draft.Elevators[0], routing.Target);
        }

        [Test]
        public void Route_CellHoldingARegionOfTheOtherKind_ShutterToolProceedsRatherThanSelectingIt()
        {
            var draft = Draft();

            // (4, 4) holds an elevator, not a shutter — the Shutter tool must not
            // pick it up, even though something is there.
            var routing = DraftClickRouting.Route(draft, new Coord(4, 4), EditorTool.Shutter);

            Assert.IsFalse(routing.SelectsExisting);
        }

        [TestCase(EditorTool.Shutter)]
        [TestCase(EditorTool.Elevator)]
        public void Route_BlockCellCoveredByNoRegion_RegionToolsProceedToCreateThere(EditorTool tool)
        {
            var draft = Draft(); // block 2, at (0, 0), sits on a packed cell no region covers

            var routing = DraftClickRouting.Route(draft, new Coord(0, 0), tool);

            Assert.IsFalse(routing.SelectsExisting);
        }

        [Test]
        public void Route_EmptyCellWithNeitherBlockNorRegion_ShutterToolProceeds()
        {
            var draft = Draft();

            var routing = DraftClickRouting.Route(draft, new Coord(0, 5), EditorTool.Shutter);

            Assert.IsFalse(routing.SelectsExisting);
        }

        [Test]
        public void Route_EmptyCell_SelectToolSelectsNothing()
        {
            var draft = Draft();

            var routing = DraftClickRouting.Route(draft, new Coord(0, 5), EditorTool.Select);

            Assert.IsTrue(routing.SelectsExisting);
            Assert.IsNull(routing.Target);
        }
    }
}
