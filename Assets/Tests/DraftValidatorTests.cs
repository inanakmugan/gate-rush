using System;
using System.Linq;
using GateRush.Core;
using GateRush.Editor;
using GateRush.Serialization;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="DraftValidator"/>. Every warning gets two tests: one
    /// that it fires when it should, one that it stays silent when it should not
    /// — a validator that warns about everything is as useless as one that warns
    /// about nothing (Module 09).
    /// </summary>
    public class DraftValidatorTests
    {
        private static bool Warns(LevelDraft draft, DraftWarningCategory category) =>
            new DraftValidator().Validate(draft).Any(w => w.Category == category);

        private static LevelDraft Draft(int width, int height, Action<LevelDraft> configure)
        {
            var draft = LevelDraft.NewEmpty(width, height);
            configure(draft);
            return draft;
        }

        private static BlockDraft RedBlock(int id, Coord origin, MovementAxis axis = MovementAxis.Free) =>
            new BlockDraft
            {
                Id = id,
                Cells = { new Coord(0, 0) },
                ColorStack = { BlockColor.Red },
                StartOrigin = origin,
                Axis = axis,
            };

        // -- ColorHasNoGate --------------------------------------

        [Test]
        public void ColorHasNoGate_BlockColourWithNoGate_Fires()
        {
            var draft = Draft(3, 3, d => d.Blocks.Add(RedBlock(1, new Coord(0, 0))));

            Assert.IsTrue(Warns(draft, DraftWarningCategory.ColorHasNoGate));
        }

        [Test]
        public void ColorHasNoGate_EveryColourHasAGate_Silent()
        {
            var draft = Draft(3, 3, d =>
            {
                d.Blocks.Add(RedBlock(1, new Coord(0, 0)));
                d.Gates.Add(new GateDraft { Id = 1, Edge = BoardEdge.Bottom, Offset = 0, Width = 1, Color = BlockColor.Red });
            });

            Assert.IsFalse(Warns(draft, DraftWarningCategory.ColorHasNoGate));
        }

        // -- GateTooNarrowForBlock ------------------------------

        [Test]
        public void GateTooNarrowForBlock_WideBlockOnlyHasANarrowGate_Fires()
        {
            var draft = Draft(4, 4, d =>
            {
                d.Blocks.Add(new BlockDraft
                {
                    Id = 1, Cells = { new Coord(0, 0), new Coord(1, 0) }, ColorStack = { BlockColor.Red },
                    StartOrigin = new Coord(0, 1),
                });
                d.Gates.Add(new GateDraft { Id = 1, Edge = BoardEdge.Bottom, Offset = 0, Width = 1, Color = BlockColor.Red });
            });

            Assert.IsTrue(Warns(draft, DraftWarningCategory.GateTooNarrowForBlock));
        }

        [Test]
        public void GateTooNarrowForBlock_GateWideEnough_Silent()
        {
            var draft = Draft(4, 4, d =>
            {
                d.Blocks.Add(new BlockDraft
                {
                    Id = 1, Cells = { new Coord(0, 0), new Coord(1, 0) }, ColorStack = { BlockColor.Red },
                    StartOrigin = new Coord(0, 1),
                });
                d.Gates.Add(new GateDraft { Id = 1, Edge = BoardEdge.Bottom, Offset = 0, Width = 2, Color = BlockColor.Red });
            });

            Assert.IsFalse(Warns(draft, DraftWarningCategory.GateTooNarrowForBlock));
        }

        // -- AxisRestrictedBlockHasNoGate -----------------------

        [Test]
        public void AxisRestrictedBlockHasNoGate_GateOnTheWrongEdge_Fires()
        {
            var draft = Draft(3, 3, d =>
            {
                d.Blocks.Add(RedBlock(1, new Coord(0, 1), MovementAxis.HorizontalOnly));
                d.Gates.Add(new GateDraft { Id = 1, Edge = BoardEdge.Bottom, Offset = 0, Width = 1, Color = BlockColor.Red });
            });

            Assert.IsTrue(Warns(draft, DraftWarningCategory.AxisRestrictedBlockHasNoGate));
        }

        [Test]
        public void AxisRestrictedBlockHasNoGate_GateAtAnAxisEnd_Silent()
        {
            var draft = Draft(3, 3, d =>
            {
                d.Blocks.Add(RedBlock(1, new Coord(0, 1), MovementAxis.HorizontalOnly));
                d.Gates.Add(new GateDraft { Id = 1, Edge = BoardEdge.Left, Offset = 0, Width = 1, Color = BlockColor.Red });
            });

            Assert.IsFalse(Warns(draft, DraftWarningCategory.AxisRestrictedBlockHasNoGate));
        }

        // -- ThresholdExceedsAvailableClears -------------------

        [Test]
        public void ThresholdExceedsAvailableClears_GateThresholdAboveTotalClears_Fires()
        {
            var draft = Draft(3, 3, d =>
            {
                d.Blocks.Add(RedBlock(1, new Coord(0, 0)));
                d.Gates.Add(new GateDraft
                {
                    Id = 1, Edge = BoardEdge.Bottom, Offset = 0, Width = 1, Color = BlockColor.Red, OpenAtClearCount = 5,
                });
            });

            Assert.IsTrue(Warns(draft, DraftWarningCategory.ThresholdExceedsAvailableClears));
        }

        [Test]
        public void ThresholdExceedsAvailableClears_ThresholdWithinReach_Silent()
        {
            var draft = Draft(3, 3, d =>
            {
                d.Blocks.Add(RedBlock(1, new Coord(0, 0)));
                d.Gates.Add(new GateDraft
                {
                    Id = 1, Edge = BoardEdge.Bottom, Offset = 0, Width = 1, Color = BlockColor.Red, OpenAtClearCount = 1,
                });
            });

            Assert.IsFalse(Warns(draft, DraftWarningCategory.ThresholdExceedsAvailableClears));
        }

        // -- LockHasTooFewKeys ---------------------------------

        [Test]
        public void LockHasTooFewKeys_FewerKeysThanRequired_Fires()
        {
            var draft = Draft(3, 3, d =>
            {
                d.Blocks.Add(new BlockDraft
                {
                    Id = 1, Cells = { new Coord(0, 0) }, ColorStack = { BlockColor.Red }, StartOrigin = new Coord(0, 0),
                    LockId = 1, RequiredKeyCount = 2,
                });
                d.Blocks.Add(new BlockDraft
                {
                    Id = 2, Cells = { new Coord(0, 0) }, ColorStack = { BlockColor.Red }, StartOrigin = new Coord(1, 0),
                    KeyTargetLockId = 1,
                });
            });

            Assert.IsTrue(Warns(draft, DraftWarningCategory.LockHasTooFewKeys));
        }

        [Test]
        public void LockHasTooFewKeys_EnoughKeys_Silent()
        {
            var draft = Draft(3, 3, d =>
            {
                d.Blocks.Add(new BlockDraft
                {
                    Id = 1, Cells = { new Coord(0, 0) }, ColorStack = { BlockColor.Red }, StartOrigin = new Coord(0, 0),
                    LockId = 1, RequiredKeyCount = 1,
                });
                d.Blocks.Add(new BlockDraft
                {
                    Id = 2, Cells = { new Coord(0, 0) }, ColorStack = { BlockColor.Red }, StartOrigin = new Coord(1, 0),
                    KeyTargetLockId = 1,
                });
            });

            Assert.IsFalse(Warns(draft, DraftWarningCategory.LockHasTooFewKeys));
        }

        // -- GateOpensOntoWall --------------------------------

        [Test]
        public void GateOpensOntoWall_EveryOpeningCellWalled_Fires()
        {
            var draft = Draft(3, 3, d =>
            {
                d.Gates.Add(new GateDraft { Id = 1, Edge = BoardEdge.Bottom, Offset = 0, Width = 1, Color = BlockColor.Red });
                d.StaticWalls.Add(new Coord(0, 0));
            });

            Assert.IsTrue(Warns(draft, DraftWarningCategory.GateOpensOntoWall));
        }

        [Test]
        public void GateOpensOntoWall_OpeningIsClear_Silent()
        {
            var draft = Draft(3, 3, d =>
            {
                d.Gates.Add(new GateDraft { Id = 1, Edge = BoardEdge.Bottom, Offset = 0, Width = 1, Color = BlockColor.Red });
                d.StaticWalls.Add(new Coord(2, 2));
            });

            Assert.IsFalse(Warns(draft, DraftWarningCategory.GateOpensOntoWall));
        }

        // -- ElevatorWaveNotExactTiling ----------------------

        [Test]
        public void ElevatorWaveNotExactTiling_WaveLeavesACellUncovered_Fires()
        {
            var draft = Draft(3, 3, d => d.Elevators.Add(new ElevatorDraft
            {
                Id = 1, Min = new Coord(0, 0), Max = new Coord(1, 0),
                Waves =
                {
                    new WaveDraft
                    {
                        Blocks = { SpawnedCell(new Coord(0, 0)) },
                    },
                },
            }));

            Assert.IsTrue(Warns(draft, DraftWarningCategory.ElevatorWaveNotExactTiling));
        }

        [Test]
        public void ElevatorWaveNotExactTiling_WaveTilesExactly_Silent()
        {
            var draft = Draft(3, 3, d => d.Elevators.Add(new ElevatorDraft
            {
                Id = 1, Min = new Coord(0, 0), Max = new Coord(1, 0),
                Waves =
                {
                    new WaveDraft
                    {
                        Blocks = { SpawnedCell(new Coord(0, 0)), SpawnedCell(new Coord(1, 0)) },
                    },
                },
            }));

            Assert.IsFalse(Warns(draft, DraftWarningCategory.ElevatorWaveNotExactTiling));
        }

        // -- NoLegalOpeningMove ------------------------------

        [Test]
        public void NoLegalOpeningMove_FullyPackedOneCellBoard_Fires()
        {
            var draft = Draft(1, 1, d => d.Blocks.Add(RedBlock(1, new Coord(0, 0))));

            Assert.IsTrue(Warns(draft, DraftWarningCategory.NoLegalOpeningMove));
        }

        [Test]
        public void NoLegalOpeningMove_BlockCanSlide_Silent()
        {
            var draft = Draft(3, 3, d => d.Blocks.Add(RedBlock(1, new Coord(1, 1))));

            Assert.IsFalse(Warns(draft, DraftWarningCategory.NoLegalOpeningMove));
        }

        // -- NoReadyOpeningMove -----------------------------

        [Test]
        public void NoReadyOpeningMove_NoBlockFlushAtAGate_Fires()
        {
            var draft = Draft(3, 3, d =>
            {
                d.Blocks.Add(RedBlock(1, new Coord(1, 1)));
                d.Gates.Add(new GateDraft { Id = 1, Edge = BoardEdge.Bottom, Offset = 1, Width = 1, Color = BlockColor.Red });
            });

            Assert.IsTrue(Warns(draft, DraftWarningCategory.NoReadyOpeningMove));
        }

        [Test]
        public void NoReadyOpeningMove_ABlockStartsFlushAtItsGate_Silent()
        {
            var draft = Draft(3, 3, d =>
            {
                d.Blocks.Add(RedBlock(1, new Coord(1, 0)));
                d.Gates.Add(new GateDraft { Id = 1, Edge = BoardEdge.Bottom, Offset = 1, Width = 1, Color = BlockColor.Red });
            });

            Assert.IsFalse(Warns(draft, DraftWarningCategory.NoReadyOpeningMove));
        }

        // -- DraftDoesNotFormValidLevel ---------------------

        [Test]
        public void DraftDoesNotFormValidLevel_BlockOutsideGrid_Fires()
        {
            var draft = Draft(3, 3, d => d.Blocks.Add(RedBlock(1, new Coord(9, 9))));

            Assert.IsTrue(Warns(draft, DraftWarningCategory.DraftDoesNotFormValidLevel));
        }

        [Test]
        public void DraftDoesNotFormValidLevel_CleanDraft_Silent()
        {
            var draft = Draft(3, 3, d =>
            {
                d.Blocks.Add(RedBlock(1, new Coord(1, 0)));
                d.Gates.Add(new GateDraft { Id = 1, Edge = BoardEdge.Bottom, Offset = 1, Width = 1, Color = BlockColor.Red });
            });

            Assert.IsFalse(Warns(draft, DraftWarningCategory.DraftDoesNotFormValidLevel));
        }

        // -- UnreadableValueDefaultedOnLoad -----------------

        [Test]
        public void UnreadableValueDefaultedOnLoad_FileHadAnUnrecognisedColourName_Fires()
        {
            var dto = new LevelDto
            {
                formatVersion = 2, levelId = 1, width = 3, height = 3,
                blocks = new[]
                {
                    new BlockDto
                    {
                        id = 1,
                        cells = new[] { new CoordDto { x = 0, y = 0 } },
                        colorStack = new[] { "Mauve" },
                        axis = "Free", keyEffect = "UnlockMovement",
                        unfreezeAtClearCount = -1, lockId = -1, keyTargetLockId = -1,
                    },
                },
            };

            var draft = LevelDraft.FromDto(dto);

            Assert.IsTrue(Warns(draft, DraftWarningCategory.UnreadableValueDefaultedOnLoad));
        }

        [Test]
        public void UnreadableValueDefaultedOnLoad_EverythingLoadedCleanly_Silent()
        {
            var draft = Draft(3, 3, d => d.Blocks.Add(RedBlock(1, new Coord(0, 0))));

            Assert.IsFalse(Warns(draft, DraftWarningCategory.UnreadableValueDefaultedOnLoad));
        }

        private static SpawnedBlockDraft SpawnedCell(Coord regionOrigin) =>
            new SpawnedBlockDraft
            {
                Cells = { new Coord(0, 0) },
                ColorStack = { BlockColor.Blue },
                RegionOrigin = regionOrigin,
            };
    }
}
