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
        public int? UnfreezeAtClearCount { get; }
        public int? LockId { get; }

        public BlockSpec(BlockDefinition block)
        {
            Cells = block.Cells;
            ColorStack = block.ColorStack;
            UnfreezeAtClearCount = block.UnfreezeAtClearCount;
            LockId = block.LockId;
        }

        public BlockSpec(SpawnedBlock block)
        {
            Cells = block.Cells;
            ColorStack = block.ColorStack;
            UnfreezeAtClearCount = block.UnfreezeAtClearCount;
            LockId = block.LockId;
        }
    }
}
