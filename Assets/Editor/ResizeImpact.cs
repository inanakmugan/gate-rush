using System.Collections.Generic;
using GateRush.Core;

namespace GateRush.Editor
{
    /// <summary>
    /// What a grid resize would push outside the board — reported by
    /// <see cref="LevelDraft.PreviewResize"/> before anything changes, so the
    /// editor can say "Shrinking to 5x7 removes 3 blocks, 1 gate and 1 shutter.
    /// Continue?" rather than silently dropping them (Module 09, no irreversible
    /// edit without confirmation).
    /// </summary>
    public sealed class ResizeImpact
    {
        public int NewWidth { get; }
        public int NewHeight { get; }
        public IReadOnlyList<int> RemovedBlockIds { get; }
        public IReadOnlyList<int> RemovedGateIds { get; }
        public IReadOnlyList<int> RemovedShutterIds { get; }
        public IReadOnlyList<int> RemovedGeneratorIds { get; }
        public IReadOnlyList<int> RemovedElevatorIds { get; }
        public IReadOnlyList<Coord> RemovedStaticWalls { get; }

        /// <summary>True when nothing escapes — always so for a grow.</summary>
        public bool IsLossless =>
            RemovedBlockIds.Count == 0
            && RemovedGateIds.Count == 0
            && RemovedShutterIds.Count == 0
            && RemovedGeneratorIds.Count == 0
            && RemovedElevatorIds.Count == 0
            && RemovedStaticWalls.Count == 0;

        public int RemovedElementCount =>
            RemovedBlockIds.Count
            + RemovedGateIds.Count
            + RemovedShutterIds.Count
            + RemovedGeneratorIds.Count
            + RemovedElevatorIds.Count
            + RemovedStaticWalls.Count;

        public ResizeImpact(
            int newWidth,
            int newHeight,
            IReadOnlyList<int> removedBlockIds,
            IReadOnlyList<int> removedGateIds,
            IReadOnlyList<int> removedShutterIds,
            IReadOnlyList<int> removedGeneratorIds,
            IReadOnlyList<int> removedElevatorIds,
            IReadOnlyList<Coord> removedStaticWalls)
        {
            NewWidth = newWidth;
            NewHeight = newHeight;
            RemovedBlockIds = removedBlockIds;
            RemovedGateIds = removedGateIds;
            RemovedShutterIds = removedShutterIds;
            RemovedGeneratorIds = removedGeneratorIds;
            RemovedElevatorIds = removedElevatorIds;
            RemovedStaticWalls = removedStaticWalls;
        }
    }
}
