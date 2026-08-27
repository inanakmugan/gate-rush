using System;
using System.Collections.Generic;

namespace GateRush.Core
{
    /// <summary>
    /// Everything about a level that changes while it is played: block positions,
    /// how many colours each block has shed, which blocks/gates/shutters are
    /// unlocked or open, spawner progress, and every progress counter. Paired
    /// with a <see cref="LevelContext"/>, which never changes and is therefore
    /// excluded from hashing (see <c>DECISIONS.md</c> D1).
    /// </summary>
    /// <remarks>
    /// <para><b>Index scheme.</b> Per-block arrays (<see cref="Origins"/>,
    /// <see cref="ClearedColors"/>, <see cref="Alive"/>, <see cref="Unfrozen"/>,
    /// <see cref="Unlocked"/>, <see cref="KeyConsumed"/>) have a <em>fixed</em>
    /// length, <see cref="LevelContext.TotalBlockCapacity"/> — top-level blocks
    /// plus every block any generator or elevator could ever spawn. Index
    /// <c>i</c> resolves to its <see cref="BlockSpec"/> via
    /// <see cref="LevelContext.SpecAt"/>, which is a pure function of the
    /// immutable <see cref="LevelContext"/> (see
    /// <c>DECISIONS.md</c> D28 for why that resolution lives there rather than
    /// here), so no new dynamic field is needed to record "which
    /// generator/elevator entry produced slot i". A not-yet-spawned slot starts
    /// with <see cref="Alive"/> false and stays inert until the module that
    /// performs spawning flips it, at that same fixed index — arrays never
    /// resize. "Indices grow during play" (as the module spec puts it) means the
    /// set of <em>active</em> (<c>Alive == true</c>) indices grows, not that
    /// array length changes.
    /// </para>
    /// <para><b><see cref="Origins"/> for a not-yet-spawned slot.</b> Its real
    /// spawn position is not derivable here: a generator's placement depends on
    /// projecting its edge and offset through the spawned block's own shape (an
    /// algorithm Module 03 owns and has not been written yet), and an elevator's
    /// per-block placement within its region has no representation at all in
    /// today's <see cref="LevelContext"/> (<see cref="SpawnedBlock"/> carries no
    /// position). Rather than store a placeholder that looks like real data,
    /// not-yet-spawned slots get <see cref="UnspawnedOrigin"/>, a coordinate
    /// that is never inside any grid, so an accidental read (bypassing the
    /// <see cref="Alive"/> check every other query in this class honours) fails
    /// loudly — it will not satisfy <see cref="LevelContext.IsInsideGrid"/> and
    /// will read as permanently occupied by <see cref="IsCellFree"/> rather than
    /// silently as free.
    /// </para>
    /// <para><b><see cref="ElevatorWaveActive"/>.</b> <see cref="ElevatorWaveIndex"/>
    /// is the count of waves already placed for that elevator.
    /// <see cref="ElevatorWaveActive"/> is true while the most recently placed
    /// wave still occupies its region, and false once that region has read empty
    /// again (ready for the next wave, if any remain). Unlike the counters this
    /// class deliberately keeps un-derived because they carry history that
    /// cannot be reconstructed after the fact, this one is a snapshot of
    /// something re-computable from <see cref="Alive"/> plus cell geometry — it
    /// is stored anyway because a <c>bool</c> costs nothing to hash and it may
    /// simplify the module that maintains it (Module 03), which must conform to
    /// this exact meaning.
    /// </para>
    /// <para><b>Hashing.</b> FNV-1a, 32-bit, computed once in the constructor and
    /// cached. Field order: <see cref="TotalClearCount"/>; then, per block index,
    /// <see cref="Origins"/> (X then Y), <see cref="ClearedColors"/>,
    /// <see cref="Alive"/>, <see cref="Unfrozen"/>, <see cref="Unlocked"/>,
    /// <see cref="KeyConsumed"/>; then <see cref="GateOpen"/> per gate; then
    /// <see cref="ShutterOpen"/> per shutter; then <see cref="GeneratorIndex"/>
    /// per generator; then, per elevator, <see cref="ElevatorWaveIndex"/> and
    /// <see cref="ElevatorWaveActive"/>; then <see cref="ClearCountByColor"/> per
    /// colour. <see cref="Equals(object)"/> performs a full field comparison
    /// after a hash match, per <c>DECISIONS.md</c> D2. This hash is computed
    /// <em>eagerly</em>, in the constructor, because every state the search
    /// constructs is hashed at least once — that is the point of the visited
    /// set.
    /// </para>
    /// <para><b>Occupancy map.</b> <see cref="IsCellFree"/> is answered by a
    /// per-state <c>int[]</c> mapping each cell to the living block index
    /// occupying it (or none), built the first time it is needed and cached for
    /// the rest of this instance's life. Unlike the hash, this cache is
    /// <em>lazy</em>: most states a search constructs are discarded as
    /// duplicates by hash/equality before move generation ever runs on them, so
    /// building the map eagerly would charge every discarded state for a scan
    /// whose result is never read. See <c>DECISIONS.md</c> D28. One consequence
    /// of the laziness: the build is also where a corrupt state — two living
    /// blocks sharing a cell — is caught (see <see cref="EnsureOccupancyMap"/>).
    /// Because that check only runs on first build, such a state can in
    /// principle exist and be hashed, compared, and passed around uncaught
    /// until something finally calls <see cref="IsCellFree"/>. Acceptable for
    /// now, since move generation queries every state it visits, but worth
    /// stating: this is not a check on construction, only on first use.
    /// <b>A second limitation compounds the first:</b> <see cref="IsCellFree"/>
    /// itself short-circuits — on grid bounds, on a static wall, on a closed
    /// shutter — before ever reaching the map for cells those checks reject.
    /// The duplicate-occupant guard therefore only fires if something queries
    /// a cell that actually reaches <see cref="EnsureOccupancyMap"/>'s build;
    /// it catches resolver bugs opportunistically, not systematically. A state
    /// where two living blocks overlap can go entirely undetected if nothing
    /// ever queries a reaching cell for it. Module 03 must not treat this
    /// guard as a safety net.
    /// </para>
    /// </remarks>
    public sealed class BoardState : IEquatable<BoardState>
    {
        /// <summary>
        /// The <see cref="Origins"/> value for a block index that has not yet
        /// been spawned. Never inside any level's grid (grid coordinates are
        /// always non-negative), so a read that bypasses <see cref="Alive"/>
        /// fails loudly instead of silently.
        /// </summary>
        public static readonly Coord UnspawnedOrigin = new Coord(-1, -1);

