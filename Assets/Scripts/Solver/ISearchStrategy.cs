using GateRush.Core;

namespace GateRush.Solver
{
    /// <summary>
    /// Decides whether a level is solvable and, if so, returns a shortest
    /// solution.
    /// </summary>
    /// <remarks>
    /// Breadth-first search is the reference implementation
    /// (<see cref="BreadthFirstStrategy"/>). A\* arrives in phase 1.11 behind this
    /// same interface, and a test asserts the two return the same move count for
    /// every board in the corpus — the proof that A\*'s heuristic is admissible,
    /// and the reason this is an interface rather than one class (see
    /// <c>DECISIONS.md</c> D3).
    /// </remarks>
    public interface ISearchStrategy
    {
        /// <summary>
        /// Searches from <paramref name="initial"/> under <paramref name="budget"/>.
        /// Never throws for an unsolvable or too-large board — that is what
        /// <see cref="SolveStatus.Unsolvable"/> and
        /// <see cref="SolveStatus.Indeterminate"/> are for.
        /// </summary>
        SolveResult Search(LevelContext ctx, BoardState initial, SearchBudget budget);
    }
}
