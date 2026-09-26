using System;
using System.Collections.Generic;

namespace GateRush.Core
{
    /// <summary>
    /// Everything about a level that never changes while it is played: grid
    /// dimensions, static walls, block/gate/shutter/generator/elevator
    /// definitions, and the level's economy values. Shared by reference
    /// alongside every <c>BoardState</c> the solver visits, and deliberately
    /// excluded from state hashing.
    /// </summary>
    /// <remarks>
    /// Every O(1) lookup this class exposes (<see cref="IsStaticWall"/>,
    /// <see cref="ShutterAt"/>, <see cref="ShutterPositionAt"/>,
    /// <see cref="SpecAt"/>, <see cref="GeneratorSpawnOrigin"/>,
    /// <see cref="BlockSymmetry"/>) is precomputed once in
    /// the constructor. That is
    /// safe because level data never changes after construction, and cheap
    /// because it is bounded by authored content, not by how many states the
    /// search visits — see <c>DECISIONS.md</c> D28 for why these live here
    /// rather than in an external cache, and why <c>BoardState</c>'s own
    /// per-state occupancy cache is lazy instead.
    /// </remarks>
    public sealed class LevelContext
    {
        public int LevelId { get; }
        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<Coord> StaticWalls { get; }
        public IReadOnlyList<BlockDefinition> Blocks { get; }
        public IReadOnlyList<GateDefinition> Gates { get; }
        public IReadOnlyList<ShutterDefinition> Shutters { get; }
        public IReadOnlyList<GeneratorDefinition> Generators { get; }
        public IReadOnlyList<ElevatorDefinition> Elevators { get; }
        public int SuggestedTimeBudgetSeconds { get; }
        public int GoldReward { get; }

        /// <summary>
        /// The total size of the flat block-index space <see cref="SpecAt"/>
        /// and <c>BoardState</c>'s per-block arrays share: top-level
        /// <see cref="Blocks"/> plus every block any <see cref="Generators"/>
        /// queue or <see cref="Elevators"/> wave could ever spawn.
        /// </summary>
        public int TotalBlockCapacity { get; }

        /// <summary>
        /// The largest number of fixpoint passes a cycle-free resolution of any
        /// action on this level can require: the total count of monotonic
        /// progress steps the level contains — every colour that can be cleared
        /// across every block (top-level and spawned), plus every generator
        /// spawn, plus every elevator wave. <c>MoveResolver</c> guards its
        /// resolution loop with this and throws when a resolution exceeds it,
        /// which indicates a cycle in the level data (see <c>DECISIONS.md</c>
        /// D8). Precomputed here because it is a pure function of immutable level
        /// data — the same reasoning as <see cref="SpecAt"/> (see
        /// <c>DECISIONS.md</c> D28).
        /// </summary>
        public int MaxResolutionPasses { get; }

        /// <summary>
        /// True when, on this level, a clear can never turn a solvable board into
        /// an unsolvable one. A solver may then commit to any reachable clear
        /// without backtracking, and may conclude a level is unsolvable once it
        /// finds no clear reachable from a state it reached — the authority the
        /// nearest-next-clear strategy relies on.
        /// </summary>
        /// <remarks>
        /// <para>The argument, in brief: between clears, moves are reversible,
        /// so every arrangement within a stratum is as solvable as any other; a
        /// clear moves no block; and everything a clear changes only ever opens
        /// things up — counters rise, gates, shutters, frozen blocks and locks
        /// only open, destroyed blocks free their cells. So a solution from
        /// before any clear still works after it.</para>
        /// <para>Three things break it, and make this false:</para>
        /// <list type="bullet">
        /// <item>A generator or an elevator. A spawn places blocks between
        /// clears and cannot be undone, and a clear can trigger one.</item>
        /// <item>A lock whose keys carry different effects. The key consumed
        /// last decides whether the lock only unlocks or also clears its owner
        /// (M8), so clearing one key's carrier early can change which effect the
        /// lock gets — and an owner that can only leave by being cleared is then
        /// stranded. A lock whose keys all share one effect is safe: an early
        /// clear only makes that same effect arrive sooner.</item>
        /// </list>
        /// <para><b>A key effect waiting for a shutter (D41) leaves it true.</b>
        /// With every key of a lock sharing one effect, which effect the lock
        /// gets never depends on clear order; waiting only decides when it
        /// lands. It lands the moment the shutter opens — the earliest moment
        /// anything can interact with a block under it, since until then the
        /// block can be neither moved nor targeted and its region is closed
        /// either way. An early clear therefore never makes the effect arrive
        /// later than it otherwise would, and the effect only unlocks or
        /// clears, which only opens things up.</para>
        /// <para><b>Every new mechanic must decide this flag consciously.</b> One
        /// that changes the board between clears, closes anything on a clear,
        /// makes a move irreversible, or lets the order of clears change what a
        /// clear does must set it false here — not inherit true.</para>
        /// </remarks>
        public bool IsClearMonotone { get; }