        private static readonly int ColorCount = Enum.GetValues(typeof(BlockColor)).Length;

        private const uint FnvOffsetBasis = 2166136261;
        private const uint FnvPrime = 16777619;

        public IReadOnlyList<Coord> Origins { get; }
        public IReadOnlyList<byte> ClearedColors { get; }
        public IReadOnlyList<bool> Alive { get; }
        public IReadOnlyList<bool> Unfrozen { get; }
        public IReadOnlyList<bool> Unlocked { get; }

        public IReadOnlyList<bool> GateOpen { get; }
        public IReadOnlyList<bool> ShutterOpen { get; }

        public IReadOnlyList<int> GeneratorIndex { get; }
        public IReadOnlyList<int> ElevatorWaveIndex { get; }
        public IReadOnlyList<bool> ElevatorWaveActive { get; }

        public int TotalClearCount { get; }
        public IReadOnlyList<int> ClearCountByColor { get; }
        public IReadOnlyList<bool> KeyConsumed { get; }

        private readonly int hashCode;

        /// <summary>
        /// Cell (row-major, <c>y * ctx.Width + x</c>) to living block index, or
        /// -1. Null until <see cref="EnsureOccupancyMap"/> first builds it — see
        /// the class remarks on why this cache is lazy where the hash is not.
        /// </summary>
        private int[] occupancyMap;

        /// <summary>
        /// The <see cref="LevelContext"/> <see cref="occupancyMap"/> was built
        /// against. Every other method here takes <c>ctx</c> as a parameter and
        /// stores nothing about it, by design (D1) — this is the one exception,
        /// so it is the one place that needs to guard against a caller passing
        /// a different context on a later call than it did on the first.
        /// </summary>
        private LevelContext occupancyMapContext;

