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
    /// and <see cref="SolveResult.ProvenShortestLength"/>. There are three
    /// strategies. <see cref="BreadthFirstStrategy"/> and
    /// <see cref="AStarStrategy"/> return a shortest solution within the
    /// requested <see cref="MoveGenMode"/>, which proves the shortest overall
    /// only under <see cref="MoveGenMode.Exhaustive"/>.
    /// <see cref="NearestNextClearStrategy"/> returns a verified solution that
    /// need not be the shortest, and accepts only an exhaustive budget.</para>
    /// <para>Breadth-first search is the reference implementation. A\* sits
    /// behind this same interface, and a test asserts the two return the same
    /// move count for every board in the corpus — the proof that A\*'s
    /// heuristic is admissible, and the reason this is an interface rather
    /// than one class (see <c>DECISIONS.md</c> D3).</para>
    /// </remarks>
    public interface ISearchStrategy
    {
        /// <summary>
        /// Searches from <paramref name="initial"/> under <paramref name="budget"/>.
        /// Never throws for an unsolvable or too-large board — that is what
        /// <see cref="SolveStatus.Unsolvable"/> and
        /// <see cref="SolveStatus.Indeterminate"/> are for.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">An argument is null.</exception>
        /// <exception cref="System.ArgumentException">
        /// The strategy cannot run under <paramref name="budget"/>'s
        /// <see cref="MoveGenMode"/>: <see cref="NearestNextClearStrategy"/>
        /// with a canonical one.
        /// </exception>
        /// <exception cref="System.OperationCanceledException">
        /// <see cref="SearchBudget.Cancellation"/> was cancelled; a cancelled
        /// search has no result.
        /// </exception>
        /// <exception cref="System.InvalidOperationException">
        /// A solver bug, never a property of the level: the resolver rejected a
        /// move the generator emitted, or a strategy caught its own invariant
        /// broken.
        /// </exception>
        SolveResult Search(LevelContext ctx, BoardState initial, SearchBudget budget);
    }
}
