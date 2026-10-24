using System;
using System.Collections.Generic;
using GateRush.Runtime;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="ScreenFadeSequence"/> (Module 47): the midpoint runs
    /// exactly once, before done; the fade is busy from its start until it
    /// has lifted; a start while busy is refused and its actions never run; an
    /// action that throws still lets the fade finish; and abandoning runs
    /// nothing and frees it.
    /// </summary>
    public class ScreenFadeSequenceTests
    {
        private ScreenFadeSequence sequence;
        private List<string> calls;

        [SetUp]
        public void SetUp()
        {
            sequence = new ScreenFadeSequence();
            calls = new List<string>();
        }

        [Test]
        public void TryStart_Idle_IsBusyAndCovering()
        {
            var isStarted = sequence.TryStart(() => calls.Add("midpoint"), () => calls.Add("done"));

            Assert.IsTrue(isStarted);
            Assert.IsTrue(sequence.IsBusy);
            Assert.AreEqual(ScreenFadePhase.Covering, sequence.Phase);
            Assert.IsEmpty(calls, "nothing runs before the screen is covered");
        }

        [Test]
        public void CoveredThenRevealed_RunsTheMidpointOnceBeforeDone()
        {
            sequence.TryStart(() => calls.Add("midpoint"), () => calls.Add("done"));

            sequence.Covered();
            sequence.Covered();
            sequence.Revealed();
            sequence.Revealed();

            CollectionAssert.AreEqual(new[] { "midpoint", "done" }, calls);
        }

        [Test]
        public void Covered_RunsTheMidpointWhileStillBusy()
        {
            var wasBusy = false;
            sequence.TryStart(() => wasBusy = sequence.IsBusy, null);

            sequence.Covered();

            Assert.IsTrue(wasBusy, "the midpoint runs under the fade");
            Assert.AreEqual(ScreenFadePhase.Revealing, sequence.Phase);
            Assert.IsTrue(sequence.IsBusy);
        }

        [Test]
        public void Revealed_IsNotBusyWhenDoneRuns()
        {
            var wasBusy = true;
            sequence.TryStart(() => { }, () => wasBusy = sequence.IsBusy);
            sequence.Covered();

            sequence.Revealed();

            Assert.IsFalse(wasBusy, "done may start another fade");
            Assert.IsFalse(sequence.IsBusy);
            Assert.AreEqual(ScreenFadePhase.Idle, sequence.Phase);
        }

        [TestCase(ScreenFadePhase.Covering)]
        [TestCase(ScreenFadePhase.Revealing)]
        public void TryStart_WhileBusy_IsRefusedAndItsActionsNeverRun(ScreenFadePhase phase)
        {
            sequence.TryStart(() => calls.Add("first midpoint"), () => calls.Add("first done"));
            if (phase == ScreenFadePhase.Revealing)
            {
                sequence.Covered();
            }

            var isStarted = sequence.TryStart(() => calls.Add("second midpoint"), () => calls.Add("second done"));
            sequence.Covered();
            sequence.Revealed();

            Assert.IsFalse(isStarted);
            CollectionAssert.AreEqual(new[] { "first midpoint", "first done" }, calls);
        }

        [Test]
        public void Covered_MidpointThrows_TheFadeCanStillFinish()
        {
            sequence.TryStart(() => throw new InvalidOperationException("a bug at the midpoint"), () => calls.Add("done"));

            Assert.Throws<InvalidOperationException>(() => sequence.Covered());
            sequence.Revealed();

            CollectionAssert.AreEqual(new[] { "done" }, calls);
            Assert.IsFalse(sequence.IsBusy, "input is not held for good");
        }

        [Test]
        public void Revealed_WithNoDone_GoesIdle()
        {
            sequence.TryStart(() => { }, null);
            sequence.Covered();

            sequence.Revealed();

            Assert.IsFalse(sequence.IsBusy);
        }

        [TestCase(ScreenFadePhase.Covering)]
        [TestCase(ScreenFadePhase.Revealing)]
        public void Abandon_WhileBusy_RunsNothingMoreAndFreesIt(ScreenFadePhase phase)
        {
            sequence.TryStart(() => calls.Add("midpoint"), () => calls.Add("done"));
            if (phase == ScreenFadePhase.Revealing)
            {
                sequence.Covered();
            }

            calls.Clear();

            sequence.Abandon();
            sequence.Covered();
            sequence.Revealed();

            Assert.IsEmpty(calls);
            Assert.IsFalse(sequence.IsBusy);
            Assert.IsTrue(sequence.TryStart(() => { }, null), "a new fade may start");
        }

        [Test]
        public void TryStart_NullMidpoint_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => sequence.TryStart(null, () => { }));

            Assert.IsFalse(sequence.IsBusy);
        }

        [Test]
        public void CoveredAndRevealed_WhileIdle_DoNothing()
        {
            sequence.Covered();
            sequence.Revealed();

            Assert.AreEqual(ScreenFadePhase.Idle, sequence.Phase);
        }
    }
}
