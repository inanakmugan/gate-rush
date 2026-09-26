using GateRush.Core;

namespace GateRush.Solver
{
    /// <summary>
    /// Decides whether a level is solvable and, if so, returns a verified
    /// solution together with what is known about its length.
    /// </summary>
    /// <remarks>
    /// <para>A strategy need not return a shortest solution; it reports how
    /// good its answer is through <see cref="SolveResult.LengthLowerBound"/>
    /// and <see cref="SolveResult.ProvenShortestLength"/>. The two strategies
    /// below both return a shortest solution within the requested
    /// <see cref="MoveGenMode"/>, which proves the shortest overall only under
    /// <see cref="MoveGenMode.Exhaustive"/>.</para>
    /// <para>Breadth-first search is the reference implementation
    /// (<see cref="BreadthFirstStrategy"/>). A\* arrives in phase 1.11 behind this
    /// same interface, and a test asserts the two return the same move count for
    /// every board in the corpus — the proof that A\*'s heuristic is admissible,
    /// and the reason this is an interface rather than one class (see
    /// <c>DECISIONS.md</c> D3).</para>
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
