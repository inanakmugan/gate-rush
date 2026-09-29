using System.Collections.Generic;
using GateRush.Core;
using GateRush.Runtime;
using NUnit.Framework;
using UnityEngine;
using static GateRush.Tests.Fixture;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers the free drag of Module 13 (D44): every position the block takes
    /// is legal, it stops exactly flush and never tunnels, the corner assist
    /// slides it into corridors without ever leaving legality, the pointer
    /// smoothing is frame-rate independent, and the release rounds to the
    /// nearest cell or decides a push exactly as Module 11 did (M1, D43).
    /// </summary>
    /// <remarks>
    /// Pointers are fractional grid positions in cell units; <see cref="At"/>
    /// is a cell's centre, so grabbing a block at its origin cell's centre
    /// leaves the target half a cell from the pointer on each axis. Grid y
    /// grows upward. <see cref="Instant"/> with <see cref="InstantRate"/> makes
    /// the smoothed pointer reach the real one in one update, so a test can
    /// place the target exactly.
    /// </remarks>
    public class DragControllerTests
    {
        private const float Threshold = 0.3f;
        private const float AboveThreshold = 0.4f;
        private const float BelowThreshold = 0.2f;

        private const float Assist = 0.3f;
        private const float WithinAssist = 0.2f;
        private const float BeyondAssist = 0.4f;

        private const float InstantRate = 100f;
        private const float Instant = 1f;
        private const float FollowRate = 20f;
        private const float FrameSeconds = 0.05f;
        private const float Tolerance = 1e-4f;

        private const int RandomPathSeed = 44;
        private const int DragsPerBlock = 4;
        private const int PointerMovesPerDrag = 12;
        private const float LongFrameChance = 0.3f;
        private const float MaxShortFrameSeconds = 0.1f;
        private const int MinimumPositionsChecked = 100;
        private const int MinimumMovesChecked = 10;

        private const int ConvergenceFrames = 200;

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

        private static DragController InstantDrag(float assist = Assist) =>
            new DragController(new DragSettings(Threshold, InstantRate, assist));

        private static DragController SmoothedDrag() =>
            new DragController(new DragSettings(Threshold, FollowRate, Assist));

        /// <summary>
        /// D44's legality: every whole-cell origin <paramref name="position"/>
        /// overlaps — floor and ceil on each axis — passes
        /// <see cref="BlockReachability.IsFootprintLegal"/>.
        /// </summary>
        private static bool IsLegal(LevelContext ctx, BoardState state, int blockIndex, Vector2 position)
        {
            var xs = new[] { Mathf.FloorToInt(position.x), Mathf.CeilToInt(position.x) };
            var ys = new[] { Mathf.FloorToInt(position.y), Mathf.CeilToInt(position.y) };
            foreach (var x in xs)
            {
                foreach (var y in ys)
                {
                    if (!BlockReachability.IsFootprintLegal(ctx, state, blockIndex, new Coord(x, y)))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Random drags over the whole corpus: pointers up to one cell beyond
        /// every edge, and frames both short (smoothed) and long (the pointer
        /// jumps). Calls <paramref name="onPosition"/> after every update and
        /// <paramref name="onRelease"/> with each release's result.
        /// </summary>
        private static void DragRandomly(
            System.Action<string, LevelContext, BoardState, DragController> onPosition,
            System.Action<string, LevelContext, BoardState, Move?> onRelease)
        {
            var rng = new System.Random(RandomPathSeed);

            foreach (var (name, ctx, _) in SearchCorpus.WholeCorpus())
            {
                var state = BoardState.CreateInitial(ctx);
                for (var i = 0; i < ctx.TotalBlockCapacity; i++)
                {
                    if (!state.CanMove(ctx, i))
                    {
                        continue;
                    }

                    var grabCell = state.Origins[i] + ctx.SpecAt(i).Cells[0];
                    for (var d = 0; d < DragsPerBlock; d++)
                    {
                        var drag = SmoothedDrag();
                        Assert.IsTrue(drag.TryBegin(ctx, state, At(grabCell.X, grabCell.Y)), name);

                        Vector2 pointer = default;
                        for (var p = 0; p < PointerMovesPerDrag; p++)
                        {
                            pointer = new Vector2(
                                (float)(rng.NextDouble() * (ctx.Width + 2) - 1),
                                (float)(rng.NextDouble() * (ctx.Height + 2) - 1));
                            var seconds = rng.NextDouble() < LongFrameChance
                                ? Instant
                                : (float)(rng.NextDouble() * MaxShortFrameSeconds);
                            drag.Update(pointer, seconds);
                            onPosition(name, ctx, state, drag);
                        }

                        onRelease(name, ctx, state, drag.End(pointer));
                    }
                }
            }
        }

        // ----- Legality -----

        [Test]
        public void Update_OverRandomPointerPaths_EveryPositionIsLegalAndItsNearestOriginReachable()
        {
            var reachability = new BlockReachability();
            var positionsChecked = 0;

            DragRandomly(
                (name, ctx, state, drag) =>
                {
                    var position = drag.Position;
                    var start = state.Origins[drag.BlockIndex];
                    Assert.IsTrue(IsLegal(ctx, state, drag.BlockIndex, position), $"{name}: {position} is not legal");
                    Assert.IsTrue(
                        reachability.IsReachable(ctx, state, drag.BlockIndex, start, drag.NearestOrigin),
                        $"{name}: {drag.NearestOrigin} is not reachable from {start}");
                    positionsChecked++;
                },
                (name, ctx, state, move) => { });

            Assert.GreaterOrEqual(positionsChecked, MinimumPositionsChecked, "too few positions checked to mean anything");
        }

        [Test]
        public void Update_PointerJumpsAcrossAWall_StopsFlushOnItsOwnSide()
        {
            var ctx = Ctx(5, 1, new[] { Block(1, new Coord(0, 0)) }, new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Blue) },
                staticWalls: new[] { new Coord(2, 0) });
            var state = BoardState.CreateInitial(ctx);
            var drag = InstantDrag();
            drag.TryBegin(ctx, state, At(0, 0));

            var position = drag.Update(At(4, 0), Instant);

            Assert.AreEqual(new Vector2(1f, 0f), position);
        }

        [TestCase(false, TestName = "Update_IntoAWall_StopsExactlyFlush")]
        [TestCase(true, TestName = "Update_IntoAnotherBlock_StopsExactlyFlush")]
        public void Update_IntoAnObstacle_StopsExactlyFlush(bool obstacleIsBlock)
        {
            // 4x1 board, dragged block at (0, 0), obstacle at (3, 0).
            var obstacle = new Coord(3, 0);
            var blocks = obstacleIsBlock
                ? new[] { Block(1, new Coord(0, 0)), Block(2, obstacle, colors: new[] { BlockColor.Blue }) }
                : new[] { Block(1, new Coord(0, 0)) };
            var walls = obstacleIsBlock ? null : new[] { obstacle };
            var ctx = Ctx(4, 1, blocks, new[] { Gate(1, BoardEdge.Left, 0, 1, BlockColor.Green) }, staticWalls: walls);
            var state = BoardState.CreateInitial(ctx);
            var drag = InstantDrag();
            drag.TryBegin(ctx, state, At(0, 0));

            var between = drag.Update(At(1, 0) + new Vector2(0.4f, 0f), Instant);
            var stopped = drag.Update(At(3, 0), Instant);

            Assert.AreEqual(1.4f, between.x, Tolerance);
            Assert.AreEqual(new Vector2(2f, 0f), stopped);
        }

        [Test]
        public void Update_OnlyFreeNeighbourIsDiagonal_NeverLeavesStart()
        {
            // 2x2 board: the block at (0, 0), walls at (1, 0) and (0, 1), and
            // the diagonal cell (1, 1) free.
            var ctx = Ctx(2, 2, new[] { Block(1, new Coord(0, 0)) }, new[] { Gate(1, BoardEdge.Left, 0, 1, BlockColor.Blue) },
                staticWalls: new[] { new Coord(1, 0), new Coord(0, 1) });
            var state = BoardState.CreateInitial(ctx);
            var pointers = new[]
            {
                At(1, 1), At(1, 0), At(0, 1), At(1, 1) + new Vector2(0.4f, 0.4f), At(0, 0) + new Vector2(0.45f, 0.45f), At(1, 1)
            };

            foreach (var drag in new[] { InstantDrag(), SmoothedDrag() })
            {
                drag.TryBegin(ctx, state, At(0, 0));
                foreach (var pointer in pointers)
                {
                    var position = drag.Update(pointer, FrameSeconds);

                    Assert.AreEqual(Vector2.zero, position, $"pointer {pointer}");
                }
            }
        }

        [TestCase(MovementAxis.HorizontalOnly, 3, 1)]
        [TestCase(MovementAxis.VerticalOnly, 1, 3)]
        public void Update_AxisRestrictedBlock_KeepsForbiddenCoordinateFixed(MovementAxis axis, int expectedX, int expectedY)
        {
            var ctx = Ctx(4, 4, new[] { Block(1, new Coord(1, 1), axis: axis) }, new[] { Gate(1, BoardEdge.Top, 0, 1, BlockColor.Blue) });
            var state = BoardState.CreateInitial(ctx);
            var drag = InstantDrag();
            drag.TryBegin(ctx, state, At(1, 1));

            var slightlyAcross = drag.Update(At(1, 1) + new Vector2(0.3f, 0.3f), Instant);
            var diagonal = drag.Update(At(3, 3), Instant);

            if (axis == MovementAxis.HorizontalOnly)
            {
                Assert.AreEqual(1f, slightlyAcross.y);
            }
            else
            {
                Assert.AreEqual(1f, slightlyAcross.x);
            }

            Assert.AreEqual(new Vector2(expectedX, expectedY), diagonal);
        }

        [Test]
        public void Update_NaNPointer_LeavesPositionUnchanged()
        {
            var ctx = Ctx(4, 1, new[] { Block(1, new Coord(0, 0)) }, new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Blue) });
            var state = BoardState.CreateInitial(ctx);
            var drag = InstantDrag();
            drag.TryBegin(ctx, state, At(0, 0));
            var before = drag.Update(At(1, 0), Instant);

            var afterNaN = drag.Update(new Vector2(float.NaN, 0.5f), Instant);
            var positionAfterNaN = drag.Position;
            var afterValid = drag.Update(At(2, 0), Instant);

            Assert.AreEqual(before, afterNaN);
            Assert.AreEqual(before, positionAfterNaN);
            Assert.AreEqual(new Vector2(2f, 0f), afterValid, "a NaN pointer must not poison the smoothed pointer");
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
            var drag = InstantDrag();

            var begun = drag.TryBegin(ctx, state, grab);

            Assert.IsFalse(begun);
            Assert.IsFalse(drag.IsDragging);
            Assert.AreEqual(-1, drag.BlockIndex);
        }

        // ----- Corner assist -----

        /// <summary>
        /// 4x2 board: the block at (0, 0), walls at (2, 1) and (3, 1), so row 0
        /// from x = 2 is a one-cell corridor and row 1 is open only at x = 0
        /// and 1.
        /// </summary>
        private static LevelContext CorridorBoard() =>
            Ctx(
                4, 2,
                new[] { Block(1, new Coord(0, 0)) },
                new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Blue) },
                staticWalls: new[] { new Coord(2, 1), new Coord(3, 1) });

        [Test]
        public void Update_OffACorridorWithinAssistDistance_SlidesIntoIt()
        {
            var ctx = CorridorBoard();
            var state = BoardState.CreateInitial(ctx);
            var drag = InstantDrag();
            drag.TryBegin(ctx, state, At(0, 0));
            drag.Update(At(0, 0) + new Vector2(0f, WithinAssist), Instant);

            var position = drag.Update(At(3, 0) + new Vector2(0f, WithinAssist), Instant);

            Assert.AreEqual(new Vector2(3f, 0f), position);
        }

        [Test]
        public void Update_OffACorridorBeyondAssistDistance_WaitsFlushAtTheCorner()
        {
            var ctx = CorridorBoard();
            var state = BoardState.CreateInitial(ctx);
            var drag = InstantDrag();
            drag.TryBegin(ctx, state, At(0, 0));
            drag.Update(At(0, 0) + new Vector2(0f, BeyondAssist), Instant);

            var position = drag.Update(At(3, 0) + new Vector2(0f, BeyondAssist), Instant);

            Assert.AreEqual(1f, position.x);
            Assert.AreEqual(BeyondAssist, position.y, Tolerance);
        }

        [Test]
        public void Update_CornerAssist_NudgesNoFurtherThanTheBlockedAxisWantedToTravel()
        {
            // Target only 0.1 cells past the corner: the nudge is capped at 0.1.
            const float pastCorner = 0.1f;
            var ctx = CorridorBoard();
            var state = BoardState.CreateInitial(ctx);
            var drag = InstantDrag();
            drag.TryBegin(ctx, state, At(0, 0));
            drag.Update(At(0, 0) + new Vector2(0f, WithinAssist), Instant);

            var position = drag.Update(At(1, 0) + new Vector2(pastCorner, WithinAssist), Instant);

            Assert.AreEqual(1f, position.x);
            Assert.AreEqual(WithinAssist - pastCorner, position.y, Tolerance);
        }

        [Test]
        public void Update_CornerAssistNearACorridorMouth_NeverProducesAnIllegalPosition()
        {
            const int offsetSteps = 20;
            const int reachSteps = 25;
            const float reachCells = 2.5f;
            var ctx = CorridorBoard();
            var state = BoardState.CreateInitial(ctx);
            var positionsChecked = 0;

            for (var o = 0; o <= offsetSteps; o++)
            {
                var offset = (float)o / offsetSteps;
                for (var r = 0; r <= reachSteps; r++)
                {
                    var reach = reachCells * r / reachSteps;
                    var drag = InstantDrag(DragSettings.MaxCornerAssistCellsExclusive - Tolerance);
                    drag.TryBegin(ctx, state, At(0, 0));
                    drag.Update(At(0, 0) + new Vector2(0f, offset), Instant);
                    Assert.IsTrue(IsLegal(ctx, state, 0, drag.Position), $"{drag.Position} is not legal");

                    var position = drag.Update(At(0, 0) + new Vector2(reach, offset), Instant);

                    Assert.IsTrue(IsLegal(ctx, state, 0, position), $"offset {offset}, reach {reach}: {position} is not legal");
                    positionsChecked++;
                }
            }

            Assert.AreEqual((offsetSteps + 1) * (reachSteps + 1), positionsChecked);
        }

        // ----- Smoothing -----

        /// <summary>A 10x10 board with one block at (0, 0) and nothing in its way.</summary>
        private static LevelContext FreeBoard() =>
            Ctx(10, 10, new[] { Block(1, new Coord(0, 0)) }, new[] { Gate(1, BoardEdge.Left, 0, 1, BlockColor.Blue) });

        [Test]
        public void Update_StillPointer_ApproachesAndConvergesOnIt()
        {
            var ctx = FreeBoard();
            var state = BoardState.CreateInitial(ctx);
            var drag = SmoothedDrag();
            drag.TryBegin(ctx, state, At(0, 0));
            var target = new Vector2(5f, 5f);
            var pointer = At(5, 5);

            var first = drag.Update(pointer, FrameSeconds);
            var previousGap = (target - first).magnitude;
            var gaps = new List<float>();
            for (var f = 0; f < ConvergenceFrames; f++)
            {
                gaps.Add((target - drag.Update(pointer, FrameSeconds)).magnitude);
            }

            Assert.Greater(previousGap, Tolerance, "the block lags a moving pointer on the first frame");
            foreach (var gap in gaps)
            {
                Assert.LessOrEqual(gap, previousGap);
                previousGap = gap;
            }

            Assert.Less(previousGap, Tolerance);
        }

        [Test]
        public void Update_OneDoubleFrameAndTwoFrames_ArriveAtNearlyTheSamePosition()
        {
            var ctx = FreeBoard();
            var state = BoardState.CreateInitial(ctx);
            var once = SmoothedDrag();
            var twice = SmoothedDrag();
            once.TryBegin(ctx, state, At(0, 0));
            twice.TryBegin(ctx, state, At(0, 0));
            var pointer = At(6, 4);

            var afterDouble = once.Update(pointer, 2f * FrameSeconds);
            twice.Update(pointer, FrameSeconds);
            var afterTwo = twice.Update(pointer, FrameSeconds);

            Assert.AreEqual(afterDouble.x, afterTwo.x, Tolerance);
            Assert.AreEqual(afterDouble.y, afterTwo.y, Tolerance);
        }

        // ----- Release -----

        [Test]
        public void End_RoundsEachAxisToTheNearestWholeCell()
        {
            var ctx = FreeBoard();
            var state = BoardState.CreateInitial(ctx);
            var drag = InstantDrag();
            drag.TryBegin(ctx, state, At(0, 0));
            drag.Update(At(0, 0) + new Vector2(2.7f, 1.2f), Instant);

            var nearest = drag.NearestOrigin;
            var move = drag.End(At(0, 0) + new Vector2(2.7f, 1.2f));

            Assert.AreEqual(new Coord(3, 1), nearest);
            Assert.AreEqual(new Move(0, new Coord(3, 1)), move);
        }

        [TestCase(0.5f, 0f, 0)]
        [TestCase(-0.5f, 0f, 0)]
        [TestCase(0f, 0.5f, 0)]
        [TestCase(0f, -0.5f, 0)]
        [TestCase(1.5f, 0f, 1)]
        [TestCase(-1.5f, 0f, -1)]
        public void NearestOrigin_ExactHalf_RoundsTowardTheStart(float dx, float dy, int expectedDx)
        {
            // Block at (4, 4) in the middle of a free board.
            var start = new Coord(4, 4);
            var ctx = Ctx(10, 10, new[] { Block(1, start) }, new[] { Gate(1, BoardEdge.Left, 0, 1, BlockColor.Blue) });
            var state = BoardState.CreateInitial(ctx);
            var drag = InstantDrag();
            drag.TryBegin(ctx, state, At(start.X, start.Y));

            var position = drag.Update(At(start.X, start.Y) + new Vector2(dx, dy), Instant);

            Assert.AreEqual(new Vector2(start.X + dx, start.Y + dy), position);
            Assert.AreEqual(start + new Coord(expectedDx, 0), drag.NearestOrigin);
        }

        [Test]
        public void End_OverRandomPointerPaths_EveryMoveIsAcceptedByTheResolver()
        {
            var resolver = new MoveResolver();
            var movesChecked = 0;

            DragRandomly(
                (name, ctx, state, drag) => { },
                (name, ctx, state, move) =>
                {
                    if (!move.HasValue)
                    {
                        return;
                    }

                    Assert.IsTrue(resolver.TryApplyMove(ctx, state, move.Value, out _, out _), $"{name}: {move.Value} was rejected");
                    movesChecked++;
                });

            Assert.GreaterOrEqual(movesChecked, MinimumMovesChecked, "too few moves released to mean anything");
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
            var drag = InstantDrag();
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
            var drag = InstantDrag();
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
            var drag = InstantDrag();

            drag.TryBegin(ctx, state, At(0, 0));
            var towardGate = drag.End(Push(At(0, 0), toward, AboveThreshold));
            drag.TryBegin(ctx, state, At(0, 0));
            var towardOtherEdge = drag.End(Push(At(0, 0), away, AboveThreshold));

            Assert.AreEqual(new Move(0, new Coord(0, 0)), towardGate);
            Assert.IsNull(towardOtherEdge);
        }

        [Test]
        public void End_DraggedAwayAndBackNearStart_IsAPushCandidate()
        {
            // 3x3 board, block in the bottom-left corner, red gate on the left.
            // Brought back to 0.2 cells from its start, it rounds to the start.
            var ctx = Ctx(3, 3, new[] { Block(1, new Coord(0, 0)) }, new[] { Gate(1, BoardEdge.Left, 0, 1, BlockColor.Red) });
            var state = BoardState.CreateInitial(ctx);
            var drag = InstantDrag();
            drag.TryBegin(ctx, state, At(0, 0));
            drag.Update(At(2, 0), Instant);

            var back = drag.Update(At(0, 0) + new Vector2(BelowThreshold, 0f), Instant);
            var move = drag.End(Push(At(0, 0), Direction.Left, AboveThreshold), out var push);

            Assert.AreEqual(BelowThreshold, back.x, Tolerance);
            Assert.AreEqual(new Move(0, new Coord(0, 0)), move);
            Assert.AreEqual(Direction.Left, push);
        }

        [TestCase(Direction.Left, true)]
        [TestCase(Direction.Right, false)]
        public void End_AfterLeavingAndReturningToStart_TheFinalDirectionDecides(Direction finalPush, bool expectsPush)
        {
            // 3x3 board, block in the bottom-left corner, red gate on the left.
            var ctx = Ctx(3, 3, new[] { Block(1, new Coord(0, 0)) }, new[] { Gate(1, BoardEdge.Left, 0, 1, BlockColor.Red) });
            var state = BoardState.CreateInitial(ctx);
            var drag = InstantDrag();
            drag.TryBegin(ctx, state, At(0, 0));
            drag.Update(At(2, 0), Instant);

            var back = drag.Update(At(0, 0), Instant);
            var move = drag.End(Push(At(0, 0), finalPush, AboveThreshold));

            Assert.AreEqual(Vector2.zero, back);
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
            var drag = InstantDrag();
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
            var drag = InstantDrag();
            drag.TryBegin(ctx, state, At(0, 0));
            drag.Update(At(2, 0), Instant);

            var move = drag.End(At(2, 0), out var reported);

            Assert.AreEqual(new Move(0, new Coord(2, 0)), move);
            Assert.IsNull(reported);
        }

        [Test]
        public void End_DoesNotMoveTheBlockTowardTheReleasePointer()
        {
            // The block settles from where the player sees it: a release far
            // from the block's position is not swept toward first.
            var ctx = Ctx(4, 1, new[] { Block(1, new Coord(0, 0)) }, new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Red) });
            var state = BoardState.CreateInitial(ctx);
            var drag = InstantDrag();
            drag.TryBegin(ctx, state, At(0, 0));
            drag.Update(At(1, 0), Instant);

            var move = drag.End(At(3, 0));

            Assert.AreEqual(new Move(0, new Coord(1, 0)), move);
        }

        [Test]
        public void End_PushThatCannotClear_ReportsNoPushDirection()
        {
            var ctx = PushBoard();
            var state = BoardState.CreateInitial(ctx);
            var drag = InstantDrag();
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
            var drag = InstantDrag();
            drag.TryBegin(ctx, state, At(1, 1));

            var move = drag.End(Push(At(1, 1), Direction.Down, AboveThreshold));

            Assert.AreEqual(new Move(0, new Coord(1, 0)), move);
        }
    }
}
