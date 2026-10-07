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
    /// Module 22 (D49): an open gate pulls a block led toward it, the pulled
    /// position stays legal and short of its target, and a release within the
    /// capture range arrives at the gate.
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

        private const float PullRange = 0.8f;
        private const float PullAmount = 0.35f;
        private const float CaptureRange = 0.6f;
        private const float LongPullRange = 1.5f;
        private const float PullFollowRate = 25f;
        private const float SnapRate = 10000f;
        private const float NudgeMax = 0.15f;
        private const float WithinNudgeMax = 0.1f;
        private const float WithinCapture = 0.55f;
        private const float BeyondCapture = 0.65f;
        private const float BeyondPullRange = 0.9f;

        // The directed last leg of every second random drag: the pointer
        // leads the block to this far short of an exit origin, moving the
        // last LeadIn toward it.
        private const float ShortOfExit = WithinCapture;
        private const float LeadIn = 0.3f;
        private const int MinimumTargetsSeen = 5;
        private const int MinimumCapturesChecked = 5;

        private static readonly Coord[] Cells1x2Vertical = { new Coord(0, 0), new Coord(0, 1) };

        private static float Linear(float strength) => strength;

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

        /// <summary>
        /// With <see cref="Instant"/> frames, both the pointer and the pulled
        /// fraction reach what they follow in one update.
        /// </summary>
        private static DragController InstantDrag(float assist = Assist) =>
            new DragController(new DragSettings(
                Threshold, InstantRate, assist, PullRange, PullAmount, CaptureRange, InstantRate, NudgeMax, Linear));

        private static DragController SmoothedDrag() =>
            new DragController(new DragSettings(
                Threshold, FollowRate, Assist, PullRange, PullAmount, CaptureRange, PullFollowRate, NudgeMax, Linear));

        /// <summary>An instant drag with its own pull range and ease; the capture range stays <see cref="CaptureRange"/>.</summary>
        private static DragController InstantDragWithPull(float pullRange, System.Func<float, float> ease) =>
            new DragController(new DragSettings(
                Threshold, InstantRate, Assist, pullRange, PullAmount, CaptureRange, InstantRate, NudgeMax, ease));

        /// <summary>
        /// A drag whose pointer snaps — it reaches the real one within a
        /// <see cref="FrameSeconds"/> frame — while its pulled fraction is
        /// smoothed at <see cref="PullFollowRate"/>: the block's unpulled
        /// position is exact, and only the pull slides.
        /// </summary>
        private static DragController SlidingPullDrag(System.Func<float, float> ease = null) =>
            new DragController(new DragSettings(
                Threshold, SnapRate, Assist, PullRange, PullAmount, CaptureRange, PullFollowRate, NudgeMax,
                ease ?? Linear));

        /// <summary>
        /// The pointer that asks a block grabbed at its origin cell's centre to
        /// stand at <paramref name="x"/>, <paramref name="y"/>.
        /// </summary>
        private static Vector2 PointerFor(float x, float y) => new Vector2(x + 0.5f, y + 0.5f);

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
        /// jumps). Every second drag ends with a directed leg: the pointer
        /// leads the block toward a point just short of an origin where it
        /// would exit, so the gate pull and its capture are exercised and not
        /// left to chance. Calls <paramref name="onPosition"/> after every
        /// update and <paramref name="onRelease"/> with each release's result
        /// and whether it was a capture — the move went to the pull's origin
        /// although the nearest cell was another.
        /// </summary>
        private static void DragRandomly(
            System.Action<string, LevelContext, BoardState, DragController> onPosition,
            System.Action<string, LevelContext, BoardState, Move?, bool> onRelease)
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

                        if (d % 2 == 1 && TryPickLegTowardAnExit(rng, ctx, state, i, out var leadIn, out var shortOfExit))
                        {
                            var grabOffset = At(grabCell.X, grabCell.Y) - new Vector2(state.Origins[i].X, state.Origins[i].Y);
                            drag.Update(leadIn + grabOffset, Instant);
                            onPosition(name, ctx, state, drag);
                            pointer = shortOfExit + grabOffset;
                            drag.Update(pointer, Instant);
                            onPosition(name, ctx, state, drag);
                        }

                        var pull = drag.Pull;
                        var nearest = drag.NearestOrigin;
                        var move = drag.End(pointer);
                        var wasCaptured = pull.HasValue
                                          && move.HasValue
                                          && move.Value.TargetOrigin == pull.Value.Origin
                                          && pull.Value.Origin != nearest;
                        onRelease(name, ctx, state, move, wasCaptured);
                    }
                }
            }
        }

        /// <summary>
        /// Two block positions for a directed leg: one <see cref="ShortOfExit"/>
        /// short of a randomly chosen origin where block
        /// <paramref name="blockIndex"/> would exit, on a random side of it,
        /// and one <see cref="LeadIn"/> further back, so moving from the
        /// second to the first leads toward that origin. False when the block
        /// would exit nowhere. Whether the block can get there is the drag's
        /// business; a leg that is blocked still checks legality.
        /// </summary>
        private static bool TryPickLegTowardAnExit(
            System.Random rng, LevelContext ctx, BoardState state, int blockIndex, out Vector2 leadIn, out Vector2 shortOfExit)
        {
            var exits = new List<Coord>();
            for (var x = 0; x < ctx.Width; x++)
            {
                for (var y = 0; y < ctx.Height; y++)
                {
                    var origin = new Coord(x, y);
                    if (BlockReachability.FindExitGate(ctx, state, blockIndex, origin, null) >= 0)
                    {
                        exits.Add(origin);
                    }
                }
            }

            leadIn = default;
            shortOfExit = default;
            if (exits.Count == 0)
            {
                return false;
            }

            var exit = exits[rng.Next(exits.Count)];
            var sign = rng.Next(2) == 0 ? 1f : -1f;
            var back = rng.Next(2) == 0 ? new Vector2(sign, 0f) : new Vector2(0f, sign);
            var at = new Vector2(exit.X, exit.Y);
            leadIn = at - back * (ShortOfExit + LeadIn);
            shortOfExit = at - back * ShortOfExit;
            return true;
        }

        // ----- Legality -----

        [Test]
        public void Update_OverRandomPointerPaths_EveryPositionIsLegalAndItsNearestOriginReachable()
        {
            var reachability = new BlockReachability();
            var positionsChecked = 0;
            var targetsSeen = 0;

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

                    var pull = drag.Pull;
                    if (pull.HasValue && pull.Value.Origin != start)
                    {
                        Assert.IsTrue(
                            reachability.IsReachable(ctx, state, drag.BlockIndex, start, pull.Value.Origin),
                            $"{name}: the pull's origin {pull.Value.Origin} is not reachable from {start}");
                        targetsSeen++;
                    }
                },
                (name, ctx, state, move, wasCaptured) => { });

            Assert.GreaterOrEqual(positionsChecked, MinimumPositionsChecked, "too few positions checked to mean anything");
            Assert.GreaterOrEqual(targetsSeen, MinimumTargetsSeen, "too few pulled positions checked to mean anything");
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
            var capturesChecked = 0;

            DragRandomly(
                (name, ctx, state, drag) => { },
                (name, ctx, state, move, wasCaptured) =>
                {
                    if (!move.HasValue)
                    {
                        return;
                    }

                    Assert.IsTrue(resolver.TryApplyMove(ctx, state, move.Value, out _, out _), $"{name}: {move.Value} was rejected");
                    movesChecked++;
                    if (wasCaptured)
                    {
                        capturesChecked++;
                    }
                });

            Assert.GreaterOrEqual(movesChecked, MinimumMovesChecked, "too few moves released to mean anything");
            Assert.GreaterOrEqual(capturesChecked, MinimumCapturesChecked, "too few captured moves released to mean anything");
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

        // ----- Gate pull (Module 22) -----

        /// <summary>
        /// 4x1 board: a red block at (0, 0) and a gate on the right edge, red
        /// and open unless told otherwise. The block would arrive at its gate
        /// at <see cref="PullOrigin"/>.
        /// </summary>
        private static LevelContext PullBoard(BlockColor gateColor = BlockColor.Red, int? gateOpensAt = null) =>
            Ctx(4, 1, new[] { Block(1, new Coord(0, 0)) }, new[] { Gate(1, BoardEdge.Right, 0, 1, gateColor, gateOpensAt) });

        private static readonly Coord PullOrigin = new Coord(3, 0);

        /// <summary>A drag of <see cref="PullBoard"/>'s block, begun at its origin cell's centre.</summary>
        private static DragController BeginOn(LevelContext ctx, DragController drag)
        {
            Assert.IsTrue(drag.TryBegin(ctx, BoardState.CreateInitial(ctx), At(0, 0)));
            return drag;
        }

        [Test]
        public void Update_LedTowardACompatibleOpenGateWithinRange_TargetsItsArrivalOrigin()
        {
            // Within the pull range but outside the capture range: the block
            // is drawn PullAmount x Strength of the way to the origin.
            const float distance = 0.7f;
            var drag = BeginOn(PullBoard(), InstantDrag());

            var position = drag.Update(PointerFor(PullOrigin.X - distance, 0f), Instant);
            var pull = drag.Pull;

            var strength = 1f - distance / PullRange;
            Assert.IsTrue(pull.HasValue);
            Assert.AreEqual(PullOrigin, pull.Value.Origin);
            Assert.AreEqual(0, pull.Value.GateIndex);
            Assert.AreEqual(strength, pull.Value.Strength, Tolerance);
            Assert.AreEqual(PullOrigin.X - distance + PullAmount * strength * distance, position.x, Tolerance);
            Assert.AreEqual(0f, position.y);
            Assert.AreEqual(Vector2.zero, pull.Value.Nudge);
        }

        [Test]
        public void Update_OutOfPullRange_HasNoTarget()
        {
            var drag = BeginOn(PullBoard(), InstantDrag());

            var position = drag.Update(PointerFor(PullOrigin.X - BeyondPullRange, 0f), Instant);

            Assert.IsFalse(drag.Pull.HasValue);
            Assert.AreEqual(PullOrigin.X - BeyondPullRange, position.x, Tolerance);
        }

        [Test]
        public void Update_OffTheGatesLine_PullsBothCoordinatesTowardTheOriginByTheSameFraction()
        {
            // 4x3 board, red gate on the right edge at row 1: the block would
            // arrive at (3, 1). It stands 0.6 short of it and 0.3 above its
            // row: outside the capture range, so it is drawn part of the way.
            const float shortBy = 0.6f;
            const float above = 0.3f;
            var origin = new Coord(3, 1);
            var ctx = Ctx(4, 3, new[] { Block(1, new Coord(0, 0)) }, new[] { Gate(1, BoardEdge.Right, 1, 1, BlockColor.Red) });
            var state = BoardState.CreateInitial(ctx);
            var drag = InstantDrag();
            drag.TryBegin(ctx, state, At(0, 0));

            var position = drag.Update(PointerFor(origin.X - shortBy, origin.Y + above), Instant);
            var pull = drag.Pull;

            Assert.IsTrue(pull.HasValue);
            Assert.AreEqual(origin, pull.Value.Origin);
            var alongFraction = (position.x - (origin.X - shortBy)) / shortBy;
            var acrossFraction = (origin.Y + above - position.y) / above;
            Assert.Greater(alongFraction, 0f);
            Assert.Less(alongFraction, 1f);
            Assert.AreEqual(alongFraction, acrossFraction, 1e-3f);
            Assert.IsTrue(IsLegal(ctx, state, 0, position), $"{position} is not legal");
        }

        [Test]
        public void Update_TowardAnOriginAnotherBlockStandsOn_HasNoTarget()
        {
            // A blue block stands where the red one would arrive at its gate.
            // The red block stops flush beside it, one cell from that origin:
            // within the long range, but nothing is pulled through anything.
            var ctx = Ctx(
                4, 1,
                new[] { Block(1, new Coord(0, 0)), Block(2, PullOrigin, colors: new[] { BlockColor.Blue }) },
                new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Red) });
            var drag = BeginOn(ctx, InstantDragWithPull(LongPullRange, Linear));

            var position = drag.Update(PointerFor(PullOrigin.X, 0f), Instant);

            Assert.AreEqual(new Vector2(PullOrigin.X - 1, 0f), position);
            Assert.IsFalse(drag.Pull.HasValue);
        }

        [Test]
        public void Update_WithinTheLongRange_TargetsAnOriginMoreThanOneCellAhead()
        {
            // The same reach as the test above with nothing in the way: what
            // removes the target there is the block, not the distance.
            var drag = BeginOn(PullBoard(), InstantDragWithPull(LongPullRange, Linear));

            drag.Update(PointerFor(PullOrigin.X - 1, 0f), Instant);

            Assert.IsTrue(drag.Pull.HasValue);
            Assert.AreEqual(PullOrigin, drag.Pull.Value.Origin);
        }

        [Test]
        public void Update_TowardAClosedGate_HasNoTarget()
        {
            var drag = BeginOn(PullBoard(gateOpensAt: 1), InstantDrag());

            drag.Update(PointerFor(PullOrigin.X - WithinCapture, 0f), Instant);

            Assert.IsFalse(drag.Pull.HasValue);
        }

        [Test]
        public void Update_TowardAGateOfAnotherColour_HasNoTarget()
        {
            var drag = BeginOn(PullBoard(BlockColor.Blue), InstantDrag());

            drag.Update(PointerFor(PullOrigin.X - WithinCapture, 0f), Instant);

            Assert.IsFalse(drag.Pull.HasValue);
        }

        [Test]
        public void Update_AlongAForbiddenAxis_HasNoTarget()
        {
            // 3x2 board: the block at (1, 0), one cell left of where it would
            // arrive at the red gate on the right edge. Led right and a little
            // up, a free block is pulled; a vertical-only one (M7) is not.
            const float up = 0.2f;
            LevelContext Board(MovementAxis axis) => Ctx(
                3, 2,
                new[] { Block(1, new Coord(1, 0), axis: axis) },
                new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Red) });
            var freeCtx = Board(MovementAxis.Free);
            var restrictedCtx = Board(MovementAxis.VerticalOnly);
            var free = InstantDragWithPull(LongPullRange, Linear);
            var restricted = InstantDragWithPull(LongPullRange, Linear);
            free.TryBegin(freeCtx, BoardState.CreateInitial(freeCtx), At(1, 0));
            restricted.TryBegin(restrictedCtx, BoardState.CreateInitial(restrictedCtx), At(1, 0));
            var pointer = At(1, 0) + new Vector2(0.6f, up);

            free.Update(pointer, Instant);
            restricted.Update(pointer, Instant);

            Assert.IsTrue(free.Pull.HasValue, "the fixture pulls a block that may move that way");
            Assert.AreEqual(new Coord(2, 0), free.Pull.Value.Origin);
            Assert.IsFalse(restricted.Pull.HasValue);
            Assert.AreEqual(1f, restricted.Position.x);
        }

        [Test]
        public void Update_DraggedAwayFromTheGate_RemovesTheTargetHoweverClose()
        {
            const float close = 0.3f;
            const float backedOff = 0.35f;
            var drag = BeginOn(PullBoard(), InstantDrag());
            drag.Update(PointerFor(PullOrigin.X - close, 0f), Instant);
            var hadTarget = drag.Pull.HasValue;

            var position = drag.Update(PointerFor(PullOrigin.X - backedOff, 0f), Instant);

            Assert.IsTrue(hadTarget);
            Assert.IsFalse(drag.Pull.HasValue);
            Assert.AreEqual(PullOrigin.X - backedOff, position.x, Tolerance);
        }

        [Test]
        public void Update_PointerHeldStill_KeepsTheTarget()
        {
            var drag = BeginOn(PullBoard(), InstantDrag());
            var pointer = PointerFor(PullOrigin.X - WithinCapture, 0f);
            drag.Update(pointer, Instant);

            drag.Update(pointer, Instant);

            Assert.IsTrue(drag.Pull.HasValue);
            Assert.AreEqual(PullOrigin, drag.Pull.Value.Origin);
        }

        [Test]
        public void Update_SamePointerRepeated_PullDoesNotAccumulate()
        {
            // Outside the capture range, so the pull is partial: repeating the
            // update must not creep the block on toward the origin.
            const int repeats = 10;
            var drag = BeginOn(PullBoard(), InstantDrag());
            var pointer = PointerFor(PullOrigin.X - BeyondCapture, 0f);
            var first = drag.Update(pointer, Instant);

            var last = first;
            for (var i = 0; i < repeats; i++)
            {
                last = drag.Update(pointer, Instant);
            }

            Assert.Greater(first.x, PullOrigin.X - BeyondCapture + Tolerance, "the fixture is pulled");
            Assert.Less(first.x, PullOrigin.X - Tolerance, "the fixture is not latched");
            Assert.AreEqual(first, last);
        }

        [Test]
        public void Update_Pulled_LiesBetweenTheSweptPositionAndTheTargetAndNeverPassesIt()
        {
            // An ease that overshoots wildly: the strength used is still at
            // most 1, so the block never passes its target. Within the
            // capture range it is drawn on the target itself.
            const int steps = 15;
            const float stepCells = 0.05f;
            var ctx = PullBoard();
            var state = BoardState.CreateInitial(ctx);

            for (var i = 1; i <= steps; i++)
            {
                var swept = PullOrigin.X - i * stepCells;
                var drag = InstantDragWithPull(PullRange, strength => 3f);
                drag.TryBegin(ctx, state, At(0, 0));

                var position = drag.Update(PointerFor(swept, 0f), Instant);
                var pull = drag.Pull;

                Assert.IsTrue(pull.HasValue, $"swept {swept}");
                Assert.AreEqual(1f, pull.Value.Strength, $"swept {swept}");
                Assert.Greater(position.x, swept, $"swept {swept}");
                Assert.LessOrEqual(position.x, PullOrigin.X, $"swept {swept}");
                Assert.IsTrue(IsLegal(ctx, state, 0, position), $"swept {swept}: {position} is not legal");
            }
        }

        [Test]
        public void Update_EaseReturningNaN_PullsNothing()
        {
            // Outside the capture range, where the ease decides the strength.
            var drag = BeginOn(PullBoard(), InstantDragWithPull(PullRange, strength => float.NaN));

            var position = drag.Update(PointerFor(PullOrigin.X - BeyondCapture, 0f), Instant);

            Assert.AreEqual(0f, drag.Pull.Value.Strength);
            Assert.AreEqual(PullOrigin.X - BeyondCapture, position.x, Tolerance);
        }

        [Test]
        public void Update_PulledFraction_RisesAsTheBlockNearsTheOriginAndIsOneWithinCaptureRange()
        {
            var distances = new[] { 0.78f, 0.72f, 0.66f, 0.62f };
            var ctx = PullBoard();
            var fractions = new List<float>();

            foreach (var distance in distances)
            {
                var swept = PullOrigin.X - distance;
                var position = BeginOn(ctx, InstantDrag()).Update(PointerFor(swept, 0f), Instant);
                fractions.Add((position.x - swept) / distance);
            }

            var latched = BeginOn(ctx, InstantDrag());
            var atLatch = latched.Update(PointerFor(PullOrigin.X - WithinCapture, 0f), Instant);

            var previous = 0f;
            foreach (var fraction in fractions)
            {
                Assert.Greater(fraction, previous, "the fraction grows as the distance shrinks");
                Assert.LessOrEqual(fraction, PullAmount + Tolerance, "PullAmount is its most outside the capture range");
                previous = fraction;
            }

            Assert.AreEqual(new Vector2(PullOrigin.X, 0f), atLatch, "within the capture range the block is drawn on the origin");
            Assert.AreEqual(1f, latched.Pull.Value.Strength);
        }

        [Test]
        public void Update_WithinCaptureRange_SlidesOntoTheOriginAndStaysThere()
        {
            var swept = PullOrigin.X - WithinCapture;
            var drag = BeginOn(PullBoard(), SlidingPullDrag());
            var pointer = PointerFor(swept, 0f);

            var first = drag.Update(pointer, FrameSeconds);
            var later = new List<float>();
            for (var f = 0; f < ConvergenceFrames; f++)
            {
                later.Add(drag.Update(pointer, FrameSeconds).x);
            }

            Assert.Greater(first.x, swept + Tolerance, "the latch takes hold on the first frame");
            Assert.Less(first.x, PullOrigin.X - Tolerance, "the block slides into the latch; it does not jump");
            var previous = first.x;
            foreach (var x in later)
            {
                Assert.GreaterOrEqual(x, previous);
                Assert.LessOrEqual(x, PullOrigin.X);
                previous = x;
            }

            Assert.AreEqual(PullOrigin.X, previous, Tolerance);
        }

        [TestCase(WithinCapture)]
        [TestCase(BeyondCapture)]
        public void Update_Smoothing_NeverOvershootsTheOrigin(float distance)
        {
            // Frames of very different lengths and an ease that overshoots:
            // the block is never drawn past the origin, nor anywhere illegal.
            var frames = new[] { FrameSeconds, 2f * FrameSeconds, 0.001f, Instant, 0.5f * FrameSeconds };
            var ctx = PullBoard();
            var state = BoardState.CreateInitial(ctx);
            var drag = SlidingPullDrag(strength => 3f);
            drag.TryBegin(ctx, state, At(0, 0));
            var pointer = PointerFor(PullOrigin.X - distance, 0f);

            for (var f = 0; f < ConvergenceFrames; f++)
            {
                var position = drag.Update(pointer, frames[f % frames.Length]);

                Assert.LessOrEqual(position.x, PullOrigin.X, $"frame {f}");
                Assert.IsTrue(IsLegal(ctx, state, 0, position), $"frame {f}: {position} is not legal");
            }
        }

        [Test]
        public void Update_OneDoubleFrameAndTwoFrames_PullNearlyTheSame()
        {
            var ctx = PullBoard();
            var once = BeginOn(ctx, SlidingPullDrag());
            var twice = BeginOn(ctx, SlidingPullDrag());
            var pointer = PointerFor(PullOrigin.X - WithinCapture, 0f);

            var afterDouble = once.Update(pointer, 2f * FrameSeconds);
            twice.Update(pointer, FrameSeconds);
            var afterTwo = twice.Update(pointer, FrameSeconds);

            Assert.Less(afterDouble.x, PullOrigin.X - Tolerance, "the fixture is still sliding");
            Assert.AreEqual(afterDouble.x, afterTwo.x, Tolerance);
        }

        [Test]
        public void Update_TargetLost_DecaysBackToTheUnpulledPosition()
        {
            // Latched, then dragged a little away: the target goes at once,
            // but the block slides out of the latch instead of jumping back.
            const float backedOff = 0.6f;
            var unpulled = PullOrigin.X - backedOff;
            var drag = BeginOn(PullBoard(), SlidingPullDrag());
            var latched = drag.Update(PointerFor(PullOrigin.X - WithinCapture, 0f), Instant);
            var pointer = PointerFor(unpulled, 0f);

            var first = drag.Update(pointer, FrameSeconds);
            var hadTarget = drag.Pull.HasValue;
            var later = new List<float>();
            for (var f = 0; f < ConvergenceFrames; f++)
            {
                later.Add(drag.Update(pointer, FrameSeconds).x);
            }

            Assert.AreEqual(PullOrigin.X, latched.x, Tolerance, "the fixture was latched");
            Assert.IsFalse(hadTarget);
            Assert.Greater(first.x, unpulled + Tolerance, "the pull lets go gradually");
            Assert.Less(first.x, PullOrigin.X - Tolerance);
            var previous = first.x;
            foreach (var x in later)
            {
                Assert.LessOrEqual(x, previous);
                Assert.GreaterOrEqual(x, unpulled - Tolerance);
                previous = x;
            }

            Assert.AreEqual(unpulled, previous, Tolerance);
        }

        /// <summary>
        /// 4x2 board: a red block at (0, 0), a red gate on the right edge at
        /// row 0, so the block would arrive at (3, 0), and a wall at (3, 1)
        /// above that origin.
        /// </summary>
        private static LevelContext WalledPullBoard() =>
            Ctx(
                4, 2,
                new[] { Block(1, new Coord(0, 0)) },
                new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Red) },
                staticWalls: new[] { new Coord(3, 1) });

        [Test]
        public void Update_TargetLostAndTheLineBlocked_DropsThePullAtOnce()
        {
            // Latched, dragged away along the row — the line back to the
            // origin is free, so the pull decays — then up: the box between
            // the block and the origin now holds the wall, so the pull goes.
            const float awayX = 1.9f;
            const float upY = 0.6f;
            var ctx = WalledPullBoard();
            var state = BoardState.CreateInitial(ctx);
            var drag = SlidingPullDrag();
            drag.TryBegin(ctx, state, At(0, 0));
            drag.Update(PointerFor(PullOrigin.X - WithinCapture, 0f), Instant);

            var decaying = drag.Update(PointerFor(awayX, 0f), FrameSeconds);
            var dropped = drag.Update(PointerFor(awayX, upY), FrameSeconds);

            Assert.Greater(decaying.x, awayX + Tolerance, "along the free row the pull is still letting go");
            Assert.AreEqual(awayX, dropped.x, Tolerance);
            Assert.AreEqual(upY, dropped.y, Tolerance);
            Assert.IsTrue(IsLegal(ctx, state, 0, decaying), $"{decaying} is not legal");
            Assert.IsTrue(IsLegal(ctx, state, 0, dropped), $"{dropped} is not legal");
        }

        [Test]
        public void Update_ThroughLatchAndRelease_EveryPositionIsLegal()
        {
            // Approach, latch, push into the gate, back off, drag away, up
            // beside the wall, across and back down toward the gate.
            const int framesPerPointer = 3;
            var blockPositions = new[]
            {
                new Vector2(2.3f, 0f), new Vector2(2.45f, 0f), new Vector2(3.6f, 0f), new Vector2(2.9f, 0f),
                new Vector2(1.9f, 0f), new Vector2(1.9f, 0.6f), new Vector2(1.2f, 0.9f), new Vector2(2.6f, 0.9f),
                new Vector2(2.5f, 0f), new Vector2(2.95f, 0.2f)
            };
            var ctx = WalledPullBoard();
            var state = BoardState.CreateInitial(ctx);
            var drag = SlidingPullDrag();
            drag.TryBegin(ctx, state, At(0, 0));
            var positionsChecked = 0;
            var pulledPositions = 0;

            foreach (var blockPosition in blockPositions)
            {
                for (var f = 0; f < framesPerPointer; f++)
                {
                    var position = drag.Update(PointerFor(blockPosition.x, blockPosition.y), FrameSeconds);

                    Assert.IsTrue(IsLegal(ctx, state, 0, position), $"asked {blockPosition}, frame {f}: {position} is not legal");
                    positionsChecked++;
                    if (drag.Pull.HasValue)
                    {
                        pulledPositions++;
                    }
                }
            }

            Assert.AreEqual(blockPositions.Length * framesPerPointer, positionsChecked);
            Assert.Greater(pulledPositions, 0, "the path passes through the pull");
        }

        // The nudge follows the pointer's overshoot up to NudgeMax, whatever
        // the pull's amount and range are.
        [TestCase(WithinNudgeMax, WithinNudgeMax, TestName = "Update_AtAnOriginPushingALittleIntoItsGate_NudgesByTheOvershoot")]
        [TestCase(0.4f, NudgeMax, TestName = "Update_AtAnOriginPushingIntoItsGate_ReportsANudgeAndKeepsPositionLegal")]
        [TestCase(2f, NudgeMax, TestName = "Update_AtAnOriginPushingFarIntoItsGate_NudgesNoFurtherThanNudgeMax")]
        [TestCase(0f, 0f, TestName = "Update_AtAnOriginNotPushing_ReportsNoNudge")]
        public void Update_AtAnOrigin_NudgesByThePointersOvershoot(float overshoot, float expectedNudge)
        {
            var ctx = PullBoard();
            var state = BoardState.CreateInitial(ctx);
            var drag = InstantDrag();
            drag.TryBegin(ctx, state, At(0, 0));

            var position = drag.Update(PointerFor(PullOrigin.X + overshoot, 0f), Instant);
            var pull = drag.Pull;

            Assert.AreEqual(new Vector2(PullOrigin.X, 0f), position);
            Assert.IsTrue(IsLegal(ctx, state, 0, position));
            Assert.IsTrue(pull.HasValue);
            Assert.AreEqual(PullOrigin, pull.Value.Origin);
            Assert.AreEqual(1f, pull.Value.Strength);
            Assert.AreEqual(expectedNudge, pull.Value.Nudge.x, Tolerance);
            Assert.AreEqual(0f, pull.Value.Nudge.y);
        }

        [TestCase(BoardEdge.Left, Direction.Left, Direction.Down)]
        [TestCase(BoardEdge.Bottom, Direction.Down, Direction.Left)]
        public void Update_PushedInPlaceAtItsStart_NudgesOnlyTowardTheEdgeWithTheUsableGate(
            BoardEdge gateEdge, Direction toward, Direction away)
        {
            var start = new Coord(0, 0);
            var ctx = Ctx(3, 3, new[] { Block(1, start) }, new[] { Gate(1, gateEdge, 0, 1, BlockColor.Red) });
            var state = BoardState.CreateInitial(ctx);
            var towardGate = InstantDrag();
            var towardOtherEdge = InstantDrag();
            towardGate.TryBegin(ctx, state, At(0, 0));
            towardOtherEdge.TryBegin(ctx, state, At(0, 0));

            var pushedToward = towardGate.Update(Push(At(0, 0), toward, BelowThreshold), Instant);
            var pushedAway = towardOtherEdge.Update(Push(At(0, 0), away, BelowThreshold), Instant);

            Assert.AreEqual(Vector2.zero, pushedToward);
            Assert.AreEqual(Vector2.zero, pushedAway);
            Assert.IsTrue(towardGate.Pull.HasValue);
            Assert.AreEqual(start, towardGate.Pull.Value.Origin);
            Assert.Greater(BelowThreshold, NudgeMax, "the fixture pushes further than the nudge may go");
            Assert.AreEqual(
                NudgeMax,
                Vector2.Dot(towardGate.Pull.Value.Nudge, Push(Vector2.zero, toward, 1f)),
                Tolerance);
            Assert.IsFalse(towardOtherEdge.Pull.HasValue);
        }

        [Test]
        public void End_ReleasedWithinCaptureRange_ReturnsMoveToTheTarget()
        {
            // The central decision (D49): a release near an open gate clears.
            // The nearest cell is the one before the gate's.
            var ctx = PullBoard();
            var state = BoardState.CreateInitial(ctx);
            var drag = InstantDrag();
            drag.TryBegin(ctx, state, At(0, 0));
            var pointer = PointerFor(PullOrigin.X - WithinCapture, 0f);
            drag.Update(pointer, Instant);
            var nearest = drag.NearestOrigin;

            var move = drag.End(pointer, out var push);

            Assert.AreEqual(new Coord(PullOrigin.X - 1, 0), nearest);
            Assert.AreEqual(new Move(0, PullOrigin), move);
            Assert.IsNull(push);
            Assert.IsTrue(new MoveResolver().TryApplyMove(ctx, state, move.Value, out var after, out _));
            Assert.IsFalse(after.Alive[0], "the block arrived at its gate and cleared");
        }

        [Test]
        public void End_ReleasedJustOutsideCaptureRange_ReturnsTheNearestCell()
        {
            var drag = BeginOn(PullBoard(), InstantDrag());
            var pointer = PointerFor(PullOrigin.X - BeyondCapture, 0f);
            drag.Update(pointer, Instant);
            var wasPulled = drag.Pull.HasValue;

            var move = drag.End(pointer);

            Assert.IsTrue(wasPulled, "within the pull range, outside the capture range");
            Assert.AreEqual(new Move(0, new Coord(PullOrigin.X - 1, 0)), move);
        }

        [Test]
        public void End_ReleasedWhileDraggingAway_ReturnsTheNearestCell()
        {
            const float backedOff = 0.58f;
            var drag = BeginOn(PullBoard(), InstantDrag());
            drag.Update(PointerFor(PullOrigin.X - WithinCapture, 0f), Instant);
            var pointer = PointerFor(PullOrigin.X - backedOff, 0f);
            drag.Update(pointer, Instant);

            var move = drag.End(pointer);

            Assert.Less(backedOff, CaptureRange, "the fixture releases within the capture range");
            Assert.AreEqual(new Move(0, new Coord(PullOrigin.X - 1, 0)), move);
        }

        [Test]
        public void End_AtItsStartOnAGateWithoutAPush_IsNotCaptured()
        {
            // The block starts flush against its gate. Leaning on it below the
            // push threshold nudges it, but the release is D43's: no push, no
            // move.
            var ctx = Ctx(3, 3, new[] { Block(1, new Coord(0, 0)) }, new[] { Gate(1, BoardEdge.Left, 0, 1, BlockColor.Red) });
            var drag = BeginOn(ctx, InstantDrag());
            var pointer = Push(At(0, 0), Direction.Left, BelowThreshold);
            drag.Update(pointer, Instant);
            var wasNudged = drag.Pull.HasValue;

            var move = drag.End(pointer);

            Assert.IsTrue(wasNudged);
            Assert.IsNull(move);
        }

        [Test]
        public void End_ReturningTowardItsStartOnAGate_IsNeitherPulledNorCaptured()
        {
            // Dragged away and led back toward its start, which is flush
            // against its gate: the start is never a pull target, so the block
            // settles in the nearest cell, one away from it.
            const float fromStart = 0.55f;
            var ctx = Ctx(3, 3, new[] { Block(1, new Coord(0, 0)) }, new[] { Gate(1, BoardEdge.Left, 0, 1, BlockColor.Red) });
            var drag = BeginOn(ctx, InstantDrag());
            drag.Update(PointerFor(2f, 0f), Instant);
            var pointer = PointerFor(fromStart, 0f);

            var position = drag.Update(pointer, Instant);
            var hadTarget = drag.Pull.HasValue;
            var move = drag.End(pointer);

            Assert.IsFalse(hadTarget);
            Assert.AreEqual(fromStart, position.x, Tolerance);
            Assert.AreEqual(new Move(0, new Coord(1, 0)), move);
        }
    }
}
