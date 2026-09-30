using System;
using System.Collections.Generic;
using GateRush.Core;

namespace GateRush.Runtime
{
    /// <summary>Which badge a <see cref="CountPop"/> is on.</summary>
    public enum CountKind
    {
        /// <summary>A frozen block's clears still needed (M3).</summary>
        Frozen,

        /// <summary>A locked block's padlock: keys still required (M8).</summary>
        Padlock,

        /// <summary>A closed gate's clears still needed (M2).</summary>
        Gate,

        /// <summary>A closed shutter's clears still needed (M5).</summary>
        Shutter,

        /// <summary>A generator's blocks still queued (M6).</summary>
        Generator
    }

    /// <summary>A badge whose number went down without reaching its end: it pops once.</summary>
    public readonly struct CountPop : IEquatable<CountPop>
    {
        /// <summary>A pop on the badge of <paramref name="kind"/> number <paramref name="index"/>.</summary>
        public CountPop(CountKind kind, int index)
        {
            Kind = kind;
            Index = index;
        }

        /// <summary>Which kind of badge.</summary>
        public CountKind Kind { get; }

        /// <summary>
        /// The badge's owner: a block slot for <see cref="CountKind.Frozen"/> and
        /// <see cref="CountKind.Padlock"/>, otherwise a position in the level's
        /// gates, shutters or generators.
        /// </summary>
        public int Index { get; }

        /// <inheritdoc />
        public bool Equals(CountPop other) => Kind == other.Kind && Index == other.Index;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is CountPop other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => ((int)Kind * 397) ^ Index;

        /// <inheritdoc />
        public override string ToString() => $"{Kind} {Index}";
    }

    /// <summary>A block that came onto the board from a generator or an elevator.</summary>
    public readonly struct BlockSpawn : IEquatable<BlockSpawn>
    {
        /// <summary>Block slot <paramref name="blockIndex"/>, delivered by the given spawner.</summary>
        public BlockSpawn(int blockIndex, SpawnerKind source, int spawnerIndex)
        {
            BlockIndex = blockIndex;
            Source = source;
            SpawnerIndex = spawnerIndex;
        }

        /// <summary>The block's slot in <c>LevelContext</c>.</summary>
        public int BlockIndex { get; }

        /// <summary>A generator or an elevator.</summary>
        public SpawnerKind Source { get; }

        /// <summary>The spawner's position in <c>LevelContext.Generators</c> or <c>Elevators</c>.</summary>
        public int SpawnerIndex { get; }

        /// <inheritdoc />
        public bool Equals(BlockSpawn other) =>
            BlockIndex == other.BlockIndex && Source == other.Source && SpawnerIndex == other.SpawnerIndex;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is BlockSpawn other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => (BlockIndex * 397) ^ ((int)Source * 31) ^ SpawnerIndex;

        /// <inheritdoc />
        public override string ToString() => $"block {BlockIndex} from {Source} {SpawnerIndex}";
    }

    /// <summary>
    /// Everything a move changed that presentation animates (Module 18): the
    /// clear the move made, and what the player sees change because of it —
    /// thawed blocks, opened gates, shutters and locks, badges that counted
    /// down, and blocks that spawned with the spawner they came from.
    /// </summary>
    /// <remarks>
    /// <para><b>Clears</b> are <see cref="ResolutionDiff"/>'s, unchanged.</para>
    /// <para><b>Everything else</b> compares what <see cref="VisibilityLayer"/>
    /// shows before and after the move, so an animation follows exactly what
    /// the player sees: a block under a closed shutter is not shown, so it
    /// never thaws, unlocks or pops in the player's eyes.</para>
    /// <para><b>Spawned or revealed.</b> A block not shown before and shown
    /// after has spawned, unless a cell of it lies in a shutter that opened
    /// on this move; then the shutter's lift reveals it, and it is not a
    /// spawn. A spawn's source comes from <see cref="LevelContext.TryGetSpawner"/>.
    /// </para>
    /// <para><b>A badge that reaches its end does not pop</b>: its owner's own
    /// change — a thaw, an opening, an unlock, an exhausted generator —
    /// plays instead.</para>
    /// </remarks>
    public sealed class MoveChanges
    {
        private MoveChanges(
            IReadOnlyList<ClearedBlock> clears,
            IReadOnlyList<int> thawedBlocks,
            IReadOnlyList<int> openedGates,
            IReadOnlyList<int> openedShutters,
            IReadOnlyList<int> unlockedBlocks,
            IReadOnlyList<CountPop> countPops,
            IReadOnlyList<BlockSpawn> spawns)
        {
            Clears = clears;
            ThawedBlocks = thawedBlocks;
            OpenedGates = openedGates;
            OpenedShutters = openedShutters;
            UnlockedBlocks = unlockedBlocks;
            CountPops = countPops;
            Spawns = spawns;
        }

