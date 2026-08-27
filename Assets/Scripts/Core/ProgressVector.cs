using System;

namespace GateRush.Core
{
    /// <summary>
    /// The monotonically non-decreasing progress vector that stratifies the
    /// solver's search space (see <c>DECISIONS.md</c> D6, D32):
    /// <see cref="BoardState.TotalClearCount"/>, then every
    /// <see cref="BoardState.GeneratorIndex"/>, then every
    /// <see cref="BoardState.ElevatorWaveIndex"/>, in that order. No action can
    /// decrease any component.
    /// </summary>
    /// <remarks>
    /// <para><b>Ordering.</b> <see cref="CompareTo"/> is lexicographic —
    /// <see cref="BoardState.TotalClearCount"/> first, then the spawn indices.
    /// Lexicographic order is a linear extension of the componentwise partial
    /// order: if one vector is componentwise ≤ another and not equal, it is also
    /// lexicographically smaller. That is exactly the property the search relies
    /// on to retire a stratum's visited set once every live frontier node sits at
    /// a strictly greater vector — nothing componentwise ≤ the retired vector can
    /// still be generated.</para>
    /// <para><b>Not part of the vector:</b>
    /// <see cref="BoardState.ElevatorWaveActive"/>. It is set when a wave arrives
    /// and cleared when the region empties, so it is not monotonic and cannot be
    /// a stratification key. States that differ only by it remain distinct under
    /// <see cref="BoardState"/> hashing.</para>
    /// <para><b>Keep in sync with <see cref="BoardState"/>.</b> When a later phase
    /// adds a monotonic counter to <see cref="BoardState"/> — generator and
    /// elevator progress land in phase 1.13 — <see cref="Of"/> and this summary
    /// must gain the matching component. That obligation is the reason this type
    /// lives in <c>GateRush.Core</c> next to the fields rather than in the solver
    /// (D32).</para>
    /// </remarks>
    public readonly struct ProgressVector : IEquatable<ProgressVector>, IComparable<ProgressVector>
    {
        private static readonly int[] NoSpawnIndices = Array.Empty<int>();

        private readonly int totalClearCount;

        // Generator spawn indices followed by elevator wave indices, in
        // LevelContext order. May be null on default(ProgressVector); read it
        // through SpawnIndices, never directly.
        private readonly int[] spawnIndices;

        private ProgressVector(int totalClearCount, int[] spawnIndices)
        {
            this.totalClearCount = totalClearCount;
            this.spawnIndices = spawnIndices;
        }

        private int[] SpawnIndices => spawnIndices ?? NoSpawnIndices;

        /// <summary>
        /// The progress vector <paramref name="state"/> currently sits at.
        /// </summary>
        public static ProgressVector Of(BoardState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var generatorCount = state.GeneratorIndex.Count;
            var elevatorCount = state.ElevatorWaveIndex.Count;
            var total = generatorCount + elevatorCount;

            if (total == 0)
            {
                return new ProgressVector(state.TotalClearCount, NoSpawnIndices);
            }

            var indices = new int[total];
            for (var i = 0; i < generatorCount; i++)
            {
                indices[i] = state.GeneratorIndex[i];
            }

            for (var i = 0; i < elevatorCount; i++)
            {
                indices[generatorCount + i] = state.ElevatorWaveIndex[i];
            }

            return new ProgressVector(state.TotalClearCount, indices);
        }

        /// <summary>
        /// Lexicographic comparison: <see cref="BoardState.TotalClearCount"/>
        /// first, then the spawn indices in order. See the type remarks on why
        /// this order is the one the search needs.
        /// </summary>
        public int CompareTo(ProgressVector other)
        {
            if (totalClearCount != other.totalClearCount)
            {
                return totalClearCount < other.totalClearCount ? -1 : 1;
            }

            var a = SpawnIndices;
            var b = other.SpawnIndices;

            // Equal length for any two vectors of the same level; the shared /
            // trailing-length handling only matters if vectors from different
            // levels are ever compared, which callers should not do.
            var shared = Math.Min(a.Length, b.Length);
            for (var i = 0; i < shared; i++)
            {
                if (a[i] != b[i])
                {
                    return a[i] < b[i] ? -1 : 1;
                }
            }

            return a.Length.CompareTo(b.Length);
        }

        public bool Equals(ProgressVector other) => CompareTo(other) == 0;

        public override bool Equals(object obj) => obj is ProgressVector other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                const uint fnvOffsetBasis = 2166136261;
                const uint fnvPrime = 16777619;

                var hash = fnvOffsetBasis;
                hash = HashInt(hash, totalClearCount, fnvPrime);

                var indices = SpawnIndices;
                for (var i = 0; i < indices.Length; i++)
                {
                    hash = HashInt(hash, indices[i], fnvPrime);
                }

                return (int)hash;
            }
        }

        private static uint HashInt(uint hash, int value, uint prime)
        {
            unchecked
            {
                hash = (hash ^ (byte)(value & 0xFF)) * prime;
                hash = (hash ^ (byte)((value >> 8) & 0xFF)) * prime;
                hash = (hash ^ (byte)((value >> 16) & 0xFF)) * prime;
                hash = (hash ^ (byte)((value >> 24) & 0xFF)) * prime;
                return hash;
            }
        }

        public override string ToString()
        {
            var indices = SpawnIndices;
            return indices.Length == 0
                ? $"ProgressVector(clears {totalClearCount})"
                : $"ProgressVector(clears {totalClearCount}, spawns [{string.Join(", ", indices)}])";
        }

        public static bool operator ==(ProgressVector a, ProgressVector b) => a.Equals(b);

        public static bool operator !=(ProgressVector a, ProgressVector b) => !a.Equals(b);

        public static bool operator <(ProgressVector a, ProgressVector b) => a.CompareTo(b) < 0;

        public static bool operator >(ProgressVector a, ProgressVector b) => a.CompareTo(b) > 0;

        public static bool operator <=(ProgressVector a, ProgressVector b) => a.CompareTo(b) <= 0;

        public static bool operator >=(ProgressVector a, ProgressVector b) => a.CompareTo(b) >= 0;
    }
}
