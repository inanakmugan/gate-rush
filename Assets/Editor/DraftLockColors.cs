using System;
using System.Collections.Generic;
using GateRush.Core;

namespace GateRush.Editor
{
    /// <summary>
    /// The colour of every lock in a draft (Module 24, D47): a lock is known
    /// by the outer colour, at level start, of the block that carries it, and
    /// a key is marked with the colour of the lock it targets. Read once from
    /// a draft, so the window asks per key without scanning the level again.
    /// Plain logic, no <c>UnityEditor</c>.
    /// </summary>
    /// <remarks>
    /// Every block the level will ever hold is searched: the board's, then
    /// every elevator wave's, then every generator queue's. A draft may hold
    /// what <c>Core</c> rejects — two blocks sharing a lock id, a locked
    /// block with no colours — so the first block found with an id gives its
    /// colour, and a locked block with no colours gives none. The validator
    /// reports both faults; this only has to answer without throwing.
    /// </remarks>
    public sealed class DraftLockColors
    {
        private readonly Dictionary<int, BlockColor?> colorByLockId = new Dictionary<int, BlockColor?>();

        private DraftLockColors()
        {
        }

        /// <summary>The lock colours of <paramref name="draft"/> as it stands now.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="draft"/> is null.</exception>
        public static DraftLockColors Of(LevelDraft draft)
        {
            if (draft == null)
            {
                throw new ArgumentNullException(nameof(draft));
            }

            var colors = new DraftLockColors();
            foreach (var block in draft.Blocks)
            {
                colors.Add(block.LockId, block.ColorStack);
            }

            foreach (var elevator in draft.Elevators)
            {
                foreach (var wave in elevator.Waves)
                {
                    foreach (var block in wave.Blocks)
                    {
                        colors.Add(block.LockId, block.ColorStack);
                    }
                }
            }

            foreach (var generator in draft.Generators)
            {
                foreach (var entry in generator.Queue)
                {
                    colors.Add(entry.LockId, entry.ColorStack);
                }
            }

            return colors;
        }

        /// <summary>
        /// The outer colour of the locked block with lock id
        /// <paramref name="lockId"/>; null when no block has that lock id, or
        /// the one that has it has no colours.
        /// </summary>
        public BlockColor? LockColor(int lockId) =>
            colorByLockId.TryGetValue(lockId, out var color) ? color : null;

        private void Add(int? lockId, IReadOnlyList<BlockColor> colorStack)
        {
            if (!lockId.HasValue || colorByLockId.ContainsKey(lockId.Value))
            {
                return;
            }

            colorByLockId[lockId.Value] = colorStack != null && colorStack.Count > 0 ? colorStack[0] : (BlockColor?)null;
        }
    }
}