        /// <summary>
        /// Which of this level's block indices are interchangeable — blocks
        /// sharing an identical spec, whose dynamic rows <c>BoardState</c>
        /// canonicalises before hashing so that permuting them is not mistaken
        /// for a different board (<c>DECISIONS.md</c> D35). Precomputed here for
        /// the same reason as <see cref="SpecAt"/>: a pure function of immutable
        /// level data (D28).
        /// </summary>
        public BlockSymmetry BlockSymmetry { get; }

        private readonly HashSet<Coord> staticWallLookup;
        private readonly Dictionary<Coord, int> shutterPositionByCell;
        private readonly BlockSpec[] specByIndex;
        private readonly Dictionary<int, int> lockOwnerByLockId;
        private readonly Dictionary<int, int[]> keyIndicesByLockId;

        /// <summary>
        /// Per flat block index, where that block's minimum corner lands when it
        /// spawns — the generator placement rule (M6, D34) for a generator queue
        /// slot, <c>Min + RegionOrigin</c> for an elevator wave slot, and
        /// <see cref="BoardState.UnspawnedOrigin"/> for a top-level block, which
        /// never spawns. Laid out by flat index so the resolver can place any
        /// slot with one lookup (D28).
        /// </summary>
        private readonly Coord[] spawnOriginBySlot;

        /// <summary>Per generator, the flat index of its queue entry 0.</summary>
        private readonly int[] generatorFirstSlot;

        /// <summary>Per elevator, per wave, the flat index of that wave's block 0.</summary>
        private readonly int[][] elevatorWaveFirstSlot;

        /// <summary>Per elevator, every cell of its region relative to <see cref="ElevatorDefinition.Min"/>.</summary>
        private readonly Coord[][] elevatorRegionCells;

        public LevelContext(
            int levelId,
            int width,
            int height,
            IReadOnlyList<Coord> staticWalls,
            IReadOnlyList<BlockDefinition> blocks,
            IReadOnlyList<GateDefinition> gates,
            IReadOnlyList<ShutterDefinition> shutters,
            IReadOnlyList<GeneratorDefinition> generators,
            IReadOnlyList<ElevatorDefinition> elevators,
            int suggestedTimeBudgetSeconds,
            int goldReward)
        {
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentException($"Level {levelId} must have positive grid dimensions; got {width}x{height}.");
            }

            LevelId = levelId;
            Width = width;
            Height = height;
            StaticWalls = new List<Coord>(staticWalls ?? Array.Empty<Coord>()).AsReadOnly();
            Blocks = new List<BlockDefinition>(blocks ?? Array.Empty<BlockDefinition>()).AsReadOnly();
            Gates = new List<GateDefinition>(gates ?? Array.Empty<GateDefinition>()).AsReadOnly();
            Shutters = new List<ShutterDefinition>(shutters ?? Array.Empty<ShutterDefinition>()).AsReadOnly();
            Generators = new List<GeneratorDefinition>(generators ?? Array.Empty<GeneratorDefinition>()).AsReadOnly();
            Elevators = new List<ElevatorDefinition>(elevators ?? Array.Empty<ElevatorDefinition>()).AsReadOnly();
            SuggestedTimeBudgetSeconds = suggestedTimeBudgetSeconds;
            GoldReward = goldReward;

            staticWallLookup = new HashSet<Coord>(StaticWalls);

            ValidateUniqueIds(Blocks, b => b.Id, "Block");
            ValidateUniqueIds(Gates, g => g.Id, "Gate");
            ValidateUniqueIds(Shutters, s => s.Id, "Shutter");
            ValidateUniqueIds(Generators, g => g.Id, "Generator");
            ValidateUniqueIds(Elevators, e => e.Id, "Elevator");

            ValidateStaticWalls();
            ValidateBlockPlacement();
            ValidateEdgeFeatures();
            ValidateShutterBounds();
            ValidateElevatorRegions();
            ValidateLocksAndKeys();

