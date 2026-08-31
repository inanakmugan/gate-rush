using System;
using System.Collections.Generic;

namespace GateRush.Core
{
    /// <summary>
    /// Validation shared by <see cref="BlockDefinition"/> and <see cref="SpawnedBlock"/>,
    /// the two places a block's shape, colour stack, and lock are defined.
    /// </summary>
    internal static class BlockValidation
    {
        /// <summary>
        /// The component-wise minimum corner of a non-empty cell set — the
        /// offset by which <see cref="BlockDefinition"/> and
        /// <see cref="SpawnedBlock"/> shift their cells at construction so the
        /// minimum becomes <c>(0, 0)</c>. Call only after
        /// <see cref="ValidateCells"/> has confirmed the set is non-empty. See
        /// <c>DECISIONS.md</c> D30 for why that normalisation is done.
        /// </summary>
        public static Coord MinCorner(IReadOnlyList<Coord> cells)
        {
            var minX = int.MaxValue;
            var minY = int.MaxValue;

            foreach (var cell in cells)
            {
                if (cell.X < minX)
                {
                    minX = cell.X;
                }

                if (cell.Y < minY)
                {
                    minY = cell.Y;
                }
            }

            return new Coord(minX, minY);
        }

        public static void ValidateCells(IReadOnlyList<Coord> cells, string ownerDescription)
        {
            if (cells == null || cells.Count == 0)
            {
                throw new ArgumentException($"{ownerDescription} must have at least one cell.");
            }

            var seen = new HashSet<Coord>();
            foreach (var cell in cells)
            {
                if (!seen.Add(cell))
                {
                    throw new ArgumentException($"{ownerDescription} has a duplicate cell at {cell}.");
                }
            }

            if (!BlockShape.IsOrthogonallyConnected(cells))
            {
                throw new ArgumentException(
                    $"{ownerDescription} has cells that are not orthogonally connected.");
            }
        }

        public static void ValidateColorStack(IReadOnlyList<BlockColor> colorStack, string ownerDescription)
        {
            if (colorStack == null || colorStack.Count == 0)
            {
                throw new ArgumentException($"{ownerDescription} must have a non-empty colour stack.");
            }

            for (var i = 1; i < colorStack.Count; i++)
            {
                if (colorStack[i] == colorStack[i - 1])
                {
                    throw new ArgumentException(
                        $"{ownerDescription} has adjacent colour-stack entries of the same colour " +
                        $"({colorStack[i]}) at positions {i - 1} and {i}.");
                }
            }
        }

        public static void ValidateLock(
            int? lockId, int requiredKeyCount, int? keyTargetLockId, string ownerDescription)
        {
            if (lockId.HasValue && keyTargetLockId.HasValue)
            {
                throw new ArgumentException(
                    $"{ownerDescription} carries both a lock (id {lockId.Value}) and a key (targeting lock " +
                    $"{keyTargetLockId.Value}); a block may carry one or the other, never both.");
            }

            if (lockId.HasValue && requiredKeyCount < 1)
            {
                throw new ArgumentException(
                    $"{ownerDescription} is locked but its required key count is {requiredKeyCount}; " +
                    "it must be at least 1.");
            }
        }

        public static void ValidateUnfreezeAtClearCount(int? unfreezeAtClearCount, string ownerDescription)
        {
            if (unfreezeAtClearCount.HasValue && unfreezeAtClearCount.Value < 0)
            {
                throw new ArgumentException(
                    $"{ownerDescription} has a negative UnfreezeAtClearCount ({unfreezeAtClearCount.Value}).");
            }
        }

        public static void ValidateTimeBonus(int timeBonusSeconds, string ownerDescription)
        {
            if (timeBonusSeconds < 0)
            {
                throw new ArgumentException(
                    $"{ownerDescription} has a negative TimeBonusSeconds ({timeBonusSeconds}).");
            }
        }
    }
}
