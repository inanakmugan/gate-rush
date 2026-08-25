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
            Queue = new List<SpawnedBlock>(queue ?? System.Array.Empty<SpawnedBlock>()).AsReadOnly();
        }
    }
}
