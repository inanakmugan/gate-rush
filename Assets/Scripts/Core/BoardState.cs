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
    /// <see cref="Unlocked"/>, <see cref="KeyConsumed"/>,
    /// <see cref="WaitingKeyEffect"/>) have a <em>fixed</em>
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
    /// <para><b><see cref="Origins"/> for a not-yet-spawned slot.</b> Where it
    /// will land is static level data (<c>LevelContext.GeneratorSpawnOrigin</c>
    /// and the wave's <see cref="SpawnedBlock.RegionOrigin"/>), but storing it
    /// before the spawn would make the slot look placed. Instead,
    /// not-yet-spawned slots get <see cref="UnspawnedOrigin"/>, a coordinate
    /// that is never inside any grid, so an accidental read (bypassing the
    /// <see cref="Alive"/> check every other query in this class honours) fails
    /// loudly — it will not satisfy <see cref="LevelContext.IsInsideGrid"/> and
    /// will read as permanently occupied by <see cref="IsCellFree"/> rather than
    /// silently as free. The sentinel is also how the resolver tells an
    /// unspawned slot from a destroyed one, which keeps the grid cell it died
    /// on.
    /// </para>
    /// <para><b><see cref="ElevatorWaveActive"/>.</b> <see cref="ElevatorWaveIndex"/>
    /// is the count of waves already placed for that elevator.
    /// <see cref="ElevatorWaveActive"/> is true while the most recently placed
    /// wave still occupies its region, and false once that region has read empty
    /// again (ready for the next wave, if any remain). Unlike the counters this
    /// class deliberately keeps un-derived because they carry history that
    /// cannot be reconstructed after the fact, this one is a snapshot of
    /// something re-computable from <see cref="Alive"/> plus cell geometry — it
    /// is stored anyway because a <c>bool</c> costs nothing to hash.
    /// <c>MoveResolver.CheckSpawnTriggers</c> maintains it: it clears the flag
    /// in the pass that finds the region empty, so a region that never reads
    /// empty — a foreign block moved in before the wave's last block left —
    /// keeps it set.
    /// </para>
    /// <para><b>Hashing.</b> FNV-1a, 32-bit, computed once in the constructor and
    /// cached. Field order: <see cref="TotalClearCount"/>; then, per block index,
    /// <see cref="Origins"/> (X then Y), <see cref="ClearedColors"/>,
    /// <see cref="Alive"/>, <see cref="Unfrozen"/>, <see cref="Unlocked"/>,
    /// <see cref="KeyConsumed"/>, <see cref="WaitingKeyEffect"/>; then
    /// <see cref="GateOpen"/> per gate; then
    /// <see cref="ShutterOpen"/> per shutter; then <see cref="GeneratorIndex"/>
    /// per generator; then, per elevator, <see cref="ElevatorWaveIndex"/> and
    /// <see cref="ElevatorWaveActive"/>; then <see cref="ClearCountByColor"/> per
    /// colour. <see cref="Equals(object)"/> performs a full field comparison
    /// after a hash match, per <c>DECISIONS.md</c> D2. This hash is computed
    /// <em>eagerly</em>, in the constructor, because every state the search
    /// constructs is hashed at least once — that is the point of the visited
    /// set.
    /// </para>
    /// <para><b>Interchangeable blocks (D35).</b> The per-block loop above does
    /// not read index <c>i</c> directly: it reads <see cref="Slot"/>, which
    /// routes through a canonical order computed once in the constructor from
    /// the level's <see cref="BlockSymmetry"/>. Within each group of blocks
    /// sharing an identical spec, the rows are sorted, so two boards that differ
    /// only in <em>which</em> of several identical blocks sits where hash and
    /// compare as one state instead of <c>n!</c> of them. Without this a level
    /// with nine interchangeable blocks funnelling through one gate — ordinary
    /// under D16, not exotic — spends its whole search budget re-exploring the
    /// same board under different labels.
    /// <b>Nothing else changes.</b> <see cref="Origins"/> and every other public
    /// array still report literal, index-addressed values, so a
    /// <see cref="Move"/> a search returns still names a literal block index and
    /// replays exactly against the real initial state. Only the visited set's
    /// notion of "seen this before" is symmetry-aware. <see cref="GetHashCode"/>
    /// and <see cref="Equals(BoardState)"/> read the <em>same</em> order through
    /// the same accessor, so a hash collision is always resolved by the
    /// identically canonicalised comparison — they cannot disagree. On a level
    /// with no repeated spec the order is the identity and neither the array nor
    /// the sort is built at all.
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

        /// <summary>
        /// Per block, the key effect a completed lock is holding because its
        /// block was under a closed shutter when the completing key was
        /// consumed (<c>DECISIONS.md</c> D41); null when nothing waits. Only
        /// ever set on a lock-owning block, and cleared — the effect applied —
        /// in the same resolution that opens the last closed shutter over it.
        /// Stored rather than derived because it cannot be recovered from
        /// <see cref="KeyConsumed"/>: when a lock's keys carry different
        /// effects the completing key decides (M8), and two completion orders
        /// leave the same keys consumed with different effects waiting. A
        /// dynamic field like any other, so it is hashed and compared (D1) and
        /// sorted with its block's row (D35).
        /// </summary>
        public IReadOnlyList<KeyEffect?> WaitingKeyEffect { get; }

        private readonly int hashCode;

        /// <summary>
        /// The order <see cref="Slot"/> reads per-block rows in: position
        /// <c>i</c> holds the block index whose row belongs there once each
        /// interchangeable group's rows are sorted (D35). Null when the level
        /// has no interchangeable blocks, which is the identity order — the
        /// common case, and the one that pays nothing for this.
        /// </summary>
        private readonly int[] canonicalOrder;

        /// <summary>
        /// Which block indices this level treats as interchangeable. Held so
        /// successor states can inherit it without a <see cref="LevelContext"/>
        /// being threaded through every construction path: a state and every
        /// state derived from it necessarily share one level, so they share this
        /// instance. The one piece of static level data this class stores, and
        /// deliberately not the context itself (D1).
        /// </summary>
        internal BlockSymmetry Symmetry { get; }

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
        /// <param name="symmetry">
        /// The level's interchangeable-block groups, from
        /// <see cref="LevelContext.BlockSymmetry"/> (D35). Every caller building
        /// a successor passes the source state's <see cref="Symmetry"/>, so the
        /// whole search shares one instance; a state built with no level behind
        /// it passes <see cref="BlockSymmetry.None"/>. Its groups must index
        /// into the per-block arrays passed here — the same sizing contract the
        /// arrays themselves are held to, and equally unchecked.
        /// </param>
        internal BoardState(
            BlockSymmetry symmetry,
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
            IReadOnlyList<bool> keyConsumed,
            IReadOnlyList<KeyEffect?> waitingKeyEffect)
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
            WaitingKeyEffect = waitingKeyEffect;

            Symmetry = symmetry ?? BlockSymmetry.None;

            // Built before the hash, which reads rows through it.
            canonicalOrder = BuildCanonicalOrder();
            hashCode = ComputeHashCode();
        }

        /// <summary>
        /// Builds the state a level starts in, <em>settled</em>: every top-level
        /// block alive at its <see cref="BlockDefinition.StartOrigin"/>, every
        /// threshold evaluated against zero clears, and then one action-free
        /// resolution, so a generator or elevator whose target is empty at
        /// level start has already spawned (<c>DECISIONS.md</c> D42). Every
        /// caller — solver, editor, runtime — starts from this same board, and
        /// the settling goes through <c>MoveResolver</c>'s one resolution loop
        /// (D9). On a level with no generator or elevator the resolution
        /// changes nothing and the state equals the unresolved one field for
        /// field.
        /// </summary>
        public static BoardState CreateInitial(LevelContext ctx) =>
            CreateInitial(ctx, ctx?.BlockSymmetry);

        /// <summary>
        /// <see cref="CreateInitial(LevelContext)"/> with the interchangeable
        /// groups chosen by the caller rather than taken from the level.
        /// </summary>
        /// <remarks>
        /// The one reason this exists: passing <see cref="BlockSymmetry.None"/>
        /// produces an initial state whose whole successor tree carries plain
        /// position-by-index identity, because every successor inherits its
        /// source's groups. That is the baseline this assembly's tests compare
        /// the D35 collapse against — same verdict, same optimum, fewer states —
        /// the same shape as <c>BreadthFirstStrategy</c>'s non-stratified
        /// baseline. Production code always wants the public overload.
        /// </remarks>
        internal static BoardState CreateInitial(LevelContext ctx, BlockSymmetry symmetry) =>
            CreateInitial(ctx, symmetry, waitingKeyEffect: null);

        /// <summary>
        /// <see cref="CreateInitial(LevelContext)"/>, except that each block
        /// starts with the given <see cref="WaitingKeyEffect"/> instead of none.
        /// </summary>
        /// <remarks>
        /// Exists for a board derived from another state rather than authored:
        /// the solver's next-clear copy (<c>NextClearAbstraction</c>) rebuilds
        /// the source state as a fresh level and carries the source's waiting
        /// effects across block for block, so the copy never silently disagrees
        /// with the state it stands for. The values are carried as identity
        /// only: an entry on a block that owns no lock is never released and
        /// changes nothing but the state's hash and equality. The list is
        /// copied, so the caller keeps ownership of it.
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// <paramref name="waitingKeyEffect"/> does not have exactly
        /// <see cref="LevelContext.TotalBlockCapacity"/> entries.
        /// </exception>
        public static BoardState CreateInitialWithWaitingKeyEffects(
            LevelContext ctx, IReadOnlyList<KeyEffect?> waitingKeyEffect)
        {
            if (ctx == null)
            {
                throw new ArgumentNullException(nameof(ctx));
            }

            if (waitingKeyEffect == null)
            {
                throw new ArgumentNullException(nameof(waitingKeyEffect));
            }

            if (waitingKeyEffect.Count != ctx.TotalBlockCapacity)
            {
                throw new ArgumentException(
                    $"Level {ctx.LevelId} has {ctx.TotalBlockCapacity} block slots but {waitingKeyEffect.Count} " +
                    "waiting key effects were given.",
                    nameof(waitingKeyEffect));
            }

            return CreateInitial(ctx, ctx.BlockSymmetry, waitingKeyEffect);
        }

        /// <summary>
        /// The one construction path behind every <c>CreateInitial</c>
        /// overload: the unresolved state, settled by
        /// <c>MoveResolver.ResolveInitial</c> (D42). The internal entry point
        /// on the resolver is what keeps this from being a second resolution
        /// path. <paramref name="waitingKeyEffect"/> null means nothing waits;
        /// otherwise it is copied, already checked for length.
        /// </summary>
        private static BoardState CreateInitial(
            LevelContext ctx, BlockSymmetry symmetry, IReadOnlyList<KeyEffect?> waitingKeyEffect)
        {
            var unresolved = CreateUnresolved(ctx, symmetry, waitingKeyEffect);
            return new MoveResolver().ResolveInitial(ctx, unresolved);
        }

        /// <summary>
        /// The level's authored starting values before any resolution: every
        /// top-level block alive at its start origin, every spawner slot inert
        /// at <see cref="UnspawnedOrigin"/>, every threshold evaluated against
        /// zero clears. <c>internal</c> only so this assembly's tests can show
        /// that settling changes nothing on a level without spawners.
        /// </summary>
        internal static BoardState CreateUnresolved(
            LevelContext ctx, BlockSymmetry symmetry, IReadOnlyList<KeyEffect?> waitingKeyEffect)
        {
            if (ctx == null)
            {
                throw new ArgumentNullException(nameof(ctx));
            }

            var totalBlocks = ctx.TotalBlockCapacity;

            var waiting = new KeyEffect?[totalBlocks];
            if (waitingKeyEffect != null)
            {
                for (var i = 0; i < totalBlocks; i++)
                {
                    waiting[i] = waitingKeyEffect[i];
                }
            }

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
                gateOpen[i] = GateDefinition.IsOpenAtZeroClears(ctx.Gates[i].OpenAtClearCount);
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
                symmetry,
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
                keyConsumed: keyConsumed,
                waitingKeyEffect: waiting);
        }

        private static bool IsUnfrozenAtZeroClears(int? unfreezeAtClearCount) =>
            !unfreezeAtClearCount.HasValue || unfreezeAtClearCount.Value <= 0;

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

        private bool IsInsideClosedShutter(LevelContext ctx, int blockIndex) =>
            Alive[blockIndex] && IsInsideClosedShutter(ctx, blockIndex, Origins[blockIndex], ShutterOpen);

        /// <summary>
        /// True when any cell of block <paramref name="blockIndex"/>, placed at
        /// <paramref name="origin"/>, lies in a shutter that
        /// <paramref name="shutterOpen"/> reports closed. The one "under a
        /// closed shutter" test in <c>Core</c>: <see cref="CanMove"/> and
        /// <see cref="CanBeTargeted"/> read it through this state's own fields,
        /// and <c>MoveResolver</c> reads it mid-resolution through its successor
        /// builder, where no <see cref="BoardState"/> exists yet — which is why
        /// it takes the two fields it reads rather than a state. The caller
        /// must know the block is alive; a dead block's origin names cells it
        /// no longer occupies.
        /// </summary>
        internal static bool IsInsideClosedShutter(
            LevelContext ctx, int blockIndex, Coord origin, IReadOnlyList<bool> shutterOpen)
        {
            var cells = ctx.SpecAt(blockIndex).Cells;
            for (var i = 0; i < cells.Count; i++)
            {
                var shutterPosition = ctx.ShutterPositionAt(origin + cells[i]);
                if (shutterPosition.HasValue && !shutterOpen[shutterPosition.Value])
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// True when any living block covers any cell of the footprint
        /// <paramref name="cells"/> placed at <paramref name="origin"/>. The
        /// spawn triggers' occupancy test (M6, M9): a generator passes its next
        /// block's cells at its spawn origin, an elevator its region's cells at
        /// its <c>Min</c>. Like <see cref="IsInsideClosedShutter(LevelContext, int, Coord, IReadOnlyList{bool})"/>
        /// it takes the two fields it reads rather than a state, because
        /// <c>MoveResolver</c> asks mid-resolution through its successor
        /// builder, where no <see cref="BoardState"/> — and so no occupancy map
        /// — exists yet. Reading the live fields is also what lets a later
        /// spawn in the same pass see the cells an earlier one filled.
        /// <para>Only occupancy counts: walls are excluded from a spawn
        /// footprint by <see cref="LevelContext"/>'s validation, and a closed
        /// shutter does not stop a spawn (D42). One sweep over the block slots,
        /// skipping every slot that is not alive; <paramref name="cells"/> are
        /// normalised (minimum <c>(0, 0)</c>, D30), which gives a cheap bounding
        /// box to reject most cells against before the exact check.</para>
        /// </summary>
        internal static bool OverlapsLivingBlock(
            LevelContext ctx, IReadOnlyList<Coord> cells, Coord origin,
            IReadOnlyList<bool> alive, IReadOnlyList<Coord> origins)
        {
            var maxX = 0;
            var maxY = 0;
            for (var i = 0; i < cells.Count; i++)
            {
                maxX = Math.Max(maxX, cells[i].X);
                maxY = Math.Max(maxY, cells[i].Y);
            }

            for (var blockIndex = 0; blockIndex < alive.Count; blockIndex++)
            {
                if (!alive[blockIndex])
                {
                    continue;
                }

                var blockOrigin = origins[blockIndex];
                var blockCells = ctx.SpecAt(blockIndex).Cells;
                for (var c = 0; c < blockCells.Count; c++)
                {
                    var relative = blockOrigin + blockCells[c] - origin;
                    if (relative.X < 0 || relative.Y < 0 || relative.X > maxX || relative.Y > maxY)
                    {
                        continue;
                    }

                    for (var t = 0; t < cells.Count; t++)
                    {
                        if (cells[t] == relative)
                        {
                            return true;
                        }
                    }
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
                && PerBlockRowsEqual(other)
                && SequenceEqual(GateOpen, other.GateOpen)
                && SequenceEqual(ShutterOpen, other.ShutterOpen)
                && SequenceEqual(GeneratorIndex, other.GeneratorIndex)
                && SequenceEqual(ElevatorWaveIndex, other.ElevatorWaveIndex)
                && SequenceEqual(ElevatorWaveActive, other.ElevatorWaveActive)
                && SequenceEqual(ClearCountByColor, other.ClearCountByColor);
        }

        /// <summary>
        /// Compares the seven per-block rows of the two states, each read through
        /// its own <see cref="Slot"/> so interchangeable blocks are compared in
        /// canonical order rather than by literal index (D35). The whole row is
        /// compared at each position, not one field across all positions, which
        /// is what makes a permutation of identical blocks compare equal while
        /// any genuine difference in a row still fails.
        /// </summary>
        /// <remarks>
        /// Only <see cref="Origins"/>'s length is checked; the other six are
        /// held to the constructor's documented sizing contract, exactly as
        /// <see cref="ComputeHashCode"/> holds them.
        /// </remarks>
        private bool PerBlockRowsEqual(BoardState other)
        {
            if (Origins.Count != other.Origins.Count)
            {
                return false;
            }

            for (var i = 0; i < Origins.Count; i++)
            {
                var a = Slot(i);
                var b = other.Slot(i);

                if (Origins[a] != other.Origins[b]
                    || ClearedColors[a] != other.ClearedColors[b]
                    || Alive[a] != other.Alive[b]
                    || Unfrozen[a] != other.Unfrozen[b]
                    || Unlocked[a] != other.Unlocked[b]
                    || KeyConsumed[a] != other.KeyConsumed[b]
                    || WaitingKeyEffect[a] != other.WaitingKeyEffect[b])
                {
                    return false;
                }
            }

            return true;
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
                    var slot = Slot(i);
                    hash = HashInt(hash, Origins[slot].X);
                    hash = HashInt(hash, Origins[slot].Y);
                    hash = HashByte(hash, ClearedColors[slot]);
                    hash = HashBool(hash, Alive[slot]);
                    hash = HashBool(hash, Unfrozen[slot]);
                    hash = HashBool(hash, Unlocked[slot]);
                    hash = HashBool(hash, KeyConsumed[slot]);
                    hash = HashByte(hash, WaitingKeyEffectCode(WaitingKeyEffect[slot]));
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

        /// <summary>
        /// The block index whose per-block row belongs at position
        /// <paramref name="position"/> — the identity on a level with no
        /// interchangeable blocks, and the canonical order otherwise (D35). The
        /// single accessor both <see cref="ComputeHashCode"/> and
        /// <see cref="PerBlockRowsEqual"/> read through, which is what
        /// guarantees the two can never disagree about what a state <em>is</em>.
        /// </summary>
        private int Slot(int position) => canonicalOrder == null ? position : canonicalOrder[position];

        /// <summary>
        /// Sorts each interchangeable group's rows into a fixed order and
        /// records the result, so that two boards differing only by a
        /// permutation of identical blocks produce the same sequence of rows
        /// (D35). Returns null when the level has no group, meaning the identity
        /// order — no allocation and no sort on levels without repeated specs.
        /// </summary>
        /// <remarks>
        /// Runs once per constructed state, on the same cadence as the hash, and
        /// touches only the indices that belong to a group. Groups are typically
        /// small, so the sort is an insertion sort: no delegate, no comparer
        /// object, no allocation beyond the order array itself.
        /// </remarks>
        private int[] BuildCanonicalOrder()
        {
            if (Symmetry.IsTrivial)
            {
                return null;
            }

            var order = new int[Origins.Count];
            for (var i = 0; i < order.Length; i++)
            {
                order[i] = i;
            }

            var groups = Symmetry.RawGroups;
            for (var g = 0; g < groups.Length; g++)
            {
                SortGroup(order, groups[g]);
            }

            return order;
        }

        /// <summary>
        /// Writes the members of one interchangeable group into
        /// <paramref name="order"/> at that group's own positions, ordered by
        /// <see cref="CompareRows"/>. The group's positions are unchanged as a
        /// set — only which row is read at each of them.
        /// </summary>
        private void SortGroup(int[] order, int[] group)
        {
            for (var i = 1; i < group.Length; i++)
            {
                var candidate = order[group[i]];
                var j = i - 1;

                while (j >= 0 && CompareRows(order[group[j]], candidate) > 0)
                {
                    order[group[j + 1]] = order[group[j]];
                    j--;
                }

                order[group[j + 1]] = candidate;
            }
        }

        /// <summary>
        /// A total order over two blocks' dynamic rows: origin, then how many
        /// colours have been shed, then the four flags, then the waiting key
        /// effect. Any total order would
        /// do — this one only has to be a function of the row's contents, so
        /// that equal multisets of rows always canonicalise to equal sequences.
        /// Rows that tie are identical, so their relative order cannot matter.
        /// </summary>
        private int CompareRows(int a, int b)
        {
            if (Origins[a].X != Origins[b].X)
            {
                return Origins[a].X < Origins[b].X ? -1 : 1;
            }

            if (Origins[a].Y != Origins[b].Y)
            {
                return Origins[a].Y < Origins[b].Y ? -1 : 1;
            }

            if (ClearedColors[a] != ClearedColors[b])
            {
                return ClearedColors[a] < ClearedColors[b] ? -1 : 1;
            }

            if (Alive[a] != Alive[b])
            {
                return Alive[a] ? 1 : -1;
            }

            if (Unfrozen[a] != Unfrozen[b])
            {
                return Unfrozen[a] ? 1 : -1;
            }

            if (Unlocked[a] != Unlocked[b])
            {
                return Unlocked[a] ? 1 : -1;
            }

            if (KeyConsumed[a] != KeyConsumed[b])
            {
                return KeyConsumed[a] ? 1 : -1;
            }

            var waitingA = WaitingKeyEffectCode(WaitingKeyEffect[a]);
            var waitingB = WaitingKeyEffectCode(WaitingKeyEffect[b]);
            if (waitingA != waitingB)
            {
                return waitingA < waitingB ? -1 : 1;
            }

            return 0;
        }

        /// <summary>
        /// <see cref="WaitingKeyEffect"/> as one byte, for hashing and row
        /// ordering: 0 when nothing waits, otherwise one more than the
        /// effect's value, so no effect shares a code with "none".
        /// </summary>
        private static byte WaitingKeyEffectCode(KeyEffect? waiting) =>
            waiting.HasValue ? (byte)((int)waiting.Value + 1) : (byte)0;

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
