using System;
using System.Collections.Generic;

namespace GateRush.Core
{
    /// <summary>
    /// A board-edge source that pushes an explicit, ordered sequence of blocks
    /// inward — the inverse of a gate (M6).
    /// </summary>
    public sealed class GeneratorDefinition
    {
        public int Id { get; }
        public BoardEdge Edge { get; }
        public int Offset { get; }
        public IReadOnlyList<SpawnedBlock> Queue { get; }

        public GeneratorDefinition(int id, BoardEdge edge, int offset, IReadOnlyList<SpawnedBlock> queue)
        {
            Id = id;
            Edge = edge;
            Offset = offset;
            Queue = new List<SpawnedBlock>(queue ?? Array.Empty<SpawnedBlock>()).AsReadOnly();

            for (var i = 0; i < Queue.Count; i++)
            {
                if (Queue[i].RegionOrigin.HasValue)
                {
                    throw new ArgumentException(
                        $"Generator {id} queue entry {i} carries a RegionOrigin. Generator output has no " +
                        "authored position — it derives from the generator's edge and offset — so the value " +
                        "would do nothing; it belongs only on elevator wave blocks (M9).");
                }
            }
        }
    }
}