            // Built only after bounds are validated: a shutter whose Max lies
            // outside the grid would otherwise make BuildShutterPositionLookup
            // iterate an unbounded region before ValidateShutterBounds ever
            // gets a chance to reject it.
            shutterPositionByCell = BuildShutterPositionLookup(Shutters);
            specByIndex = BuildSpecByIndex(Blocks, Generators, Elevators);
            TotalBlockCapacity = specByIndex.Length;
            generatorFirstSlot = new int[Generators.Count];
            elevatorWaveFirstSlot = new int[Elevators.Count][];
            spawnOriginBySlot = BuildSpawnLayout();
            elevatorRegionCells = BuildElevatorRegionCells(Elevators);
            ValidateGeneratorSpawnFootprints();
            MaxResolutionPasses = ComputeMaxResolutionPasses(specByIndex, Generators, Elevators);
            lockOwnerByLockId = BuildLockOwnerLookup(specByIndex);
            keyIndicesByLockId = BuildKeyIndexLookup(specByIndex);
            LockOwnerIndices = BuildLockOwnerIndices(specByIndex);
            IsClearMonotone = Generators.Count == 0 && Elevators.Count == 0 && !HasLockWithMixedKeyEffects();
            // Fully qualified because the property name shadows the type name
            // inside this class — the same shape as BoardState.ProgressVector.
            BlockSymmetry = GateRush.Core.BlockSymmetry.Of(specByIndex);
        }

        private static int ComputeMaxResolutionPasses(
            IReadOnlyList<BlockSpec> specs,
            IReadOnlyList<GeneratorDefinition> generators,
            IReadOnlyList<ElevatorDefinition> elevators)
        {
            var passes = 0;

            for (var i = 0; i < specs.Count; i++)
            {
                passes += specs[i].ColorStack.Count;
            }

            for (var g = 0; g < generators.Count; g++)
            {
                passes += generators[g].Queue.Count;
            }

            for (var e = 0; e < elevators.Count; e++)
            {
                passes += elevators[e].Waves.Count;
            }

            return passes;
        }

        private static void ValidateUniqueIds<T>(IReadOnlyList<T> items, Func<T, int> idSelector, string typeName)
        {
            var seenIds = new HashSet<int>();
            foreach (var item in items)
            {
                var id = idSelector(item);
                if (!seenIds.Add(id))
                {
                    throw new ArgumentException($"{typeName} id {id} is used by more than one {typeName.ToLowerInvariant()}.");
                }
            }
        }

        public bool IsInsideGrid(Coord c) => c.X >= 0 && c.X < Width && c.Y >= 0 && c.Y < Height;

        public bool IsStaticWall(Coord c) => staticWallLookup.Contains(c);

        /// <summary>The id of the shutter covering this cell, or null if none does.</summary>
        public int? ShutterAt(Coord c) =>
            shutterPositionByCell.TryGetValue(c, out var position) ? Shutters[position].Id : (int?)null;

        /// <summary>
        /// The 0-based position in <see cref="Shutters"/> of the shutter
        /// covering this cell, or null if none does. O(1): callers on a hot
        /// path (e.g. <c>BoardState.IsCellFree</c>) should prefer this over
        /// translating <see cref="ShutterAt"/>'s id back to a position
        /// themselves.
        /// </summary>
        public int? ShutterPositionAt(Coord c) =>
            shutterPositionByCell.TryGetValue(c, out var position) ? position : (int?)null;

        /// <summary>
        /// The <see cref="BlockSpec"/> for block index <paramref name="blockIndex"/>
        /// — O(1) regardless of whether the index names a top-level block or a
        /// generator/elevator spawn slot. See <see cref="TotalBlockCapacity"/>
        /// for the valid index range.
        /// </summary>
        public BlockSpec SpecAt(int blockIndex)
        {
            if (blockIndex < 0 || blockIndex >= specByIndex.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(blockIndex), blockIndex,
                    $"Block index must be within [0, {specByIndex.Length}) — this level's TotalBlockCapacity.");
            }

