using System.Collections.Generic;
using GateRush.Core;
using GateRush.Solver;

namespace GateRush.Tests
{
    /// <summary>
    /// A <see cref="MoveGenerator"/> that disagrees with the resolver: it emits
    /// exactly one move, block 0 to <see cref="Target"/> — a cell outside every
    /// test grid, so <see cref="MoveResolver"/> always rejects it. Proves each
    /// strategy throws on a rejected generated move instead of skipping it.
    /// </summary>
    internal sealed class IllegalMoveGenerator : MoveGenerator
    {
        /// <summary>The target of the one move emitted; outside any grid a test builds.</summary>
        internal static readonly Coord Target = new Coord(99, 99);

        /// <inheritdoc />
        public override IEnumerable<Move> Generate(LevelContext ctx, BoardState state, MoveGenMode mode) =>
            new[] { new Move(0, Target) };
    }
}
