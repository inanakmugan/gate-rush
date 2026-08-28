using System.Collections.Generic;

namespace GateRush.Core
{
    /// <summary>
    /// A block awaiting delivery by a <see cref="GeneratorDefinition"/> or
    /// <see cref="ElevatorDefinition"/>. Carries the same shape, colour stack,
    /// axis, and modifier fields as <see cref="BlockDefinition"/>; its position
    /// is not stored here because it derives from where it spawns from.
    /// </summary>
    /// <remarks>
    /// <b>Cell normalisation.</b> <see cref="Cells"/> is stored shifted so its
    /// component-wise minimum is <c>(0, 0)</c>, matching
    /// <see cref="BlockDefinition"/> (see <c>DECISIONS.md</c> D30). The spawn
    /// placement computed when this block is delivered (Module 03 / phase 1.13)
    /// treats that normalised minimum corner as the block's origin.
    /// </remarks>
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
            BlockValidation.ValidateLock(lockId, requiredKeyCount, keyTargetLockId, description);
            BlockValidation.ValidateUnfreezeAtClearCount(unfreezeAtClearCount, description);
            BlockValidation.ValidateTimeBonus(timeBonusSeconds, description);

            var minCorner = BlockValidation.MinCorner(cells);
            var normalizedCells = new Coord[cells.Count];
            for (var i = 0; i < cells.Count; i++)
            {
                normalizedCells[i] = cells[i] - minCorner;
            }

            Cells = new List<Coord>(normalizedCells).AsReadOnly();
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
