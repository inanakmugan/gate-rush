using System.Collections.Generic;
using GateRush.Core;
using GateRush.Editor;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 24's <see cref="DraftLockColors"/>: a key's lock is found
    /// wherever its block is — on the board, in an elevator wave or in a
    /// generator's queue — and gives that block's outer colour at level start
    /// (D47); an unknown lock id gives none.
    /// </summary>
    public class DraftLockColorsTests
    {
        private const int WaveLockId = 1;
        private const int QueueLockId = 2;
        private const int BoardLockId = 3;
        private const int UnknownLockId = 9;

        /// <summary>
        /// A draft with two keys on the board, one for a lock on a wave block
        /// and one for a lock on a queue entry, and a locked board block.
        /// Every locked block is layered, so "outer" is told from "beneath".
        /// </summary>
        private static LevelDraft DraftWithLocksEverywhere()
        {
            var draft = LevelDraft.NewEmpty(4, 4);
            draft.Blocks.Add(new BlockDraft
            {
                Id = 1,
                ColorStack = new List<BlockColor> { BlockColor.Yellow },
                KeyTargetLockId = WaveLockId,
            });
            draft.Blocks.Add(new BlockDraft
            {
                Id = 2,
                ColorStack = new List<BlockColor> { BlockColor.Yellow },
                KeyTargetLockId = QueueLockId,
            });
            draft.Blocks.Add(new BlockDraft
            {
                Id = 3,
                ColorStack = new List<BlockColor> { BlockColor.Purple, BlockColor.Yellow },
                LockId = BoardLockId,
            });

            var elevator = new ElevatorDraft { Id = 1 };
            var wave = new WaveDraft();
            wave.Blocks.Add(new SpawnedBlockDraft
            {
                Id = 1,
                ColorStack = new List<BlockColor> { BlockColor.Green, BlockColor.Red },
                LockId = WaveLockId,
            });
            elevator.Waves.Add(new WaveDraft());
            elevator.Waves.Add(wave);
            draft.Elevators.Add(elevator);

            var generator = new GeneratorDraft { Id = 1 };
            generator.Queue.Add(new SpawnedBlockDraft { Id = 1, ColorStack = new List<BlockColor> { BlockColor.Orange } });
            generator.Queue.Add(new SpawnedBlockDraft
            {
                Id = 2,
                ColorStack = new List<BlockColor> { BlockColor.Blue, BlockColor.Red },
                LockId = QueueLockId,
            });
            draft.Generators.Add(generator);

            return draft;
        }

        [Test]
        public void LockColor_KeyOnTheBoardWhoseLockIsOnAWaveBlock_IsThatBlocksOuterColour()
        {
            var draft = DraftWithLocksEverywhere();
            var key = draft.Blocks[0];

            var color = DraftLockColors.Of(draft).LockColor(key.KeyTargetLockId.Value);

            Assert.AreEqual(BlockColor.Green, color);
        }

        [Test]
        public void LockColor_KeyOnTheBoardWhoseLockIsOnAQueueEntry_IsThatEntrysOuterColour()
        {
            var draft = DraftWithLocksEverywhere();
            var key = draft.Blocks[1];

            var color = DraftLockColors.Of(draft).LockColor(key.KeyTargetLockId.Value);

            Assert.AreEqual(BlockColor.Blue, color);
        }

        [Test]
        public void LockColor_LockOnABoardBlock_IsItsOuterColour()
        {
            var draft = DraftWithLocksEverywhere();

            var color = DraftLockColors.Of(draft).LockColor(BoardLockId);

            Assert.AreEqual(BlockColor.Purple, color);
        }

        [Test]
        public void LockColor_UnknownLockId_IsNull()
        {
            var draft = DraftWithLocksEverywhere();

            var color = DraftLockColors.Of(draft).LockColor(UnknownLockId);

            Assert.IsNull(color);
        }

        [Test]
        public void LockColor_LockedBlockWithNoColours_IsNullAndDoesNotThrow()
        {
            // A draft holds what Core rejects; the canvas must still draw.
            var draft = LevelDraft.NewEmpty(2, 2);
            draft.Blocks.Add(new BlockDraft { Id = 1, LockId = BoardLockId });

            var color = DraftLockColors.Of(draft).LockColor(BoardLockId);

            Assert.IsNull(color);
        }
    }
}
