using System;
using System.Collections.Generic;

namespace GateRush.Core
{
    /// <summary>
    /// A rectangular region that delivers explicit, ordered waves of blocks
    /// once emptied of the previous wave (M9).
    /// </summary>
    public sealed class ElevatorDefinition
    {
        public int Id { get; }
        public Coord Min { get; }
        public Coord Max { get; }
        public IReadOnlyList<IReadOnlyList<SpawnedBlock>> Waves { get; }

        public ElevatorDefinition(int id, Coord min, Coord max, IReadOnlyList<IReadOnlyList<SpawnedBlock>> waves)
        {
            if (min.X > max.X || min.Y > max.Y)
            {
                throw new ArgumentException($"Elevator {id} has Min {min} greater than Max {max}.");
            }

            Id = id;
            Min = min;
            Max = max;
            Waves = CopyWaves(waves);

            ValidateWaveTilings();
        }

        /// <summary>
        /// Every non-empty wave must tile the region <c>[Min, Max]</c> exactly —
        /// every cell covered once, no gaps, no overlaps, nothing outside (M9).
        /// The tiling is authored, so a wave that does not tile is a level-data
        /// error, reported here with the elevator id, the wave index, and what is
        /// wrong. See <see cref="ElevatorTiling"/>, which the Level Editor shares
        /// to warn about the same fault before a draft becomes a level.
        /// </summary>
        private void ValidateWaveTilings()
        {
            for (var w = 0; w < Waves.Count; w++)
            {
                var wave = Waves[w];
                if (wave.Count == 0)
                {
                    continue;
                }

                var tiling = ElevatorTiling.Check(Min, Max, wave);
                if (tiling.IsExact)
                {
                    continue;
                }

                if (tiling.BlocksWithoutRegionOrigin.Count > 0)
                {
                    throw new ArgumentException(
                        $"Elevator {Id} wave {w} block {tiling.BlocksWithoutRegionOrigin[0]} has no " +
                        "RegionOrigin; every block in an elevator wave needs a position relative to the " +
                        "region's Min corner (M9).");
                }

                if (tiling.OutsideRegionCells.Count > 0)
                {
                    throw new ArgumentException(
                        $"Elevator {Id} wave {w} places a block cell at {tiling.OutsideRegionCells[0]}, " +
                        $"outside the region [{Min}, {Max}].");
                }

                if (tiling.OverlappingCells.Count > 0)
                {
                    throw new ArgumentException(
                        $"Elevator {Id} wave {w} has two blocks overlapping at {tiling.OverlappingCells[0]}.");
                }

                throw new ArgumentException(
                    $"Elevator {Id} wave {w} leaves {tiling.UncoveredCells.Count} cell(s) of the region " +
                    $"[{Min}, {Max}] uncovered, the first at {tiling.UncoveredCells[0]}; a wave must tile its " +
                    "region exactly (M9).");
            }
        }

        private static IReadOnlyList<IReadOnlyList<SpawnedBlock>> CopyWaves(
            IReadOnlyList<IReadOnlyList<SpawnedBlock>> waves)
        {
            if (waves == null)
            {
                return Array.Empty<IReadOnlyList<SpawnedBlock>>();
            }

            var copy = new List<IReadOnlyList<SpawnedBlock>>(waves.Count);
            foreach (var wave in waves)
            {
                copy.Add(new List<SpawnedBlock>(wave).AsReadOnly());
            }

            return copy.AsReadOnly();
        }
    }
}
