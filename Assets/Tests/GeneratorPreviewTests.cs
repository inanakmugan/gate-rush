using System;
using GateRush.Core;
using GateRush.Runtime;
using NUnit.Framework;
using static GateRush.Tests.Fixture;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 26's <see cref="GeneratorPreview"/> (D53): a held block
    /// standing on a cell a generator's next block needs gives that generator,
    /// with the cells the resolver will spawn the block on and the colour the
    /// machine's screen shows; anything else gives nothing.
    /// </summary>
    /// <remarks>
    /// Grid convention as elsewhere: y = 0 is the bottom row. A left-edge
    /// generator at offset 0 spawns with its minimum corner on (0, 0).
    /// </remarks>
    public class GeneratorPreviewTests
    {
        private const int InFrontSlot = 0;
        private const int ElsewhereSlot = 1;
        private const int QueuedSlot = 2;

        private static readonly Coord[] Horizontal1x2 = { new Coord(0, 0), new Coord(1, 0) };
        private static readonly Coord[] Vertical2x1 = { new Coord(0, 0), new Coord(0, 1) };
        private static readonly Coord[] Corner = { new Coord(0, 0), new Coord(1, 0), new Coord(0, 1) };

        /// <summary>
        /// 3x3: a left-edge generator whose one queued block (slot 2), a blue
        /// horizontal 1x2, needs (0, 0) and (1, 0). Slot 0 stands on (1, 0),
        /// holding it back; slot 1 stands at (2, 2), on none of its cells.
        /// </summary>
        private static LevelContext BlockInFrontOfAGenerator() =>
            Ctx(
                3, 3,
                new[] { Block(1, new Coord(1, 0)), Block(2, new Coord(2, 2)) },
                generators: new[]
                {
                    Spawner(1, BoardEdge.Left, 0, 1, Spawned(colors: new[] { BlockColor.Blue }, cells: Horizontal1x2))
                });

        [Test]
        public void For_HeldBlockCoversOneOfTheNextBlocksCells_GivesThatGeneratorWithItsCellsAndColour()
        {
            var ctx = BlockInFrontOfAGenerator();
            var state = BoardState.CreateInitial(ctx);

            var targets = GeneratorPreview.For(ctx, state, InFrontSlot);

            Assert.AreEqual(1, targets.Count);
            Assert.AreEqual(0, targets[0].GeneratorIndex);
            Assert.That(targets[0].Cells, Is.EqualTo(new[] { new Coord(0, 0), new Coord(1, 0) }));
            Assert.AreEqual(BlockColor.Blue, targets[0].Color);
            Assert.IsFalse(targets[0].IsFrozen);
        }

        [Test]
        public void For_HeldBlockCoversNoneOfTheNextBlocksCells_GivesNothing()
        {
            var ctx = BlockInFrontOfAGenerator();
            var state = BoardState.CreateInitial(ctx);

            var targets = GeneratorPreview.For(ctx, state, ElsewhereSlot);

            Assert.IsEmpty(targets);
        }

        [Test]
        public void For_HeldBlockCoversCellsOfTwoGeneratorsNextBlocks_GivesBothInGeneratorOrder()
        {
            // The left generator's block needs (0, 0) and (1, 0); the bottom
            // one's needs (1, 0) and (1, 1). The held block stands on (1, 0).
            var ctx = Ctx(
                3, 3,
                new[] { Block(1, new Coord(1, 0)) },
                generators: new[]
                {
                    Spawner(1, BoardEdge.Left, 0, 1, Spawned(colors: new[] { BlockColor.Blue }, cells: Horizontal1x2)),
                    Spawner(2, BoardEdge.Bottom, 1, 1, Spawned(colors: new[] { BlockColor.Green }, cells: Vertical2x1))
                });
            var state = BoardState.CreateInitial(ctx);

            var targets = GeneratorPreview.For(ctx, state, InFrontSlot);

            Assert.AreEqual(2, targets.Count);
            Assert.AreEqual(0, targets[0].GeneratorIndex);
            Assert.AreEqual(BlockColor.Blue, targets[0].Color);
            Assert.That(targets[0].Cells, Is.EqualTo(new[] { new Coord(0, 0), new Coord(1, 0) }));
            Assert.AreEqual(1, targets[1].GeneratorIndex);
            Assert.AreEqual(BlockColor.Green, targets[1].Color);
            Assert.That(targets[1].Cells, Is.EqualTo(new[] { new Coord(1, 0), new Coord(1, 1) }));
        }

        [Test]
        public void For_GeneratorWithItsQueueExhausted_GivesNothing()
        {
            // The generator's only block spawns at level start, onto the very
            // cell it is then held on.
            var ctx = Ctx(2, 1, generators: new[] { Spawner(1, BoardEdge.Left, 0, 1, Spawned()) });
            var state = BoardState.CreateInitial(ctx);
            var spawnedSlot = ctx.GeneratorSlot(0, 0);

            var targets = GeneratorPreview.For(ctx, state, spawnedSlot);

            Assert.IsTrue(state.Alive[spawnedSlot], "the held block is the one the generator spawned");
            Assert.AreEqual(ctx.Generators[0].Queue.Count, state.GeneratorIndex[0], "the queue is exhausted");
            Assert.IsEmpty(targets);
        }

        [Test]
        public void For_NextBlockWouldSpawnFrozen_IsFrozenWithNoColourUntilEnoughClears()
        {
            // Slot 0 holds the generator's cell; slot 1 is pre-aligned with a
            // red gate, so pushing it gives the one clear the queued block's
            // threshold asks for, without freeing the generator's cell.
            var ctx = Ctx(
                3, 1,
                new[] { Block(1, new Coord(0, 0)), Block(2, new Coord(2, 0)) },
                new[] { Gate(1, BoardEdge.Bottom, 2, 1, BlockColor.Red) },
                generators: new[]
                {
                    Spawner(1, BoardEdge.Left, 0, 1, Spawned(colors: new[] { BlockColor.Blue }, unfreezeAt: 1))
                });
            var before = BoardState.CreateInitial(ctx);
            var cleared = new MoveResolver().TryApplyMove(
                ctx, before, new Move(ElsewhereSlot, new Coord(2, 0)), out var after, out _);

            var frozen = GeneratorPreview.For(ctx, before, InFrontSlot);
            var thawed = GeneratorPreview.For(ctx, after, InFrontSlot);

            Assert.IsTrue(cleared);
            Assert.AreEqual(1, after.TotalClearCount);
            Assert.AreEqual(0, after.GeneratorIndex[0], "the generator still waits behind the held block");
            Assert.AreEqual(1, frozen.Count);
            Assert.IsTrue(frozen[0].IsFrozen);
            Assert.IsNull(frozen[0].Color);
            Assert.AreEqual(1, thawed.Count);
            Assert.IsFalse(thawed[0].IsFrozen);
            Assert.AreEqual(BlockColor.Blue, thawed[0].Color);
        }

        /// <summary>
        /// 4x3 with no authored blocks: a top-edge generator at offset 1 whose
        /// first block, a 1x1, spawns at level start on (1, 2), and whose
        /// second, a three-cell corner two rows deep, needs that cell too and
        /// so waits behind it.
        /// </summary>
        private static LevelContext SecondEntryWaitingBehindTheFirst() =>
            Ctx(
                4, 3,
                generators: new[]
                {
                    Spawner(
                        1, BoardEdge.Top, 1, 2,
                        Spawned(colors: new[] { BlockColor.Blue }),
                        Spawned(colors: new[] { BlockColor.Green }, cells: Corner))
                });

        [Test]
        public void For_CurrentQueueIndex_CellsAreTheSpawnOriginPlusThatEntrysFootprint()
        {
            var ctx = SecondEntryWaitingBehindTheFirst();
            var state = BoardState.CreateInitial(ctx);
            var queueIndex = state.GeneratorIndex[0];
            var origin = ctx.GeneratorSpawnOrigin(0, queueIndex);
            var footprint = ctx.Generators[0].Queue[queueIndex].Cells;

            var targets = GeneratorPreview.For(ctx, state, ctx.GeneratorSlot(0, 0));

            Assert.AreEqual(1, queueIndex, "the preview is of the second entry, not the first");
            Assert.AreEqual(1, targets.Count);
            Assert.AreEqual(footprint.Count, targets[0].Cells.Count);
            for (var i = 0; i < footprint.Count; i++)
            {
                Assert.AreEqual(origin + footprint[i], targets[0].Cells[i], $"cell {i}");
            }
        }

        [Test]
        public void For_HeldBlockMovedAway_ResolverSpawnsTheNextBlockOnThePreviewedCells()
        {
            // The central decision (D53): the preview lands exactly where the
            // resolver spawns.
            var ctx = SecondEntryWaitingBehindTheFirst();
            var state = BoardState.CreateInitial(ctx);
            var heldSlot = ctx.GeneratorSlot(0, 0);
            var nextSlot = ctx.GeneratorSlot(0, 1);
            var targets = GeneratorPreview.For(ctx, state, heldSlot);

            var moved = new MoveResolver().TryApplyMove(ctx, state, new Move(heldSlot, new Coord(0, 2)), out var result, out _);

            Assert.IsTrue(moved);
            Assert.IsFalse(state.Alive[nextSlot], "the next block had not spawned when it was previewed");
            Assert.IsTrue(result.Alive[nextSlot], "moving the held block away spawns it");
            Assert.AreEqual(1, targets.Count);
            Assert.That(targets[0].Cells, Is.EquivalentTo(result.OccupiedCells(ctx, nextSlot)));
        }

        [Test]
        public void For_BlockThatIsNotAlive_GivesNothing()
        {
            var ctx = BlockInFrontOfAGenerator();
            var state = BoardState.CreateInitial(ctx);

            var targets = GeneratorPreview.For(ctx, state, QueuedSlot);

            Assert.IsFalse(state.Alive[QueuedSlot], "the queued block has not spawned");
            Assert.IsEmpty(targets);
        }

        [Test]
        public void For_BlockIndexOutOfRange_Throws()
        {
            var ctx = BlockInFrontOfAGenerator();
            var state = BoardState.CreateInitial(ctx);

            Assert.Throws<ArgumentOutOfRangeException>(() => GeneratorPreview.For(ctx, state, ctx.TotalBlockCapacity));
        }

        [Test]
        public void For_NullContextOrState_Throws()
        {
            var ctx = BlockInFrontOfAGenerator();
            var state = BoardState.CreateInitial(ctx);

            Assert.Throws<ArgumentNullException>(() => GeneratorPreview.For(null, state, InFrontSlot));
            Assert.Throws<ArgumentNullException>(() => GeneratorPreview.For(ctx, null, InFrontSlot));
        }
    }
}
