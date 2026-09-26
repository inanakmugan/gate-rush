using System;
using GateRush.Core;
using GateRush.Solver;
using NUnit.Framework;
using static GateRush.Tests.Fixture;
using static GateRush.Tests.SearchCorpus;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="ExitCandidates"/>: which exits count as possible for a
    /// stratum — the dead-end signal the nearest-next-clear strategy acts on —
    /// and the blocker count it orders its search by, which counts blocks on the
    /// route to an exit as well as on the exit.
    /// </summary>
    public class ExitCandidatesTests
    {
        // ----- The start origin under an axis restriction (D39) ---------------

        [Test]
        public void IsEmpty_AxisBlockBoxedInAboveACrossAxisGate_IsTrue()
        {
            var ctx = AxisBlockBoxedAboveCrossAxisGateBoard();

            Assert.IsTrue(ExitCandidates.Of(ctx, BoardState.CreateInitial(ctx)).IsEmpty);
        }

        [Test]
        public void IsEmpty_AxisBlockThatCanStepOffItsCrossAxisGate_IsFalse()
        {
            var ctx = AxisBlockReturnsToCrossAxisGateBoard();

            Assert.IsFalse(ExitCandidates.Of(ctx, BoardState.CreateInitial(ctx)).IsEmpty);
        }

        [Test]
        public void FewestBlockers_AxisBlockMustStepOffPastABlockerToArriveBack_CountsTheBlocker()
        {
            // The red block cannot be pushed down in place; stepping off either
            // way runs into a blue block, and arriving back meets nobody.
            var ctx = Ctx(
                3, 2,
                new[]
                {
                    Block(1, new Coord(1, 0), axis: MovementAxis.HorizontalOnly),
                    Block(2, new Coord(0, 0), colors: new[] { BlockColor.Blue }),
                    Block(3, new Coord(2, 0), colors: new[] { BlockColor.Blue })
                },
                new[] { Gate(1, BoardEdge.Bottom, 1, 1, BlockColor.Red) });
            var state = BoardState.CreateInitial(ctx);

            var fewest = ExitCandidates.Of(ctx, state).FewestBlockers(ctx, state);

            Assert.AreEqual(1, fewest);
        }

        [Test]
        public void FewestBlockers_AxisBlockBoxedInAgainstAnAxisEndGate_IsZero()
        {
            // Pushing right, along its axis, is allowed in place.
            var ctx = Ctx(
                3, 1,
                new[] { Block(1, new Coord(2, 0), axis: MovementAxis.HorizontalOnly) },
                new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Red) },
                staticWalls: new[] { new Coord(1, 0) });
            var state = BoardState.CreateInitial(ctx);

            var candidates = ExitCandidates.Of(ctx, state);

            Assert.IsFalse(candidates.IsEmpty);
            Assert.AreEqual(0, candidates.FewestBlockers(ctx, state));
        }

        // ----- Exits in general ------------------------------------------------

        [Test]
        public void IsEmpty_ColourHasNoOpenGate_IsTrue()
        {
            var ctx = Ctx(3, 1, new[] { Block(1, new Coord(0, 0)) }, new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Blue) });

            Assert.IsTrue(ExitCandidates.Of(ctx, BoardState.CreateInitial(ctx)).IsEmpty);
        }

        [Test]
        public void IsEmpty_OnlyExitCoveredByAFrozenBlock_IsTrue()
        {
            // The blue block sits frozen in the red gate's mouth; no clear can
            // thaw it before a clear, so the red block has nowhere to exit.
            var ctx = Ctx(
                3, 1,
                new[]
                {
                    Block(1, new Coord(0, 0)),
                    Block(2, new Coord(2, 0), colors: new[] { BlockColor.Blue }, unfreezeAt: 1)
                },
                new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Red) });

            Assert.IsTrue(ExitCandidates.Of(ctx, BoardState.CreateInitial(ctx)).IsEmpty);
        }

        [Test]
        public void IsEmpty_OnlyExitUnderAClosedShutter_IsTrue()
        {
            var ctx = Ctx(
                3, 1,
                new[] { Block(1, new Coord(0, 0)) },
                new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Red) },
                shutters: new[] { Shutter(1, new Coord(2, 0), new Coord(2, 0), threshold: 1) });

            Assert.IsTrue(ExitCandidates.Of(ctx, BoardState.CreateInitial(ctx)).IsEmpty);
        }

        [Test]
        public void IsEmpty_OnlyRouteToTheExitPassesAFrozenBlock_IsTrue()
        {
            // The exit cell itself is free, but the frozen blue block stands on
            // the only way there and cannot step aside before a clear.
            var ctx = Ctx(
                4, 1,
                new[]
                {
                    Block(1, new Coord(0, 0)),
                    Block(2, new Coord(1, 0), colors: new[] { BlockColor.Blue }, unfreezeAt: 1)
                },
                new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Red) });

            Assert.IsTrue(ExitCandidates.Of(ctx, BoardState.CreateInitial(ctx)).IsEmpty);
        }

        [Test]
        public void FewestBlockers_RouteAroundTheBlocker_IsZero()
        {
            // Same layout without the wall: red can go over blue through row 1.
            var ctx = Ctx(
                3, 2,
                new[]
                {
                    Block(1, new Coord(0, 0)),
                    Block(2, new Coord(1, 0), colors: new[] { BlockColor.Blue })
                },
                new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Red) });
            var state = BoardState.CreateInitial(ctx);

            Assert.AreEqual(0, ExitCandidates.Of(ctx, state).FewestBlockers(ctx, state));
        }

        [Test]
        public void FewestBlockers_ExitAlreadyFree_IsZero()
        {
            var ctx = Ctx(3, 1, new[] { Block(1, new Coord(0, 0)) }, new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Red) });
            var state = BoardState.CreateInitial(ctx);

            Assert.AreEqual(0, ExitCandidates.Of(ctx, state).FewestBlockers(ctx, state));
        }

        [Test]
        public void FewestBlockers_BlockAlreadyAtItsExit_DoesNotCountItself()
        {
            var ctx = Ctx(3, 1, new[] { Block(1, new Coord(2, 0)) }, new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Red) });
            var state = BoardState.CreateInitial(ctx);

            Assert.AreEqual(0, ExitCandidates.Of(ctx, state).FewestBlockers(ctx, state));
        }

        [Test]
        public void FewestBlockers_StepAsideBoard_CountsTheOneBlockInTheWay()
        {
            // Red needs (2, 0); blue at (1, 0) stands on the way, not on the exit
            // itself, and the wall at (2, 1) closes the route around it.
            var ctx = StepAsideBeforeFirstClearBoard();
            var state = BoardState.CreateInitial(ctx);

            Assert.AreEqual(1, ExitCandidates.Of(ctx, state).FewestBlockers(ctx, state));
        }

        [Test]
        public void FewestBlockers_MultiCellBlockerOverSeveralFootprintCells_CountsOnce()
        {
            // A 2x2 red block must reach the 2-wide gate at the right of rows 0-1;
            // one vertical 1x2 blue block covers both cells of the column it needs.
            var square = new[] { new Coord(0, 0), new Coord(1, 0), new Coord(0, 1), new Coord(1, 1) };
            var tall = new[] { new Coord(0, 0), new Coord(0, 1) };
            var ctx = Ctx(
                4, 2,
                new[]
                {
                    Block(1, new Coord(0, 0), cells: square),
                    Block(2, new Coord(3, 0), cells: tall, colors: new[] { BlockColor.Blue })
                },
                new[] { Gate(1, BoardEdge.Right, 0, 2, BlockColor.Red) });
            var state = BoardState.CreateInitial(ctx);

            Assert.AreEqual(1, ExitCandidates.Of(ctx, state).FewestBlockers(ctx, state));
        }

        [Test]
        public void FewestBlockers_EmptyStratum_Throws()
        {
            var ctx = Ctx(3, 1, new[] { Block(1, new Coord(0, 0)) }, new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Blue) });
            var state = BoardState.CreateInitial(ctx);

            Assert.Throws<InvalidOperationException>(() => ExitCandidates.Of(ctx, state).FewestBlockers(ctx, state));
        }
    }
}
