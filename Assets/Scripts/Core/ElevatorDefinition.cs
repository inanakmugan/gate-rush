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
