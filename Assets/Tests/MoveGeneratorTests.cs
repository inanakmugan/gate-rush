using System;
using System.Collections.Generic;
using System.Linq;
using GateRush.Core;
using GateRush.Solver;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 04: exhaustive enumeration as the flood fill's full output,
    /// canonical pruning as a strict subset of it, zero-distance gate clears in
    /// both modes, the elevator-region criterion, and deterministic order.
    /// </summary>
    /// <remarks>
    /// The generator-spawn-cell canonical criterion is an extension point until
    /// phase 1.13 (no spawn-placement algorithm exists yet), so it has no test
    /// here — generators cannot appear in a level.
    /// </remarks>
    public class MoveGeneratorTests
    {
        private static readonly Coord[] Cell1x1 = { new Coord(0, 0) };

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

        private static ElevatorDefinition Elevator(int id, Coord min, Coord max)
        {
            return new ElevatorDefinition(id, min, max, waves: null);
        }

        private static LevelContext Ctx(
            int width,
            int height,
            IReadOnlyList<BlockDefinition> blocks,
            IReadOnlyList<GateDefinition> gates = null,
            IReadOnlyList<ElevatorDefinition> elevators = null,
            IReadOnlyList<Coord> staticWalls = null)
        {
            return new LevelContext(
                levelId: 1,
                width: width,
                height: height,
                staticWalls: staticWalls ?? Array.Empty<Coord>(),
                blocks: blocks,
                gates: gates ?? Array.Empty<GateDefinition>(),
                shutters: Array.Empty<ShutterDefinition>(),
                generators: Array.Empty<GeneratorDefinition>(),
                elevators: elevators ?? Array.Empty<ElevatorDefinition>(),
                suggestedTimeBudgetSeconds: 60,
                goldReward: 100);
        }

        private static MoveGenerator Generator() => new MoveGenerator();

        private static List<Move> Generate(LevelContext ctx, BoardState state, MoveGenMode mode) =>
            Generator().Generate(ctx, state, mode).ToList();

        private static HashSet<Move> GenerateSet(LevelContext ctx, BoardState state, MoveGenMode mode) =>
            new HashSet<Move>(Generator().Generate(ctx, state, mode));

        private static IEnumerable<Coord> TargetsFor(IEnumerable<Move> moves, int blockIndex) =>
            moves.Where(m => m.BlockIndex == blockIndex).Select(m => m.TargetOrigin);

        // ----- Exhaustive mode = the full flood fill --------------------------

        [Test]
        public void Generate_Exhaustive_EmitsOneMovePerReachablePosition_IncludingAroundCorners()
        {
            var ctx = Ctx(3, 3, new[] { Block(1, new Coord(0, 0)) });
            var state = BoardState.CreateInitial(ctx);

            var targets = TargetsFor(Generate(ctx, state, MoveGenMode.Exhaustive), 0).ToList();

            var everyCellButOrigin = new[]
            {
                new Coord(0, 1), new Coord(0, 2), new Coord(1, 0), new Coord(1, 1),
                new Coord(1, 2), new Coord(2, 0), new Coord(2, 1), new Coord(2, 2)
            };
            CollectionAssert.AreEquivalent(everyCellButOrigin, targets);
            CollectionAssert.Contains(targets, new Coord(2, 2)); // reached only by turning corners
        }

        [Test]
        public void Generate_Exhaustive_NoMoveToADiagonalCellWithBothOrthogonalApproachesBlocked()
        {
            var ctx = Ctx(
                3, 3,
                new[] { Block(1, new Coord(1, 1)) },
                staticWalls: new[] { new Coord(0, 1), new Coord(1, 0) });
            var state = BoardState.CreateInitial(ctx);

            var moves = Generate(ctx, state, MoveGenMode.Exhaustive);

            CollectionAssert.DoesNotContain(
                moves.Select(m => m.TargetOrigin).ToList(), new Coord(0, 0));
        }

        [Test]
        public void Generate_Exhaustive_AxisRestrictedBlockYieldsMovesInTwoDirectionsOnly()
        {
            var ctx = Ctx(
                3, 3, new[] { Block(1, new Coord(1, 1), axis: MovementAxis.HorizontalOnly) });
            var state = BoardState.CreateInitial(ctx);

            var targets = TargetsFor(Generate(ctx, state, MoveGenMode.Exhaustive), 0).ToList();

            CollectionAssert.AreEquivalent(
                new[] { new Coord(0, 1), new Coord(2, 1) }, targets);
        }

        // ----- Canonical is a pruned subset ----------------------------------

        /// <summary>Fixtures where both modes are meaningful. Reused by the
        /// subset and property tests.</summary>
        private static IEnumerable<(string name, LevelContext ctx, BoardState state)> Corpus()
        {
            var open3x3 = Ctx(3, 3, new[] { Block(1, new Coord(0, 0)) });
            yield return ("open 3x3, no gate", open3x3, BoardState.CreateInitial(open3x3));

            var twoBlocks = Ctx(
                5, 1, new[] { Block(1, new Coord(0, 0)), Block(2, new Coord(4, 0)) });
            yield return ("two blocks in a 5x1 corridor", twoBlocks, BoardState.CreateInitial(twoBlocks));

            var gated = Ctx(
                5, 5,
                new[] { Block(1, new Coord(2, 4)) },
                gates: new[] { Gate(1, BoardEdge.Bottom, 2, 1, BlockColor.Red) });
            yield return ("single block, one bottom gate", gated, BoardState.CreateInitial(gated));

            var restricted = Ctx(
                3, 3, new[] { Block(1, new Coord(1, 1), axis: MovementAxis.VerticalOnly) });
            yield return ("axis-restricted block, open board", restricted, BoardState.CreateInitial(restricted));

            var elevator = Ctx(
                5, 1,
                new[] { Block(1, new Coord(0, 0)) },
                elevators: new[] { Elevator(1, new Coord(3, 0), new Coord(3, 0)) });
            yield return ("single block, one elevator cell", elevator, BoardState.CreateInitial(elevator));
        }

        [Test]
        public void Generate_Canonical_IsAStrictSubsetOfExhaustive_OverTheCorpus()
        {
            var totalCanonical = 0;
            var totalExhaustive = 0;

            foreach (var (name, ctx, state) in Corpus())
            {
                var canonical = GenerateSet(ctx, state, MoveGenMode.Canonical);
                var exhaustive = GenerateSet(ctx, state, MoveGenMode.Exhaustive);

                Assert.IsTrue(canonical.IsSubsetOf(exhaustive), $"[{name}] canonical not a subset");
                totalCanonical += canonical.Count;
                totalExhaustive += exhaustive.Count;
            }

            Assert.Less(totalCanonical, totalExhaustive, "canonical pruned nothing across the corpus");
        }

        [Test]
        public void Generate_Canonical_EveryMoveIsPresentInTheExhaustiveSet()
        {
            foreach (var (name, ctx, state) in Corpus())
            {
                var exhaustive = GenerateSet(ctx, state, MoveGenMode.Exhaustive);

                foreach (var move in Generate(ctx, state, MoveGenMode.Canonical))
                {
                    Assert.IsTrue(exhaustive.Contains(move), $"[{name}] {move} missing from exhaustive");
                }
            }
        }

        [Test]
        public void Generate_Canonical_IncludesAPositionRestingAgainstAnotherBlock()
        {
            var ctx = Ctx(
                5, 1, new[] { Block(1, new Coord(0, 0)), Block(2, new Coord(4, 0)) });
            var state = BoardState.CreateInitial(ctx);

            var canonical = Generate(ctx, state, MoveGenMode.Canonical);

            // Block 0 can slide to (1,0), (2,0), (3,0); only (3,0) rests against block 2.
            CollectionAssert.AreEquivalent(
                new[] { new Coord(3, 0) }, TargetsFor(canonical, 0).ToList());
        }

        [Test]
        public void Generate_Canonical_IncludesAGateAlignedPositionThatRestsAgainstNothing()
        {
            var ctx = Ctx(
                5, 5,
                new[] { Block(1, new Coord(2, 4)) },
                gates: new[] { Gate(1, BoardEdge.Bottom, 2, 1, BlockColor.Red) });
            var state = BoardState.CreateInitial(ctx);

            var canonical = Generate(ctx, state, MoveGenMode.Canonical);

            // (2,0) is flush and aligned with the gate but has free cells on
            // three sides; it survives pruning only through the gate criterion.
            CollectionAssert.Contains(TargetsFor(canonical, 0).ToList(), new Coord(2, 0));
            // A neighbouring open-space position that matches no criterion is pruned.
            CollectionAssert.DoesNotContain(TargetsFor(canonical, 0).ToList(), new Coord(2, 1));
        }

        [Test]
        public void Generate_Canonical_IncludesAPositionThatOccupiesAnElevatorRegion()
        {
            var ctx = Ctx(
                5, 1,
                new[] { Block(1, new Coord(0, 0)) },
                elevators: new[] { Elevator(1, new Coord(3, 0), new Coord(3, 0)) });
            var state = BoardState.CreateInitial(ctx);

            var canonical = Generate(ctx, state, MoveGenMode.Canonical);

            // Sliding onto (3,0) enters the elevator region; the intermediate
            // open-space positions (1,0) and (2,0) match no criterion.
            CollectionAssert.AreEquivalent(
                new[] { new Coord(3, 0) }, TargetsFor(canonical, 0).ToList());
        }

        [Test]
        public void Generate_Canonical_IncludesAPositionThatVacatesAnElevatorRegion()
        {
            var ctx = Ctx(
                5, 1,
                new[] { Block(1, new Coord(3, 0)) },
                elevators: new[] { Elevator(1, new Coord(3, 0), new Coord(3, 0)) });
            var state = BoardState.CreateInitial(ctx);

            var canonical = Generate(ctx, state, MoveGenMode.Canonical);

            // Every move off (3,0) vacates the region, so (2,0) — which rests
            // against nothing — is kept where it would otherwise be pruned.
            CollectionAssert.Contains(TargetsFor(canonical, 0).ToList(), new Coord(2, 0));
        }

        // ----- Zero-distance moves (D25) ------------------------------------

        [TestCase(MoveGenMode.Canonical)]
        [TestCase(MoveGenMode.Exhaustive)]
        public void Generate_EmitsTheZeroDistanceMove_ForABlockFlushAgainstACompatibleOpenGate(
            MoveGenMode mode)
        {
            var ctx = Ctx(
                5, 5,
                new[] { Block(1, new Coord(2, 0)) },
                gates: new[] { Gate(1, BoardEdge.Bottom, 2, 1, BlockColor.Red) });
            var state = BoardState.CreateInitial(ctx);

            var moves = Generate(ctx, state, mode);

            CollectionAssert.Contains(moves, new Move(0, new Coord(2, 0)));
            Assert.AreEqual(new Move(0, new Coord(2, 0)), moves[0], "zero-distance move must be emitted first");
        }

        [TestCase(MoveGenMode.Canonical)]
        [TestCase(MoveGenMode.Exhaustive)]
        public void Generate_EmitsNoZeroDistanceMove_WhenTheBlockIsNotAtACompatibleGate(MoveGenMode mode)
        {
            var ctx = Ctx(
                5, 5,
                new[] { Block(1, new Coord(2, 2)) },
                gates: new[] { Gate(1, BoardEdge.Bottom, 2, 1, BlockColor.Red) });
            var state = BoardState.CreateInitial(ctx);

            var moves = Generate(ctx, state, mode);

            CollectionAssert.DoesNotContain(moves, new Move(0, new Coord(2, 2)));
        }

        [TestCase(MoveGenMode.Canonical)]
        [TestCase(MoveGenMode.Exhaustive)]
        public void Generate_OnAFullyPackedBoardWithOnePreAlignedBlock_EmitsExactlyOneMove(MoveGenMode mode)
        {
            var ctx = Ctx(
                2, 2,
                new[]
                {
                    Block(1, new Coord(0, 0), colors: new[] { BlockColor.Red }),
                    Block(2, new Coord(1, 0), colors: new[] { BlockColor.Blue }),
                    Block(3, new Coord(0, 1), colors: new[] { BlockColor.Green }),
                    Block(4, new Coord(1, 1), colors: new[] { BlockColor.Yellow })
                },
                gates: new[] { Gate(1, BoardEdge.Bottom, 0, 1, BlockColor.Red) });
            var state = BoardState.CreateInitial(ctx);

            var moves = Generate(ctx, state, mode);

            CollectionAssert.AreEqual(new[] { new Move(0, new Coord(0, 0)) }, moves);
        }

        // ----- Blocks that yield nothing ----------------------------------

        [Test]
        public void Generate_FrozenBlock_YieldsNoMoves()
        {
            var ctx = Ctx(3, 1, new[] { Block(1, new Coord(0, 0), unfreezeAt: 5) });
            var state = BoardState.CreateInitial(ctx);

            CollectionAssert.IsEmpty(Generate(ctx, state, MoveGenMode.Exhaustive));
        }

        [Test]
        public void Generate_LockedBlock_YieldsNoMovesForThatBlock()
        {
            var ctx = Ctx(
                3, 1,
                new[]
                {
                    Block(1, new Coord(0, 0), lockId: 9, requiredKeys: 1),
                    Block(2, new Coord(2, 0), keyTarget: 9)
                });
            var state = BoardState.CreateInitial(ctx);

            var moves = Generate(ctx, state, MoveGenMode.Exhaustive);

            Assert.IsFalse(moves.Any(m => m.BlockIndex == 0));
            Assert.IsTrue(moves.Any(m => m.BlockIndex == 1), "the key block should still move");
        }

        // ----- Deterministic order (concrete sequence, not just run-to-run) ---

        [Test]
        public void Generate_Exhaustive_ProducesThisExactSequence()
        {
            var ctx = Ctx(3, 3, new[] { Block(1, new Coord(0, 0)) });
            var state = BoardState.CreateInitial(ctx);

            var moves = Generate(ctx, state, MoveGenMode.Exhaustive);

            // Breadth-first from (0,0), expanding steps in Direction enum order
            // (Up, Down, Left, Right). Pinned so that reordering the step arrays
            // in BlockReachability — which would silently change every generated
            // ordering — fails a test.
            CollectionAssert.AreEqual(
                new[]
                {
                    new Move(0, new Coord(0, 1)),
                    new Move(0, new Coord(1, 0)),
                    new Move(0, new Coord(0, 2)),
                    new Move(0, new Coord(1, 1)),
                    new Move(0, new Coord(2, 0)),
                    new Move(0, new Coord(1, 2)),
                    new Move(0, new Coord(2, 1)),
                    new Move(0, new Coord(2, 2))
                },
                moves);
        }

        [Test]
        public void Generate_IsReproducibleAcrossRuns()
        {
            var ctx = Ctx(
                5, 5,
                new[] { Block(1, new Coord(2, 4)), Block(2, new Coord(0, 0)) },
                gates: new[] { Gate(1, BoardEdge.Bottom, 2, 1, BlockColor.Red) });
            var state = BoardState.CreateInitial(ctx);

            var first = Generate(ctx, state, MoveGenMode.Canonical);
            var second = Generate(ctx, state, MoveGenMode.Canonical);

            CollectionAssert.AreEqual(first, second);
        }

        // ----- The shared flood fill does not diverge from the resolver -------

        [TestCase(MoveGenMode.Canonical)]
        [TestCase(MoveGenMode.Exhaustive)]
        public void Generate_EveryEmittedMoveReplaysThroughMoveResolver(MoveGenMode mode)
        {
            // For Canonical this is D5's safety claim under test: pruning may only
            // drop playable moves, never invent unplayable ones.
            var ctx = Ctx(
                4, 4,
                new[] { Block(1, new Coord(0, 0)), Block(2, new Coord(3, 3)) },
                gates: new[] { Gate(1, BoardEdge.Left, 0, 1, BlockColor.Red) });
            var state = BoardState.CreateInitial(ctx);
            var resolver = new MoveResolver();

            var moves = Generate(ctx, state, mode);
            CollectionAssert.IsNotEmpty(moves);

            foreach (var move in moves)
            {
                Assert.IsTrue(
                    resolver.TryApplyMove(ctx, state, move, out _),
                    $"resolver rejected an enumerated {mode} move: {move}");
            }
        }
    }
}
