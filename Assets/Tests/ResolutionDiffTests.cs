using GateRush.Core;
using GateRush.Runtime;
using NUnit.Framework;
using static GateRush.Tests.Fixture;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 12's <see cref="ResolutionDiff"/>: the moved block's clear
    /// is reported with the colour removed, whether the block died, and the
    /// gate it left through; a move that clears nothing reports nothing.
    /// Grid y grows upward.
    /// </summary>
    public class ResolutionDiffTests
    {
        [Test]
        public void Between_SingleColourBlockArrivingAtItsGate_IsListedAsDestroyedWithThatGatesEdge()
        {
            var ctx = Ctx(3, 1, new[] { Block(1, new Coord(0, 0)) }, new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Red) });
            var before = BoardState.CreateInitial(ctx);
            var move = new Move(0, new Coord(2, 0));
            Assert.IsTrue(new MoveResolver().TryApplyMove(ctx, before, move, out var after, out _));

            var clears = ResolutionDiff.Between(ctx, before, after, move, push: null);

            Assert.AreEqual(1, clears.Count);
            Assert.AreEqual(0, clears[0].BlockIndex);
            Assert.AreEqual(BlockColor.Red, clears[0].RemovedColor);
            Assert.IsTrue(clears[0].IsDestroyed);
            Assert.IsNull(clears[0].ExposedColor);
            Assert.AreEqual(0, clears[0].GateIndex);
            Assert.AreEqual(BoardEdge.Right, clears[0].GateEdge);
        }

        [Test]
        public void Between_LayeredBlockPushedIntoItsGate_IsListedAsSurvivingWithTheRemovedColour()
        {
            var ctx = Ctx(3, 1,
                new[] { Block(1, new Coord(0, 0), colors: new[] { BlockColor.Red, BlockColor.Blue }) },
                new[] { Gate(1, BoardEdge.Left, 0, 1, BlockColor.Red), Gate(2, BoardEdge.Right, 0, 1, BlockColor.Blue) });
            var before = BoardState.CreateInitial(ctx);
            var move = new Move(0, new Coord(0, 0));
            Assert.IsTrue(new MoveResolver().TryApplyMove(ctx, before, move, out var after, out _));

            var clears = ResolutionDiff.Between(ctx, before, after, move, Direction.Left);

            Assert.AreEqual(1, clears.Count);
            Assert.AreEqual(BlockColor.Red, clears[0].RemovedColor);
            Assert.IsFalse(clears[0].IsDestroyed);
            Assert.AreEqual(BlockColor.Blue, clears[0].ExposedColor);
            Assert.AreEqual(BoardEdge.Left, clears[0].GateEdge);
        }

        [TestCase(Direction.Left, BoardEdge.Left)]
        [TestCase(Direction.Down, BoardEdge.Bottom)]
        public void Between_PushInACornerWithGatesOnBothEdges_ReportsTheEdgePushedToward(Direction push, BoardEdge expected)
        {
            var ctx = Ctx(3, 3, new[] { Block(1, new Coord(0, 0)), Block(2, new Coord(2, 2), colors: new[] { BlockColor.Blue }) },
                new[] { Gate(1, BoardEdge.Left, 0, 1, BlockColor.Red), Gate(2, BoardEdge.Bottom, 0, 1, BlockColor.Red) });
            var before = BoardState.CreateInitial(ctx);
            var move = new Move(0, new Coord(0, 0));
            Assert.IsTrue(new MoveResolver().TryApplyMove(ctx, before, move, out var after, out _));

            var clears = ResolutionDiff.Between(ctx, before, after, move, push);

            Assert.AreEqual(1, clears.Count);
            Assert.AreEqual(expected, clears[0].GateEdge);
        }

        [Test]
        public void Between_MoveThatClearsNothing_IsEmpty()
        {
            var ctx = Ctx(3, 1, new[] { Block(1, new Coord(0, 0)) }, new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Blue) });
            var before = BoardState.CreateInitial(ctx);
            var move = new Move(0, new Coord(2, 0));
            Assert.IsTrue(new MoveResolver().TryApplyMove(ctx, before, move, out var after, out _));

            var clears = ResolutionDiff.Between(ctx, before, after, move, push: null);

            Assert.IsEmpty(clears);
        }
    }
}
