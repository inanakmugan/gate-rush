using System.Collections.Generic;

namespace GateRush.Core
{
    /// <summary>
    /// The fields <see cref="BlockDefinition"/> and <see cref="SpawnedBlock"/>
    /// share. <see cref="LevelContext"/> resolves every block index — top-level
    /// or a generator/elevator spawn slot — to one of these, so callers that
    /// only need this data never have to care which kind of definition backs
    /// a given index.
    /// </summary>
    public readonly struct BlockSpec
    {
        public IReadOnlyList<Coord> Cells { get; }
        public IReadOnlyList<BlockColor> ColorStack { get; }

        /// <summary>
        /// Which cardinal directions this block may move along (M7). Resolved
        /// through this struct rather than read from <see cref="BlockDefinition"/>
        /// directly so that <c>MoveResolver</c> and <c>MoveGenerator</c>, which
        /// address blocks by flat index, do not need a second lookup path for
        /// generator/elevator spawn slots. See <c>DECISIONS.md</c> D29.
        /// </summary>
        public MovementAxis Axis { get; }

        public int? UnfreezeAtClearCount { get; }
        public int? LockId { get; }

        /// <summary>
        /// Seconds this block adds to the level's countdown when it is destroyed
        /// — its final colour cleared, not each clear of a stack (M10). Reported
        /// by <c>MoveResolver</c> as an output; never stored in
        /// <see cref="BoardState"/>, since <c>Core</c> has no countdown
        /// (<c>DECISIONS.md</c> D12). Zero for a block carrying no bonus.
        /// </summary>
        public int TimeBonusSeconds { get; }

        public BlockSpec(BlockDefinition block)
        {
            Cells = block.Cells;
            ColorStack = block.ColorStack;
            Axis = block.Axis;
            UnfreezeAtClearCount = block.UnfreezeAtClearCount;
            LockId = block.LockId;
            TimeBonusSeconds = block.TimeBonusSeconds;
        }

        public BlockSpec(SpawnedBlock block)
        {
            Cells = block.Cells;
            ColorStack = block.ColorStack;
            Axis = block.Axis;
            UnfreezeAtClearCount = block.UnfreezeAtClearCount;
            LockId = block.LockId;
            TimeBonusSeconds = block.TimeBonusSeconds;
        }
    }
}
