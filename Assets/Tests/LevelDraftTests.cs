using System;
using System.Text.RegularExpressions;
using GateRush.Core;
using GateRush.Editor;
using GateRush.Serialization;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="LevelDraft"/>: that <c>draft &lt;-&gt; LevelDto</c> is
    /// lossless over every field (the drift guard), that <c>draft -&gt;
    /// LevelContext</c> applies <c>Core</c>'s rules and surfaces its errors
    /// unchanged, and that a draft can hold states <see cref="LevelContext"/>
    /// would reject without throwing.
    /// </summary>
    public class LevelDraftTests
    {
        private static string Canonical(string json) => Regex.Replace(json, @"\s+", string.Empty);

        [Test]
        public void DraftDtoRoundTrip_OverEveryField_IsLossless()
        {
            // ctx -> json -> dto -> draft -> dto -> json. Any field LevelDraft
            // drops (or invents) shows up as a JSON diff. The fixture is the same
            // one the serializer round trip uses, so a new schema field has to be
            // added here to keep either test green.
            var beforeJson = LevelSerializer.ToJson(Corpus.EveryFieldLevel());

            var draft = LevelDraft.FromDto(LevelSerializer.ParseDto(beforeJson));
            var afterJson = LevelSerializer.ToJson(draft.ToDto());

            Assert.AreEqual(Canonical(beforeJson), Canonical(afterJson));
        }

        [Test]
        public void ToContext_ValidDraft_ProducesTheContext()
        {
            var draft = LevelDraft.FromDto(LevelSerializer.ParseDto(LevelSerializer.ToJson(Corpus.EveryFieldLevel())));

            var ctx = draft.ToContext();

            Assert.AreEqual(5, ctx.Blocks.Count);
            Assert.AreEqual(new Coord(1, 0), ctx.Elevators[0].Waves[0][1].RegionOrigin);
            Assert.AreEqual(2, ctx.Generators[0].Width);
            Assert.IsNull(ctx.Generators[0].Queue[0].RegionOrigin);
        }

        [Test]
        public void FromDto_SpawnedBlockIds_AreReadVerbatimRatherThanRenumbered()
        {
            // The id is what lets SelectionKey find a wave block again after an
            // undo rebuilds the draft. Renumbering by position on load would make
            // it another name for the index and reintroduce exactly the shifting
            // the id exists to survive — so ids that are not in list order must
            // come back in the order the file gave them (D34).
            var dto = LevelSerializer.ParseDto(LevelSerializer.ToJson(Corpus.EveryFieldLevel()));
            var wave = dto.elevators[0].waves[0];
            wave.blocks[0].id = 9;
            wave.blocks[1].id = 4;

            var draft = LevelDraft.FromDto(dto);

            var blocks = draft.Elevators[0].Waves[0].Blocks;
            Assert.AreEqual(9, blocks[0].Id);
            Assert.AreEqual(4, blocks[1].Id);
        }

        [Test]
        public void ToContext_InvalidDraft_SurfacesCoresErrorUnchanged()
        {
            var draft = LevelDraft.NewEmpty(5, 5);
            draft.Blocks.Add(new BlockDraft
            {
                Id = 1,
                Cells = { new Coord(0, 0) },
                ColorStack = { BlockColor.Red },
                StartOrigin = new Coord(99, 99),
            });

            var ex = Assert.Throws<ArgumentException>(() => draft.ToContext());

            StringAssert.Contains("outside the 5x5 grid", ex.Message);
        }

        [Test]
        public void Draft_HoldsBlockOutsideGridAndKeyWithNoLock_WithoutThrowing()
        {
            var draft = LevelDraft.NewEmpty(3, 3);

            Assert.DoesNotThrow(() =>
            {
                draft.Blocks.Add(new BlockDraft
                {
                    Id = 1,
                    Cells = { new Coord(0, 0) },
                    ColorStack = { BlockColor.Red },
                    StartOrigin = new Coord(50, 50),
                    KeyTargetLockId = 999,
                });
            });

            Assert.AreEqual(1, draft.Blocks.Count);
            Assert.AreEqual(999, draft.Blocks[0].KeyTargetLockId);
        }

        [Test]
        public void FromDto_UnreadableEnumOrColourName_DefaultsButRecordsALoadIssueRatherThanRepairingSilently()
        {
            var dto = new LevelDto
            {
                formatVersion = LevelSerializer.FormatVersion,
                levelId = 1,
                width = 3,
                height = 3,
                blocks = new[]
                {
                    new BlockDto
                    {
                        id = 3,
                        cells = new[] { new CoordDto { x = 0, y = 0 } },
                        colorStack = new[] { "Mauve" },
                        axis = "NotARealAxis",
                        unfreezeAtClearCount = -1,
                        lockId = -1,
                        keyTargetLockId = -1,
                    },
                },
            };

            var draft = LevelDraft.FromDto(dto);

            // Still falls back to a default so the level opens...
            Assert.AreEqual(MovementAxis.Free, draft.Blocks[0].Axis);
            Assert.AreEqual(BlockColor.Red, draft.Blocks[0].ColorStack[0]);

            // ...but the substitution is recorded, naming the element and raw value.
            Assert.AreEqual(2, draft.LoadIssues.Count);
            Assert.IsTrue(draft.LoadIssues.Exists(i => i.RawValue == "Mauve" && i.ElementLabel.Contains("Block 3")));
            Assert.IsTrue(draft.LoadIssues.Exists(i => i.RawValue == "NotARealAxis"));
        }

        // -- Key count follows the lock (Module 27) ----------------

        private const int StaleKeyCount = 3;

        [Test]
        public void SetLockId_ClearingTheLockOnABlock_ResetsTheKeyCountToTheDefault()
        {
            var block = new BlockDraft { LockId = 4, RequiredKeyCount = StaleKeyCount };

            block.SetLockId(null);

            Assert.IsNull(block.LockId);
            Assert.AreEqual(LockKeyCount.Default, block.RequiredKeyCount);
        }

        [Test]
        public void SetLockId_ClearingTheLockOnASpawnedBlock_ResetsTheKeyCountToTheDefault()
        {
            var block = new SpawnedBlockDraft { LockId = 4, RequiredKeyCount = StaleKeyCount };

            block.SetLockId(null);

            Assert.IsNull(block.LockId);
            Assert.AreEqual(LockKeyCount.Default, block.RequiredKeyCount);
        }

        [Test]
        public void SetLockId_GainingALockOnABlock_RaisesTheDefaultKeyCountToTheMinimum()
        {
            // Core rejects a lock that requires no keys, so a freshly locked
            // block starts at the fewest it can.
            var block = new BlockDraft();

            block.SetLockId(4);

            Assert.AreEqual(4, block.LockId);
            Assert.AreEqual(LockKeyCount.MinimumForLock, block.RequiredKeyCount);
        }

        [Test]
        public void SetLockId_GainingALockOnASpawnedBlock_RaisesTheDefaultKeyCountToTheMinimum()
        {
            var block = new SpawnedBlockDraft();

            block.SetLockId(4);

            Assert.AreEqual(4, block.LockId);
            Assert.AreEqual(LockKeyCount.MinimumForLock, block.RequiredKeyCount);
        }

        [Test]
        public void SetLockId_GainingALockWithAValidKeyCountAlreadyHeld_KeepsIt()
        {
            var block = new BlockDraft { RequiredKeyCount = StaleKeyCount };

            block.SetLockId(4);

            Assert.AreEqual(StaleKeyCount, block.RequiredKeyCount);
        }

        [Test]
        public void SetLockId_ChangingOneLockIdForAnother_LeavesTheKeyCountAlone()
        {
            // Not a lock gained: the count is the designer's, even one Core
            // will reject, and is not corrected behind their back.
            var typed = new BlockDraft { LockId = 4, RequiredKeyCount = StaleKeyCount };
            var invalid = new BlockDraft { LockId = 4, RequiredKeyCount = LockKeyCount.Default };

            typed.SetLockId(5);
            invalid.SetLockId(5);

            Assert.AreEqual(StaleKeyCount, typed.RequiredKeyCount);
            Assert.AreEqual(LockKeyCount.Default, invalid.RequiredKeyCount);
        }

        [Test]
        public void SetLockId_ClearThenSetAgain_EndsAtTheMinimumNotTheOldCount()
        {
            var block = new BlockDraft { LockId = 4, RequiredKeyCount = StaleKeyCount };

            block.SetLockId(null);
            block.SetLockId(4);

            Assert.AreEqual(LockKeyCount.MinimumForLock, block.RequiredKeyCount);
        }

        [Test]
        public void ToDto_LockFreeBlocksHoldingAStaleKeyCount_SaveTheDefault()
        {
            // The plain setters, as a loaded file leaves them: a placed block,
            // a queue entry and a wave block, none locked, all with a count.
            var draft = LevelDraft.NewEmpty(3, 3);
            draft.Blocks.Add(new BlockDraft
            {
                Id = 1, Cells = { new Coord(0, 0) }, ColorStack = { BlockColor.Red }, RequiredKeyCount = StaleKeyCount,
            });
            draft.Generators.Add(new GeneratorDraft
            {
                Id = 1, Edge = BoardEdge.Bottom, Offset = 1, Width = 1,
                Queue =
                {
                    new SpawnedBlockDraft
                    {
                        Id = 1, Cells = { new Coord(0, 0) }, ColorStack = { BlockColor.Red }, RequiredKeyCount = StaleKeyCount,
                    },
                },
            });
            draft.Elevators.Add(new ElevatorDraft
            {
                Id = 1, Min = new Coord(2, 2), Max = new Coord(2, 2),
                Waves =
                {
                    new WaveDraft
                    {
                        Blocks =
                        {
                            new SpawnedBlockDraft
                            {
                                Id = 1, Cells = { new Coord(0, 0) }, ColorStack = { BlockColor.Red },
                                RegionOrigin = new Coord(0, 0), RequiredKeyCount = StaleKeyCount,
                            },
                        },
                    },
                },
            });

            var dto = draft.ToDto();

            Assert.AreEqual(StaleKeyCount, draft.Blocks[0].RequiredKeyCount, "The draft must still hold the stale count, or this proves nothing.");
            Assert.AreEqual(LockKeyCount.Default, dto.blocks[0].requiredKeyCount);
            Assert.AreEqual(LockKeyCount.Default, dto.generators[0].queue[0].requiredKeyCount);
            Assert.AreEqual(LockKeyCount.Default, dto.elevators[0].waves[0].blocks[0].requiredKeyCount);
        }

        [Test]
        public void ToDto_LockedBlock_SavesItsOwnKeyCount()
        {
            var draft = LevelDraft.NewEmpty(3, 3);
            draft.Blocks.Add(new BlockDraft
            {
                Id = 1, Cells = { new Coord(0, 0) }, ColorStack = { BlockColor.Red },
                LockId = 4, RequiredKeyCount = StaleKeyCount,
            });

            var dto = draft.ToDto();

            Assert.AreEqual(4, dto.blocks[0].lockId);
            Assert.AreEqual(StaleKeyCount, dto.blocks[0].requiredKeyCount);
        }
    }
}
