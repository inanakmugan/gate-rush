using System.Collections.Generic;
using GateRush.Core;
using GateRush.Runtime;
using NUnit.Framework;
using UnityEngine;
using static GateRush.Tests.Fixture;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers the drag model of Module 11 (M1, D27): the block follows the
    /// pointer cell by cell, stops against obstacles and is steered around
    /// them, respects its axis, and the release decides the move — including
    /// the push in place into a gate on the edge it is pushed toward.
    /// </summary>
    /// <remarks>
    /// Pointers are fractional grid positions in cell units; <see cref="At"/>
    /// is a cell's centre. Grid y grows upward.
    /// </remarks>
    public class DragControllerTests
    {
        private const float Threshold = 0.3f;
        private const float AboveThreshold = 0.4f;
        private const float BelowThreshold = 0.2f;

        private const int RandomPathSeed = 27;
        private const int PointerMovesPerDrag = 12;
        private const int MinimumStepsChecked = 100;

        private static readonly Coord[] Cells1x2Vertical = { new Coord(0, 0), new Coord(0, 1) };

        private static Vector2 At(int x, int y) => new Vector2(x + 0.5f, y + 0.5f);

        private static Vector2 Push(Vector2 from, Direction push, float distance)
        {
            switch (push)
            {
                case Direction.Up:
                    return from + new Vector2(0f, distance);
                case Direction.Down:
                    return from - new Vector2(0f, distance);
                case Direction.Left:
                    return from - new Vector2(distance, 0f);
                default:
                    return from + new Vector2(distance, 0f);
            }
        }

        [Test]
        public void Update_AlongFreeCorridor_FollowsCellByCellAndEndReleasesAtLastOrigin()
        {
            var ctx = Ctx(5, 1, new[] { Block(1, new Coord(0, 0)) }, new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Blue) });
            var state = BoardState.CreateInitial(ctx);
            var drag = new DragController(Threshold);
            var steps = new List<Coord>();
            drag.Stepped += steps.Add;
            drag.TryBegin(ctx, state, At(0, 0));

            var afterOne = drag.Update(At(1, 0));
            var afterThree = drag.Update(At(3, 0));
            var move = drag.End(At(3, 0));

            Assert.AreEqual(new Coord(1, 0), afterOne);
            Assert.AreEqual(new Coord(3, 0), afterThree);
            CollectionAssert.AreEqual(new[] { new Coord(1, 0), new Coord(2, 0), new Coord(3, 0) }, steps);
            Assert.AreEqual(new Move(0, new Coord(3, 0)), move);
            Assert.IsFalse(drag.IsDragging);
        }

        [TestCase(false, TestName = "Update_IntoAWall_StopsAndIsSteeredAround")]
        [TestCase(true, TestName = "Update_IntoAnotherBlock_StopsAndIsSteeredAround")]
        public void Update_IntoAnObstacle_StopsAtLastLegalOriginAndIsSteeredAround(bool obstacleIsBlock)
        {
            // 4x3 board, dragged block at (0, 1), obstacle at (2, 1).
            var obstacle = new Coord(2, 1);
            var blocks = obstacleIsBlock
                ? new[] { Block(1, new Coord(0, 1)), Block(2, obstacle, colors: new[] { BlockColor.Blue }) }
                : new[] { Block(1, new Coord(0, 1)) };
            var walls = obstacleIsBlock ? null : new[] { obstacle };
            var ctx = Ctx(4, 3, blocks, new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Green) }, staticWalls: walls);
            var state = BoardState.CreateInitial(ctx);
            var drag = new DragController(Threshold);
            drag.TryBegin(ctx, state, At(0, 1));

            var stopped = drag.Update(At(3, 1));
            var over = drag.Update(At(3, 2));
            var down = drag.Update(At(3, 1));
            var move = drag.End(At(3, 1));

            Assert.AreEqual(new Coord(1, 1), stopped);
            Assert.AreEqual(new Coord(3, 2), over);
            Assert.AreEqual(new Coord(3, 1), down);
            Assert.AreEqual(new Move(0, new Coord(3, 1)), move);
        }

        [Test]
        public void Update_OverRandomPointerPaths_OnlyTakesSingleOrthogonalStepsToReachableOrigins()
        {
            var rng = new System.Random(RandomPathSeed);
            var reachability = new BlockReachability();
            var stepsChecked = 0;

            foreach (var (name, ctx, _) in SearchCorpus.WholeCorpus())
            {
                var state = BoardState.CreateInitial(ctx);
                for (var i = 0; i < ctx.TotalBlockCapacity; i++)
                {
                    if (!state.CanMove(ctx, i))
                    {
                        continue;
                    }

                    var start = state.Origins[i];
                    var grabCell = start + ctx.SpecAt(i).Cells[0];
                    var drag = new DragController(Threshold);
                    var previous = start;
                    drag.Stepped += next =>
                    {
                        var delta = next - previous;
                        Assert.AreEqual(1, System.Math.Abs(delta.X) + System.Math.Abs(delta.Y), $"{name}: step {previous} -> {next}");
                        Assert.IsTrue(reachability.IsReachable(ctx, state, i, start, next), $"{name}: {next} is not reachable");
                        previous = next;
                        stepsChecked++;
                    };

                    Assert.IsTrue(drag.TryBegin(ctx, state, At(grabCell.X, grabCell.Y)), name);
                    for (var p = 0; p < PointerMovesPerDrag; p++)
                    {
                        // Up to one cell beyond every edge, so pushes off the
                        // board are exercised too.
                        var pointer = new Vector2(
                            (float)(rng.NextDouble() * (ctx.Width + 2) - 1),
                            (float)(rng.NextDouble() * (ctx.Height + 2) - 1));
                        // Update first: its Stepped handler advances
                        // `previous`, and arguments are read left to right.
                        var displayed = drag.Update(pointer);
                        Assert.AreEqual(previous, displayed, name);
                    }

                    drag.Cancel();
                }
            }

            Assert.GreaterOrEqual(stepsChecked, MinimumStepsChecked, "too few steps taken to mean anything");
        }

        [TestCase(MovementAxis.HorizontalOnly, 1, 3, 3, 3, 3, 1)]
        [TestCase(MovementAxis.VerticalOnly, 3, 1, 3, 3, 1, 3)]
        public void Update_AxisRestrictedBlock_IgnoresPointerMovementAcrossItsAxis(
            MovementAxis axis, int acrossX, int acrossY, int bothX, int bothY, int expectedX, int expectedY)
        {
            var ctx = Ctx(4, 4, new[] { Block(1, new Coord(1, 1), axis: axis) }, new[] { Gate(1, BoardEdge.Top, 0, 1, BlockColor.Blue) });
            var state = BoardState.CreateInitial(ctx);
            var drag = new DragController(Threshold);
            var steps = new List<Coord>();
            drag.Stepped += steps.Add;
            drag.TryBegin(ctx, state, At(1, 1));

            var acrossOnly = drag.Update(At(acrossX, acrossY));
            var stepsAcross = steps.Count;
            var both = drag.Update(At(bothX, bothY));

            Assert.AreEqual(new Coord(1, 1), acrossOnly);
            Assert.AreEqual(0, stepsAcross);
            Assert.AreEqual(new Coord(expectedX, expectedY), both);
        }

        [TestCase("frozen")]
        [TestCase("locked")]
        [TestCase("shuttered")]
        [TestCase("dead")]
        [TestCase("empty cell")]
        public void TryBegin_OnABlockThatCannotMove_ReturnsFalse(string condition)
        {
            var target = new Coord(0, 0);
            var gates = new[] { Gate(1, BoardEdge.Left, 0, 1, BlockColor.Red) };
            LevelContext ctx;
            switch (condition)
            {
                case "frozen":
                    ctx = Ctx(3, 3, new[] { Block(1, target, unfreezeAt: 1) }, gates);
                    break;
                case "locked":
                    ctx = Ctx(3, 3,
                        new[] { Block(1, target, lockId: 0, requiredKeys: 1), Block(2, new Coord(2, 2), keyTarget: 0) },
                        gates);
                    break;
                case "shuttered":
                    ctx = Ctx(3, 3, new[] { Block(1, target) }, gates, shutters: new[] { Shutter(1, target, target) });
                    break;
                default:
                    ctx = Ctx(3, 3, new[] { Block(1, target) }, gates);
                    break;
            }

            var state = BoardState.CreateInitial(ctx);
            if (condition == "dead")
            {
                var cleared = new MoveResolver().TryApplyMove(ctx, state, new Move(0, target), out var afterClear, out _);
                Assert.IsTrue(cleared);
                state = afterClear;
            }

            var grab = condition == "empty cell" ? At(1, 1) : At(target.X, target.Y);
            var drag = new DragController(Threshold);

            var begun = drag.TryBegin(ctx, state, grab);

            Assert.IsFalse(begun);
            Assert.IsFalse(drag.IsDragging);
            Assert.AreEqual(-1, drag.BlockIndex);
        }

        /// <summary>
        /// Bottom-left corner block with a red gate on the left. A blue gate on
        /// the bottom makes that edge one with a gate, but no usable one; a
        /// blue block sits to the right and a wall above.
        /// </summary>
        private static LevelContext PushBoard() =>
            Ctx(
                3, 3,
                new[] { Block(1, new Coord(0, 0)), Block(2, new Coord(1, 0), colors: new[] { BlockColor.Blue }) },
                new[] { Gate(1, BoardEdge.Left, 0, 1, BlockColor.Red), Gate(2, BoardEdge.Bottom, 0, 1, BlockColor.Blue) },
                staticWalls: new[] { new Coord(0, 1) });

        [Test]
        public void End_PushIntoCompatibleGateItIsFlushAgainst_ReturnsMoveToStart()
        {
            var ctx = PushBoard();
            var state = BoardState.CreateInitial(ctx);
            var drag = new DragController(Threshold);
            drag.TryBegin(ctx, state, At(0, 0));

            var move = drag.End(Push(At(0, 0), Direction.Left, AboveThreshold));

            Assert.AreEqual(new Move(0, new Coord(0, 0)), move);
        }

        [TestCase(Direction.Right, AboveThreshold, TestName = "End_PushTowardANeighbouringBlock_ReturnsNoMove")]
        [TestCase(Direction.Up, AboveThreshold, TestName = "End_PushIntoAWall_ReturnsNoMove")]
        [TestCase(Direction.Down, AboveThreshold, TestName = "End_PushTowardAnEdgeWithNoUsableGate_ReturnsNoMove")]
        [TestCase(Direction.Left, BelowThreshold, TestName = "End_PushBelowTheThreshold_ReturnsNoMove")]
        public void End_PushThatCannotClear_ReturnsNoMove(Direction push, float distance)
        {
            var ctx = PushBoard();
            var state = BoardState.CreateInitial(ctx);
            var drag = new DragController(Threshold);
            drag.TryBegin(ctx, state, At(0, 0));

            var move = drag.End(Push(At(0, 0), push, distance));

            Assert.IsNull(move);
            Assert.IsFalse(drag.IsDragging);
        }

        [TestCase(BoardEdge.Left, Direction.Left, Direction.Down)]
        [TestCase(BoardEdge.Bottom, Direction.Down, Direction.Left)]
        public void End_PushInACorner_ClearsOnlyTowardTheEdgeWithTheUsableGate(
            BoardEdge gateEdge, Direction toward, Direction away)
        {
            var ctx = Ctx(3, 3, new[] { Block(1, new Coord(0, 0)) }, new[] { Gate(1, gateEdge, 0, 1, BlockColor.Red) });
            var state = BoardState.CreateInitial(ctx);
            var drag = new DragController(Threshold);

            drag.TryBegin(ctx, state, At(0, 0));
            var towardGate = drag.End(Push(At(0, 0), toward, AboveThreshold));
            drag.TryBegin(ctx, state, At(0, 0));
            var towardOtherEdge = drag.End(Push(At(0, 0), away, AboveThreshold));

            Assert.AreEqual(new Move(0, new Coord(0, 0)), towardGate);
            Assert.IsNull(towardOtherEdge);
        }

        [TestCase(Direction.Left, true)]
        [TestCase(Direction.Right, false)]
        public void End_AfterLeavingAndReturningToStart_TheFinalDirectionDecides(Direction finalPush, bool expectsPush)
        {
            // 3x3 board, block in the bottom-left corner, red gate on the left.
            var ctx = Ctx(3, 3, new[] { Block(1, new Coord(0, 0)) }, new[] { Gate(1, BoardEdge.Left, 0, 1, BlockColor.Red) });
            var state = BoardState.CreateInitial(ctx);
            var drag = new DragController(Threshold);
            drag.TryBegin(ctx, state, At(0, 0));
            drag.Update(At(2, 0));

            var back = drag.Update(At(0, 0));
            var move = drag.End(Push(At(0, 0), finalPush, AboveThreshold));

            Assert.AreEqual(new Coord(0, 0), back);
            if (expectsPush)
            {
                Assert.AreEqual(new Move(0, new Coord(0, 0)), move);
            }
            else
            {
                Assert.IsNull(move);
            }
        }

        [TestCase(BoardEdge.Left, Direction.Left)]
        [TestCase(BoardEdge.Bottom, Direction.Down)]
        public void End_PushInPlace_ReportsThePushDirection(BoardEdge gateEdge, Direction push)
        {
            var ctx = Ctx(3, 3, new[] { Block(1, new Coord(0, 0)) }, new[] { Gate(1, gateEdge, 0, 1, BlockColor.Red) });
            var state = BoardState.CreateInitial(ctx);
            var drag = new DragController(Threshold);
            drag.TryBegin(ctx, state, At(0, 0));

            var move = drag.End(Push(At(0, 0), push, AboveThreshold), out var reported);

            Assert.AreEqual(new Move(0, new Coord(0, 0)), move);
            Assert.AreEqual(push, reported);
        }

        [Test]
        public void End_MoveThatArrives_ReportsNoPushDirection()
        {
            var ctx = Ctx(3, 1, new[] { Block(1, new Coord(0, 0)) }, new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Red) });
            var state = BoardState.CreateInitial(ctx);
            var drag = new DragController(Threshold);
            drag.TryBegin(ctx, state, At(0, 0));

            var move = drag.End(At(2, 0), out var reported);

            Assert.AreEqual(new Move(0, new Coord(2, 0)), move);
            Assert.IsNull(reported);
        }

        [Test]
        public void End_PushThatCannotClear_ReportsNoPushDirection()
        {
            var ctx = PushBoard();
            var state = BoardState.CreateInitial(ctx);
            var drag = new DragController(Threshold);
            drag.TryBegin(ctx, state, At(0, 0));

            var move = drag.End(Push(At(0, 0), Direction.Right, AboveThreshold), out var reported);

            Assert.IsNull(move);
            Assert.IsNull(reported);
        }

        [Test]
        public void End_PushAlongAMultiCellBlocksAxis_UsesTheGrabbedCellsDisplacement()
        {
            // A vertical 1x2 block flush against the bottom, grabbed by its
            // upper cell; the red gate is below it.
            var ctx = Ctx(3, 3, new[] { Block(1, new Coord(1, 0), cells: Cells1x2Vertical) }, new[] { Gate(1, BoardEdge.Bottom, 1, 1, BlockColor.Red) });
            var state = BoardState.CreateInitial(ctx);
            var drag = new DragController(Threshold);
            drag.TryBegin(ctx, state, At(1, 1));

            var move = drag.End(Push(At(1, 1), Direction.Down, AboveThreshold));

            Assert.AreEqual(new Move(0, new Coord(1, 0)), move);
        }
    }
}