        /// <summary>
        /// Builds a state from explicit field values with no validation. Not
        /// part of this module's public surface: it exists so this assembly's
        /// tests can construct fixtures that differ from a baseline in exactly
        /// one field, and so later Core-assembly code (the move resolver) can
        /// produce successor states without re-deriving values <see cref="CreateInitial"/>
        /// already knows how to compute.
        /// </summary>
        /// <remarks>
        /// <b>Ownership of every array passed here transfers to the new
        /// instance.</b> None of them are copied, and the hash cached by this
        /// constructor is computed once, from their contents at this exact
        /// moment. A caller that mutates an array afterwards — including one it
        /// kept a reference to so it could hand the same instance to a later
        /// successor state unchanged (structural sharing is intentionally legal
        /// here: two states may validly hold the very same array for a field
        /// that did not change between them) — corrupts this state silently:
        /// its contents and its cached hash disagree, <see cref="Equals(object)"/>
        /// stops reflecting reality, and the visited set can no longer tell two
        /// genuinely different states apart. Treat every array handed to this
        /// constructor as consumed. Build a new array for anything that
        /// changes; never mutate one in place. Callers are also trusted to pass
        /// consistently sized arrays — this constructor does not check.
        /// </remarks>
        internal BoardState(
            IReadOnlyList<Coord> origins,
            IReadOnlyList<byte> clearedColors,
            IReadOnlyList<bool> alive,
            IReadOnlyList<bool> unfrozen,
            IReadOnlyList<bool> unlocked,
            IReadOnlyList<bool> gateOpen,
            IReadOnlyList<bool> shutterOpen,
            IReadOnlyList<int> generatorIndex,
            IReadOnlyList<int> elevatorWaveIndex,
            IReadOnlyList<bool> elevatorWaveActive,
            int totalClearCount,
            IReadOnlyList<int> clearCountByColor,
            IReadOnlyList<bool> keyConsumed)
        {
            Origins = origins;
            ClearedColors = clearedColors;
            Alive = alive;
            Unfrozen = unfrozen;
            Unlocked = unlocked;
            GateOpen = gateOpen;
            ShutterOpen = shutterOpen;
            GeneratorIndex = generatorIndex;
            ElevatorWaveIndex = elevatorWaveIndex;
            ElevatorWaveActive = elevatorWaveActive;
            TotalClearCount = totalClearCount;
            ClearCountByColor = clearCountByColor;
            KeyConsumed = keyConsumed;

            hashCode = ComputeHashCode();
        }

        /// <summary>
        /// Builds the state a level starts in: every top-level block alive at
        /// its <see cref="BlockDefinition.StartOrigin"/>, every spawner slot
        /// inert, every threshold evaluated against zero clears.
        /// </summary>
        public static BoardState CreateInitial(LevelContext ctx)
        {
            if (ctx == null)
            {
                throw new ArgumentNullException(nameof(ctx));
            }

            var totalBlocks = ctx.TotalBlockCapacity;

            var origins = new Coord[totalBlocks];
            var clearedColors = new byte[totalBlocks];
            var alive = new bool[totalBlocks];
            var unfrozen = new bool[totalBlocks];
            var unlocked = new bool[totalBlocks];
            var keyConsumed = new bool[totalBlocks];

            for (var i = 0; i < ctx.Blocks.Count; i++)
            {
                var block = ctx.Blocks[i];
                origins[i] = block.StartOrigin;
                alive[i] = true;
                unfrozen[i] = IsUnfrozenAtZeroClears(block.UnfreezeAtClearCount);
                unlocked[i] = !block.LockId.HasValue;
            }

            for (var i = ctx.Blocks.Count; i < totalBlocks; i++)
            {
                var spec = ctx.SpecAt(i);
                origins[i] = UnspawnedOrigin;
                alive[i] = false;
                unfrozen[i] = IsUnfrozenAtZeroClears(spec.UnfreezeAtClearCount);
                unlocked[i] = !spec.LockId.HasValue;
            }

            var gateOpen = new bool[ctx.Gates.Count];
            for (var i = 0; i < gateOpen.Length; i++)
            {
                gateOpen[i] = IsOpenAtZeroClears(ctx.Gates[i].OpenAtClearCount);
            }

            var shutterOpen = new bool[ctx.Shutters.Count];
            for (var i = 0; i < shutterOpen.Length; i++)
            {
                shutterOpen[i] = ctx.Shutters[i].Threshold <= 0;
            }

            var generatorIndex = new int[ctx.Generators.Count];
            var elevatorWaveIndex = new int[ctx.Elevators.Count];
            var elevatorWaveActive = new bool[ctx.Elevators.Count];
            var clearCountByColor = new int[ColorCount];

            return new BoardState(
                origins,
                clearedColors,
                alive,
                unfrozen,
                unlocked,
                gateOpen,
                shutterOpen,
                generatorIndex,
                elevatorWaveIndex,
                elevatorWaveActive,
                totalClearCount: 0,
                clearCountByColor: clearCountByColor,
                keyConsumed: keyConsumed);
        }

