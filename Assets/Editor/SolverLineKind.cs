using System;

namespace GateRush.Editor
{
    /// <summary>
    /// Which of the four tints the Level Editor's solver line takes. Tracks the
    /// answer being shown, not the pipeline's progress: a run in progress or a
    /// cancelled one has no answer yet, so both read as <see cref="NotRun"/>.
    /// </summary>
    public enum SolverLineKind
    {
        NotRun,
        Solvable,
        Unsolvable,
        Indeterminate,
    }

    /// <summary>
    /// Maps what the window knows about the last Validate to a
    /// <see cref="SolverLineKind"/>. Kept out of the window so the choice is
    /// testable without drawing anything.
    /// </summary>
    public static class SolverLineKinds
    {
        /// <summary>
        /// The kind for a line showing <paramref name="verdict"/>, or
        /// <see cref="SolverLineKind.NotRun"/> when there is none to show — never
        /// run, cancelled, not validatable, or still running. The caller passes
        /// null while a run is in progress even if an older verdict is held, since
        /// the line is then showing progress, not that verdict.
        /// </summary>
        public static SolverLineKind Of(LevelSolveVerdict? verdict)
        {
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
