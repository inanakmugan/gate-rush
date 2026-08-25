using System.Collections.Generic;

namespace GateRush.Core
{
    /// <summary>
    /// A block awaiting delivery by a <see cref="GeneratorDefinition"/> or
    /// <see cref="ElevatorDefinition"/>. Carries the same shape, colour stack,
    /// axis, and modifier fields as <see cref="BlockDefinition"/>; its position
    /// is not stored here because it derives from where it spawns from.
    /// </summary>
    public sealed class SpawnedBlock
    {
        public IReadOnlyList<Coord> Cells { get; }
        public IReadOnlyList<BlockColor> ColorStack { get; }
        public MovementAxis Axis { get; }
        public int? UnfreezeAtClearCount { get; }
        public int? LockId { get; }
        public int RequiredKeyCount { get; }
        public int? KeyTargetLockId { get; }
        public KeyEffect KeyEffect { get; }
        public int TimeBonusSeconds { get; }

        public SpawnedBlock(
            IReadOnlyList<Coord> cells,
            IReadOnlyList<BlockColor> colorStack,
            MovementAxis axis,
            int? unfreezeAtClearCount,
            int? lockId,
            int requiredKeyCount,
            int? keyTargetLockId,
            KeyEffect keyEffect,
            int timeBonusSeconds)
        {
            const string description = "Spawned block";

            BlockValidation.ValidateCells(cells, description);
            BlockValidation.ValidateColorStack(colorStack, description);
            BlockValidation.ValidateLock(lockId, requiredKeyCount, description);
            BlockValidation.ValidateUnfreezeAtClearCount(unfreezeAtClearCount, description);
            BlockValidation.ValidateTimeBonus(timeBonusSeconds, description);

            Cells = new List<Coord>(cells).AsReadOnly();
            ColorStack = new List<BlockColor>(colorStack).AsReadOnly();
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
