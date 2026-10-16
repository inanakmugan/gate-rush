using System.Collections.Generic;
using GateRush.Core;
using GateRush.Runtime;
using GateRush.Solver;
using NUnit.Framework;
using UnityEngine;
using static GateRush.Tests.Fixture;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 11's <see cref="LevelSession"/>: the board changes only
    /// through <see cref="MoveResolver"/>, each change is announced once, restart
    /// returns to the level's initial state, and a solved level reads solved.
    /// Module 12 adds <see cref="LevelSession.TimeBonusEarned"/> (M10). Module 33
    /// adds the Rocket and the Broom: each check agrees with its
    /// <see cref="MoveResolver"/> call and changes nothing, and each apply is
    /// announced as a move is.
    /// </summary>
    public class LevelSessionTests
    {
        private const float PushThreshold = 0.3f;
        private const float PushDistance = 0.4f;
        private const float FollowRate = 20f;
        private const float CornerAssist = 0.3f;
        private const float PullRange = 0.8f;
        private const float PullAmount = 0.35f;
        private const float CaptureRange = 0.6f;
        private const float PullFollowRate = 25f;
        private const float NudgeMax = 0.15f;
        private const int MinimumLevelsSolved = 10;
        private const int TimeBonus = 7;
        private const int SecondTimeBonus = 11;
        private const int JokerLockId = 1;
        private const int UnreachedThreshold = 5;
        private const int BonusBlockIndex = 1;
        private const int FrozenBlockIndex = 2;
        private const int ShutteredBlockIndex = 4;
        private const int TargetableBlocks = 4;

        [Test]
        public void TryApply_LegalMove_ReplacesStateAndRaisesStateChangedOnce()
        {
            var ctx = Ctx(3, 1, new[] { Block(1, new Coord(0, 0)) }, new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Blue) });
            var session = new LevelSession(ctx);
            var before = session.State;
            var raised = 0;
            session.StateChanged += () => raised++;

            var applied = session.TryApply(new Move(0, new Coord(1, 0)));

            Assert.IsTrue(applied);
            Assert.AreNotEqual(before, session.State);
            Assert.AreEqual(new Coord(1, 0), session.State.Origins[0]);
            Assert.AreEqual(1, raised);
        }

        [Test]
        public void TryApply_IllegalMove_ChangesNothingAndRaisesNothing()
        {
            // A zero-distance move with no gate to push into is illegal (D25).
            var ctx = Ctx(3, 1, new[] { Block(1, new Coord(0, 0)) }, new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Blue) });
            var session = new LevelSession(ctx);
            var before = session.State;
            var raised = 0;
            session.StateChanged += () => raised++;

            var applied = session.TryApply(new Move(0, new Coord(0, 0)));

            Assert.IsFalse(applied);
            Assert.AreSame(before, session.State);
            Assert.AreEqual(0, raised);
        }

        [Test]
        public void TryApply_PushAtCompatibleGateFromADrag_ClearsTheBlock()
        {
            // The ready opening move, end to end: a block pre-aligned with its
            // gate is grabbed, pushed toward the gate without moving, released.
            var ctx = Ctx(3, 1, new[] { Block(1, new Coord(0, 0)), Block(2, new Coord(1, 0), colors: new[] { BlockColor.Blue }) },
                new[] { Gate(1, BoardEdge.Left, 0, 1, BlockColor.Red) });
            var session = new LevelSession(ctx);
            var drag = new DragController(new DragSettings(
                PushThreshold, FollowRate, CornerAssist, PullRange, PullAmount, CaptureRange, PullFollowRate,
                NudgeMax, linear => linear));
            var grab = new Vector2(0.5f, 0.5f);
            drag.TryBegin(session.Context, session.State, grab);
            var move = drag.End(grab - new Vector2(PushDistance, 0f));

            var applied = session.TryApply(move.Value);

            Assert.IsTrue(applied);
            Assert.IsFalse(session.State.Alive[0]);
            Assert.AreEqual(1, session.State.TotalClearCount);
        }

        [Test]
        public void TryApply_MoveDestroyingATimeBonusBlock_RaisesTimeBonusEarnedWithItsBonus()
        {
            var ctx = Ctx(3, 1,
                new[] { Block(1, new Coord(0, 0), timeBonusSeconds: TimeBonus), Block(2, new Coord(2, 0), colors: new[] { BlockColor.Blue }) },
                new[] { Gate(1, BoardEdge.Left, 0, 1, BlockColor.Red) });
            var session = new LevelSession(ctx);
            var earned = new List<int>();
            session.TimeBonusEarned += seconds => earned.Add(seconds);

            var applied = session.TryApply(new Move(0, new Coord(0, 0)));

            Assert.IsTrue(applied);
            CollectionAssert.AreEqual(new[] { TimeBonus }, earned);
        }

        [Test]
        public void TryApply_ClearThatLeavesATimeBonusBlockAlive_RaisesNoTimeBonus()
        {
            // M10: the bonus is paid when the block is destroyed, not on each clear.
            var ctx = Ctx(3, 1,
                new[] { Block(1, new Coord(0, 0), colors: new[] { BlockColor.Red, BlockColor.Blue }, timeBonusSeconds: TimeBonus) },
                new[] { Gate(1, BoardEdge.Left, 0, 1, BlockColor.Red), Gate(2, BoardEdge.Right, 0, 1, BlockColor.Blue) });
            var session = new LevelSession(ctx);
            var raised = 0;
            session.TimeBonusEarned += _ => raised++;

            var applied = session.TryApply(new Move(0, new Coord(0, 0)));

            Assert.IsTrue(applied);
            Assert.IsTrue(session.State.Alive[0]);
            Assert.AreEqual(0, raised);
        }

        [Test]
        public void Restart_AfterAMove_ReturnsToTheInitialStateWithSpawnedBlocks()
        {
            // No authored blocks: the generator's first block spawns at level
            // start into the empty cell (D42) and occupies slot 0.
            var ctx = Ctx(3, 1,
                gates: new[] { Gate(1, BoardEdge.Right, 0, 1, BlockColor.Blue) },
                generators: new[] { Spawner(1, BoardEdge.Left, 0, 1, Spawned(), Spawned()) });
            var session = new LevelSession(ctx);
            Assert.IsTrue(session.State.Alive[0], "the first queued block should spawn at level start");
            Assert.IsTrue(session.TryApply(new Move(0, new Coord(1, 0))));
            var raised = 0;
            session.StateChanged += () => raised++;

            session.Restart();

            Assert.AreEqual(BoardState.CreateInitial(ctx), session.State);
            Assert.IsTrue(session.State.Alive[0]);
            Assert.AreEqual(new Coord(0, 0), session.State.Origins[0]);
            Assert.AreEqual(1, raised);
        }

        // ----- Jokers (Module 33) ----------------------------------------------

        /// <summary>
        /// One block per targeting case (D11), each of its own colour so a
        /// Broom of that colour finds it alone: 0 a blue key carrier for lock
        /// 1; 1 a red block worth <see cref="TimeBonus"/>; 2 a green frozen
        /// block; 3 a yellow block locked by lock 1; 4 a pink block under a
        /// closed shutter; and a purple block queued in a left-edge generator
        /// whose cell block 0 holds, so it never spawns. Cyan is on no block.
        /// </summary>
        private static LevelContext JokerTargetingLevel() =>
            Ctx(5, 2,
                new[]
                {
                    Block(1, new Coord(0, 0), colors: new[] { BlockColor.Blue }, keyTarget: JokerLockId),
                    Block(2, new Coord(1, 0), timeBonusSeconds: TimeBonus),
                    Block(3, new Coord(2, 0), colors: new[] { BlockColor.Green }, unfreezeAt: UnreachedThreshold),
                    Block(4, new Coord(3, 0), colors: new[] { BlockColor.Yellow }, lockId: JokerLockId, requiredKeys: 1),
                    Block(5, new Coord(4, 1), colors: new[] { BlockColor.Pink })
                },
                shutters: new[] { Shutter(1, new Coord(4, 1), new Coord(4, 1), UnreachedThreshold) },
                generators: new[] { Spawner(1, BoardEdge.Left, 0, 1, Spawned(colors: new[] { BlockColor.Purple })) });

        [Test]
        public void CanRocketAndCanSweep_AnyArgument_ChangeNothingAndRaiseNothing()
        {
            var session = new LevelSession(JokerTargetingLevel());
            var before = session.State;
            var raised = 0;
            session.StateChanged += () => raised++;
            session.TimeBonusEarned += _ => raised++;

            for (var i = -1; i <= session.Context.TotalBlockCapacity; i++)
            {
                session.CanRocket(i);
            }

            foreach (BlockColor color in System.Enum.GetValues(typeof(BlockColor)))
            {
                session.CanSweep(color);
            }

            Assert.AreSame(before, session.State);
            Assert.AreEqual(0, raised);
        }

        [Test]
        public void CanRocket_EveryIndex_AgreesWithTryClearBlock()
        {
            var session = new LevelSession(JokerTargetingLevel());
            var resolver = new MoveResolver();
            var allowed = 0;

            for (var i = -1; i <= session.Context.TotalBlockCapacity; i++)
            {
                var canRocket = session.CanRocket(i);
                var cleared = resolver.TryClearBlock(session.Context, session.State, i, out _, out _);

                Assert.AreEqual(cleared, canRocket, $"block index {i}");
                if (canRocket)
                {
                    allowed++;
                }
            }

            Assert.AreEqual(TargetableBlocks, allowed, "the normal, frozen, locked and key blocks; not the shuttered or unspawned one");
        }

        [Test]
        public void CanSweep_EveryColour_AgreesWithTrySweepColor()
        {
            var session = new LevelSession(JokerTargetingLevel());
            var resolver = new MoveResolver();
            var allowed = 0;

            foreach (BlockColor color in System.Enum.GetValues(typeof(BlockColor)))
            {
                var canSweep = session.CanSweep(color);
                var swept = resolver.TrySweepColor(session.Context, session.State, color, out _, out _);

                Assert.AreEqual(swept, canSweep, color.ToString());
                if (canSweep)
                {
                    allowed++;
                }
            }

            Assert.AreEqual(TargetableBlocks, allowed, "one colour per targetable block; not pink, purple or cyan");
        }

        [Test]
        public void TryRocket_TargetableBonusBlock_RaisesTimeBonusThenStateChangedOnce()
        {
            var session = new LevelSession(JokerTargetingLevel());
            var raised = new List<string>();
            var earned = new List<int>();
            session.TimeBonusEarned += seconds =>
            {
                raised.Add(nameof(LevelSession.TimeBonusEarned));
                earned.Add(seconds);
            };
            session.StateChanged += () => raised.Add(nameof(LevelSession.StateChanged));

            var applied = session.TryRocket(BonusBlockIndex);

            Assert.IsTrue(applied);
            Assert.IsFalse(session.State.Alive[BonusBlockIndex]);
            CollectionAssert.AreEqual(new[] { nameof(LevelSession.TimeBonusEarned), nameof(LevelSession.StateChanged) }, raised);
            CollectionAssert.AreEqual(new[] { TimeBonus }, earned);
        }

        [Test]
        public void TryRocket_BlockWithNoBonus_RaisesStateChangedOnly()
        {
            var session = new LevelSession(JokerTargetingLevel());
            var changed = 0;
            var bonuses = 0;
            session.StateChanged += () => changed++;
            session.TimeBonusEarned += _ => bonuses++;

            var applied = session.TryRocket(FrozenBlockIndex);

            Assert.IsTrue(applied);
            Assert.AreEqual(1, changed);
            Assert.AreEqual(0, bonuses);
        }

        [Test]
        public void TryRocket_RefusedTarget_ChangesNothingAndRaisesNothing()
        {
            var session = new LevelSession(JokerTargetingLevel());
            var before = session.State;
            var raised = 0;
            session.StateChanged += () => raised++;
            session.TimeBonusEarned += _ => raised++;

            var applied = session.TryRocket(ShutteredBlockIndex);

            Assert.IsFalse(applied);
            Assert.AreSame(before, session.State);
            Assert.AreEqual(0, raised);
        }

        [Test]
        public void TrySweep_SeveralBonusBlocks_RaisesTimeBonusOnceWithTheSum()
        {
            var ctx = Ctx(3, 1,
                new[]
                {
                    Block(1, new Coord(0, 0), timeBonusSeconds: TimeBonus),
                    Block(2, new Coord(1, 0), timeBonusSeconds: SecondTimeBonus),
                    Block(3, new Coord(2, 0), colors: new[] { BlockColor.Blue })
                });
            var session = new LevelSession(ctx);
            var earned = new List<int>();
            var changed = 0;
            session.TimeBonusEarned += seconds => earned.Add(seconds);
            session.StateChanged += () => changed++;

            var applied = session.TrySweep(BlockColor.Red);

            Assert.IsTrue(applied);
            CollectionAssert.AreEqual(new[] { TimeBonus + SecondTimeBonus }, earned);
            Assert.AreEqual(1, changed);
        }

        [Test]
        public void TrySweep_NoTargetableMatch_ChangesNothingAndRaisesNothing()
        {
            var session = new LevelSession(JokerTargetingLevel());
            var before = session.State;
            var raised = 0;
            session.StateChanged += () => raised++;
            session.TimeBonusEarned += _ => raised++;

            var applied = session.TrySweep(BlockColor.Pink);

            Assert.IsFalse(applied);
            Assert.AreSame(before, session.State);
            Assert.AreEqual(0, raised);
        }

        [Test]
        public void TryRocket_LastBlock_IsSolved()
        {
            var session = new LevelSession(Ctx(3, 1, new[] { Block(1, new Coord(1, 0)) }));

            var applied = session.TryRocket(0);

            Assert.IsTrue(applied);
            Assert.IsTrue(session.IsSolved);
        }

        [Test]
        public void TrySweep_LastBlocks_IsSolved()
        {
            var session = new LevelSession(Ctx(3, 1, new[] { Block(1, new Coord(0, 0)), Block(2, new Coord(2, 0)) }));

            var applied = session.TrySweep(BlockColor.Red);

            Assert.IsTrue(applied);
            Assert.IsTrue(session.IsSolved);
        }

        [Test]
        public void IsSolved_AfterTheLastClearOfASolvableCorpusLevel_BecomesTrue()
        {
            var solved = 0;

            foreach (var (name, ctx, _, _) in SearchCorpus.SolvableCorpus())
            {
                var session = new LevelSession(ctx);
                var result = new BreadthFirstStrategy().Search(ctx, session.State, Solve.Budget(MoveGenMode.Canonical));
                Assert.AreEqual(SolveStatus.Solvable, result.Status, name);

                for (var m = 0; m < result.Solution.Count; m++)
                {
                    Assert.IsFalse(session.IsSolved, $"{name}: solved before move {m}");
                    Assert.IsTrue(session.TryApply(result.Solution[m]), $"{name}: move {m} rejected");
                }

                Assert.IsTrue(session.IsSolved, name);
                solved++;
            }

            Assert.GreaterOrEqual(solved, MinimumLevelsSolved, "too few corpus levels to mean anything");
        }
    }
}
