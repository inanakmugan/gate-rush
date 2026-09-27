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
    /// agrees with the directionless overload the resolver judges by.
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
    }
}
