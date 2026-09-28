using GateRush.Editor;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="SolverLineKinds.Of"/>: each verdict gets its own tint,
    /// and no verdict — never run, cancelled, not validatable, or a run still
    /// in progress — reads as not run.
    /// </summary>
    public class SolverLineKindTests
    {
        [Test]
        public void Of_NoVerdict_IsNotRun()
        {
            var kind = SolverLineKinds.Of(null);

            Assert.AreEqual(SolverLineKind.NotRun, kind);
        }

        [TestCase(LevelSolveVerdict.Solvable, SolverLineKind.Solvable)]
        [TestCase(LevelSolveVerdict.Unsolvable, SolverLineKind.Unsolvable)]
        [TestCase(LevelSolveVerdict.Indeterminate, SolverLineKind.Indeterminate)]
        public void Of_Verdict_IsItsOwnKind(LevelSolveVerdict verdict, SolverLineKind expected)
        {
            var kind = SolverLineKinds.Of(verdict);

            Assert.AreEqual(expected, kind);
        }
    }
}