        private static bool IsUnfrozenAtZeroClears(int? unfreezeAtClearCount) =>
            !unfreezeAtClearCount.HasValue || unfreezeAtClearCount.Value <= 0;

        private static bool IsOpenAtZeroClears(int? openAtClearCount) =>
            !openAtClearCount.HasValue || openAtClearCount.Value <= 0;

        /// <summary>
        /// The block's outermost remaining colour — the single point of truth
        /// gates, rockets, and brooms all match against.
        /// </summary>
        public BlockColor CurrentColorOf(LevelContext ctx, int blockIndex)
        {
            var colorStack = ctx.SpecAt(blockIndex).ColorStack;
            var cleared = ClearedColors[blockIndex];

            if (cleared >= colorStack.Count)
            {
                throw new InvalidOperationException(
                    $"Block {blockIndex} has no current colour; its {colorStack.Count}-colour stack is fully cleared.");
            }

            return colorStack[cleared];
        }

        /// <summary>True once every colour in the block's stack has been cleared.</summary>
        public bool IsFullyCleared(LevelContext ctx, int blockIndex) =>
            ClearedColors[blockIndex] >= ctx.SpecAt(blockIndex).ColorStack.Count;

        /// <summary>
        /// The cells this block currently occupies. Empty for a block that is
        /// not alive — dead or not yet spawned, cells are free either way.
        /// </summary>
        public IEnumerable<Coord> OccupiedCells(LevelContext ctx, int blockIndex)
        {
            if (!Alive[blockIndex])
            {
                yield break;
            }

            var origin = Origins[blockIndex];
            var cells = ctx.SpecAt(blockIndex).Cells;

            for (var i = 0; i < cells.Count; i++)
            {
                yield return origin + cells[i];
            }
        }

        /// <summary>
        /// True when <paramref name="c"/> is inside the grid, not a static wall,
        /// not covered by a closed shutter, and not occupied by any living block
        /// other than <paramref name="ignoreBlockIndex"/>.
        /// </summary>
        public bool IsCellFree(LevelContext ctx, Coord c, int ignoreBlockIndex = -1)
        {
            if (!ctx.IsInsideGrid(c) || ctx.IsStaticWall(c))
            {
                return false;
            }

            var shutterPosition = ctx.ShutterPositionAt(c);
            if (shutterPosition.HasValue && !ShutterOpen[shutterPosition.Value])
            {
                return false;
            }

            var occupant = EnsureOccupancyMap(ctx)[CellIndex(ctx, c)];
            return occupant < 0 || occupant == ignoreBlockIndex;
        }

