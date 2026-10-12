using System;
using System.Collections.Generic;
using GateRush.Core;
using GateRush.Runtime;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 25's <see cref="MoveCues"/>: the sound cues each stage of
    /// a move's presentation plays, each kind at most once and in a fixed
    /// order. The changes are built as plain data
    /// (<see cref="MoveChanges.Of"/>), so a stage can hold what no board
    /// produces — two clears in one move — which is the case the "once per
    /// kind" decision is about.
    /// </summary>
    public class MoveCuesTests
    {
        private static readonly int[] NoBlocks = Array.Empty<int>();

        [Test]
        public void Leave_DestroyedClear_GivesExit()
        {
            var changes = WithClears(Destroyed(0));

            var cues = MoveCues.Leave(changes);

            CollectionAssert.AreEqual(new[] { SoundCue.Exit }, cues);
        }

        [Test]
        public void Leave_SurvivingClear_GivesPeel()
        {
            var changes = WithClears(Peeled(0));

            var cues = MoveCues.Leave(changes);

            CollectionAssert.AreEqual(new[] { SoundCue.Peel }, cues);
        }

        [Test]
        public void Leave_OneOfEach_GivesExitThenPeel()
        {
            // Peel first in the data: the order of the cues is the stage's,
            // not the order the clears happen to be listed in.
            var changes = WithClears(Peeled(0), Destroyed(1));

            var cues = MoveCues.Leave(changes);

            CollectionAssert.AreEqual(new[] { SoundCue.Exit, SoundCue.Peel }, cues);
        }

        [Test]
        public void Leave_TwoDestroyedClears_GiveOneExit()
        {
            var changes = WithClears(Destroyed(0), Destroyed(1));

            var cues = MoveCues.Leave(changes);

            CollectionAssert.AreEqual(new[] { SoundCue.Exit }, cues);
        }

        [Test]
        public void Leave_TwoSurvivingClears_GiveOnePeel()
        {
            var changes = WithClears(Peeled(0), Peeled(1));

            var cues = MoveCues.Leave(changes);

            CollectionAssert.AreEqual(new[] { SoundCue.Peel }, cues);
        }

        [Test]
        public void Leave_ArrivalsWithoutAClear_GiveNothing()
        {
            var changes = MoveChanges.Of(
                Array.Empty<ClearedBlock>(), new[] { 1 }, new[] { 0 }, new[] { 0 }, new[] { 2 },
                new[] { new CountPop(CountKind.Gate, 1) }, new[] { new BlockSpawn(3, SpawnerKind.Generator, 0) });

            var cues = MoveCues.Leave(changes);

            Assert.IsEmpty(cues);
        }

        [Test]
        public void Arrive_ThawedBlockAndOpenedGate_GiveOneIceBreak()
        {
            var changes = WithArrivals(thawedBlocks: new[] { 1 }, openedGates: new[] { 0 });

            var cues = MoveCues.Arrive(changes);

            CollectionAssert.AreEqual(new[] { SoundCue.IceBreak }, cues);
        }

        [Test]
        public void Arrive_ThawedBlockAlone_GivesIceBreak()
        {
            var changes = WithArrivals(thawedBlocks: new[] { 1 });

            var cues = MoveCues.Arrive(changes);

            CollectionAssert.AreEqual(new[] { SoundCue.IceBreak }, cues);
        }

        [Test]
        public void Arrive_OpenedGateAlone_GivesIceBreak()
        {
            var changes = WithArrivals(openedGates: new[] { 0 });

            var cues = MoveCues.Arrive(changes);

            CollectionAssert.AreEqual(new[] { SoundCue.IceBreak }, cues);
        }

        [Test]
        public void Arrive_OpenedShutter_GivesShutterOpen()
        {
            var changes = WithArrivals(openedShutters: new[] { 0, 1 });

            var cues = MoveCues.Arrive(changes);

            CollectionAssert.AreEqual(new[] { SoundCue.ShutterOpen }, cues);
        }

        [Test]
        public void Arrive_UnlockedBlock_GivesUnlock()
        {
            var changes = WithArrivals(unlockedBlocks: new[] { 2 });

            var cues = MoveCues.Arrive(changes);

            CollectionAssert.AreEqual(new[] { SoundCue.Unlock }, cues);
        }

        [TestCase(SpawnerKind.Generator)]
        [TestCase(SpawnerKind.Elevator)]
        public void Arrive_SpawnFromEitherSpawner_GivesSpawn(SpawnerKind source)
        {
            var changes = WithArrivals(spawns: new[] { new BlockSpawn(3, source, 0) });

            var cues = MoveCues.Arrive(changes);

            CollectionAssert.AreEqual(new[] { SoundCue.Spawn }, cues);
        }

        [Test]
        public void Arrive_AWholeElevatorWave_GivesOneSpawn()
        {
            var changes = WithArrivals(spawns: new[]
            {
                new BlockSpawn(3, SpawnerKind.Elevator, 0),
                new BlockSpawn(4, SpawnerKind.Elevator, 0),
                new BlockSpawn(5, SpawnerKind.Generator, 1)
            });

            var cues = MoveCues.Arrive(changes);

            CollectionAssert.AreEqual(new[] { SoundCue.Spawn }, cues);
        }

        [Test]
        public void Arrive_CountPops_GiveOneCountPop()
        {
            var changes = WithArrivals(countPops: new[]
            {
                new CountPop(CountKind.Frozen, 1), new CountPop(CountKind.Gate, 0)
            });

            var cues = MoveCues.Arrive(changes);

            CollectionAssert.AreEqual(new[] { SoundCue.CountPop }, cues);
        }

        [Test]
        public void Arrive_EveryKindOfChange_GivesEachCueOnceInTheFixedOrder()
        {
            var changes = WithArrivals(
                thawedBlocks: new[] { 1 },
                openedGates: new[] { 0 },
                openedShutters: new[] { 0 },
                unlockedBlocks: new[] { 2 },
                countPops: new[] { new CountPop(CountKind.Shutter, 1) },
                spawns: new[] { new BlockSpawn(3, SpawnerKind.Generator, 0) });

            var cues = MoveCues.Arrive(changes);

            CollectionAssert.AreEqual(
                new[] { SoundCue.IceBreak, SoundCue.ShutterOpen, SoundCue.Unlock, SoundCue.Spawn, SoundCue.CountPop },
                cues);
        }

        [Test]
        public void Arrive_AClearAndNothingElse_GivesNothing()
        {
            var changes = WithClears(Destroyed(0));

            var cues = MoveCues.Arrive(changes);

            Assert.IsEmpty(cues);
        }

        [Test]
        public void LeaveAndArrive_NoChanges_GiveNothing()
        {
            var leave = MoveCues.Leave(MoveChanges.None);
            var arrive = MoveCues.Arrive(MoveChanges.None);

            Assert.IsEmpty(leave);
            Assert.IsEmpty(arrive);
        }

        [Test]
        public void LeaveAndArrive_Null_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => MoveCues.Leave(null));
            Assert.Throws<ArgumentNullException>(() => MoveCues.Arrive(null));
        }

        /// <summary>A block whose last colour was cleared through a bottom gate.</summary>
        private static ClearedBlock Destroyed(int blockIndex) =>
            new ClearedBlock(blockIndex, BlockColor.Red, null, 0, BoardEdge.Bottom);

        /// <summary>A layered block that shed red and now shows blue.</summary>
        private static ClearedBlock Peeled(int blockIndex) =>
            new ClearedBlock(blockIndex, BlockColor.Red, BlockColor.Blue, 0, BoardEdge.Bottom);

        private static MoveChanges WithClears(params ClearedBlock[] clears) =>
            MoveChanges.Of(
                clears, NoBlocks, NoBlocks, NoBlocks, NoBlocks, Array.Empty<CountPop>(), Array.Empty<BlockSpawn>());

        private static MoveChanges WithArrivals(
            IReadOnlyList<int> thawedBlocks = null,
            IReadOnlyList<int> openedGates = null,
            IReadOnlyList<int> openedShutters = null,
            IReadOnlyList<int> unlockedBlocks = null,
            IReadOnlyList<CountPop> countPops = null,
            IReadOnlyList<BlockSpawn> spawns = null) =>
            MoveChanges.Of(
                Array.Empty<ClearedBlock>(),
                thawedBlocks ?? NoBlocks,
                openedGates ?? NoBlocks,
                openedShutters ?? NoBlocks,
                unlockedBlocks ?? NoBlocks,
                countPops ?? Array.Empty<CountPop>(),
                spawns ?? Array.Empty<BlockSpawn>());
    }
}
