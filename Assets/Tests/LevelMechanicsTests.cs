using System;
using System.Collections.Generic;
using GateRush.Core;
using GateRush.Runtime;
using NUnit.Framework;
using static GateRush.Tests.Fixture;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 19's <see cref="LevelMechanics"/>: a level contains a
    /// mechanic when any block it can ever show has it — top-level, queued in
    /// a generator or in an elevator wave — or when it has the gate, shutter,
    /// generator or elevator; and a level introduces what it contains that no
    /// earlier level does, so introductions follow the levels' content and
    /// order, not a hand-kept list.
    /// </summary>
    /// <remarks>
    /// Every board is a 3x1 row. A level that needs a plain block keeps it at
    /// (1, 0); generators feed (0, 0) from the left edge and elevators and
    /// shutters cover one cell.
    /// </remarks>
    public class LevelMechanicsTests
    {
        private const int LockId = 5;

        private static readonly BlockColor[] TwoColours = { BlockColor.Red, BlockColor.Blue };

        /// <summary>The mechanics a block can carry, which may therefore sit only in a queue or a wave.</summary>
        private static readonly LevelMechanic[] BlockMechanics =
        {
            LevelMechanic.IceBlock, LevelMechanic.LayeredBlock, LevelMechanic.OneWayBlock, LevelMechanic.LockAndKey,
            LevelMechanic.TimeBonus
        };

        /// <summary>One level per mechanic, holding that mechanic on a top-level block or feature and no other.</summary>
        private static IEnumerable<TestCaseData> TopLevelLevels()
        {
            TestCaseData Case(LevelContext ctx, LevelMechanic mechanic) =>
                new TestCaseData(ctx, mechanic).SetName($"Of_{mechanic}OnATopLevelBlockOrFeature_IsFound");

            yield return Case(Ctx(3, 1, new[] { Block(1, new Coord(1, 0), unfreezeAt: 2) }), LevelMechanic.IceBlock);
            yield return Case(
                Ctx(3, 1, new[] { Block(1, new Coord(1, 0)) }, new[] { Gate(1, BoardEdge.Left, 0, 1, BlockColor.Red, openAt: 2) }),
                LevelMechanic.IceDoor);
            yield return Case(Ctx(3, 1, new[] { Block(1, new Coord(1, 0), colors: TwoColours) }), LevelMechanic.LayeredBlock);
            yield return Case(
                Ctx(3, 1, new[] { Block(1, new Coord(1, 0), axis: MovementAxis.HorizontalOnly) }), LevelMechanic.OneWayBlock);
            yield return Case(
                Ctx(3, 1, new[] { Block(1, new Coord(1, 0), lockId: LockId, requiredKeys: 1), KeyBlock() }),
                LevelMechanic.LockAndKey);
            yield return Case(
                Ctx(3, 1, new[] { Block(1, new Coord(1, 0)) }, shutters: new[] { Shutter(1, new Coord(2, 0), new Coord(2, 0)) }),
                LevelMechanic.Shutter);
            yield return Case(
                Ctx(3, 1, new[] { Block(1, new Coord(1, 0)) }, generators: new[] { Spawner(1, BoardEdge.Left, 0, 1, Spawned()) }),
                LevelMechanic.Generator);
            yield return Case(
                Ctx(
                    3, 1, new[] { Block(1, new Coord(1, 0)) },
                    elevators: new[]
                    {
                        Elevator(1, new Coord(0, 0), new Coord(0, 0), new[] { Spawned(regionOrigin: new Coord(0, 0)) })
                    }),
                LevelMechanic.Elevator);
            yield return Case(Ctx(3, 1, new[] { Block(1, new Coord(1, 0), timeBonusSeconds: 5) }), LevelMechanic.TimeBonus);
        }

        /// <summary>A blue key carrier at (2, 0) for lock <see cref="LockId"/>: a lock needs a key somewhere in its level.</summary>
        private static BlockDefinition KeyBlock() =>
            Block(9, new Coord(2, 0), colors: new[] { BlockColor.Blue }, keyTarget: LockId);

        /// <summary>A 1x1 spawned block carrying <paramref name="mechanic"/> and nothing else.</summary>
        private static SpawnedBlock SpawnedWith(LevelMechanic mechanic, Coord? regionOrigin)
        {
            switch (mechanic)
            {
                case LevelMechanic.IceBlock:
                    return Spawned(unfreezeAt: 2, regionOrigin: regionOrigin);
                case LevelMechanic.LayeredBlock:
                    return Spawned(colors: TwoColours, regionOrigin: regionOrigin);
                case LevelMechanic.OneWayBlock:
                    return Spawned(axis: MovementAxis.VerticalOnly, regionOrigin: regionOrigin);
                case LevelMechanic.LockAndKey:
                    return Spawned(lockId: LockId, requiredKeys: 1, regionOrigin: regionOrigin);
                case LevelMechanic.TimeBonus:
                    return Spawned(timeBonusSeconds: 5, regionOrigin: regionOrigin);
                default:
                    throw new ArgumentOutOfRangeException(nameof(mechanic), mechanic, "Not a mechanic a block carries.");
            }
        }

        /// <summary>The top-level blocks a level needs beside a spawned block carrying <paramref name="mechanic"/>: a key for a lock, nothing otherwise.</summary>
        private static BlockDefinition[] BlocksBeside(LevelMechanic mechanic) =>
            mechanic == LevelMechanic.LockAndKey ? new[] { KeyBlock() } : new BlockDefinition[0];

        [Test]
        public void Of_OnePlainBlock_ContainsNothing()
        {
            var ctx = Ctx(3, 1, new[] { Block(1, new Coord(1, 0)) }, new[] { Gate(1, BoardEdge.Left, 0, 1, BlockColor.Red) });

            var mechanics = LevelMechanics.Of(ctx);

            Assert.IsEmpty(mechanics);
        }

        [TestCaseSource(nameof(TopLevelLevels))]
        public void Of_MechanicOnATopLevelBlockOrFeature_IsFound(LevelContext ctx, LevelMechanic expected)
        {
            var mechanics = LevelMechanics.Of(ctx);

            CollectionAssert.AreEqual(new[] { expected }, mechanics, "that mechanic and no other");
        }

        [TestCaseSource(nameof(BlockMechanics))]
        public void Of_BlockMechanicOnlyInAGeneratorQueue_IsFound(LevelMechanic mechanic)
        {
            var ctx = Ctx(
                3, 1, BlocksBeside(mechanic),
                generators: new[] { Spawner(1, BoardEdge.Left, 0, 1, SpawnedWith(mechanic, regionOrigin: null)) });

            var mechanics = LevelMechanics.Of(ctx);

            CollectionAssert.AreEquivalent(new[] { mechanic, LevelMechanic.Generator }, mechanics);
        }

        [TestCaseSource(nameof(BlockMechanics))]
        public void Of_BlockMechanicOnlyInAnElevatorWave_IsFound(LevelMechanic mechanic)
        {
            var ctx = Ctx(
                3, 1, BlocksBeside(mechanic),
                elevators: new[]
                {
                    Elevator(1, new Coord(0, 0), new Coord(0, 0), new[] { SpawnedWith(mechanic, new Coord(0, 0)) })
                });

            var mechanics = LevelMechanics.Of(ctx);

            CollectionAssert.AreEquivalent(new[] { mechanic, LevelMechanic.Elevator }, mechanics);
        }

        [Test]
        public void Of_SingleColourBlock_IsNotLayered()
        {
            var ctx = Ctx(3, 1, new[] { Block(1, new Coord(1, 0), colors: new[] { BlockColor.Green }) });

            var mechanics = LevelMechanics.Of(ctx);

            CollectionAssert.DoesNotContain(mechanics, LevelMechanic.LayeredBlock);
        }

        [Test]
        public void Of_TwoColourBlock_IsLayered()
        {
            var ctx = Ctx(3, 1, new[] { Block(1, new Coord(1, 0), colors: TwoColours) });

            var mechanics = LevelMechanics.Of(ctx);

            CollectionAssert.Contains(mechanics, LevelMechanic.LayeredBlock);
        }

        [Test]
        public void Of_FeatureThatNeverShows_IsNotContained()
        {
            // A block, a gate and two shutters whose thresholds are met at
            // zero clears, a generator with nothing queued and an elevator
            // without waves: all legal, none ever drawn.
            var ctx = Ctx(
                3, 1,
                new[] { Block(1, new Coord(1, 0), unfreezeAt: 0) },
                new[] { Gate(1, BoardEdge.Left, 0, 1, BlockColor.Red, openAt: 0) },
                shutters: new[]
                {
                    Shutter(1, new Coord(2, 0), new Coord(2, 0), threshold: 0),
                    Shutter(2, new Coord(0, 0), new Coord(0, 0), threshold: 0, requiredColor: BlockColor.Red)
                },
                generators: new[] { Spawner(1, BoardEdge.Top, 0, 1) },
                elevators: new[] { Elevator(1, new Coord(0, 0), new Coord(0, 0)) });

            var mechanics = LevelMechanics.Of(ctx);

            Assert.IsEmpty(mechanics);
        }

        [Test]
        public void Of_AnyLevel_NeverContainsHowToPlay()
        {
            var checkedLevels = 0;

            foreach (var level in TopLevelLevels())
            {
                var mechanics = LevelMechanics.Of((LevelContext)level.Arguments[0]);

                CollectionAssert.DoesNotContain(mechanics, LevelMechanic.HowToPlay);
                checkedLevels++;
            }

            Assert.AreEqual(LevelMechanics.All.Count - 1, checkedLevels, "one level per mechanic but How to Play");
        }

        [Test]
        public void IntroducedBy_FirstLevel_IsHowToPlayThenEverythingItContains()
        {
            var inOrder = new[] { Set(LevelMechanic.IceBlock, LevelMechanic.Shutter), Set(LevelMechanic.Elevator) };

            var introduced = LevelMechanics.IntroducedBy(inOrder, 0);

            CollectionAssert.AreEqual(
                new[] { LevelMechanic.HowToPlay, LevelMechanic.IceBlock, LevelMechanic.Shutter }, introduced);
        }

        [Test]
        public void IntroducedBy_LaterLevel_IsOnlyWhatNoEarlierLevelContains_InEnumOrder()
        {
            // The later level's set is given out of enum order, so the order
            // of the result cannot come from the input's.
            var inOrder = new[]
            {
                Set(LevelMechanic.IceBlock),
                Set(LevelMechanic.TimeBonus, LevelMechanic.IceBlock, LevelMechanic.IceDoor)
            };

            var introduced = LevelMechanics.IntroducedBy(inOrder, 1);

            CollectionAssert.AreEqual(new[] { LevelMechanic.IceDoor, LevelMechanic.TimeBonus }, introduced);
        }

        [Test]
        public void IntroducedBy_LevelWhoseMechanicsAllAppearedEarlier_IsEmpty()
        {
            var inOrder = new[]
            {
                Set(LevelMechanic.IceBlock), Set(LevelMechanic.Shutter), Set(LevelMechanic.Shutter, LevelMechanic.IceBlock)
            };

            var introduced = LevelMechanics.IntroducedBy(inOrder, 2);

            Assert.IsEmpty(introduced);
        }

        [Test]
        public void IntroducedBy_ReorderedLevels_IntroductionMovesToTheFirstLevelThatContainsIt()
        {
            var plain = Set();
            var withGenerator = Set(LevelMechanic.Generator);
            var withGeneratorAndElevator = Set(LevelMechanic.Generator, LevelMechanic.Elevator);
            var original = new[] { plain, withGenerator, withGeneratorAndElevator };
            var reordered = new[] { plain, withGeneratorAndElevator, withGenerator };

            var originalSecond = LevelMechanics.IntroducedBy(original, 1);
            var originalThird = LevelMechanics.IntroducedBy(original, 2);
            var reorderedSecond = LevelMechanics.IntroducedBy(reordered, 1);
            var reorderedThird = LevelMechanics.IntroducedBy(reordered, 2);

            CollectionAssert.AreEqual(new[] { LevelMechanic.Generator }, originalSecond);
            CollectionAssert.AreEqual(new[] { LevelMechanic.Elevator }, originalThird);
            CollectionAssert.AreEqual(
                new[] { LevelMechanic.Generator, LevelMechanic.Elevator }, reorderedSecond,
                "the level moved forward now introduces the generator too");
            Assert.IsEmpty(reorderedThird, "and the level that used to introduce it introduces nothing");
        }

        [TestCase(-1)]
        [TestCase(2)]
        public void IntroducedBy_PositionOutOfRange_Throws(int position)
        {
            var inOrder = new[] { Set(LevelMechanic.IceBlock), Set(LevelMechanic.Shutter) };

            Assert.Throws<ArgumentOutOfRangeException>(() => LevelMechanics.IntroducedBy(inOrder, position));
        }

        [Test]
        public void IntroducedBy_NullOrder_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => LevelMechanics.IntroducedBy(null, 0));
        }

        /// <summary>What one level contains, as <see cref="LevelMechanics.Of"/> would report it.</summary>
        private static IReadOnlyCollection<LevelMechanic> Set(params LevelMechanic[] mechanics) => mechanics;
    }
}
