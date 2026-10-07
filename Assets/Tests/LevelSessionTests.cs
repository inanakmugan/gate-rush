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
    /// Module 12 adds <see cref="LevelSession.TimeBonusEarned"/> (M10).
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
