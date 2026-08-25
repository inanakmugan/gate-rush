using System.Collections.Generic;

namespace GateRush.Core
{
    /// <summary>
    /// The unchanging definition of one block: its shape, colour stack, starting
    /// position, and every optional restriction (frozen, locked, axis, key,
    /// time bonus) it may carry. A block may hold any combination of these at
    /// once, so they are a flat set of optional fields rather than a type
    /// hierarchy.
    /// </summary>
    public sealed class BlockDefinition
    {
        public int Id { get; }
        public IReadOnlyList<Coord> Cells { get; }
        public IReadOnlyList<BlockColor> ColorStack { get; }
        public Coord StartOrigin { get; }
        public MovementAxis Axis { get; }
        public int? UnfreezeAtClearCount { get; }
        public int? LockId { get; }
        public int RequiredKeyCount { get; }
        public int? KeyTargetLockId { get; }
        public KeyEffect KeyEffect { get; }
        public int TimeBonusSeconds { get; }

        public BlockDefinition(
            int id,
            IReadOnlyList<Coord> cells,
            IReadOnlyList<BlockColor> colorStack,
            Coord startOrigin,
            MovementAxis axis,
            int? unfreezeAtClearCount,
            int? lockId,
            int requiredKeyCount,
            int? keyTargetLockId,
            KeyEffect keyEffect,
            int timeBonusSeconds)
        {
            var description = $"Block {id}";

            BlockValidation.ValidateCells(cells, description);
            BlockValidation.ValidateColorStack(colorStack, description);
            BlockValidation.ValidateLock(lockId, requiredKeyCount, description);
            BlockValidation.ValidateUnfreezeAtClearCount(unfreezeAtClearCount, description);
            BlockValidation.ValidateTimeBonus(timeBonusSeconds, description);

            Id = id;
            Cells = new List<Coord>(cells).AsReadOnly();
            ColorStack = new List<BlockColor>(colorStack).AsReadOnly();
            StartOrigin = startOrigin;
            Axis = axis;
            UnfreezeAtClearCount = unfreezeAtClearCount;
            LockId = lockId;
            RequiredKeyCount = requiredKeyCount;
            KeyTargetLockId = keyTargetLockId;
            KeyEffect = keyEffect;
            TimeBonusSeconds = timeBonusSeconds;
        }
    }
}
