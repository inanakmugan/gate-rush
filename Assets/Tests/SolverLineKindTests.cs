using GateRush.Editor;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="SolverLineKinds.Of"/>: each verdict gets its own tint,
    /// a failed run reads as an error whatever verdict is held (Module 27),
    /// and neither — never run, cancelled, not validatable, or a run still in
    /// progress — reads as not run.
    /// </summary>
    public class SolverLineKindTests
    {
        [Test]
        public void Of_NoVerdictAndNoError_IsNotRun()
        {
            var kind = SolverLineKinds.Of(null, hasError: false);

            Assert.AreEqual(SolverLineKind.NotRun, kind);
        }

        [TestCase(LevelSolveVerdict.Solvable, SolverLineKind.Solvable)]
        [TestCase(LevelSolveVerdict.Unsolvable, SolverLineKind.Unsolvable)]
        [TestCase(LevelSolveVerdict.Indeterminate, SolverLineKind.Indeterminate)]
        public void Of_Verdict_IsItsOwnKind(LevelSolveVerdict verdict, SolverLineKind expected)
        {
            var kind = SolverLineKinds.Of(verdict, hasError: false);

            Assert.AreEqual(expected, kind);
        }

        [Test]
        public void Of_ErrorWithNoVerdict_IsError()
        {
            // The case the window meets: a failed run has no verdict, and
            // used to read as not run.
            var kind = SolverLineKinds.Of(null, hasError: true);

            Assert.AreEqual(SolverLineKind.Error, kind);
        }

        [TestCase(LevelSolveVerdict.Solvable)]
        [TestCase(LevelSolveVerdict.Unsolvable)]
        [TestCase(LevelSolveVerdict.Indeterminate)]
        public void Of_ErrorWithAVerdict_IsStillError(LevelSolveVerdict verdict)
        {
            // A failure must never read as an answer.
            var kind = SolverLineKinds.Of(verdict, hasError: true);

            Assert.AreEqual(SolverLineKind.Error, kind);
        }
    }
}
