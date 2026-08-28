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
        /// How many keys the lock this block owns requires before its effect
        /// fires (M8). Meaningful only when <see cref="LockId"/> has a value;
        /// zero otherwise. Resolved through this struct for the same reason as
        /// <see cref="Axis"/>: <c>MoveResolver.ApplyKeyEffects</c> addresses the
        /// lock's owner by flat index and must not need a second lookup path for
        /// generator/elevator spawn slots. See <c>DECISIONS.md</c> D29.
        /// </summary>
        public int RequiredKeyCount { get; }

        /// <summary>
        /// The id of the lock this block's key targets (M8), or null when the
        /// block carries no key. A block carries a lock <em>or</em> a key, never
        /// both — <see cref="BlockValidation"/> rejects the combination — so this
        /// and <see cref="LockId"/> are never both set.
        /// </summary>
        public int? KeyTargetLockId { get; }

        /// <summary>
        /// What this block's key does to its target lock when consumed (M8).
        /// Meaningful only when <see cref="KeyTargetLockId"/> has a value.
        /// </summary>
        public KeyEffect KeyEffect { get; }

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
            RequiredKeyCount = block.RequiredKeyCount;
            KeyTargetLockId = block.KeyTargetLockId;
            KeyEffect = block.KeyEffect;
            TimeBonusSeconds = block.TimeBonusSeconds;
        }

        public BlockSpec(SpawnedBlock block)
        {
            Cells = block.Cells;
            ColorStack = block.ColorStack;
            Axis = block.Axis;
            UnfreezeAtClearCount = block.UnfreezeAtClearCount;
            LockId = block.LockId;
            RequiredKeyCount = block.RequiredKeyCount;
            KeyTargetLockId = block.KeyTargetLockId;
            KeyEffect = block.KeyEffect;
            TimeBonusSeconds = block.TimeBonusSeconds;
        }
    }
}