        /// <summary>A move that changed nothing presentation animates.</summary>
        public static MoveChanges None { get; } = new MoveChanges(
            Array.Empty<ClearedBlock>(), Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>(),
            Array.Empty<int>(), Array.Empty<CountPop>(), Array.Empty<BlockSpawn>());

        /// <summary>The clear the move made, exactly as <see cref="ResolutionDiff.Between"/> reports it.</summary>
        public IReadOnlyList<ClearedBlock> Clears { get; }

        /// <summary>Blocks shown frozen before and shown unfrozen after (M3), by slot.</summary>
        public IReadOnlyList<int> ThawedBlocks { get; }

        /// <summary>Gates closed before and open after (M2), by position in <c>LevelContext.Gates</c>.</summary>
        public IReadOnlyList<int> OpenedGates { get; }

        /// <summary>Shutters closed before and open after (M5), by position in <c>LevelContext.Shutters</c>.</summary>
        public IReadOnlyList<int> OpenedShutters { get; }

        /// <summary>Blocks shown locked before and shown unlocked after (M8), by slot.</summary>
        public IReadOnlyList<int> UnlockedBlocks { get; }

        /// <summary>Badges whose number went down without reaching their end.</summary>
        public IReadOnlyList<CountPop> CountPops { get; }

        /// <summary>Blocks that came onto the board from a generator or an elevator.</summary>
        public IReadOnlyList<BlockSpawn> Spawns { get; }

        /// <summary>True when anything beyond the clear changed: the arrive stage has something to play.</summary>
        public bool HasArrivals =>
            ThawedBlocks.Count > 0 || OpenedGates.Count > 0 || OpenedShutters.Count > 0
            || UnlockedBlocks.Count > 0 || CountPops.Count > 0 || Spawns.Count > 0;

        /// <summary>
        /// What <paramref name="move"/> changed between <paramref name="before"/>
        /// and <paramref name="after"/>. Arguments as
        /// <see cref="ResolutionDiff.Between"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The move's block index is not a slot of <paramref name="ctx"/>.</exception>
        /// <exception cref="InvalidOperationException">
        /// The clear's gate cannot be found (see <see cref="ResolutionDiff.Between"/>),
        /// or a block appeared that neither a spawner nor an opened shutter
        /// explains — in both cases the states and the move do not belong
        /// together.
        /// </exception>
        public static MoveChanges Between(
            LevelContext ctx, BoardState before, BoardState after, Move move, Direction? push)
        {
            var clears = ResolutionDiff.Between(ctx, before, after, move, push);
            return Build(ctx, before, after, clears);
        }

        /// <summary>
        /// Every change but the clear, for when the clear cannot be worked out:
        /// the rest still animates.
        /// </summary>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="InvalidOperationException">
        /// A block appeared that neither a spawner nor an opened shutter
        /// explains.
        /// </exception>
        public static MoveChanges WithoutClears(LevelContext ctx, BoardState before, BoardState after)
        {
            if (ctx == null)
            {
                throw new ArgumentNullException(nameof(ctx));
            }

            if (before == null)
            {
                throw new ArgumentNullException(nameof(before));
            }

            if (after == null)
            {
                throw new ArgumentNullException(nameof(after));
            }

            return Build(ctx, before, after, Array.Empty<ClearedBlock>());
        }