            return specByIndex[blockIndex];
        }

        /// <summary>
        /// Where entry <paramref name="queueIndex"/> of generator
        /// <paramref name="generatorIndex"/> (a position in
        /// <see cref="Generators"/>) lands when it spawns: the origin its
        /// normalised minimum corner occupies, flush against the generator's
        /// edge and aligned to its offset (M6, D34). With <c>maxX</c>/<c>maxY</c>
        /// the block's largest cell coordinates: bottom <c>(Offset, 0)</c>, top
        /// <c>(Offset, Height − 1 − maxY)</c>, left <c>(0, Offset)</c>, right
        /// <c>(Width − 1 − maxX, Offset)</c>. Precomputed (D28); the one
        /// placement rule, which <c>MoveResolver</c>, <c>MoveGenerator</c> and
        /// the Level Editor all read rather than derive (D31).
        /// </summary>
        public Coord GeneratorSpawnOrigin(int generatorIndex, int queueIndex)
        {
            if (generatorIndex < 0 || generatorIndex >= Generators.Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(generatorIndex), generatorIndex,
                    $"Level {LevelId} has {Generators.Count} generator(s).");
            }

            if (queueIndex < 0 || queueIndex >= Generators[generatorIndex].Queue.Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(queueIndex), queueIndex,
                    $"Generator {Generators[generatorIndex].Id} has {Generators[generatorIndex].Queue.Count} " +
                    "queue entr(ies).");
            }

            return spawnOriginBySlot[GeneratorSlot(generatorIndex, queueIndex)];
        }

        /// <summary>
        /// The flat block index of entry <paramref name="queueIndex"/> of
        /// generator <paramref name="generatorIndex"/>. Unchecked: the resolver
        /// only asks for an entry it knows exists.
        /// </summary>
        internal int GeneratorSlot(int generatorIndex, int queueIndex) =>
            generatorFirstSlot[generatorIndex] + queueIndex;

        /// <summary>
        /// The flat block index of block 0 of wave <paramref name="waveIndex"/>
        /// of elevator <paramref name="elevatorIndex"/>; the wave's blocks
        /// occupy the next <c>Waves[waveIndex].Count</c> indices. Unchecked.
        /// </summary>
        internal int ElevatorWaveFirstSlot(int elevatorIndex, int waveIndex) =>
            elevatorWaveFirstSlot[elevatorIndex][waveIndex];

        /// <summary>
        /// Where the spawner slot <paramref name="blockIndex"/> lands when it
        /// spawns. <see cref="BoardState.UnspawnedOrigin"/> for a top-level
        /// block, which never spawns. Unchecked.
        /// </summary>
        internal Coord SpawnOriginAt(int blockIndex) => spawnOriginBySlot[blockIndex];

        /// <summary>
        /// Every cell of elevator <paramref name="elevatorIndex"/>'s region,
        /// relative to its <see cref="ElevatorDefinition.Min"/> — the region as
        /// a footprint, so the resolver's one occupancy test serves both a
        /// generator's incoming block and an elevator's region.
        /// </summary>
        internal IReadOnlyList<Coord> ElevatorRegionCells(int elevatorIndex) => elevatorRegionCells[elevatorIndex];

        /// <summary>
        /// The flat block index that owns lock <paramref name="lockId"/>. Lock
        /// ids are unique within a level (M8), so this resolves to one block, not
        /// a set. Precomputed here beside <see cref="SpecAt"/> because
        /// <c>MoveResolver.ApplyKeyEffects</c> needs it inside the fixpoint drain
        /// loop, once per key consumed — scanning every block there would repeat
        /// a walk over data that never changes (see <c>DECISIONS.md</c> D28).
        /// </summary>
        public int LockOwnerIndex(int lockId)
        {
            if (!lockOwnerByLockId.TryGetValue(lockId, out var index))
            {
                throw new ArgumentException(
                    $"No block in level {LevelId} owns lock {lockId}.", nameof(lockId));
            }

            return index;
        }

        /// <summary>
        /// Every flat block index carrying a key for lock <paramref name="lockId"/>,
        /// in ascending index order. Empty when nothing targets that lock.
        /// Precomputed for the same reason as <see cref="LockOwnerIndex"/>:
        /// <c>MoveResolver</c> counts consumed keys against this list inside the
        /// drain loop.
        /// </summary>
        public IReadOnlyList<int> KeyIndicesForLock(int lockId) =>
            keyIndicesByLockId.TryGetValue(lockId, out var indices) ? indices : Array.Empty<int>();

        /// <summary>
        /// Every flat block index that owns a lock — top-level or spawned — in
        /// ascending index order. Empty on a level with no locks. Precomputed
        /// for the same reason as <see cref="LockOwnerIndex"/> (D28):
        /// <c>MoveResolver</c> walks it when a shutter opens, to release every
        /// key effect waiting on a block the opening uncovered (D41), and
        /// scanning every block slot there would repeat a walk over data that
        /// never changes. The ascending order is what makes several releases in
        /// one opening enqueue their clears in a fixed, reproducible order.
        /// </summary>
        public IReadOnlyList<int> LockOwnerIndices { get; }

        /// <summary>
        /// The lock-owning flat block indices for <see cref="LockOwnerIndices"/>,
        /// ascending, over the index space <see cref="BuildSpecByIndex"/>
        /// produced.
        /// </summary>
        private static int[] BuildLockOwnerIndices(IReadOnlyList<BlockSpec> specs)
        {
            var owners = new List<int>();

            for (var i = 0; i < specs.Count; i++)
            {
                if (specs[i].LockId.HasValue)
                {
                    owners.Add(i);
                }
            }

            return owners.ToArray();
        }

        /// <summary>
        /// Maps each lock id to the flat block index that owns it, over the index
        /// space <see cref="BuildSpecByIndex"/> produced. Lock id uniqueness is
        /// already enforced by <see cref="ValidateLocksAndKeys"/>; this only
        /// records the resolved index.
        /// </summary>
        private static Dictionary<int, int> BuildLockOwnerLookup(IReadOnlyList<BlockSpec> specs)
        {
            var lookup = new Dictionary<int, int>();

            for (var i = 0; i < specs.Count; i++)
            {
                var lockId = specs[i].LockId;
                if (lockId.HasValue)
                {
                    lookup[lockId.Value] = i;
                }
            }

            return lookup;
        }

        /// <summary>
        /// Maps each targeted lock id to the flat block indices carrying a key
        /// for it, in ascending index order. That keys point at real locks and
        /// that each lock has enough of them is already enforced by
        /// <see cref="ValidateLocksAndKeys"/>.
        /// </summary>
        /// <summary>
        /// True when some lock is targeted by keys — top-level or spawned — that
        /// do not all carry the same <see cref="KeyEffect"/>. See
        /// <see cref="IsClearMonotone"/> for why that matters.
        /// </summary>
        private bool HasLockWithMixedKeyEffects()
        {
            foreach (var keyIndices in keyIndicesByLockId.Values)
            {
                for (var k = 1; k < keyIndices.Length; k++)
                {
                    if (specByIndex[keyIndices[k]].KeyEffect != specByIndex[keyIndices[0]].KeyEffect)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static Dictionary<int, int[]> BuildKeyIndexLookup(IReadOnlyList<BlockSpec> specs)
        {
            var keyLists = new Dictionary<int, List<int>>();

            for (var i = 0; i < specs.Count; i++)
            {
                var keyTargetLockId = specs[i].KeyTargetLockId;
                if (!keyTargetLockId.HasValue)
                {
                    continue;
                }

                if (!keyLists.TryGetValue(keyTargetLockId.Value, out var list))
                {
                    list = new List<int>();
                    keyLists[keyTargetLockId.Value] = list;
                }

                list.Add(i);
            }

            var lookup = new Dictionary<int, int[]>(keyLists.Count);
            foreach (var pair in keyLists)
            {
                lookup[pair.Key] = pair.Value.ToArray();
            }

            return lookup;
        }

        private static Dictionary<Coord, int> BuildShutterPositionLookup(IReadOnlyList<ShutterDefinition> shutters)
        {
            var lookup = new Dictionary<Coord, int>();
            for (var position = 0; position < shutters.Count; position++)
            {
                var shutter = shutters[position];
                for (var x = shutter.Min.X; x <= shutter.Max.X; x++)
                {
                    for (var y = shutter.Min.Y; y <= shutter.Max.Y; y++)
                    {
                        var cell = new Coord(x, y);
                        if (lookup.TryGetValue(cell, out var existingPosition))
                        {
                            throw new ArgumentException(
                                $"Shutters {shutters[existingPosition].Id} and {shutter.Id} both cover cell {cell}.");
                        }

                        lookup[cell] = position;
                    }
                }
            }

            return lookup;
        }

        /// <summary>
        /// Flattens <paramref name="blocks"/>, then every generator queue in
        /// order, then every elevator wave in order, into the single index
        /// space <see cref="SpecAt"/> and <c>BoardState</c>'s per-block
        /// arrays share. Purely a function of already-ordered level data — no
        /// new authoring input is required.
        /// </summary>
        private static BlockSpec[] BuildSpecByIndex(
            IReadOnlyList<BlockDefinition> blocks,
            IReadOnlyList<GeneratorDefinition> generators,
            IReadOnlyList<ElevatorDefinition> elevators)
        {
            var capacity = blocks.Count;
            foreach (var generator in generators)
            {
                capacity += generator.Queue.Count;
            }

            foreach (var elevator in elevators)
            {
                foreach (var wave in elevator.Waves)
                {
                    capacity += wave.Count;
                }
            }

            var specs = new List<BlockSpec>(capacity);

            foreach (var block in blocks)
            {
                specs.Add(new BlockSpec(block));
            }

            foreach (var generator in generators)
            {
                foreach (var spawned in generator.Queue)
                {
                    specs.Add(new BlockSpec(spawned));
                }
            }

            foreach (var elevator in elevators)
            {
                foreach (var wave in elevator.Waves)
                {
                    foreach (var spawned in wave)
                    {
                        specs.Add(new BlockSpec(spawned));
                    }
                }
            }

            return specs.ToArray();
        }

        /// <summary>
        /// Walks the flat index space in <see cref="BuildSpecByIndex"/>'s order,
        /// records where each generator's queue and each elevator wave starts,
        /// and returns every slot's spawn origin. Must stay in step with
        /// <see cref="BuildSpecByIndex"/>: top-level blocks, then generator
        /// queues, then elevator waves.
        /// </summary>
        private Coord[] BuildSpawnLayout()
        {
            var origins = new Coord[TotalBlockCapacity];
            var slot = 0;

            for (; slot < Blocks.Count; slot++)
            {
                origins[slot] = BoardState.UnspawnedOrigin;
            }

            for (var g = 0; g < Generators.Count; g++)
            {
                var generator = Generators[g];
                generatorFirstSlot[g] = slot;
                foreach (var spawned in generator.Queue)
                {
                    origins[slot++] = GeneratorPlacement(generator, spawned.Cells);
                }
            }

            for (var e = 0; e < Elevators.Count; e++)
            {
                var elevator = Elevators[e];
                var firstSlots = new int[elevator.Waves.Count];
                for (var w = 0; w < elevator.Waves.Count; w++)
                {
                    firstSlots[w] = slot;
                    foreach (var spawned in elevator.Waves[w])
                    {
                        // ElevatorDefinition has already required a RegionOrigin
                        // on every wave block (its tiling check).
                        origins[slot++] = elevator.Min + spawned.RegionOrigin.Value;
                    }
                }

                elevatorWaveFirstSlot[e] = firstSlots;
            }

            return origins;
        }

        /// <summary>
        /// The generator placement rule — see <see cref="GeneratorSpawnOrigin"/>.
        /// <paramref name="cells"/> are normalised so their minimum is
        /// <c>(0, 0)</c> (D30).
        /// </summary>
        private Coord GeneratorPlacement(GeneratorDefinition generator, IReadOnlyList<Coord> cells)
        {
            var maxX = 0;
            var maxY = 0;
            foreach (var cell in cells)
            {
                maxX = Math.Max(maxX, cell.X);
                maxY = Math.Max(maxY, cell.Y);
            }

            switch (generator.Edge)
            {
                case BoardEdge.Bottom:
                    return new Coord(generator.Offset, 0);
                case BoardEdge.Top:
                    return new Coord(generator.Offset, Height - 1 - maxY);
                case BoardEdge.Left:
                    return new Coord(0, generator.Offset);
                case BoardEdge.Right:
                    return new Coord(Width - 1 - maxX, generator.Offset);
                default:
                    throw new ArgumentException($"Generator {generator.Id} sits on unknown edge {generator.Edge}.");
            }
        }

        private static Coord[][] BuildElevatorRegionCells(IReadOnlyList<ElevatorDefinition> elevators)
        {
            var regions = new Coord[elevators.Count][];
            for (var e = 0; e < elevators.Count; e++)
            {
                var elevator = elevators[e];
                var cells = new List<Coord>();
                for (var y = 0; y <= elevator.Max.Y - elevator.Min.Y; y++)
                {
                    for (var x = 0; x <= elevator.Max.X - elevator.Min.X; x++)
                    {
                        cells.Add(new Coord(x, y));
                    }
                }

                regions[e] = cells.ToArray();
            }

            return regions;
        }

        /// <summary>
        /// Every queued block of every generator must land entirely inside the
        /// grid and clear of static walls, or it could never spawn — a level
        /// data error naming the generator and the entry. Blocks, shutters and
        /// elevator regions under the footprint are legal: they only make the
        /// generator wait, or hide what it delivers (D42).
        /// </summary>
        private void ValidateGeneratorSpawnFootprints()
        {
            for (var g = 0; g < Generators.Count; g++)
            {
                var generator = Generators[g];
                for (var q = 0; q < generator.Queue.Count; q++)
                {
                    var origin = spawnOriginBySlot[GeneratorSlot(g, q)];
                    foreach (var cell in generator.Queue[q].Cells)
                    {
                        var absolute = origin + cell;
                        if (!IsInsideGrid(absolute))
                        {
                            throw new ArgumentException(
                                $"Generator {generator.Id} queue entry {q} would spawn at {origin} with a cell at " +
                                $"{absolute}, outside the {Width}x{Height} grid.");
                        }

                        if (IsStaticWall(absolute))
                        {
                            throw new ArgumentException(
                                $"Generator {generator.Id} queue entry {q} would spawn at {origin} with a cell on " +
                                $"the static wall at {absolute}.");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Every elevator region lies inside the grid and covers no static wall,
        /// and no two regions share a cell. Each is a level data error naming
        /// the elevator: a wave that tiles a region with a wall in it, or one
        /// half off the grid, could never be placed, and two overlapping regions
        /// would make "the region reads empty" (M9) ambiguous.
        /// </summary>
        private void ValidateElevatorRegions()
        {
            var elevatorIdByCell = new Dictionary<Coord, int>();

            foreach (var elevator in Elevators)
            {
                if (!IsInsideGrid(elevator.Min) || !IsInsideGrid(elevator.Max))
                {
                    throw new ArgumentException(
                        $"Elevator {elevator.Id} region [{elevator.Min}, {elevator.Max}] falls outside the " +
                        $"{Width}x{Height} grid.");
                }

                for (var y = elevator.Min.Y; y <= elevator.Max.Y; y++)
                {
                    for (var x = elevator.Min.X; x <= elevator.Max.X; x++)
                    {
                        var cell = new Coord(x, y);
                        if (IsStaticWall(cell))
                        {
                            throw new ArgumentException(
                                $"Elevator {elevator.Id} region [{elevator.Min}, {elevator.Max}] covers the static " +
                                $"wall at {cell}.");
                        }

                        if (elevatorIdByCell.TryGetValue(cell, out var otherId))
                        {
                            throw new ArgumentException(
                                $"Elevators {otherId} and {elevator.Id} both cover cell {cell}; elevator regions " +
                                "may not overlap.");
                        }

                        elevatorIdByCell[cell] = elevator.Id;
                    }
                }
            }
        }

        private void ValidateStaticWalls()
        {
            var seen = new HashSet<Coord>();
            foreach (var wall in StaticWalls)
            {
                if (!IsInsideGrid(wall))
                {
                    throw new ArgumentException($"Static wall at {wall} is outside the {Width}x{Height} grid.");
                }

                if (!seen.Add(wall))
                {
                    throw new ArgumentException($"Static wall at {wall} is duplicated.");
                }
            }
        }

        private void ValidateBlockPlacement()
        {
            var occupiedBy = new Dictionary<Coord, int>();

            foreach (var block in Blocks)
            {
                foreach (var relative in block.Cells)
                {
                    var absolute = block.StartOrigin + relative;

                    if (!IsInsideGrid(absolute))
                    {
                        throw new ArgumentException(
                            $"Block {block.Id} has a cell at {absolute} outside the {Width}x{Height} grid.");
                    }

                    if (IsStaticWall(absolute))
                    {
                        throw new ArgumentException(
                            $"Block {block.Id} has a cell at {absolute} overlapping a static wall.");
                    }

                    if (occupiedBy.TryGetValue(absolute, out var otherBlockId))
                    {
                        throw new ArgumentException(
                            $"Block {block.Id} overlaps block {otherBlockId} at {absolute}.");
                    }

                    occupiedBy[absolute] = block.Id;
                }
            }
        }

        /// <summary>
        /// One gate or generator reduced to the only three things the edge rules
        /// care about: which edge it sits on and the half-open span
        /// <c>[Offset, Offset + Width)</c> of that edge it occupies. The label
        /// carries its identity into the error message.
        /// </summary>
        private readonly struct EdgeFeature
        {
            public string Label { get; }
            public BoardEdge Edge { get; }
            public int Offset { get; }
            public int Width { get; }

            public EdgeFeature(string label, BoardEdge edge, int offset, int width)
            {
                Label = label;
                Edge = edge;
                Offset = offset;
                Width = width;
            }

            /// <summary>One past the last cell of the edge this feature occupies.</summary>
            public int End => Offset + Width;

            public bool OverlapsOnSameEdge(EdgeFeature other) =>
                Edge == other.Edge && Offset < other.End && other.Offset < End;
        }

        /// <summary>
        /// Checks every edge feature — gate or generator — twice: its span must
        /// fall within the length of the edge it sits on, and it must not overlap
        /// any other feature on that same edge. Gates and generators go through
        /// one pass over one list because M6 states the rule once for all three
        /// pairings: "two gates, two generators, or a gate and a generator on the
        /// same edge may sit side by side, but their spans are disjoint. An
        /// overlap is a level data error, not a warning."
        /// </summary>
        /// <remarks>
        /// The pairwise comparison is quadratic, which is irrelevant here: it is
        /// bounded by how many edge features an author placed and runs once, at
        /// construction.
        /// </remarks>
        private void ValidateEdgeFeatures()
        {
            var features = new List<EdgeFeature>(Gates.Count + Generators.Count);

            foreach (var gate in Gates)
            {
                features.Add(new EdgeFeature($"Gate {gate.Id}", gate.Edge, gate.Offset, gate.Width));
            }

            foreach (var generator in Generators)
            {
                features.Add(new EdgeFeature(
                    $"Generator {generator.Id}", generator.Edge, generator.Offset, generator.Width));
            }

            foreach (var feature in features)
            {
                var edgeLength = EdgeLength(feature.Edge);

                if (feature.Offset < 0 || feature.End > edgeLength)
                {
                    throw new ArgumentException(
                        $"{feature.Label} on edge {feature.Edge} with offset {feature.Offset} and width " +
                        $"{feature.Width} does not fit within the edge length of {edgeLength}.");
                }
            }

            for (var i = 0; i < features.Count; i++)
            {
                for (var j = i + 1; j < features.Count; j++)
                {
                    if (!features[i].OverlapsOnSameEdge(features[j]))
                    {
                        continue;
                    }

                    throw new ArgumentException(
                        $"{features[i].Label} and {features[j].Label} overlap on edge {features[i].Edge}: " +
                        $"spans [{features[i].Offset}, {features[i].End}) and " +
                        $"[{features[j].Offset}, {features[j].End}). Edge features never overlap (M6).");
                }
            }
        }

        /// <summary>
        /// How many cells long <paramref name="edge"/> is: the grid's width for
        /// the top and bottom edges, its height for the left and right.
        /// </summary>
        private int EdgeLength(BoardEdge edge) =>
            edge == BoardEdge.Top || edge == BoardEdge.Bottom ? Width : Height;

        private void ValidateShutterBounds()
        {
            foreach (var shutter in Shutters)
            {
                if (!IsInsideGrid(shutter.Min) || !IsInsideGrid(shutter.Max))
                {
                    throw new ArgumentException(
                        $"Shutter {shutter.Id} region [{shutter.Min}, {shutter.Max}] falls outside the " +
                        $"{Width}x{Height} grid.");
                }
            }
        }

        private void ValidateLocksAndKeys()
        {
            var requiredKeyCountByLockId = new Dictionary<int, int>();
            var keyCountByTargetLockId = new Dictionary<int, int>();

            void RegisterLock(int? lockId, int requiredKeyCount)
            {
                if (lockId.HasValue)
                {
                    if (requiredKeyCountByLockId.ContainsKey(lockId.Value))
                    {
                        throw new ArgumentException(
                            $"Lock {lockId.Value} is assigned to more than one block; lock ids must be unique " +
                            "within a level.");
                    }

                    requiredKeyCountByLockId[lockId.Value] = requiredKeyCount;
                }
            }

            void RegisterKey(int? keyTargetLockId)
            {
                if (keyTargetLockId.HasValue)
                {
                    keyCountByTargetLockId.TryGetValue(keyTargetLockId.Value, out var count);
                    keyCountByTargetLockId[keyTargetLockId.Value] = count + 1;
                }
            }

            foreach (var block in Blocks)
            {
                RegisterLock(block.LockId, block.RequiredKeyCount);
                RegisterKey(block.KeyTargetLockId);
            }

            foreach (var generator in Generators)
            {
                foreach (var spawned in generator.Queue)
                {
                    RegisterLock(spawned.LockId, spawned.RequiredKeyCount);
                    RegisterKey(spawned.KeyTargetLockId);
                }
            }

            foreach (var elevator in Elevators)
            {
                foreach (var wave in elevator.Waves)
                {
                    foreach (var spawned in wave)
                    {
                        RegisterLock(spawned.LockId, spawned.RequiredKeyCount);
                        RegisterKey(spawned.KeyTargetLockId);
                    }
                }
            }

            foreach (var targetLockId in keyCountByTargetLockId.Keys)
            {
                if (!requiredKeyCountByLockId.ContainsKey(targetLockId))
                {
                    throw new ArgumentException($"A key targets lock {targetLockId}, which does not exist.");
                }
            }

            foreach (var pair in requiredKeyCountByLockId)
            {
                keyCountByTargetLockId.TryGetValue(pair.Key, out var availableKeys);
                if (availableKeys < pair.Value)
                {
                    throw new ArgumentException(
                        $"Lock {pair.Key} requires {pair.Value} key(s) but only {availableKeys} target it.");
                }
            }
        }
    }
}
