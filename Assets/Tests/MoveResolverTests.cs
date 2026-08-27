using System;
using System.Collections.Generic;
using GateRush.Core;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 03's Phase 1.3 scope: M1 (movement, gate exit, obstruction),
    /// M7 (axis restriction), zero-distance moves, and the joker entry points.
    /// </summary>
    /// <remarks>
    /// Deferred until later phases give the resolver something to chain on:
    /// a gate/shutter that opens mid-resolution, the broom cascade stress test,
    /// and the "resolution cycle throws" test. None can be built without the
    /// condition system (phase 1.7) or spawners (phase 1.13).
    /// </remarks>
    public class MoveResolverTests
    {
        private static readonly Coord[] Cell1x1 = { new Coord(0, 0) };
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

        private static BlockDefinition Block(
            int id,
            Coord start,
            IReadOnlyList<Coord> cells = null,
            IReadOnlyList<BlockColor> colors = null,
            MovementAxis axis = MovementAxis.Free,
            int? unfreezeAt = null,
            int? lockId = null,
            int requiredKeys = 0,
            int? keyTarget = null)
        {
            return new BlockDefinition(
                id: id,
                cells: cells ?? Cell1x1,
                colorStack: colors ?? new[] { BlockColor.Red },
                startOrigin: start,
                axis: axis,
                unfreezeAtClearCount: unfreezeAt,
                lockId: lockId,
                requiredKeyCount: requiredKeys,
                keyTargetLockId: keyTarget,
                keyEffect: KeyEffect.UnlockMovement,
                timeBonusSeconds: 0);
        }

        private static GateDefinition Gate(
            int id, BoardEdge edge, int offset, int width, BlockColor color, int? openAt = null)
        {
            return new GateDefinition(id, edge, offset, width, color, openAt);
        }

        private static LevelContext Ctx(
            int width,
            int height,
            IReadOnlyList<BlockDefinition> blocks,
            IReadOnlyList<GateDefinition> gates = null,
            IReadOnlyList<ShutterDefinition> shutters = null,
            IReadOnlyList<Coord> staticWalls = null)
        {
            return new LevelContext(
                levelId: 1,
                width: width,
                height: height,
                staticWalls: staticWalls ?? Array.Empty<Coord>(),
                blocks: blocks,
                gates: gates ?? Array.Empty<GateDefinition>(),
                shutters: shutters ?? Array.Empty<ShutterDefinition>(),
                generators: Array.Empty<GeneratorDefinition>(),
                elevators: Array.Empty<ElevatorDefinition>(),
                suggestedTimeBudgetSeconds: 60,
                goldReward: 100);
        }

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

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(2, 2)), out var result);

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

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(0, 0)), out var result);

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

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(3, 0)), out var result);

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

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(3, 0)), out var result);

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

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(2, 0)), out var result);

            Assert.IsFalse(moved);
            Assert.IsNull(result);
        }

        [Test]
        public void TryApplyMove_ReachesAnIntermediateCellAlongItsRoute()
        {
            var ctx = Ctx(4, 1, new[] { Block(1, new Coord(0, 0)) });
            var state = BoardState.CreateInitial(ctx);

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(2, 0)), out var result);

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

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(4, 0)), out var result);

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

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(2, 0)), out var result);

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

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(0, 2)), out var result);

            Assert.IsFalse(moved);
            Assert.IsNull(result);
        }

        // ----- Move legality gates: frozen, locked, shutter ----------------

        [Test]
        public void TryApplyMove_FrozenBlock_Fails()
        {
            var ctx = Ctx(3, 1, new[] { Block(1, new Coord(0, 0), unfreezeAt: 5) });
            var state = BoardState.CreateInitial(ctx);

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(1, 0)), out _);

            Assert.IsFalse(moved);
        }

        [Test]
        public void TryApplyMove_UnfrozenBlock_Succeeds()
        {
            var ctx = Ctx(3, 1, new[] { Block(1, new Coord(0, 0), unfreezeAt: 0) });
            var state = BoardState.CreateInitial(ctx);

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(1, 0)), out var result);

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

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(1, 0)), out _);

            Assert.IsFalse(moved);
        }

        [Test]
        public void TryApplyMove_IntoAClosedShutterRegion_Fails()
        {
            var ctx = Ctx(
                5, 1,
                new[] { Block(1, new Coord(0, 0)) },
                shutters: new[] { new ShutterDefinition(1, new Coord(2, 0), new Coord(2, 0), 3, null) });
            var state = BoardState.CreateInitial(ctx);

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(4, 0)), out _);

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

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(2, 0)), out var result);

            Assert.IsTrue(moved);
            Assert.IsFalse(result.Alive[0]);
            Assert.AreEqual(1, result.TotalClearCount);
        }

        [Test]
        public void TryApplyMove_ZeroDistanceWhenNotAtACompatibleGate_Fails()
        {
            var ctx = Ctx(5, 5, new[] { Block(1, new Coord(2, 2)) });
            var state = BoardState.CreateInitial(ctx);

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(2, 2)), out var result);

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

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(3, 0)), out var result);

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

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(2, 0)), out var result);

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

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(2, 0)), out var result);

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

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(0, 1)), out _);

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

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(0, 1)), out var result);

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

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(1, 0)), out _);

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

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(1, 0)), out var result);

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

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(2, 0)), out _);

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

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(2, 0)), out _);

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

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(2, 0)), out _);

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

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(1, 0)), out _);

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

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(2, 0)), out var result);

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

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(2, 0)), out var result);

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

            Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(2, 0)), out var result);

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
                ctx, BoardState.CreateInitial(ctx), new Move(0, new Coord(2, 0)), out var afterFirst);

            var movedAgain = resolver.TryApplyMove(ctx, afterFirst, new Move(0, new Coord(2, 0)), out _);

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

            Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(2, 0)), out var result);

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

            var moved = Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(2, 0)), out _);

            Assert.IsFalse(moved);
        }

        // ----- Plain repositioning --------------------------------------

        [Test]
        public void TryApplyMove_LegalNonGateMove_RepositionsWithoutClearing()
        {
            var ctx = Ctx(4, 4, new[] { Block(1, new Coord(0, 0)) });
            var state = BoardState.CreateInitial(ctx);

            Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(3, 3)), out var result);

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

            var cleared = Resolver().TryClearBlock(ctx, state, 0, out var result);

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
                shutters: new[] { new ShutterDefinition(1, new Coord(0, 0), new Coord(0, 0), 3, null) });
            var state = BoardState.CreateInitial(ctx);

            var cleared = Resolver().TryClearBlock(ctx, state, 0, out var result);

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

            var swept = Resolver().TrySweepColor(ctx, state, BlockColor.Red, out var result);

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

            var swept = Resolver().TrySweepColor(ctx, state, BlockColor.Green, out var result);

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

            var moved = resolver.TryApplyMove(ctx, state, new Move(0, new Coord(1, 0)), out _);

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
                () => resolver.TryApplyMove(ctx, state, new Move(0, new Coord(1, 0)), out _));
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

            Resolver().TryApplyMove(ctx, state, new Move(0, new Coord(2, 0)), out _);

            Assert.IsTrue(state.Alive[0]);
            Assert.AreEqual(0, state.TotalClearCount);
        }
    }
}
