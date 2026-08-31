using System;
using System.Collections.Generic;
using GateRush.Core;

namespace GateRush.Editor
{
    /// <summary>
    /// Runs <see cref="ElevatorTiling"/> over an elevator draft's wave — the
    /// draft-side bridge to <c>Core</c>'s checker. Shared so the live warning
    /// (<see cref="DraftValidator"/>) and the wave-list status in the window's
    /// properties panel are the same computation, never two.
    /// </summary>
    public static class DraftTiling
    {
        /// <summary>
        /// The tiling result for <paramref name="wave"/> against
        /// <paramref name="elevator"/>'s region, or <c>null</c> when a block's
        /// shape is too broken to place at all (that fault surfaces through
        /// <see cref="LevelDraft.ToContext"/> instead).
        /// </summary>
        public static ElevatorTiling.Result Check(ElevatorDraft elevator, WaveDraft wave)
        {
            var converted = new List<SpawnedBlock>();
            foreach (var block in wave.Blocks)
            {
                if (!TryConvert(block, out var spawned))
                {
                    return null;
                }

                converted.Add(spawned);
            }

            return ElevatorTiling.Check(elevator.Min, elevator.Max, converted);
        }

        /// <summary>
        /// Converts a draft spawned block to its <c>Core</c> form, returning
        /// false rather than throwing when the shape or colour stack is invalid.
        /// </summary>
        public static bool TryConvert(SpawnedBlockDraft draft, out SpawnedBlock spawned)
        {
            try
            {
                spawned = new SpawnedBlock(
                    cells: draft.Cells.ToArray(),
                    colorStack: draft.ColorStack.ToArray(),
                    axis: draft.Axis,
                    unfreezeAtClearCount: draft.UnfreezeAtClearCount,
                    lockId: draft.LockId,
                    requiredKeyCount: draft.RequiredKeyCount,
                    keyTargetLockId: draft.KeyTargetLockId,
                    keyEffect: draft.KeyEffect,
                    timeBonusSeconds: draft.TimeBonusSeconds,
                    regionOrigin: draft.RegionOrigin);
                return true;
            }
            catch (ArgumentException)
            {
                spawned = null;
                return false;
            }
        }
    }
}
