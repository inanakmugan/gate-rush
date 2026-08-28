using System.Collections.Generic;

namespace GateRush.Core
{
    /// <summary>
    /// A block awaiting delivery by a <see cref="GeneratorDefinition"/> or
    /// <see cref="ElevatorDefinition"/>. Carries the same shape, colour stack,
    /// axis, and modifier fields as <see cref="BlockDefinition"/>. It has no
    /// absolute start position — that derives from where it spawns from — but an
    /// elevator wave block additionally carries a <see cref="RegionOrigin"/>,
    /// because a region usually admits several tilings and the author has to say
    /// which one this wave is (M9).
    /// </summary>
    /// <remarks>
    /// <para><b>Cell normalisation.</b> <see cref="Cells"/> is stored shifted so
    /// its component-wise minimum is <c>(0, 0)</c>, matching
    /// <see cref="BlockDefinition"/> (see <c>DECISIONS.md</c> D30). The spawn
    /// placement computed when this block is delivered (Module 03 / phase 1.13)
    /// treats that normalised minimum corner as the block's origin.</para>
    /// <para><b><see cref="RegionOrigin"/>.</b> Meaningful only for elevator
    /// waves, where it is the grid cell — relative to the elevator region's
    /// <see cref="ElevatorDefinition.Min"/> corner — that the normalised
    /// footprint's minimum corner occupies. It is <c>null</c> for generator
    /// output, whose placement is fully determined by the generator's edge and
    /// offset; <see cref="GeneratorDefinition"/> rejects a queued block that
    /// carries one, since the value would silently do nothing. When present it is
    /// shifted by the same offset as <see cref="Cells"/> so that the absolute
    /// positions <c>region.Min + RegionOrigin + cell</c> are unchanged by
    /// normalisation.</para>
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

        /// <summary>
        /// For an elevator wave block, the position of the normalised footprint's
        /// minimum corner relative to the elevator region's <c>Min</c>. Null for
        /// generator output. See the type remarks.
        /// </summary>
        public Coord? RegionOrigin { get; }

        public SpawnedBlock(
            IReadOnlyList<Coord> cells,
            IReadOnlyList<BlockColor> colorStack,
            MovementAxis axis,
            int? unfreezeAtClearCount,
            int? lockId,
            int requiredKeyCount,
            int? keyTargetLockId,
            KeyEffect keyEffect,
            int timeBonusSeconds,
            Coord? regionOrigin = null)
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
            RegionOrigin = regionOrigin.HasValue ? regionOrigin.Value + minCorner : (Coord?)null;
        }
    }
}
