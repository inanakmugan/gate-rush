using System;
using System.Collections.Generic;

namespace GateRush.Core
{
    /// <summary>
    /// Checks whether an elevator wave's blocks tile the elevator's region
    /// exactly (M9): every cell covered once, no gaps, no overlaps, nothing
    /// outside. This helper only <em>reports</em> what it finds and decides
    /// nothing — <see cref="ElevatorDefinition"/>'s constructor turns a
    /// non-exact result into a thrown authoring error, while the Level Editor
    /// surfaces the same result as a live warning against a draft that is not
    /// yet a <see cref="LevelContext"/>. One implementation, two responses.
    /// </summary>
    public static class ElevatorTiling
    {
        /// <summary>
        /// The outcome of tiling one wave against a region. <see cref="IsExact"/>
        /// is true only when every list is empty.
        /// </summary>
        public sealed class Result
        {
            /// <summary>Region cells no block covers.</summary>
            public IReadOnlyList<Coord> UncoveredCells { get; }

            /// <summary>Cells more than one block covers.</summary>
            public IReadOnlyList<Coord> OverlappingCells { get; }

            /// <summary>Block cells that fall outside the region.</summary>
            public IReadOnlyList<Coord> OutsideRegionCells { get; }

            /// <summary>
            /// Indices, within the wave, of blocks that carry no
            /// <see cref="SpawnedBlock.RegionOrigin"/> — an elevator wave block
            /// must have one, so its footprint cannot be placed and it
            /// contributes nothing else to this result.
            /// </summary>
            public IReadOnlyList<int> BlocksWithoutRegionOrigin { get; }

            public bool IsExact =>
                UncoveredCells.Count == 0
                && OverlappingCells.Count == 0
                && OutsideRegionCells.Count == 0
                && BlocksWithoutRegionOrigin.Count == 0;

            internal Result(
                IReadOnlyList<Coord> uncoveredCells,
                IReadOnlyList<Coord> overlappingCells,
                IReadOnlyList<Coord> outsideRegionCells,
                IReadOnlyList<int> blocksWithoutRegionOrigin)
            {
                UncoveredCells = uncoveredCells;
                OverlappingCells = overlappingCells;
                OutsideRegionCells = outsideRegionCells;
                BlocksWithoutRegionOrigin = blocksWithoutRegionOrigin;
            }
        }

        /// <summary>
        /// Tiles <paramref name="wave"/> against the inclusive region
        /// <paramref name="regionMin"/>..<paramref name="regionMax"/>. A block's
        /// absolute footprint is <c>regionMin + block.RegionOrigin + cell</c>.
        /// </summary>
        public static Result Check(
            Coord regionMin, Coord regionMax, IReadOnlyList<SpawnedBlock> wave)
        {
            var covered = new HashSet<Coord>();
            var overlapping = new List<Coord>();
            var outside = new List<Coord>();
            var withoutOrigin = new List<int>();

            // An absent or empty wave covers nothing: every region cell reads as
            // uncovered. A draft under edit reaches here legitimately.
            var blocks = wave ?? Array.Empty<SpawnedBlock>();

            for (var b = 0; b < blocks.Count; b++)
            {
                var block = blocks[b];
                if (!block.RegionOrigin.HasValue)
                {
                    withoutOrigin.Add(b);
                    continue;
                }

                var origin = regionMin + block.RegionOrigin.Value;
                foreach (var cell in block.Cells)
                {
                    var absolute = origin + cell;

                    if (absolute.X < regionMin.X || absolute.X > regionMax.X
                        || absolute.Y < regionMin.Y || absolute.Y > regionMax.Y)
                    {
                        outside.Add(absolute);
                        continue;
                    }

                    if (!covered.Add(absolute))
                    {
                        overlapping.Add(absolute);
                    }
                }
            }

            var uncovered = new List<Coord>();
            for (var y = regionMin.Y; y <= regionMax.Y; y++)
            {
                for (var x = regionMin.X; x <= regionMax.X; x++)
                {
                    var cell = new Coord(x, y);
                    if (!covered.Contains(cell))
                    {
                        uncovered.Add(cell);
                    }
                }
            }

            return new Result(uncovered, overlapping, outside, withoutOrigin);
        }
    }
}
