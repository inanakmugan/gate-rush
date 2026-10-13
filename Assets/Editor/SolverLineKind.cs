using System;

namespace GateRush.Editor
{
    /// <summary>
    /// Which of the five tints the Level Editor's solver line takes. Tracks the
    /// answer being shown, not the pipeline's progress: a run in progress or a
    /// cancelled one has no answer yet, so both read as <see cref="NotRun"/>.
    /// </summary>
    public enum SolverLineKind
    {
        NotRun,
        Solvable,
        Unsolvable,
        Indeterminate,

        /// <summary>
        /// The last run ended in a solver failure — a bug, never a verdict —
        /// so the line shows the failure instead of passing for not run.
        /// </summary>
        Error,
    }

    /// <summary>
    /// Maps what the window knows about the last Validate to a
    /// <see cref="SolverLineKind"/>. Kept out of the window so the choice is
    /// testable without drawing anything.
    /// </summary>
    public static class SolverLineKinds
    {
        /// <summary>
        /// The kind for a line showing <paramref name="verdict"/>;
        /// <see cref="SolverLineKind.Error"/> when the last run failed
        /// instead; or <see cref="SolverLineKind.NotRun"/> when there is
        /// neither to show — never run, cancelled, not validatable, or still
        /// running. The caller passes null and false while a run is in
        /// progress even if an older verdict or error is held, since the line
        /// is then showing progress, not that answer.
        /// </summary>
        /// <param name="verdict">The verdict on show, or null when there is none.</param>
        /// <param name="hasError">
        /// Whether the last run ended in a solver failure. A failed run has no
        /// verdict, so an error wins over whatever <paramref name="verdict"/>
        /// holds: a failure must never read as an answer.
        /// </param>
        public static SolverLineKind Of(LevelSolveVerdict? verdict, bool hasError)
        {
            if (hasError)
            {
                return SolverLineKind.Error;
            }

            if (!verdict.HasValue)
            {
                return SolverLineKind.NotRun;
            }

            switch (verdict.Value)
            {
                case LevelSolveVerdict.Solvable:
                    return SolverLineKind.Solvable;
                case LevelSolveVerdict.Unsolvable:
                    return SolverLineKind.Unsolvable;
                case LevelSolveVerdict.Indeterminate:
                    return SolverLineKind.Indeterminate;
                default:
                    throw new ArgumentOutOfRangeException(nameof(verdict), verdict, "Unknown solve verdict.");
            }
        }
    }
}
