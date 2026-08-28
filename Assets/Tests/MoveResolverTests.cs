using System;
using GateRush.Core;
using NUnit.Framework;
using static GateRush.Tests.Fixture;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 03's Phase 1.3 scope — M1 (movement, gate exit,
    /// obstruction), M7 (axis restriction), zero-distance moves, the joker entry
    /// points — and Module 06's Phase 1.7 additions: M2 (count-gated gates),
    /// M3 (count-gated blocks), M5's threshold evaluation (shutters opening),
    /// M10 (time-bonus output), the "opening never clears" rule (D25), and the
    /// fixpoint loop's first real chains.
    /// </summary>
    /// <remarks>
    /// Still deferred to phase 1.13 (spawners): a generator or elevator wave
    /// arriving mid-resolution. The "resolution cycle throws" case is exercised
    /// through <see cref="ScriptedLoopResolver"/> here and does not need real
    /// level data.
    /// </remarks>
    public class MoveResolverTests
    {
        private static readonly Coord[] CellsVertical1x2 = { new Coord(0, 0), new Coord(0, 1) };
        private static readonly Coord[] CellsHorizontal1x3 =
            { new Coord(0, 0), new Coord(1, 0), new Coord(2, 0) };
        private static readonly Coord[] Cells2x2 =
            { new Coord(0, 0), new Coord(1, 0), new Coord(0, 1), new Coord(1, 1) };

        // An L: only (0,0) touches a bottom edge, but the bounding box spans x 0..1,
        // so the projection onto the bottom edge is 2.
        private static readonly Coord[] CellsL =
            { new Coord(0, 0), new Coord(0, 1), new Coord(1, 1) };

        // An L filling the bottom-left of its bounding box: (0,0), (1,0), (0,1).
        private static readonly Coord[] CellsLCorner =
            { new Coord(0, 0), new Coord(1, 0), new Coord(0, 1) };

        private static MoveResolver Resolver() => new MoveResolver();

        /// <summary>
        /// Which fixpoint-loop hook <see cref="ScriptedLoopResolver"/> drives.
        /// Declared here, not nested inside the private
        /// <see cref="ScriptedLoopResolver"/>, because NUnit test methods must be
        /// public and a public method cannot take a parameter of a type nested
        /// in a private class (CS0051).
        /// </summary>
        public enum Hook
        {
            Conditions,
            Spawns
        }

        /// <summary>
        /// Drives the fixpoint loop from the test: whichever hook
        /// <paramref name="drivingHook"/> selects reports "something changed" for
        /// its first <c>changingPasses</c> calls, then settles; the other hook
        /// always reports no change. Pass <see cref="int.MaxValue"/> for a loop
        /// that never settles. Lets the loop's own mechanism be exercised
        /// through either extension point while both are still no-ops.
        /// </summary>
        private sealed class ScriptedLoopResolver : MoveResolver
        {
            private readonly Hook drivingHook;
            private readonly int changingPasses;

            internal ScriptedLoopResolver(Hook drivingHook, int changingPasses)
            {
                this.drivingHook = drivingHook;
                this.changingPasses = changingPasses;
            }

            /// <summary>How many times the driving hook has reported a change.
            /// The loop runs one pass more than this — the settling pass on
            /// which the hook finally reports no change.</summary>
            internal int ChangesReported { get; private set; }

            internal override bool ReevaluateConditions(LevelContext ctx, SuccessorBuilder builder) =>
                drivingHook == Hook.Conditions && ReportChange();

            internal override bool CheckSpawnTriggers(LevelContext ctx, SuccessorBuilder builder) =>
                drivingHook == Hook.Spawns && ReportChange();

            private bool ReportChange()
            {
                if (ChangesReported >= changingPasses)
                {
                    return false;
                }

                ChangesReported++;
                return true;
            }
        }

        // ----- Movement (M1, D27) --------------------------------------------

        [Test]
        public void TryApplyMove_TargetAroundACorner_Succeeds()
        {
            var ctx = Ctx(3, 3, new[] { Block(1, new Coord(0, 0)) });
            var state = BoardState.CreateInitial(ctx);

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(2, 2)), out var result, out _);

            Assert.IsTrue(moved);
            Assert.AreEqual(new Coord(2, 2), result.Origins[0]);
        }

        [Test]
        public void TryApplyMove_DiagonalCellWithBothOrthogonalApproachesBlocked_Fails()
        {
            var ctx = Ctx(
                3, 3,
                new[] { Block(1, new Coord(1, 1)) },
                staticWalls: new[] { new Coord(0, 1), new Coord(1, 0) });
            var state = BoardState.CreateInitial(ctx);

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(0, 0)), out var result, out _);

            Assert.IsFalse(moved);
            Assert.IsNull(result);
        }

        // Free cells form a 1-wide bent corridor: column 0 (y0..y3) plus row 0
        // (x0..x3), meeting at the corner (0,0). A 1x1 block flows around it; a
        // thicker block cannot fit the turn.
        private static Coord[] BentCorridorWalls() => new[]
        {
            new Coord(1, 1), new Coord(2, 1), new Coord(3, 1),
            new Coord(1, 2), new Coord(2, 2), new Coord(3, 2),
            new Coord(1, 3), new Coord(2, 3), new Coord(3, 3)
        };

        [Test]
        public void TryApplyMove_1x1TurnsTheCornerOfAOneWideCorridor_Succeeds()
        {
            var ctx = Ctx(
                4, 4,
                new[] { Block(1, new Coord(0, 3)) },
                staticWalls: BentCorridorWalls());
            var state = BoardState.CreateInitial(ctx);

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(3, 0)), out var result, out _);

            Assert.IsTrue(moved);
            Assert.AreEqual(new Coord(3, 0), result.Origins[0]);
        }

        [Test]
        public void TryApplyMove_Vertical1x2CannotLeaveTheCorridorArmThatA1x1FlowsThrough()
        {
            var ctx = Ctx(
                4, 4,
                new[] { Block(1, new Coord(0, 2), cells: CellsVertical1x2) },
                staticWalls: BentCorridorWalls());
            var state = BoardState.CreateInitial(ctx);

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(3, 0)), out var result, out _);

            Assert.IsFalse(moved);
            Assert.IsNull(result);
        }

        [Test]
        public void TryApplyMove_LShapedBlockAtTheCornerIsImmobileWhereA1x1WouldFlow()
        {
            var ctx = Ctx(
                4, 4,
                new[] { Block(1, new Coord(0, 0), cells: CellsLCorner) },
                staticWalls: BentCorridorWalls());
            var state = BoardState.CreateInitial(ctx);

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(2, 0)), out var result, out _);

            Assert.IsFalse(moved);
            Assert.IsNull(result);
        }

        [Test]
        public void TryApplyMove_ReachesAnIntermediateCellAlongItsRoute()
        {
            var ctx = Ctx(4, 1, new[] { Block(1, new Coord(0, 0)) });
            var state = BoardState.CreateInitial(ctx);

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(2, 0)), out var result, out _);

            Assert.IsTrue(moved);
            Assert.AreEqual(new Coord(2, 0), result.Origins[0]);
        }

        [Test]
        public void TryApplyMove_CannotPassThroughAnotherBlock()
        {
            var ctx = Ctx(
                5, 1,
                new[] { Block(1, new Coord(0, 0)), Block(2, new Coord(2, 0)) });
            var state = BoardState.CreateInitial(ctx);

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(4, 0)), out var result, out _);

            Assert.IsFalse(moved);
            Assert.IsNull(result);
        }

        // ----- Axis restriction (M7) ---------------------------------------

        [Test]
        public void TryApplyMove_AxisRestrictedBlockAlongItsAxis_Succeeds()
        {
            var ctx = Ctx(
                3, 3,
                new[] { Block(1, new Coord(0, 0), axis: MovementAxis.HorizontalOnly) });
            var state = BoardState.CreateInitial(ctx);

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(2, 0)), out var result, out _);

            Assert.IsTrue(moved);
            Assert.AreEqual(new Coord(2, 0), result.Origins[0]);
        }

        [Test]
        public void TryApplyMove_AxisRestrictedBlockPerpendicularTarget_Fails()
        {
            // A free block could reach (0,2) by turning a corner; the restricted
            // one cannot take a single vertical step.
            var ctx = Ctx(
                3, 3,
                new[] { Block(1, new Coord(0, 0), axis: MovementAxis.HorizontalOnly) });
            var state = BoardState.CreateInitial(ctx);

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(0, 2)), out var result, out _);

            Assert.IsFalse(moved);
            Assert.IsNull(result);
        }

        // ----- Move legality gates: frozen, locked, shutter ----------------

        [Test]
        public void TryApplyMove_FrozenBlock_Fails()
        {
            var ctx = Ctx(3, 1, new[] { Block(1, new Coord(0, 0), unfreezeAt: 5) });
            var state = BoardState.CreateInitial(ctx);

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(1, 0)), out _, out _);

            Assert.IsFalse(moved);
        }

        [Test]
        public void TryApplyMove_UnfrozenBlock_Succeeds()
        {
            var ctx = Ctx(3, 1, new[] { Block(1, new Coord(0, 0), unfreezeAt: 0) });
            var state = BoardState.CreateInitial(ctx);

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(1, 0)), out var result, out _);

            Assert.IsTrue(moved);
            Assert.AreEqual(new Coord(1, 0), result.Origins[0]);
        }

        [Test]
        public void TryApplyMove_LockedBlock_Fails()
        {
            var ctx = Ctx(
                3, 1,
                new[]
                {
                    Block(1, new Coord(0, 0), lockId: 9, requiredKeys: 1),
                    Block(2, new Coord(2, 0), keyTarget: 9)
                });
            var state = BoardState.CreateInitial(ctx);

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(1, 0)), out _, out _);

            Assert.IsFalse(moved);
        }

        [Test]
        public void TryApplyMove_IntoAClosedShutterRegion_Fails()
        {
            var ctx = Ctx(
                5, 1,
                new[] { Block(1, new Coord(0, 0)) },
                shutters: new[] { Shutter(1, new Coord(2, 0), new Coord(2, 0), 3) });
            var state = BoardState.CreateInitial(ctx);

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(4, 0)), out _, out _);

            Assert.IsFalse(moved);
        }

        // ----- Zero-distance moves (D25) ----------------------------------

        [Test]
        public void TryApplyMove_ZeroDistanceAtACompatibleOpenGate_ClearsTheBlock()
        {
            var ctx = Ctx(
                5, 5,
                new[] { Block(1, new Coord(2, 0)) },
                gates: new[] { Gate(1, BoardEdge.Bottom, 2, 1, BlockColor.Red) });
            var state = BoardState.CreateInitial(ctx);

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(2, 0)), out var result, out _);

            Assert.IsTrue(moved);
            Assert.IsFalse(result.Alive[0]);
            Assert.AreEqual(1, result.TotalClearCount);
        }

        [Test]
        public void TryApplyMove_ZeroDistanceWhenNotAtACompatibleGate_Fails()
        {
            var ctx = Ctx(5, 5, new[] { Block(1, new Coord(2, 2)) });
            var state = BoardState.CreateInitial(ctx);

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(2, 2)), out var result, out _);

            Assert.IsFalse(moved);
            Assert.IsNull(result);
        }

        [Test]
        public void TryApplyMove_SlidingPastACompatibleGateAndStoppingBeyondIt_DoesNotClear()
        {
            var ctx = Ctx(
                5, 5,
                new[] { Block(1, new Coord(0, 0)) },
                gates: new[] { Gate(1, BoardEdge.Bottom, 1, 1, BlockColor.Red) });
            var state = BoardState.CreateInitial(ctx);

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(3, 0)), out var result, out _);

            Assert.IsTrue(moved);
            Assert.IsTrue(result.Alive[0]);
            Assert.AreEqual(0, result.ClearedColors[0]);
            Assert.AreEqual(new Coord(3, 0), result.Origins[0]);
        }

        [Test]
        public void TryApplyMove_MovingOntoACompatibleOpenGate_ClearsImmediately()
        {
            // D25: there is no parking on a usable gate.
            var ctx = Ctx(
                5, 5,
                new[] { Block(1, new Coord(0, 0)) },
                gates: new[] { Gate(1, BoardEdge.Bottom, 2, 1, BlockColor.Red) });
            var state = BoardState.CreateInitial(ctx);

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(2, 0)), out var result, out _);

            Assert.IsTrue(moved);
            Assert.IsFalse(result.Alive[0]);
            Assert.AreEqual(1, result.TotalClearCount);
        }

        // ----- Gate compatibility (M1) -----------------------------------

        [Test]
        public void TryApplyMove_Vertical1x2ThroughAWidth1BottomGate_Exits()
        {
            var ctx = Ctx(
                5, 5,
                new[] { Block(1, new Coord(2, 0), cells: CellsVertical1x2) },
                gates: new[] { Gate(1, BoardEdge.Bottom, 2, 1, BlockColor.Red) });
            var state = BoardState.CreateInitial(ctx);

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(2, 0)), out var result, out _);

            Assert.IsTrue(moved);
            Assert.IsFalse(result.Alive[0]);
        }

        [Test]
        public void TryApplyMove_Vertical1x2AgainstAWidth1SideGate_IsRejected()
        {
            var ctx = Ctx(
                5, 5,
                new[] { Block(1, new Coord(0, 1), cells: CellsVertical1x2) },
                gates: new[] { Gate(1, BoardEdge.Left, 1, 1, BlockColor.Red) });
            var state = BoardState.CreateInitial(ctx);

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(0, 1)), out _, out _);

            Assert.IsFalse(moved);
        }

        [Test]
        public void TryApplyMove_Vertical1x2ThroughAWidth2SideGate_Exits()
        {
            var ctx = Ctx(
                5, 5,
                new[] { Block(1, new Coord(0, 1), cells: CellsVertical1x2) },
                gates: new[] { Gate(1, BoardEdge.Left, 1, 2, BlockColor.Red) });
            var state = BoardState.CreateInitial(ctx);

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(0, 1)), out var result, out _);

            Assert.IsTrue(moved);
            Assert.IsFalse(result.Alive[0]);
        }

        [Test]
        public void TryApplyMove_2x2BlockAgainstAWidth1Gate_IsRejected()
        {
            var ctx = Ctx(
                5, 5,
                new[] { Block(1, new Coord(1, 0), cells: Cells2x2) },
                gates: new[] { Gate(1, BoardEdge.Bottom, 1, 1, BlockColor.Red) });
            var state = BoardState.CreateInitial(ctx);

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(1, 0)), out _, out _);

            Assert.IsFalse(moved);
        }

        [Test]
        public void TryApplyMove_1x1ThroughAWidth2Gate_Exits()
        {
            var ctx = Ctx(
                5, 5,
                new[] { Block(1, new Coord(1, 0)) },
                gates: new[] { Gate(1, BoardEdge.Bottom, 1, 2, BlockColor.Red) });
            var state = BoardState.CreateInitial(ctx);

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(1, 0)), out var result, out _);

            Assert.IsTrue(moved);
            Assert.IsFalse(result.Alive[0]);
        }

        [Test]
        public void TryApplyMove_LBlockWhoseProjectionIs2_IsRejectedByAWidth1Gate()
        {
            var ctx = Ctx(
                5, 5,
                new[] { Block(1, new Coord(2, 0), cells: CellsL) },
                gates: new[] { Gate(1, BoardEdge.Bottom, 2, 1, BlockColor.Red) });
            var state = BoardState.CreateInitial(ctx);

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(2, 0)), out _, out _);

            Assert.IsFalse(moved);
        }

        [Test]
        public void TryApplyMove_ColorMismatchAtAGate_IsRejected()
        {
            var ctx = Ctx(
                5, 5,
                new[] { Block(1, new Coord(2, 0)) },
                gates: new[] { Gate(1, BoardEdge.Bottom, 2, 1, BlockColor.Blue) });
            var state = BoardState.CreateInitial(ctx);

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(2, 0)), out _, out _);

            Assert.IsFalse(moved);
        }

        [Test]
        public void TryApplyMove_ClosedGate_IsRejected()
        {
            var ctx = Ctx(
                5, 5,
                new[] { Block(1, new Coord(2, 0)) },
                gates: new[] { Gate(1, BoardEdge.Bottom, 2, 1, BlockColor.Red, openAt: 5) });
            var state = BoardState.CreateInitial(ctx);

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(2, 0)), out _, out _);

            Assert.IsFalse(moved);
        }

        [Test]
        public void TryApplyMove_ProjectionOnlyPartlyWithinTheGate_IsRejected()
        {
            var ctx = Ctx(
                6, 6,
                new[] { Block(1, new Coord(1, 0), cells: CellsHorizontal1x3) },
                gates: new[] { Gate(1, BoardEdge.Bottom, 2, 3, BlockColor.Red) });
            var state = BoardState.CreateInitial(ctx);

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(1, 0)), out _, out _);

            Assert.IsFalse(moved);
        }

        [Test]
        public void TryApplyMove_NonZeroMoveOntoAnIncompatibleColorGateMouth_SucceedsWithoutClearing()
        {
            // The move is legal and the block comes to rest in the gate mouth;
            // the colour just does not match, so nothing is cleared. Distinct
            // from the zero-distance path, where an incompatible gate fails the
            // whole move.
            var ctx = Ctx(
                5, 5,
                new[] { Block(1, new Coord(0, 0)) },
                gates: new[] { Gate(1, BoardEdge.Bottom, 2, 1, BlockColor.Blue) });
            var state = BoardState.CreateInitial(ctx);

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(2, 0)), out var result, out _);

            Assert.IsTrue(moved);
            Assert.IsTrue(result.Alive[0]);
            Assert.AreEqual(0, result.ClearedColors[0]);
            Assert.AreEqual(new Coord(2, 0), result.Origins[0]);
            Assert.AreEqual(0, result.TotalClearCount);
        }

        [Test]
        public void TryApplyMove_NonZeroMoveOntoAClosedButOtherwiseCompatibleGateMouth_SucceedsWithoutClearing()
        {
            var ctx = Ctx(
                5, 5,
                new[] { Block(1, new Coord(0, 0)) },
                gates: new[] { Gate(1, BoardEdge.Bottom, 2, 1, BlockColor.Red, openAt: 5) });
            var state = BoardState.CreateInitial(ctx);

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(2, 0)), out var result, out _);

            Assert.IsTrue(moved);
            Assert.IsTrue(result.Alive[0]);
            Assert.AreEqual(new Coord(2, 0), result.Origins[0]);
        }

        // ----- Layered blocks (step 3 of the algorithm) -------------------

        [Test]
        public void TryApplyMove_TwoColorBlockAtAGate_ClearsOnceAndStaysShowingTheSecondColor()
        {
            var ctx = Ctx(
                5, 5,
                new[] { Block(1, new Coord(2, 0), colors: new[] { BlockColor.Red, BlockColor.Blue }) },
                gates: new[] { Gate(1, BoardEdge.Bottom, 2, 1, BlockColor.Red) });
            var state = BoardState.CreateInitial(ctx);

            Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(2, 0)), out var result, out _);

            Assert.IsTrue(result.Alive[0]);
            Assert.AreEqual(1, result.ClearedColors[0]);
            Assert.AreEqual(BlockColor.Blue, result.CurrentColorOf(ctx, 0));
            Assert.AreEqual(new Coord(2, 0), result.Origins[0]);
        }

        [Test]
        public void TryApplyMove_TwoColorBlockAlreadyCleared_CannotClearAgainAtTheSameGate()
        {
            var ctx = Ctx(
                5, 5,
                new[] { Block(1, new Coord(2, 0), colors: new[] { BlockColor.Red, BlockColor.Blue }) },
                gates: new[] { Gate(1, BoardEdge.Bottom, 2, 1, BlockColor.Red) });
            var resolver = Resolver();
            resolver.TryApplyMove(
                ctx, BoardState.CreateInitial(ctx), new Move(0, new Coord(2, 0)), out var afterFirst, out _);

            var movedAgain = resolver.TryApplyMove(ctx, afterFirst, new Move(0, new Coord(2, 0)), out _, out _);

            Assert.IsFalse(movedAgain);
        }

        [Test]
        public void TryApplyMove_LayeredClear_CreditsTheColorThatWasRemovedNotTheOneBeneath()
        {
            var ctx = Ctx(
                5, 5,
                new[] { Block(1, new Coord(2, 0), colors: new[] { BlockColor.Red, BlockColor.Blue }) },
                gates: new[] { Gate(1, BoardEdge.Bottom, 2, 1, BlockColor.Red) });
            var state = BoardState.CreateInitial(ctx);

            Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(2, 0)), out var result, out _);

            Assert.AreEqual(1, result.ClearCountByColor[(int)BlockColor.Red]);
            Assert.AreEqual(0, result.ClearCountByColor[(int)BlockColor.Blue]);
        }

        // ----- Obstruction (M1) -----------------------------------------

        [Test]
        public void TryApplyMove_ABlockParkedAtAGate_BlocksAnotherBlockFromUsingIt()
        {
            // Block 2 (blue) starts flush against the red gate — authored there,
            // so it is not cleared — and obstructs it. Block 1 (red) cannot reach
            // the gate mouth.
            var ctx = Ctx(
                5, 5,
                new[]
                {
                    Block(1, new Coord(0, 0)),
                    Block(2, new Coord(2, 0), colors: new[] { BlockColor.Blue })
                },
                gates: new[] { Gate(1, BoardEdge.Bottom, 2, 1, BlockColor.Red) });
            var state = BoardState.CreateInitial(ctx);

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(2, 0)), out _, out _);

            Assert.IsFalse(moved);
        }

        // ----- Plain repositioning --------------------------------------

        [Test]
        public void TryApplyMove_LegalNonGateMove_RepositionsWithoutClearing()
        {
            var ctx = Ctx(4, 4, new[] { Block(1, new Coord(0, 0)) });
            var state = BoardState.CreateInitial(ctx);

            Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(3, 3)), out var result, out _);

            Assert.AreEqual(new Coord(3, 3), result.Origins[0]);
            Assert.IsTrue(result.Alive[0]);
            Assert.AreEqual(0, result.TotalClearCount);
        }

        // ----- Joker entry points (D9) ---------------------------------

        [Test]
        public void TryClearBlock_FrozenBlock_IsClearedEvenThoughItCannotMove()
        {
            var ctx = Ctx(3, 1, new[] { Block(1, new Coord(0, 0), unfreezeAt: 5) });
            var state = BoardState.CreateInitial(ctx);

            var cleared = Resolver().TryClearBlock(ctx, state, 0, out var result, out _);

            Assert.IsTrue(cleared);
            Assert.IsFalse(result.Alive[0]);
            Assert.AreEqual(1, result.TotalClearCount);
        }

        [Test]
        public void TryClearBlock_BlockUnderAClosedShutter_IsRejected()
        {
            var ctx = Ctx(
                3, 1,
                new[] { Block(1, new Coord(0, 0)) },
                shutters: new[] { Shutter(1, new Coord(0, 0), new Coord(0, 0), 3) });
            var state = BoardState.CreateInitial(ctx);

            var cleared = Resolver().TryClearBlock(ctx, state, 0, out var result, out _);

            Assert.IsFalse(cleared);
            Assert.IsNull(result);
        }

        [Test]
        public void TrySweepColor_ClearsEveryTargetableBlockShowingThatColor()
        {
            var ctx = Ctx(
                5, 1,
                new[]
                {
                    Block(1, new Coord(0, 0), colors: new[] { BlockColor.Red }),
                    Block(2, new Coord(1, 0), colors: new[] { BlockColor.Blue }),
                    Block(3, new Coord(2, 0), colors: new[] { BlockColor.Red })
                });
            var state = BoardState.CreateInitial(ctx);

            var swept = Resolver().TrySweepColor(ctx, state, BlockColor.Red, out var result, out _);

            Assert.IsTrue(swept);
            Assert.IsFalse(result.Alive[0]);
            Assert.IsTrue(result.Alive[1]);
            Assert.IsFalse(result.Alive[2]);
            Assert.AreEqual(2, result.TotalClearCount);
        }

        [Test]
        public void TrySweepColor_NoTargetableBlockMatches_ReturnsFalse()
        {
            var ctx = Ctx(3, 1, new[] { Block(1, new Coord(0, 0), colors: new[] { BlockColor.Red }) });
            var state = BoardState.CreateInitial(ctx);

            var swept = Resolver().TrySweepColor(ctx, state, BlockColor.Green, out var result, out _);

            Assert.IsFalse(swept);
            Assert.IsNull(result);
        }

        // ----- Fixpoint loop mechanism (D8) ---------------------------

        [TestCase(Hook.Conditions)]
        [TestCase(Hook.Spawns)]
        public void ResolveToFixpoint_WhenAHookKeepsChanging_RunsThatManyExtraPasses(Hook hook)
        {
            // Four-colour block so MaxResolutionPasses (4) leaves headroom for
            // three changing passes without tripping the iteration bound.
            var ctx = Ctx(
                3, 1,
                new[]
                {
                    Block(1, new Coord(0, 0),
                        colors: new[]
                        {
                            BlockColor.Red, BlockColor.Blue, BlockColor.Green, BlockColor.Yellow
                        })
                });
            var state = BoardState.CreateInitial(ctx);
            var resolver = new ScriptedLoopResolver(hook, changingPasses: 3);

            var moved = resolver.TryApplyMove(ctx, state, new Move(0, new Coord(1, 0)), out _, out _);

            Assert.IsTrue(moved);
            Assert.AreEqual(3, resolver.ChangesReported);
        }

        [TestCase(Hook.Conditions)]
        [TestCase(Hook.Spawns)]
        public void ResolveToFixpoint_WhenAHookNeverSettles_ThrowsOnceTheIterationBoundIsExceeded(Hook hook)
        {
            var ctx = Ctx(3, 1, new[] { Block(1, new Coord(0, 0)) });
            var state = BoardState.CreateInitial(ctx);
            var resolver = new ScriptedLoopResolver(hook, int.MaxValue);

            Assert.Throws<InvalidOperationException>(
                () => resolver.TryApplyMove(ctx, state, new Move(0, new Coord(1, 0)), out _, out _));
        }

        // ----- Successor independence ----------------------------------

        [Test]
        public void TryApplyMove_DoesNotMutateTheSourceState()
        {
            var ctx = Ctx(
                5, 5,
                new[] { Block(1, new Coord(2, 0)) },
                gates: new[] { Gate(1, BoardEdge.Bottom, 2, 1, BlockColor.Red) });
            var state = BoardState.CreateInitial(ctx);

            Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(2, 0)), out _, out _);

            Assert.IsTrue(state.Alive[0]);
            Assert.AreEqual(0, state.TotalClearCount);
        }

        // ----- M2: count-gated gates (phase 1.7) -------------------------

        [Test]
        public void CreateInitial_GateWithNoThreshold_IsOpen()
        {
            var ctx = Ctx(
                3, 1,
                new[] { Block(1, new Coord(0, 0)) },
                gates: new[] { Gate(1, BoardEdge.Left, 0, 1, BlockColor.Red) });

            Assert.IsTrue(BoardState.CreateInitial(ctx).GateOpen[0]);
        }

        [Test]
        public void TryApplyMove_CrossingAGateThreshold_OpensThatGateInTheSameResolution()
        {
            var ctx = Ctx(
                6, 1,
                new[] { Block(1, new Coord(0, 0)), Block(2, new Coord(4, 0)) },
                gates: new[]
                {
                    Gate(1, BoardEdge.Bottom, 0, 1, BlockColor.Red),
                    Gate(2, BoardEdge.Bottom, 4, 1, BlockColor.Red, openAt: 1)
                });
            var state = BoardState.CreateInitial(ctx);

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(0, 0)), out var result, out _);

            Assert.IsTrue(moved);
            Assert.IsFalse(state.GateOpen[1]);
            Assert.IsTrue(result.GateOpen[1]);
        }

        [Test]
        public void TryApplyMove_AnOpenedGateStaysOpenAcrossAFurtherMove()
        {
            var ctx = Ctx(
                6, 1,
                new[] { Block(1, new Coord(0, 0)), Block(2, new Coord(4, 0)) },
                gates: new[]
                {
                    Gate(1, BoardEdge.Bottom, 0, 1, BlockColor.Red),
                    Gate(2, BoardEdge.Bottom, 4, 1, BlockColor.Red, openAt: 1)
                });
            var resolver = Resolver();
            resolver.TryApplyMove(
                ctx, BoardState.CreateInitial(ctx), new Move(0, new Coord(0, 0)), out var afterOpen, out _);

            var moved = resolver.TryApplyMove(ctx, afterOpen, new Move(1, new Coord(4, 0)), out var result, out _);

            Assert.IsTrue(moved);
            Assert.IsFalse(result.Alive[1]);
            Assert.IsTrue(result.GateOpen[1]);
        }

        // ----- M3: count-gated (frozen) blocks (phase 1.7) --------------

        [Test]
        public void TryApplyMove_AFrozenBlockCannotMoveButStillObstructs()
        {
            var ctx = Ctx(
                4, 1,
                new[] { Block(1, new Coord(0, 0)), Block(2, new Coord(2, 0), unfreezeAt: 5) });
            var state = BoardState.CreateInitial(ctx);

            var movedFrozen = Resolver().TryApplyMove(ctx, state, new Move(1, new Coord(3, 0)), out _, out _);
            var blockedByFrozen = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(3, 0)), out _, out _);

            Assert.IsFalse(movedFrozen);
            Assert.IsFalse(blockedByFrozen);
        }

        [Test]
        public void TryApplyMove_CrossingTheUnfreezeThreshold_UnfreezesTheBlockInTheSameResolution()
        {
            var ctx = Ctx(
                6, 1,
                new[] { Block(1, new Coord(0, 0)), Block(2, new Coord(4, 0), unfreezeAt: 1) },
                gates: new[] { Gate(1, BoardEdge.Bottom, 0, 1, BlockColor.Red) });
            var resolver = Resolver();

            var cleared = resolver.TryApplyMove(
                ctx, BoardState.CreateInitial(ctx), new Move(0, new Coord(0, 0)), out var afterClear, out _);

            Assert.IsTrue(cleared);
            Assert.IsTrue(afterClear.Unfrozen[1]);

            var moved = resolver.TryApplyMove(ctx, afterClear, new Move(1, new Coord(5, 0)), out var result, out _);

            Assert.IsTrue(moved, "the unfrozen block now moves");
            Assert.AreEqual(new Coord(5, 0), result.Origins[1]);
        }

        [Test]
        public void TryApplyMove_AnUnfrozenBlockStaysUnfrozenAcrossLaterMoves()
        {
            var ctx = Ctx(
                6, 1,
                new[] { Block(1, new Coord(0, 0)), Block(2, new Coord(4, 0), unfreezeAt: 1) },
                gates: new[] { Gate(1, BoardEdge.Bottom, 0, 1, BlockColor.Red) });
            var resolver = Resolver();
            resolver.TryApplyMove(
                ctx, BoardState.CreateInitial(ctx), new Move(0, new Coord(0, 0)), out var afterClear, out _);
            resolver.TryApplyMove(ctx, afterClear, new Move(1, new Coord(5, 0)), out var afterMove, out _);

            var moved = resolver.TryApplyMove(ctx, afterMove, new Move(1, new Coord(3, 0)), out var result, out _);

            Assert.IsTrue(moved);
            Assert.IsTrue(result.Unfrozen[1]);
        }

        // ----- M5: shutters, threshold evaluation only (phase 1.7) ------

        [Test]
        public void TryApplyMove_ABlockUnderAClosedShutter_CannotBeMoved()
        {
            var ctx = Ctx(
                5, 1,
                new[] { Block(1, new Coord(2, 0)) },
                shutters: new[] { Shutter(1, new Coord(2, 0), new Coord(2, 0), 3) });
            var state = BoardState.CreateInitial(ctx);

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(3, 0)), out _, out _);

            Assert.IsFalse(moved);
        }

        [Test]
        public void TryApplyMove_AGlobalShutter_OpensOnTheTotalClearCount()
        {
            var ctx = Ctx(
                6, 2,
                new[] { Block(1, new Coord(0, 0)) },
                gates: new[] { Gate(1, BoardEdge.Bottom, 0, 1, BlockColor.Red) },
                shutters: new[] { Shutter(1, new Coord(3, 0), new Coord(3, 1), 1) });
            var state = BoardState.CreateInitial(ctx);

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(0, 0)), out var result, out _);

            Assert.IsTrue(moved);
            Assert.IsFalse(state.ShutterOpen[0]);
            Assert.IsTrue(result.ShutterOpen[0]);
        }

        [Test]
        public void TryApplyMove_AColourBoundShutter_OpensOnItsColourAndNotOnOthers()
        {
            var ctx = Ctx(
                7, 2,
                new[]
                {
                    Block(1, new Coord(0, 0), colors: new[] { BlockColor.Red }),
                    Block(2, new Coord(4, 0), colors: new[] { BlockColor.Blue })
                },
                gates: new[]
                {
                    Gate(1, BoardEdge.Bottom, 0, 1, BlockColor.Red),
                    Gate(2, BoardEdge.Bottom, 4, 1, BlockColor.Blue)
                },
                shutters: new[] { Shutter(1, new Coord(6, 0), new Coord(6, 1), 1, BlockColor.Blue) });
            var resolver = Resolver();

            resolver.TryApplyMove(
                ctx, BoardState.CreateInitial(ctx), new Move(0, new Coord(0, 0)), out var afterRed, out _);
            var movedBlue = resolver.TryApplyMove(ctx, afterRed, new Move(1, new Coord(4, 0)), out var afterBlue, out _);

            Assert.IsTrue(movedBlue);
            Assert.IsFalse(afterRed.ShutterOpen[0], "a red clear does not satisfy a blue-bound threshold");
            Assert.IsTrue(afterBlue.ShutterOpen[0]);
        }

        [Test]
        public void TryApplyMove_AfterAShutterOpens_TheBlockUnderItBecomesTargetable()
        {
            var ctx = Ctx(
                6, 2,
                new[] { Block(1, new Coord(0, 0)), Block(2, new Coord(3, 0)) },
                gates: new[] { Gate(1, BoardEdge.Bottom, 0, 1, BlockColor.Red) },
                shutters: new[] { Shutter(1, new Coord(3, 0), new Coord(3, 1), 1) });
            var state = BoardState.CreateInitial(ctx);

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(0, 0)), out var result, out _);

            Assert.IsTrue(moved);
            Assert.IsFalse(state.CanBeTargeted(ctx, 1));
            Assert.IsTrue(result.CanBeTargeted(ctx, 1));
        }

        // ----- Opening never clears — the three cases (D25) -------------

        [Test]
        public void TryApplyMove_GateOpensWithACompatibleBlockFlushAgainstIt_DoesNotClearIt()
        {
            var ctx = Ctx(
                6, 1,
                new[] { Block(1, new Coord(0, 0)), Block(2, new Coord(4, 0)) },
                gates: new[]
                {
                    Gate(1, BoardEdge.Bottom, 0, 1, BlockColor.Red),
                    Gate(2, BoardEdge.Bottom, 4, 1, BlockColor.Red, openAt: 1)
                });
            var resolver = Resolver();

            resolver.TryApplyMove(
                ctx, BoardState.CreateInitial(ctx), new Move(0, new Coord(0, 0)), out var afterOpen, out _);

            Assert.IsTrue(afterOpen.GateOpen[1]);
            Assert.IsTrue(afterOpen.Alive[1], "the waiting block is not cleared by the gate opening");
            Assert.AreEqual(0, afterOpen.ClearedColors[1]);

            var pushed = resolver.TryApplyMove(ctx, afterOpen, new Move(1, new Coord(4, 0)), out var afterPush, out _);

            Assert.IsTrue(pushed, "a zero-distance move then clears it");
            Assert.IsFalse(afterPush.Alive[1]);
            Assert.AreEqual(2, afterPush.TotalClearCount);
        }

        [Test]
        public void TryApplyMove_BlockUnfreezesWhileFlushAgainstACompatibleOpenGate_DoesNotClearIt()
        {
            var ctx = Ctx(
                6, 1,
                new[] { Block(1, new Coord(0, 0)), Block(2, new Coord(4, 0), unfreezeAt: 1) },
                gates: new[]
                {
                    Gate(1, BoardEdge.Bottom, 0, 1, BlockColor.Red),
                    Gate(2, BoardEdge.Bottom, 4, 1, BlockColor.Red)
                });
            var resolver = Resolver();

            resolver.TryApplyMove(
                ctx, BoardState.CreateInitial(ctx), new Move(0, new Coord(0, 0)), out var afterUnfreeze, out _);

            Assert.IsTrue(afterUnfreeze.Unfrozen[1]);
            Assert.IsTrue(afterUnfreeze.Alive[1], "unfreezing does not clear the block");
            Assert.AreEqual(0, afterUnfreeze.ClearedColors[1]);

            var pushed = resolver.TryApplyMove(ctx, afterUnfreeze, new Move(1, new Coord(4, 0)), out var afterPush, out _);

            Assert.IsTrue(pushed);
            Assert.IsFalse(afterPush.Alive[1]);
        }

        [Test]
        public void TryApplyMove_ShutterOpensExposingABlockFlushAgainstACompatibleOpenGate_DoesNotClearIt()
        {
            var ctx = Ctx(
                6, 2,
                new[] { Block(1, new Coord(0, 0)), Block(2, new Coord(4, 0)) },
                gates: new[]
                {
                    Gate(1, BoardEdge.Bottom, 0, 1, BlockColor.Red),
                    Gate(2, BoardEdge.Bottom, 4, 1, BlockColor.Red)
                },
                shutters: new[] { Shutter(1, new Coord(4, 0), new Coord(4, 1), 1) });
            var resolver = Resolver();

            resolver.TryApplyMove(
                ctx, BoardState.CreateInitial(ctx), new Move(0, new Coord(0, 0)), out var afterOpen, out _);

            Assert.IsTrue(afterOpen.ShutterOpen[0]);
            Assert.IsTrue(afterOpen.Alive[1], "the exposed block is not cleared by the shutter opening");
            Assert.AreEqual(0, afterOpen.ClearedColors[1]);

            var pushed = resolver.TryApplyMove(ctx, afterOpen, new Move(1, new Coord(4, 0)), out var afterPush, out _);

            Assert.IsTrue(pushed);
            Assert.IsFalse(afterPush.Alive[1]);
        }

        // ----- Chains: the fixpoint loop's first real work (D8) --------

        [Test]
        public void TryApplyMove_OneClearCrossesAGateAndAFrozenBlockThreshold_BothInOneResolution()
        {
            var ctx = Ctx(
                7, 1,
                new[]
                {
                    Block(1, new Coord(0, 0)),
                    Block(2, new Coord(4, 0)),
                    Block(3, new Coord(5, 0), unfreezeAt: 1)
                },
                gates: new[]
                {
                    Gate(1, BoardEdge.Bottom, 0, 1, BlockColor.Red),
                    Gate(2, BoardEdge.Bottom, 4, 1, BlockColor.Red, openAt: 1)
                });
            var state = BoardState.CreateInitial(ctx);

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(0, 0)), out var result, out _);

            Assert.IsTrue(moved);
            Assert.IsTrue(result.GateOpen[1]);
            Assert.IsTrue(result.Unfrozen[2]);
        }

        [Test]
        public void TrySweepColor_CrossesSeveralThresholdsInOneResolution()
        {
            var ctx = Ctx(
                6, 2,
                new[]
                {
                    Block(1, new Coord(0, 0), colors: new[] { BlockColor.Red }),
                    Block(2, new Coord(1, 0), colors: new[] { BlockColor.Red }),
                    Block(3, new Coord(2, 0), colors: new[] { BlockColor.Red }),
                    Block(4, new Coord(4, 0), colors: new[] { BlockColor.Blue }, unfreezeAt: 3)
                },
                gates: new[] { Gate(1, BoardEdge.Bottom, 0, 1, BlockColor.Red, openAt: 2) },
                shutters: new[] { Shutter(1, new Coord(5, 0), new Coord(5, 1), 3, BlockColor.Red) });
            var state = BoardState.CreateInitial(ctx);

            var swept = Resolver().TrySweepColor(ctx, state, BlockColor.Red, out var result, out _);

            Assert.IsTrue(swept);
            Assert.AreEqual(3, result.TotalClearCount);
            Assert.IsTrue(result.GateOpen[0], "gate threshold 2 crossed");
            Assert.IsTrue(result.Unfrozen[3], "frozen block threshold 3 crossed");
            Assert.IsTrue(result.ShutterOpen[0], "red-bound shutter threshold 3 crossed");
        }

        [Test]
        public void TryApplyMove_AColourBoundShutterAndAGlobalGate_CrossOnTheSameClear()
        {
            var ctx = Ctx(
                6, 2,
                new[] { Block(1, new Coord(0, 0), colors: new[] { BlockColor.Red }) },
                gates: new[]
                {
                    Gate(1, BoardEdge.Bottom, 0, 1, BlockColor.Red),
                    Gate(2, BoardEdge.Bottom, 3, 1, BlockColor.Green, openAt: 1)
                },
                shutters: new[] { Shutter(1, new Coord(5, 0), new Coord(5, 1), 1, BlockColor.Red) });
            var state = BoardState.CreateInitial(ctx);

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(0, 0)), out var result, out _);

            Assert.IsTrue(moved);
            Assert.IsTrue(result.GateOpen[1], "global-count gate crossed");
            Assert.IsTrue(result.ShutterOpen[0], "colour-bound shutter crossed");
        }

        // ----- M10: time-bonus blocks (phase 1.7) ----------------------

        [Test]
        public void TryApplyMove_ABlockWithNoBonus_ReportsZeroSeconds()
        {
            var ctx = Ctx(
                5, 5,
                new[] { Block(1, new Coord(2, 0)) },
                gates: new[] { Gate(1, BoardEdge.Bottom, 2, 1, BlockColor.Red) });
            var state = BoardState.CreateInitial(ctx);

            Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(2, 0)), out _, out var seconds);

            Assert.AreEqual(0, seconds);
        }

        [Test]
        public void TryApplyMove_ALayeredBonusBlock_ReportsItsSecondsOnlyWhenTheFinalColourClears()
        {
            var ctx = Ctx(
                5, 5,
                new[]
                {
                    Block(1, new Coord(2, 0),
                        colors: new[] { BlockColor.Red, BlockColor.Blue }, timeBonusSeconds: 7)
                },
                gates: new[]
                {
                    Gate(1, BoardEdge.Bottom, 2, 1, BlockColor.Red),
                    Gate(2, BoardEdge.Top, 2, 1, BlockColor.Blue)
                });
            var resolver = Resolver();

            resolver.TryApplyMove(
                ctx, BoardState.CreateInitial(ctx), new Move(0, new Coord(2, 0)), out var afterRed, out var redBonus);
            resolver.TryApplyMove(ctx, afterRed, new Move(0, new Coord(1, 4)), out var afterMove, out var moveBonus);
            resolver.TryApplyMove(ctx, afterMove, new Move(0, new Coord(2, 4)), out _, out var blueBonus);

            Assert.AreEqual(0, redBonus, "the red clear leaves the block alive");
            Assert.AreEqual(0, moveBonus, "the reposition past the top edge clears nothing");
            Assert.AreEqual(7, blueBonus, "the blue clear destroys the block");
        }

        [Test]
        public void TrySweepColor_KillingSeveralBonusBlocks_ReportsTheSum()
        {
            var ctx = Ctx(
                5, 1,
                new[]
                {
                    Block(1, new Coord(0, 0), colors: new[] { BlockColor.Red }, timeBonusSeconds: 4),
                    Block(2, new Coord(1, 0), colors: new[] { BlockColor.Red }, timeBonusSeconds: 5),
                    Block(3, new Coord(2, 0), colors: new[] { BlockColor.Red }, timeBonusSeconds: 6)
                });
            var state = BoardState.CreateInitial(ctx);

            var swept = Resolver().TrySweepColor(ctx, state, BlockColor.Red, out _, out var seconds);

            Assert.IsTrue(swept);
            Assert.AreEqual(15, seconds);
        }

        [Test]
        public void TryApplyMove_AFailedMove_ReportsZeroSeconds()
        {
            var ctx = Ctx(
                5, 1,
                new[]
                {
                    Block(1, new Coord(0, 0), timeBonusSeconds: 9),
                    Block(2, new Coord(2, 0))
                });
            var state = BoardState.CreateInitial(ctx);

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(4, 0)), out _, out var seconds);

            Assert.IsFalse(moved);
            Assert.AreEqual(0, seconds);
        }
    }
}
