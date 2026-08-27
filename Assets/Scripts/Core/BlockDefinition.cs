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
    /// <remarks>
    /// <b>Cell normalisation.</b> <see cref="Cells"/> is stored shifted so its
    /// component-wise minimum is <c>(0, 0)</c>, and <see cref="StartOrigin"/> is
    /// compensated by the same offset. Absolute cell positions
    /// (<c>StartOrigin + cell</c>) are unchanged, but a caller that reads
    /// <see cref="StartOrigin"/> back gets the normalised value — always the
    /// grid cell of the footprint's minimum corner. See <c>DECISIONS.md</c> D30.
    /// </remarks>
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

            var minCorner = BlockValidation.MinCorner(cells);
            var normalizedCells = new Coord[cells.Count];
            for (var i = 0; i < cells.Count; i++)
            {
                normalizedCells[i] = cells[i] - minCorner;
            }

            Id = id;
            Cells = new List<Coord>(normalizedCells).AsReadOnly();
            ColorStack = new List<BlockColor>(colorStack).AsReadOnly();
            StartOrigin = startOrigin + minCorner;
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
