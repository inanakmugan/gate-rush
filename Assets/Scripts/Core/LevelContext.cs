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

        private readonly HashSet<Coord> staticWallLookup;
        private readonly Dictionary<Coord, int> shutterLookup;

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

            shutterLookup = BuildShutterLookup(Shutters);

            ValidateStaticWalls();
            ValidateBlockPlacement();
            ValidateGates();
            ValidateShutterBounds();
            ValidateLocksAndKeys();
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
        public int? ShutterAt(Coord c) => shutterLookup.TryGetValue(c, out var id) ? id : (int?)null;

        private static Dictionary<Coord, int> BuildShutterLookup(IReadOnlyList<ShutterDefinition> shutters)
        {
            var lookup = new Dictionary<Coord, int>();
            foreach (var shutter in shutters)
            {
                for (var x = shutter.Min.X; x <= shutter.Max.X; x++)
                {
                    for (var y = shutter.Min.Y; y <= shutter.Max.Y; y++)
                    {
                        var cell = new Coord(x, y);
                        if (lookup.TryGetValue(cell, out var existingShutterId))
                        {
                            throw new ArgumentException(
                                $"Shutters {existingShutterId} and {shutter.Id} both cover cell {cell}.");
                        }

                        lookup[cell] = shutter.Id;
                    }
                }
            }

            return lookup;
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

        private void ValidateGates()
        {
            foreach (var gate in Gates)
            {
                var edgeLength = gate.Edge == BoardEdge.Top || gate.Edge == BoardEdge.Bottom ? Width : Height;

                if (gate.Offset < 0 || gate.Offset + gate.Width > edgeLength)
                {
                    throw new ArgumentException(
                        $"Gate {gate.Id} on edge {gate.Edge} with offset {gate.Offset} and width {gate.Width} " +
                        $"does not fit within the edge length of {edgeLength}.");
                }
            }
        }

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
