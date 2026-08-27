namespace GateRush.Solver
{
    /// <summary>
    /// Selects how <see cref="MoveGenerator"/> enumerates the moves available
    /// from a board state.
    /// </summary>
    public enum MoveGenMode
    {
        /// <summary>
        /// Only the positions that can plausibly matter — gate-aligned landings,
        /// the zero-distance gate clear, positions that change a generator's or
        /// elevator's region occupancy, and positions where the block comes to
        /// rest against an obstacle. A strict subset of <see cref="Exhaustive"/>;
        /// pruning it can only ever produce false negatives (<c>DECISIONS.md</c>
        /// D5), which the editor covers by falling back to <see cref="Exhaustive"/>.
        /// </summary>
        Canonical,

        /// <summary>
        /// Every position each movable block can reach, plus the zero-distance
        /// move where the block is already flush against a compatible open gate.
        /// This is the player's full move set.
        /// </summary>
        Exhaustive
    }
}
