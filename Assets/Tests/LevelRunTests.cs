using GateRush.Core;
using GateRush.Runtime;
using NUnit.Framework;
using static GateRush.Tests.Fixture;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 12's <see cref="LevelRun"/>: whichever of winning and
    /// running out of time comes first decides the level and the other never
    /// follows; time bonuses (M10) reach the countdown; restart starts over
    /// with the whole budget; a level without a countdown can still be won.
    /// </summary>
    /// <remarks>
    /// Every board is a 3x1 row with a red block at (0, 0) pre-aligned with a
    /// red gate on the left, so a push in place is the level's opening move.
    /// </remarks>
    public class LevelRunTests
    {
        private const float Budget = 10f;
        private const float Tick = 3f;
        private const int Bonus = 5;

        private static readonly Move OpeningPush = new Move(0, new Coord(0, 0));

        private static LevelContext OneBlockLevel() =>
            Ctx(3, 1, new[] { Block(1, new Coord(0, 0)) }, new[] { Gate(1, BoardEdge.Left, 0, 1, BlockColor.Red) });

        [Test]
        public void TryApply_SolvingMove_WinsAndStopsTheCountdownSoTimeCanNoLongerLose()
        {
            var run = new LevelRun(new LevelSession(OneBlockLevel()), new Countdown(Budget));
            run.Start();

            Assert.IsTrue(run.Session.TryApply(OpeningPush));
            run.Tick(Budget * 2f);

            Assert.AreEqual(LevelOutcome.Won, run.Outcome);
            Assert.IsFalse(run.Countdown.IsRunning);
            Assert.IsFalse(run.Countdown.HasExpired);
        }

        [Test]
        public void Tick_ToZero_LosesAndALaterSolvingMoveDoesNotWin()
        {
            var run = new LevelRun(new LevelSession(OneBlockLevel()), new Countdown(Budget));
            run.Start();

            run.Tick(Budget);
            Assert.IsTrue(run.Session.TryApply(OpeningPush));

            Assert.AreEqual(LevelOutcome.Lost, run.Outcome);
        }

        [Test]
        public void TryApply_DestroyingATimeBonusBlock_AddsItsSecondsToTheCountdown()
        {
            // A second block keeps the level from being won by the clear.
            var ctx = Ctx(3, 1,
                new[] { Block(1, new Coord(0, 0), timeBonusSeconds: Bonus), Block(2, new Coord(2, 0), colors: new[] { BlockColor.Blue }) },
                new[] { Gate(1, BoardEdge.Left, 0, 1, BlockColor.Red) });
            var run = new LevelRun(new LevelSession(ctx), new Countdown(Budget));
            run.Start();
            run.Tick(Tick);

            Assert.IsTrue(run.Session.TryApply(OpeningPush));

            Assert.AreEqual(Budget - Tick + Bonus, run.Countdown.RemainingSeconds, 1e-5f);
            Assert.AreEqual(LevelOutcome.None, run.Outcome);
        }

        [Test]
        public void Restart_AfterALoss_StartsOverWithTheWholeBudgetRunning()
        {
            var ctx = OneBlockLevel();
            var run = new LevelRun(new LevelSession(ctx), new Countdown(Budget));
            run.Start();
            run.Tick(Budget);

            run.Restart();

            Assert.AreEqual(LevelOutcome.None, run.Outcome);
            Assert.AreEqual(Budget, run.Countdown.RemainingSeconds);
            Assert.IsTrue(run.Countdown.IsRunning);
            Assert.AreEqual(BoardState.CreateInitial(ctx), run.Session.State);
        }

        [Test]
        public void TryApply_SolvingMoveWithoutACountdown_Wins()
        {
            var run = new LevelRun(new LevelSession(OneBlockLevel()), countdown: null);
            run.Start();
            run.Tick(Tick);

            Assert.IsTrue(run.Session.TryApply(OpeningPush));

            Assert.AreEqual(LevelOutcome.Won, run.Outcome);
        }
    }
}
