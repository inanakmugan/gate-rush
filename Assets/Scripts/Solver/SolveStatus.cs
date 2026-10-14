namespace GateRush.Solver
{
    /// <summary>
    /// The three outcomes a search can report (see <c>DECISIONS.md</c> D4).
    /// </summary>
    /// <remarks>
    /// <see cref="Indeterminate"/> is not a softer <see cref="Unsolvable"/>: it
    /// means the search stopped before it could prove anything. The editor
    /// renders the three in three colours so a designer never reads a timeout
    /// as a broken level. A boolean plus a separate "timed out" flag was
    /// rejected — callers forget to check the flag.
    /// </remarks>
    public enum SolveStatus
    {
        /// <summary>A solution was found; <see cref="SolveResult.Solution"/> is a
        /// verified one. Says nothing about whether it is the shortest — that is
        /// <see cref="SolveResult.ProvenShortestLength"/>'s job.</summary>
        Solvable,

        /// <summary>The search proved there is no solution within the requested
        /// <see cref="MoveGenMode"/>, without hitting any budget limit.
        /// <see cref="BreadthFirstStrategy"/> and <see cref="AStarStrategy"/>
        /// prove it by exhausting the reachable state space;
        /// <see cref="NearestNextClearStrategy"/> by exhausting one stratum
        /// with no clear reachable from it, on a level where
        /// <see cref="GateRush.Core.LevelContext.IsClearMonotone"/> holds.</summary>
        Unsolvable,

        /// <summary>The search stopped without a solution and without a proof
        /// that there is none: a budget limit — depth, explored-state count, or
        /// wall clock — cut it short, or <see cref="NearestNextClearStrategy"/>
        /// reached a dead end on a level that is not clear-monotone, where a
        /// dead end proves nothing. Says nothing about whether the level is
        /// solvable.</summary>
        Indeterminate
    }
}
