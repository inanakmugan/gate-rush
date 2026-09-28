using System;
using System.Collections.Generic;
using GateRush.Core;

namespace GateRush.Runtime
{
    /// <summary>
    /// The queue of single-cell steps a dragged block's view still has to
    /// show, and how long each one takes. Steps come out in the order they
    /// went in and none is ever dropped, so the view never shows a block
    /// crossing a cell it did not pass through.
    /// </summary>
    /// <remarks>
    /// <para><b>Catching up.</b> A fast drag queues steps faster than they
    /// play. Each step's duration is decided when it is dequeued, from the
    /// backlog at that moment (the step itself included): up to
    /// <c>maxLagSteps</c> queued steps play at the configured duration; a
    /// longer backlog shares the time of <c>maxLagSteps</c> steps, so each
    /// takes <c>stepSeconds × maxLagSteps / backlog</c>. However long the
    /// backlog, it drains in about the time <c>maxLagSteps</c> ordinary steps
    /// take, and as it shrinks the steps slow back to normal.</para>
    /// <para>The limit is a bound in time, not in cells: the view can only
    /// trail the logical origin by fewer cells by skipping some, which it
    /// never does.</para>
    /// </remarks>
    public sealed class StepPlayback
    {
        private readonly Queue<Coord> steps = new Queue<Coord>();
        private readonly float stepSeconds;
        private readonly int maxLagSteps;

        /// <summary>An empty playback.</summary>
        /// <param name="stepSeconds">Duration of one step with no backlog. Comes from <c>RuntimeConfig</c>.</param>
        /// <param name="maxLagSteps">The backlog above which steps speed up. Comes from <c>RuntimeConfig</c>.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="stepSeconds"/> is not positive, or
        /// <paramref name="maxLagSteps"/> is less than 1.
        /// </exception>
        public StepPlayback(float stepSeconds, int maxLagSteps)
        {
            if (!(stepSeconds > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(stepSeconds), stepSeconds, "The step duration must be positive.");
            }

            if (maxLagSteps < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxLagSteps), maxLagSteps, "The lag limit must be at least one step.");
            }

            this.stepSeconds = stepSeconds;
            this.maxLagSteps = maxLagSteps;
        }

        /// <summary>Steps queued and not yet dequeued.</summary>
        public int Count => steps.Count;

        /// <summary>Queues the origin one step reached — fed from <see cref="DragController.Stepped"/>.</summary>
        public void Enqueue(Coord origin)
        {
            steps.Enqueue(origin);
        }

        /// <summary>
        /// Takes the next step and the duration to show it in; false when the
        /// queue is empty.
        /// </summary>
        public bool TryDequeue(out Coord origin, out float seconds)
        {
            var backlog = steps.Count;
            if (backlog == 0)
            {
                origin = default;
                seconds = 0f;
                return false;
            }

            origin = steps.Dequeue();
            seconds = backlog <= maxLagSteps ? stepSeconds : stepSeconds * maxLagSteps / backlog;
            return true;
        }

        /// <summary>Drops every queued step — the view is about to be snapped or redrawn.</summary>
        public void Clear()
        {
            steps.Clear();
        }
    }
}
