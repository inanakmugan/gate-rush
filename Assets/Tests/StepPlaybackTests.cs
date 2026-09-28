using System.Collections.Generic;
using GateRush.Core;
using GateRush.Runtime;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers Module 12's <see cref="StepPlayback"/>: steps come out in order
    /// with none skipped, a short queue plays at the configured duration, and a
    /// backlog above the limit shortens each step until it has caught up.
    /// </summary>
    public class StepPlaybackTests
    {
        private const float Step = 0.1f;
        private const int Limit = 2;
        private const float Tolerance = 1e-6f;

        [Test]
        public void TryDequeue_ManySteps_ComeOutInTheOrderTheyWentInWithNoneSkipped()
        {
            var playback = new StepPlayback(Step, Limit);
            var steps = new[] { new Coord(1, 0), new Coord(2, 0), new Coord(2, 1), new Coord(3, 1), new Coord(3, 2) };
            foreach (var step in steps)
            {
                playback.Enqueue(step);
            }

            var played = new List<Coord>();
            while (playback.TryDequeue(out var origin, out _))
            {
                played.Add(origin);
            }

            CollectionAssert.AreEqual(steps, played);
            Assert.AreEqual(0, playback.Count);
        }

        [Test]
        public void TryDequeue_EmptyQueue_ReturnsFalse()
        {
            var playback = new StepPlayback(Step, Limit);

            var dequeued = playback.TryDequeue(out _, out _);

            Assert.IsFalse(dequeued);
        }

        [Test]
        public void TryDequeue_QueueWithinTheLimit_PlaysAtTheConfiguredDuration()
        {
            var playback = new StepPlayback(Step, Limit);
            playback.Enqueue(new Coord(1, 0));
            playback.Enqueue(new Coord(2, 0));

            playback.TryDequeue(out _, out var first);
            playback.TryDequeue(out _, out var second);

            Assert.AreEqual(Step, first, Tolerance);
            Assert.AreEqual(Step, second, Tolerance);
        }

        [Test]
        public void TryDequeue_BacklogAboveTheLimit_ShortensEachStepUntilCaughtUp()
        {
            // Four queued, limit two: the backlog shares the time of two steps
            // (2/4, then 2/3 of a step), then plays at full length once it is
            // back within the limit.
            var playback = new StepPlayback(Step, Limit);
            for (var x = 1; x <= 4; x++)
            {
                playback.Enqueue(new Coord(x, 0));
            }

            var durations = new List<float>();
            while (playback.TryDequeue(out _, out var seconds))
            {
                durations.Add(seconds);
            }

            var expected = new[] { Step * 2f / 4f, Step * 2f / 3f, Step, Step };
            Assert.AreEqual(expected.Length, durations.Count);
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], durations[i], Tolerance, $"step {i}");
            }
        }

        [Test]
        public void Clear_DropsEveryQueuedStep()
        {
            var playback = new StepPlayback(Step, Limit);
            playback.Enqueue(new Coord(1, 0));

            playback.Clear();

            Assert.AreEqual(0, playback.Count);
            Assert.IsFalse(playback.TryDequeue(out _, out _));
        }

        [TestCase(0f, Limit)]
        [TestCase(Step, 0)]
        public void Constructor_NonPositiveDurationOrLimit_Throws(float stepSeconds, int maxLagSteps)
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new StepPlayback(stepSeconds, maxLagSteps));
        }
    }
}
