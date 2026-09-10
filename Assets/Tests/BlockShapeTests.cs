using System;
using GateRush.Core;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="BlockShape"/> — the reporting side of block geometry
    /// that <see cref="BlockValidation"/> throws on. The Level Editor asks these
    /// so a free-draw stroke never builds a disconnected block, and the
    /// generator marker sizes itself to its queue.
    /// </summary>
    public class BlockShapeTests
    {
        private static Coord C(int x, int y) => new Coord(x, y);

        [Test]
        public void IsOrthogonallyConnected_EmptyOrSingleCell_IsTrue()
        {
            Assert.IsTrue(BlockShape.IsOrthogonallyConnected(Array.Empty<Coord>()));
            Assert.IsTrue(BlockShape.IsOrthogonallyConnected(new[] { C(3, 3) }));
        }

        [Test]
        public void IsOrthogonallyConnected_LineAndLShape_IsTrue()
        {
            Assert.IsTrue(BlockShape.IsOrthogonallyConnected(new[] { C(0, 0), C(1, 0), C(2, 0) }));
            Assert.IsTrue(BlockShape.IsOrthogonallyConnected(new[] { C(0, 0), C(1, 0), C(1, 1) }));
        }

        [Test]
        public void IsOrthogonallyConnected_DiagonalOnlyContact_IsFalse()
        {
            Assert.IsFalse(BlockShape.IsOrthogonallyConnected(new[] { C(0, 0), C(1, 1) }));
        }

        [Test]
        public void IsOrthogonallyConnected_GapInALine_IsFalse()
        {
            Assert.IsFalse(BlockShape.IsOrthogonallyConnected(new[] { C(0, 0), C(1, 0), C(3, 0) }));
        }

        [Test]
        public void IsOrthogonallyConnected_TwoSeparateClusters_IsFalse()
        {
            Assert.IsFalse(BlockShape.IsOrthogonallyConnected(new[] { C(0, 0), C(0, 1), C(5, 5), C(5, 6) }));
        }

        [Test]
        public void AreOrthogonallyAdjacent_EdgeSharing_IsTrue_DiagonalAndSelf_AreFalse()
        {
            Assert.IsTrue(BlockShape.AreOrthogonallyAdjacent(C(2, 2), C(2, 3)));
            Assert.IsTrue(BlockShape.AreOrthogonallyAdjacent(C(2, 2), C(1, 2)));
            Assert.IsFalse(BlockShape.AreOrthogonallyAdjacent(C(2, 2), C(3, 3)));
            Assert.IsFalse(BlockShape.AreOrthogonallyAdjacent(C(2, 2), C(2, 2)));
            Assert.IsFalse(BlockShape.AreOrthogonallyAdjacent(C(2, 2), C(2, 4)));
        }

        [Test]
        public void ProjectionOnto_VerticalDomino_Is1AcrossHorizontalEdgesAnd2AcrossVerticalEdges()
        {
            var vertical = new[] { C(0, 0), C(0, 1) };

            Assert.AreEqual(1, BlockShape.ProjectionOnto(vertical, BoardEdge.Top));
            Assert.AreEqual(1, BlockShape.ProjectionOnto(vertical, BoardEdge.Bottom));
            Assert.AreEqual(2, BlockShape.ProjectionOnto(vertical, BoardEdge.Left));
            Assert.AreEqual(2, BlockShape.ProjectionOnto(vertical, BoardEdge.Right));
        }

        [Test]
        public void ProjectionOnto_LShape_ProjectsTheWholeFootprint()
        {
            // Cells (0,0),(1,0),(0,1): x-extent 2, y-extent 2.
            var l = new[] { C(0, 0), C(1, 0), C(0, 1) };

            Assert.AreEqual(2, BlockShape.ProjectionOnto(l, BoardEdge.Bottom));
            Assert.AreEqual(2, BlockShape.ProjectionOnto(l, BoardEdge.Left));
        }

        [Test]
        public void ProjectionOnto_EmptySet_IsZero()
        {
            Assert.AreEqual(0, BlockShape.ProjectionOnto(Array.Empty<Coord>(), BoardEdge.Top));
        }

        [Test]
        public void BlockDefinition_StillRejectsDiagonalOnlyCells_AfterTheRefactor()
        {
            var ex = Assert.Throws<ArgumentException>(() => new BlockDefinition(
                id: 4,
                cells: new[] { C(0, 0), C(1, 1) },
                colorStack: new[] { BlockColor.Red },
                startOrigin: C(0, 0),
                axis: MovementAxis.Free,
                unfreezeAtClearCount: null,
                lockId: null,
                requiredKeyCount: 0,
                keyTargetLockId: null,
                keyEffect: KeyEffect.UnlockMovement,
                timeBonusSeconds: 0));

            StringAssert.Contains("not orthogonally connected", ex.Message);
        }
    }
}
