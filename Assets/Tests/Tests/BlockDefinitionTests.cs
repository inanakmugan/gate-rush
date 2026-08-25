using System;
using System.Collections.Generic;
using GateRush.Core;
using NUnit.Framework;

namespace GateRush.Tests
{
    public class BlockDefinitionTests
    {
        private static BlockDefinition Build(
            IReadOnlyList<Coord> cells = null,
            IReadOnlyList<BlockColor> colorStack = null,
            int? lockId = null,
            int requiredKeyCount = 0,
            int? unfreezeAtClearCount = null,
            int timeBonusSeconds = 0)
        {
            return new BlockDefinition(
                id: 1,
                cells: cells ?? new[] { new Coord(0, 0) },
                colorStack: colorStack ?? new[] { BlockColor.Red },
                startOrigin: new Coord(0, 0),
                axis: MovementAxis.Free,
                unfreezeAtClearCount: unfreezeAtClearCount,
                lockId: lockId,
                requiredKeyCount: requiredKeyCount,
                keyTargetLockId: null,
                keyEffect: KeyEffect.UnlockMovement,
                timeBonusSeconds: timeBonusSeconds);
        }

        [Test]
        public void Constructor_ColorStackWithAdjacentDuplicateColours_Throws()
        {
            var colorStack = new[] { BlockColor.Red, BlockColor.Red };

            Assert.Throws<ArgumentException>(() => Build(colorStack: colorStack));
        }

        [Test]
        public void Constructor_ColorStackWithNonAdjacentRepeatedColours_Succeeds()
        {
            var colorStack = new[] { BlockColor.Red, BlockColor.Blue, BlockColor.Red };

            var block = Build(colorStack: colorStack);

            Assert.AreEqual(3, block.ColorStack.Count);
        }

        [Test]
        public void Constructor_DuplicateCells_Throws()
        {
            var cells = new[] { new Coord(0, 0), new Coord(0, 0) };

            Assert.Throws<ArgumentException>(() => Build(cells: cells));
        }

        [Test]
        public void Constructor_DisconnectedCells_Throws()
        {
            var cells = new[] { new Coord(0, 0), new Coord(5, 5) };

            Assert.Throws<ArgumentException>(() => Build(cells: cells));
        }

        [Test]
        public void Constructor_ConnectedLShapeCells_Succeeds()
        {
            var cells = new[] { new Coord(0, 0), new Coord(1, 0), new Coord(0, 1) };

            var block = Build(cells: cells);

            Assert.AreEqual(3, block.Cells.Count);
        }

        [Test]
        public void Constructor_LockedWithZeroRequiredKeys_Throws()
        {
            Assert.Throws<ArgumentException>(() => Build(lockId: 7, requiredKeyCount: 0));
        }

        [Test]
        public void Constructor_LockedWithAtLeastOneRequiredKey_Succeeds()
        {
            var block = Build(lockId: 7, requiredKeyCount: 1);

            Assert.AreEqual(7, block.LockId);
        }

        [Test]
        public void Constructor_NegativeTimeBonusSeconds_Throws()
        {
            Assert.Throws<ArgumentException>(() => Build(timeBonusSeconds: -1));
        }

        [Test]
        public void Constructor_NegativeUnfreezeAtClearCount_Throws()
        {
            Assert.Throws<ArgumentException>(() => Build(unfreezeAtClearCount: -1));
        }

        [Test]
        public void Constructor_ZeroUnfreezeAtClearCount_Succeeds()
        {
            var block = Build(unfreezeAtClearCount: 0);

            Assert.AreEqual(0, block.UnfreezeAtClearCount);
        }
    }
}