        private static MoveChanges Build(
            LevelContext ctx, BoardState before, BoardState after, IReadOnlyList<ClearedBlock> clears)
        {
            var visibility = new VisibilityLayer(ctx);
            var pops = new List<CountPop>();

            var openedGates = new List<int>();
            for (var g = 0; g < ctx.Gates.Count; g++)
            {
                var was = visibility.Gate(before, g);
                var now = visibility.Gate(after, g);
                if (was.IsOpen)
                {
                    continue;
                }

                if (now.IsOpen)
                {
                    openedGates.Add(g);
                }
                else if (now.OpensInClears < was.OpensInClears)
                {
                    pops.Add(new CountPop(CountKind.Gate, g));
                }
            }

            var openedShutters = new List<int>();
            for (var s = 0; s < ctx.Shutters.Count; s++)
            {
                var was = visibility.Shutter(before, s);
                var now = visibility.Shutter(after, s);
                if (!was.IsClosed)
                {
                    continue;
                }

                if (!now.IsClosed)
                {
                    openedShutters.Add(s);
                }
                else if (now.OpensInClears < was.OpensInClears)
                {
                    pops.Add(new CountPop(CountKind.Shutter, s));
                }
            }

            for (var g = 0; g < ctx.Generators.Count; g++)
            {
                var was = visibility.Generator(before, g);
                var now = visibility.Generator(after, g);
                if (was.IsShown && now.IsShown && now.Queued < was.Queued)
                {
                    pops.Add(new CountPop(CountKind.Generator, g));
                }
            }

            var thawed = new List<int>();
            var unlocked = new List<int>();
            var spawns = new List<BlockSpawn>();
            for (var i = 0; i < ctx.TotalBlockCapacity; i++)
            {
                var was = visibility.Block(before, i);
                var now = visibility.Block(after, i);
                if (!now.IsShown)
                {
                    continue;
                }

                if (!was.IsShown)
                {
                    AddSpawnUnlessRevealed(ctx, after, i, openedShutters, spawns);
                    continue;
                }

                if (was.IsFrozen)
                {
                    if (!now.IsFrozen)
                    {
                        thawed.Add(i);
                    }
                    else if (now.FrozenRemaining < was.FrozenRemaining)
                    {
                        pops.Add(new CountPop(CountKind.Frozen, i));
                    }
                }

                if (was.IsLocked)
                {
                    if (!now.IsLocked)
                    {
                        unlocked.Add(i);
                    }
                    else if (now.KeysStillRequired < was.KeysStillRequired)
                    {
                        pops.Add(new CountPop(CountKind.Padlock, i));
                    }
                }
            }

            return new MoveChanges(clears, thawed, openedGates, openedShutters, unlocked, pops, spawns);
        }

        /// <summary>
        /// Records block <paramref name="blockIndex"/>, newly shown, as a spawn
        /// — unless a cell of it lies in a shutter that opened on this move,
        /// which reveals it instead.
        /// </summary>
        private static void AddSpawnUnlessRevealed(
            LevelContext ctx, BoardState after, int blockIndex, IReadOnlyList<int> openedShutters, List<BlockSpawn> spawns)
        {
            var origin = after.Origins[blockIndex];
            var cells = ctx.SpecAt(blockIndex).Cells;
            for (var c = 0; c < cells.Count; c++)
            {
                var shutter = ctx.ShutterPositionAt(origin + cells[c]);
                if (shutter.HasValue && Contains(openedShutters, shutter.Value))
                {
                    return;
                }
            }

            if (!ctx.TryGetSpawner(blockIndex, out var kind, out var spawnerIndex))
            {
                throw new InvalidOperationException(
                    $"Level {ctx.LevelId}: block slot {blockIndex} appeared on the board, but it has no spawner " +
                    "and no shutter over it opened.");
            }

            spawns.Add(new BlockSpawn(blockIndex, kind, spawnerIndex));
        }

        private static bool Contains(IReadOnlyList<int> values, int value)
        {
            for (var i = 0; i < values.Count; i++)
            {
                if (values[i] == value)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
