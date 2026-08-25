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
        private static readonly Coord[] OrthogonalNeighbors =
        {
            new Coord(1, 0),
            new Coord(-1, 0),
            new Coord(0, 1),
            new Coord(0, -1)
        };

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

            var reached = new HashSet<Coord> { cells[0] };
            var frontier = new Queue<Coord>();
            frontier.Enqueue(cells[0]);

            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();
                foreach (var offset in OrthogonalNeighbors)
                {
                    var neighbor = current + offset;
                    if (seen.Contains(neighbor) && reached.Add(neighbor))
                    {
                        frontier.Enqueue(neighbor);
                    }
                }
            }

            if (reached.Count != seen.Count)
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

        public static void ValidateLock(int? lockId, int requiredKeyCount, string ownerDescription)
        {
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
