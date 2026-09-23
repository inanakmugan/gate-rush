using System.Collections.Generic;
using GateRush.Core;
using NUnit.Framework;
using static GateRush.Tests.Fixture;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="BlockSymmetry"/>: which block indices a level treats as
    /// interchangeable (<c>DECISIONS.md</c> D35). Every case is stated as the
    /// concrete grouping the level produces, not merely as "some grouping
    /// happened" — over-grouping is a correctness bug, not a performance one, so
    /// a test that only counted groups would miss the failure that matters.
    /// </summary>
    public class BlockSymmetryTests
    {
        private static readonly Coord[] Horizontal1x2 = { new Coord(0, 0), new Coord(1, 0) };

        /// <summary>
        /// Asserts the level's grouping is exactly <paramref name="expected"/>,
        /// group for group and index for index.
        /// </summary>
        private static void AssertGroups(LevelContext ctx, params int[][] expected)
        {
            var groups = ctx.BlockSymmetry.Groups;

            Assert.AreEqual(expected.Length, groups.Count, "group count");
            for (var i = 0; i < expected.Length; i++)
            {
                CollectionAssert.AreEqual(expected[i], groups[i], $"group {i}");
            }

            Assert.AreEqual(expected.Length == 0, ctx.BlockSymmetry.IsTrivial);
        }

        // ----- Grouping ---------------------------------------------------

        [Test]
        public void Of_ThreeIdenticalBlocksAndOneDifferent_GroupsOnlyTheIdenticalThree()
        {
            var ctx = Ctx(
                5, 1,
                new[]
                {
                    Block(1, new Coord(0, 0)),
                    Block(2, new Coord(1, 0)),
                    Block(3, new Coord(2, 0)),
                    Block(4, new Coord(3, 0), colors: new[] { BlockColor.Blue })
                });

            AssertGroups(ctx, new[] { 0, 1, 2 });
        }

        [Test]
        public void Of_TwoDisjointSetsOfIdenticalBlocks_GroupsEachSeparately_OrderedByLowestMember()
        {
            var ctx = Ctx(
                6, 1,
                new[]
                {
                    Block(1, new Coord(0, 0), colors: new[] { BlockColor.Green }),
                    Block(2, new Coord(1, 0), colors: new[] { BlockColor.Purple }),
                    Block(3, new Coord(2, 0), colors: new[] { BlockColor.Green }),
                    Block(4, new Coord(3, 0), colors: new[] { BlockColor.Purple }),
                    Block(5, new Coord(4, 0), colors: new[] { BlockColor.Green })
                });

            AssertGroups(ctx, new[] { 0, 2, 4 }, new[] { 1, 3 });
        }

        [Test]
        public void Of_NoTwoBlocksShareASpec_IsTrivial()
        {
            var ctx = Ctx(
                4, 1,
                new[]
                {
                    Block(1, new Coord(0, 0), colors: new[] { BlockColor.Red }),
                    Block(2, new Coord(1, 0), colors: new[] { BlockColor.Blue }),
                    Block(3, new Coord(2, 0), colors: new[] { BlockColor.Green })
                });

            AssertGroups(ctx);
        }

        [Test]
        public void Of_SingleBlockLevel_IsTrivial()
        {
            AssertGroups(Ctx(3, 1, new[] { Block(1, new Coord(0, 0)) }));
        }

        [Test]
        public void Of_BlocksDifferingOnlyInStartPosition_AreGrouped()
        {
            // Start origin is not part of a block's spec: once both are on the
            // board and interchangeable, where each began says nothing about
            // which board the state now is.
            var ctx = Ctx(
                5, 5,
                new[] { Block(1, new Coord(0, 0)), Block(2, new Coord(4, 4)) });

            AssertGroups(ctx, new[] { 0, 1 });
        }

        // ----- What must not be grouped -----------------------------------

        [Test]
        public void Of_BlocksDifferingInColorStackDepth_AreNotGrouped()
        {
            var ctx = Ctx(
                3, 1,
                new[]
                {
                    Block(1, new Coord(0, 0), colors: new[] { BlockColor.Red }),
                    Block(2, new Coord(1, 0), colors: new[] { BlockColor.Red, BlockColor.Blue })
                });

            AssertGroups(ctx);
        }

        [Test]
        public void Of_BlocksWithTheSameColoursInADifferentStackOrder_AreNotGrouped()
        {
            // A stack is ordered: only the outermost colour is addressable (M4),
            // so red-over-blue and blue-over-red are different blocks.
            var ctx = Ctx(
                3, 1,
                new[]
                {
                    Block(1, new Coord(0, 0), colors: new[] { BlockColor.Red, BlockColor.Blue }),
                    Block(2, new Coord(1, 0), colors: new[] { BlockColor.Blue, BlockColor.Red })
                });

            AssertGroups(ctx);
        }

        [Test]
        public void Of_BlocksDifferingInShape_AreNotGrouped()
        {
            var ctx = Ctx(
                5, 1,
                new[]
                {
                    Block(1, new Coord(0, 0)),
                    Block(2, new Coord(1, 0), cells: Horizontal1x2)
                });

            AssertGroups(ctx);
        }

        [Test]
        public void Of_BlocksDifferingInMovementAxis_AreNotGrouped()
        {
            var ctx = Ctx(
                3, 1,
                new[]
                {
                    Block(1, new Coord(0, 0), axis: MovementAxis.HorizontalOnly),
                    Block(2, new Coord(1, 0), axis: MovementAxis.Free)
                });

            AssertGroups(ctx);
        }

        [Test]
        public void Of_BlocksDifferingInUnfreezeThreshold_AreNotGrouped()
        {
            var ctx = Ctx(
                4, 1,
                new[]
                {
                    Block(1, new Coord(0, 0), unfreezeAt: 2),
                    Block(2, new Coord(1, 0), unfreezeAt: 3),
                    Block(3, new Coord(2, 0))
                });

            AssertGroups(ctx);
        }

        [Test]
        public void Of_LockedBlocks_AreNeverGrouped()
        {
            // Lock ids are unique within a level (M8), so two locked blocks can
            // never share a spec — which is what keeps LevelContext.LockOwnerIndex
            // outside the reach of any permutation this type admits.
            var ctx = Ctx(
                5, 1,
                new[]
                {
                    Block(1, new Coord(0, 0), lockId: 1, requiredKeys: 1),
                    Block(2, new Coord(1, 0), lockId: 2, requiredKeys: 1),
                    Block(3, new Coord(2, 0), keyTarget: 1),
                    Block(4, new Coord(3, 0), keyTarget: 2)
                });

            // The two key carriers target different locks, so they do not group
            // either; nothing in this level does.
            AssertGroups(ctx);
        }

        [Test]
        public void Of_KeysForTheSameLockWithDifferentEffects_AreNotGrouped()
        {
            var ctx = Ctx(
                4, 1,
                new[]
                {
                    Block(1, new Coord(0, 0), lockId: 1, requiredKeys: 2),
                    Block(2, new Coord(1, 0), keyTarget: 1, keyEffect: KeyEffect.UnlockMovement),
                    Block(3, new Coord(2, 0), keyTarget: 1, keyEffect: KeyEffect.ClearOuterColor)
                });

            AssertGroups(ctx);
        }

        // ----- What must be grouped ---------------------------------------

        [Test]
        public void Of_KeysForTheSameLockWithTheSameEffect_AreGrouped()
        {
            // Nothing distinguishes two such keys: MoveResolver counts consumed
            // keys against a lock rather than telling them apart.
            var ctx = Ctx(
                4, 1,
                new[]
                {
                    Block(1, new Coord(0, 0), lockId: 1, requiredKeys: 2),
                    Block(2, new Coord(1, 0), keyTarget: 1, keyEffect: KeyEffect.ClearOuterColor),
                    Block(3, new Coord(2, 0), keyTarget: 1, keyEffect: KeyEffect.ClearOuterColor)
                });

            AssertGroups(ctx, new[] { 1, 2 });
        }

        [Test]
        public void Of_BlocksDifferingOnlyInTimeBonus_AreGrouped()
        {
            // Time is outside the search space (D12) and the solver discards the
            // bonus MoveResolver reports, so a bonus cannot make two otherwise
            // identical blocks behave differently under any rule a search
            // applies. Excluding it from the key is what lets a designer drop a
            // bonus on one of nine identical blocks without costing the search
            // the whole grouping.
            var ctx = Ctx(
                3, 1,
                new[]
                {
                    Block(1, new Coord(0, 0), timeBonusSeconds: 30),
                    Block(2, new Coord(1, 0))
                });

            AssertGroups(ctx, new[] { 0, 1 });
        }

        [Test]
        public void Of_SameShapeAuthoredInADifferentCellOrder_IsGrouped()
        {
            // Shapes are compared as sets of cells, both already normalised to a
            // (0, 0) minimum corner (D30) — the order an author happened to list
            // them in is not part of the block.
            var reversed = new[] { new Coord(1, 0), new Coord(0, 0) };
            var ctx = Ctx(
                5, 2,
                new[]
                {
                    Block(1, new Coord(0, 0), cells: Horizontal1x2),
                    Block(2, new Coord(0, 1), cells: reversed)
                });

            AssertGroups(ctx, new[] { 0, 1 });
        }

        [Test]
        public void Of_SpawnerSlotsShareTheIndexSpace_AndGroupWithTopLevelBlocks()
        {
            // The grouping spans the whole flat index space SpecAt resolves:
            // index 0 is the top-level block, 1 the generator's queued block,
            // 2 the elevator's wave block. All three carry the same spec.
            var ctx = Ctx(
                4, 4,
                blocks: new[] { Block(1, new Coord(0, 0)) },
                gates: new[] { Gate(1, BoardEdge.Left, 0, 1, BlockColor.Red) },
                generators: new[] { Spawner(1, BoardEdge.Top, 0, 1, Spawned()) },
                elevators: new[]
                {
                    Elevator(
                        1, new Coord(3, 3), new Coord(3, 3),
                        new List<SpawnedBlock> { Spawned(regionOrigin: new Coord(0, 0)) })
                });

            AssertGroups(ctx, new[] { 0, 1, 2 });
        }
    }
}