        /// <summary>
        /// Builds <see cref="occupancyMap"/> on first call and returns the
        /// cached copy afterwards. O(occupied cells) the first time, O(1) after.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// <paramref name="ctx"/> is not the same <see cref="LevelContext"/> the
        /// map was built against. A <see cref="BoardState"/> is only ever valid
        /// for one context; a mismatch means a caller bug, and a stale cached
        /// map would otherwise be returned silently.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Two living blocks occupy the same cell. That is an invariant the
        /// move resolver must maintain — a real bug there, not a case this
        /// class should paper over by keeping whichever block it saw last.
        /// </exception>
        private int[] EnsureOccupancyMap(LevelContext ctx)
        {
            if (occupancyMap != null)
            {
                if (!ReferenceEquals(occupancyMapContext, ctx))
                {
                    throw new ArgumentException(
                        "This BoardState's occupancy map was built against a different LevelContext instance.",
                        nameof(ctx));
                }

                return occupancyMap;
            }

            var map = new int[ctx.Width * ctx.Height];
            for (var i = 0; i < map.Length; i++)
            {
                map[i] = -1;
            }

            for (var blockIndex = 0; blockIndex < Alive.Count; blockIndex++)
            {
                if (!Alive[blockIndex])
                {
                    continue;
                }

                foreach (var cell in OccupiedCells(ctx, blockIndex))
                {
                    var cellIndex = CellIndex(ctx, cell);
                    if (map[cellIndex] >= 0)
                    {
                        throw new InvalidOperationException(
                            $"Blocks {map[cellIndex]} and {blockIndex} both occupy cell {cell}; " +
                            "two living blocks may not share a cell.");
                    }

                    map[cellIndex] = blockIndex;
                }
            }

            occupancyMapContext = ctx;
            occupancyMap = map;
            return map;
        }

        private static int CellIndex(LevelContext ctx, Coord c) => c.Y * ctx.Width + c.X;

        /// <summary>
        /// True for a normal, alive, unfrozen, unlocked block outside any
        /// closed shutter. See <c>DECISIONS.md</c> D11 for the full truth table
        /// distinguishing this from <see cref="CanBeTargeted"/>.
        /// </summary>
        public bool CanMove(LevelContext ctx, int blockIndex) =>
            Alive[blockIndex]
            && Unfrozen[blockIndex]
            && Unlocked[blockIndex]
            && !IsInsideClosedShutter(ctx, blockIndex);

        /// <summary>
        /// True for any alive block outside a closed shutter — frozen and
        /// locked blocks remain targetable by jokers even though they cannot be
        /// moved. See <c>DECISIONS.md</c> D11.
        /// </summary>
        public bool CanBeTargeted(LevelContext ctx, int blockIndex) =>
            Alive[blockIndex] && !IsInsideClosedShutter(ctx, blockIndex);

