using System;
using GateRush.Core;
using NUnit.Framework;
using static GateRush.Tests.Fixture;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers the directional <see cref="BlockReachability.CanClearInPlace(LevelContext, BoardState, int, Direction)"/>
    /// overload added in Module 11: a push in place clears only toward the
    /// edge whose gate it uses, never across the block's axis (D39), and it
    /// agrees with the directionless overload the resolver judges by. Also
    /// covers <see cref="BlockReachability.FindExitGate"/>, added in Module 12,
    /// which names the gate those checks find.
    /// </summary>
    /// <remarks>
    /// The rest of <see cref="BlockReachability"/> is exercised through
    /// <c>MoveResolverTests</c> and <c>MoveGeneratorTests</c>, which were
    /// written before this class existed. Grid y grows upward.
    /// </remarks>
    public class BlockReachabilityTests
    {
        private const int AgreementSeed = 11;
        private const int AgreementBoardCount = 300;
        private const int MinimumCasesPerOutcome = 5;

        private static readonly Direction[] AllDirections =
            { Direction.Up, Direction.Down, Direction.Left, Direction.Right };

        [TestCase(BoardEdge.Left, Direction.Left)]
        [TestCase(BoardEdge.Bottom, Direction.Down)]
        public void CanClearInPlaceWithDirection_CornerBlock_TrueOnlyTowardTheGatesEdge(
            BoardEdge gateEdge, Direction facing)
        {
            // Block in the bottom-left corner, flush against both the left and
            // the bottom edge; its gate is on one of them.
            var ctx = Ctx(3, 3, new[] { Block(1, new Coord(0, 0)) }, new[] { Gate(1, gateEdge, 0, 1, BlockColor.Red) });
            var state = BoardState.CreateInitial(ctx);

            foreach (var push in AllDirections)
            {
                Assert.AreEqual(
                    push == facing,
                    BlockReachability.CanClearInPlace(ctx, state, 0, push),
                    $"push {push}");
            }
        }

        [TestCase(MovementAxis.HorizontalOnly, BoardEdge.Bottom, Direction.Down)]
        [TestCase(MovementAxis.VerticalOnly, BoardEdge.Left, Direction.Left)]
        public void CanClearInPlaceWithDirection_DirectionAcrossTheAxis_IsFalse(
            MovementAxis axis, BoardEdge gateEdge, Direction facing)
        {
            var ctx = Ctx(3, 3, new[] { Block(1, new Coord(0, 0), axis: axis) }, new[] { Gate(1, gateEdge, 0, 1, BlockColor.Red) });
            var state = BoardState.CreateInitial(ctx);

            var result = BlockReachability.CanClearInPlace(ctx, state, 0, facing);

            Assert.IsFalse(result);
        }

        [TestCase(MovementAxis.HorizontalOnly, BoardEdge.Left, Direction.Left)]
        [TestCase(MovementAxis.VerticalOnly, BoardEdge.Bottom, Direction.Down)]
        public void CanClearInPlaceWithDirection_DirectionAlongTheAxis_IsTrue(
            MovementAxis axis, BoardEdge gateEdge, Direction facing)
        {
            var ctx = Ctx(3, 3, new[] { Block(1, new Coord(0, 0), axis: axis) }, new[] { Gate(1, gateEdge, 0, 1, BlockColor.Red) });
            var state = BoardState.CreateInitial(ctx);

            var result = BlockReachability.CanClearInPlace(ctx, state, 0, facing);

            Assert.IsTrue(result);
        }

        [Test]
        public void CanClearInPlaceWithDirection_TrueForSomeDirection_ExactlyWhenDirectionlessIsTrue_OverSeededRandomBoards()
        {
            var rng = new Random(AgreementSeed);
            var clearableChecked = 0;
            var unclearableChecked = 0;

            for (var b = 0; b < AgreementBoardCount; b++)
            {
                var ctx = RandomBoards.Next(rng);
                var state = BoardState.CreateInitial(ctx);

                for (var i = 0; i < ctx.TotalBlockCapacity; i++)
                {
                    if (!state.Alive[i])
                    {
                        continue;
                    }

                    var directionless = BlockReachability.CanClearInPlace(ctx, state, i);
                    var anyDirection = false;
                    foreach (var push in AllDirections)
                    {
                        anyDirection |= BlockReachability.CanClearInPlace(ctx, state, i, push);
                    }

                    Assert.AreEqual(directionless, anyDirection, $"board {b}, block slot {i}");
                    clearableChecked += directionless ? 1 : 0;
                    unclearableChecked += directionless ? 0 : 1;
                }
            }

            Assert.GreaterOrEqual(clearableChecked, MinimumCasesPerOutcome, "too few clearable blocks to mean anything");
            Assert.GreaterOrEqual(unclearableChecked, MinimumCasesPerOutcome, "too few unclearable blocks to mean anything");
        }

        [TestCase(Direction.Left, BoardEdge.Left)]
        [TestCase(Direction.Down, BoardEdge.Bottom)]
        public void FindExitGate_PushInACornerWithGatesOnBothEdges_ReturnsTheGateItFaces(Direction push, BoardEdge facedEdge)
        {
            var ctx = Ctx(3, 3, new[] { Block(1, new Coord(0, 0)) },
                new[] { Gate(1, BoardEdge.Left, 0, 1, BlockColor.Red), Gate(2, BoardEdge.Bottom, 0, 1, BlockColor.Red) });
            var state = BoardState.CreateInitial(ctx);

            var gate = BlockReachability.FindExitGate(ctx, state, 0, state.Origins[0], push);

            Assert.GreaterOrEqual(gate, 0);
            Assert.AreEqual(facedEdge, ctx.Gates[gate].Edge);
        }

        [Test]
        public void FindExitGate_Arrival_FindsTheGateAtTheLandingOrigin()
        {
            // The block starts in the middle of the left column and would arrive
            // flush against the right edge, in line with the right gate.
            var ctx = Ctx(3, 3, new[] { Block(1, new Coord(0, 1)) },
                new[] { Gate(1, BoardEdge.Top, 0, 1, BlockColor.Red), Gate(2, BoardEdge.Right, 1, 1, BlockColor.Red) });
            var state = BoardState.CreateInitial(ctx);

            var gate = BlockReachability.FindExitGate(ctx, state, 0, new Coord(2, 1), push: null);

            Assert.AreEqual(1, gate);
        }

        [TestCase(BoardEdge.Left, BoardEdge.Bottom)]
        [TestCase(BoardEdge.Bottom, BoardEdge.Left)]
        public void FindExitGate_ArrivalInACornerWithGatesOnBothEdges_ReturnsTheLowestIndex(
            BoardEdge firstEdge, BoardEdge secondEdge)
        {
            // Both orders, so the answer is the lowest index and not an edge
            // that happens to come first in some other order.
            var ctx = Ctx(3, 3, new[] { Block(1, new Coord(1, 1)) },
                new[] { Gate(1, firstEdge, 0, 1, BlockColor.Red), Gate(2, secondEdge, 0, 1, BlockColor.Red) });
            var state = BoardState.CreateInitial(ctx);

            var gate = BlockReachability.FindExitGate(ctx, state, 0, new Coord(0, 0), push: null);

            Assert.AreEqual(0, gate);
        }

        [TestCase(Direction.Left)]
        [TestCase(null)]
        public void FindExitGate_NoCompatibleGate_ReturnsMinusOne(Direction? push)
        {
            // The block is flush against the left gate, but the gate is blue.
            var ctx = Ctx(3, 3, new[] { Block(1, new Coord(0, 1)) }, new[] { Gate(1, BoardEdge.Left, 1, 1, BlockColor.Blue) });
            var state = BoardState.CreateInitial(ctx);

            var gate = BlockReachability.FindExitGate(ctx, state, 0, state.Origins[0], push);

            Assert.AreEqual(-1, gate);
        }
    }
}
