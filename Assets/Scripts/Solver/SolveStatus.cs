namespace GateRush.Solver
{
    /// <summary>
    /// The three outcomes a search can report (see <c>DECISIONS.md</c> D4).
    /// </summary>
    /// <remarks>
    /// <see cref="Indeterminate"/> is not a softer <see cref="Unsolvable"/>: it
    /// means a budget limit stopped the search before it could prove anything.
    /// The editor renders the three in three colours so a designer never reads a
    /// timeout as a broken level. A boolean plus a separate "timed out" flag was
    /// rejected — callers forget to check the flag.
    /// </remarks>
    public enum SolveStatus
    {
        /// <summary>A solution was found; <see cref="SolveResult.Solution"/> is
        /// the shortest one.</summary>
        Solvable,

        /// <summary>The search exhausted the reachable state space (within the
        /// requested <see cref="MoveGenMode"/>) without a solution and without
        /// hitting any budget limit.</summary>
        Unsolvable,

        /// <summary>A budget limit — depth, explored-state count, or wall clock —
        /// stopped the search first. Says nothing about whether the level is
        /// solvable.</summary>
        Indeterminate
    }
}