        private bool IsInsideClosedShutter(LevelContext ctx, int blockIndex)
        {
            foreach (var cell in OccupiedCells(ctx, blockIndex))
            {
                var shutterPosition = ctx.ShutterPositionAt(cell);
                if (shutterPosition.HasValue && !ShutterOpen[shutterPosition.Value])
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// True once no living blocks remain and every generator and elevator
        /// is exhausted. "No living blocks" alone is not sufficient: with the
        /// index scheme above, an <see cref="Alive"/> entry of false means
        /// either "destroyed" or "not spawned yet", and the initial state of a
        /// level whose only content is queued generator or elevator output is
        /// exactly that second case. The generator and elevator exhaustion
        /// checks are what tell the two apart.
        /// </summary>
        /// <remarks>
        /// Also requires every <see cref="ElevatorWaveActive"/> to be false. A
        /// state with every wave placed, no living blocks, and an elevator
        /// still marked active is internally contradictory — <c>Active</c>
        /// means its region is still occupied, which cannot be true with no
        /// living blocks anywhere. Checking it here has no effect on a correct
        /// move resolver, which would never produce that combination, but
        /// gives an incorrect one the earliest possible signal instead of a
        /// level that silently reports itself solved.
        /// </remarks>
        public bool IsSolved(LevelContext ctx)
        {
            for (var i = 0; i < Alive.Count; i++)
            {
                if (Alive[i])
                {
                    return false;
                }
            }

            for (var g = 0; g < GeneratorIndex.Count; g++)
            {
                if (GeneratorIndex[g] < ctx.Generators[g].Queue.Count)
                {
                    return false;
                }
            }

            for (var e = 0; e < ElevatorWaveIndex.Count; e++)
            {
                if (ElevatorWaveIndex[e] < ctx.Elevators[e].Waves.Count || ElevatorWaveActive[e])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// This state's <see cref="GateRush.Core.ProgressVector"/> — the
        /// monotonic vector the solver stratifies its search on
        /// (<c>DECISIONS.md</c> D6, D32). A pure read of already-stored fields;
        /// recomputed per call, which is cheap. Fully qualified below because the
        /// property name shadows the type name inside this class.
        /// </summary>
        public ProgressVector ProgressVector => GateRush.Core.ProgressVector.Of(this);

        public override int GetHashCode() => hashCode;

        public override bool Equals(object obj) => Equals(obj as BoardState);

        public bool Equals(BoardState other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (hashCode != other.hashCode)
            {
                return false;
            }

            return TotalClearCount == other.TotalClearCount
                && SequenceEqual(Origins, other.Origins)
                && SequenceEqual(ClearedColors, other.ClearedColors)
                && SequenceEqual(Alive, other.Alive)
                && SequenceEqual(Unfrozen, other.Unfrozen)
                && SequenceEqual(Unlocked, other.Unlocked)
                && SequenceEqual(KeyConsumed, other.KeyConsumed)
                && SequenceEqual(GateOpen, other.GateOpen)
                && SequenceEqual(ShutterOpen, other.ShutterOpen)
                && SequenceEqual(GeneratorIndex, other.GeneratorIndex)
                && SequenceEqual(ElevatorWaveIndex, other.ElevatorWaveIndex)
                && SequenceEqual(ElevatorWaveActive, other.ElevatorWaveActive)
                && SequenceEqual(ClearCountByColor, other.ClearCountByColor);
        }

        private static bool SequenceEqual<T>(IReadOnlyList<T> a, IReadOnlyList<T> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }

            var comparer = EqualityComparer<T>.Default;
            for (var i = 0; i < a.Count; i++)
            {
                if (!comparer.Equals(a[i], b[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private int ComputeHashCode()
        {
            unchecked
            {
                var hash = FnvOffsetBasis;

                hash = HashInt(hash, TotalClearCount);

                for (var i = 0; i < Origins.Count; i++)
                {
                    hash = HashInt(hash, Origins[i].X);
                    hash = HashInt(hash, Origins[i].Y);
                    hash = HashByte(hash, ClearedColors[i]);
                    hash = HashBool(hash, Alive[i]);
                    hash = HashBool(hash, Unfrozen[i]);
                    hash = HashBool(hash, Unlocked[i]);
                    hash = HashBool(hash, KeyConsumed[i]);
                }

                for (var i = 0; i < GateOpen.Count; i++)
                {
                    hash = HashBool(hash, GateOpen[i]);
                }

                for (var i = 0; i < ShutterOpen.Count; i++)
                {
                    hash = HashBool(hash, ShutterOpen[i]);
                }

                for (var i = 0; i < GeneratorIndex.Count; i++)
                {
                    hash = HashInt(hash, GeneratorIndex[i]);
                }

                for (var i = 0; i < ElevatorWaveIndex.Count; i++)
                {
                    hash = HashInt(hash, ElevatorWaveIndex[i]);
                    hash = HashBool(hash, ElevatorWaveActive[i]);
                }

                for (var i = 0; i < ClearCountByColor.Count; i++)
                {
                    hash = HashInt(hash, ClearCountByColor[i]);
                }

                return unchecked((int)hash);
            }
        }

        private static uint HashByte(uint hash, byte value)
        {
            hash ^= value;
            hash *= FnvPrime;
            return hash;
        }

        private static uint HashBool(uint hash, bool value) => HashByte(hash, value ? (byte)1 : (byte)0);

        private static uint HashInt(uint hash, int value)
        {
            hash = HashByte(hash, (byte)(value & 0xFF));
            hash = HashByte(hash, (byte)((value >> 8) & 0xFF));
            hash = HashByte(hash, (byte)((value >> 16) & 0xFF));
            hash = HashByte(hash, (byte)((value >> 24) & 0xFF));
            return hash;
        }
    }
}
