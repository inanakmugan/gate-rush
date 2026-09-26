using System;
using System.Collections.Generic;
using GateRush.Core;
using static GateRush.Tests.Fixture;

namespace GateRush.Tests
{
    /// <summary>
    /// Builds small random levels from a seeded <see cref="Random"/>, for
    /// property tests that compare two ways of answering the same question over
    /// many boards no one designed by hand. The same seed always yields the same
    /// sequence of boards, so a failure names a reproducible board.
    /// </summary>
    /// <remarks>
    /// <para>Every mechanic that exists at runtime can appear: static walls,
    /// multi-cell and L-shaped blocks, layered colour stacks, axis-restricted
    /// and frozen blocks, count-gated gates, locks with
    /// <see cref="KeyEffect.UnlockMovement"/> or
    /// <see cref="KeyEffect.ClearOuterColor"/> keys, global or colour-bound
    /// shutters, generators and elevators (M6, M9). A lock's owner may be a
    /// generator's queued block, so a key can complete a lock that has not
    /// spawned yet (D42); keys are always carried by top-level blocks.</para>
    /// <para><b>Reproducibility.</b> Every choice is drawn from the one
    /// <see cref="Random"/> passed in, in a fixed order, and nothing iterates a
    /// hash set or dictionary to make one. Queued blocks are chosen to fit their
    /// generator by construction, and each elevator wave is tiled by a
    /// row-major scan whose only choices are draws. A draw that
    /// <see cref="LevelContext"/> still rejects — two edge features
    /// overlapping, a shutter or a spawn footprint over a wall, a lock with too
    /// few keys — is redrawn from the same generator, which keeps the sequence
    /// deterministic. Adding generators and elevators changed the boards every
    /// existing seed produces, once.</para>
    /// <para>Boards are not required to be solvable.</para>
    /// </remarks>
    internal static class RandomBoards
    {
        private const int MinSide = 3;
        private const int DefaultMaxSide = 5;
        private const int MaxDrawAttempts = 200;
        private const int MaxPlacementTries = 400;
        private const double MinFill = 0.5;
        private const double FillSpread = 0.4;
        private const double LayeredChance = 0.3;
        private const double ThirdLayerChance = 0.2;
        private const double AxisChance = 0.1;
        private const double FrozenChance = 0.1;
        private const double LockChance = 0.35;
        private const double ClearOuterColorKeyChance = 0.5;
        private const double ShutterChance = 0.35;
        private const double ColorBoundShutterChance = 0.4;
        private const double WallChance = 0.3;
        private const double SecondGateChance = 0.3;
        private const double CountGatedGateChance = 0.2;
        private const double GeneratorChance = 0.2;
        private const double ElevatorChance = 0.2;
        private const int MaxThreshold = 2;
        private const int MaxShutterSide = 2;
        private const int MaxRequiredKeys = 2;
        private const int MaxGateWidth = 3;
        private const int MaxQueueLength = 2;
        private const int MaxWaves = 2;
        private const int MaxRegionSide = 2;
        private const int EdgeCount = 4;

        private static readonly Coord[][] Shapes =
        {
            new[] { new Coord(0, 0) },
            new[] { new Coord(0, 0), new Coord(0, 1) },
            new[] { new Coord(0, 0), new Coord(1, 0) },
            new[] { new Coord(0, 0), new Coord(0, 1) },
            new[] { new Coord(0, 0), new Coord(1, 0) },
            new[] { new Coord(0, 0), new Coord(1, 0), new Coord(0, 1), new Coord(1, 1) },
            new[] { new Coord(0, 0), new Coord(1, 0), new Coord(0, 1) },
        };

        private static readonly Coord[] WaveCell1x1 = { new Coord(0, 0) };
        private static readonly Coord[] WaveCellHorizontal1x2 = { new Coord(0, 0), new Coord(1, 0) };
        private static readonly Coord[] WaveCellVertical1x2 = { new Coord(0, 0), new Coord(0, 1) };

        private static readonly BlockColor[] Palette =
        {
            BlockColor.Red, BlockColor.Blue, BlockColor.Green, BlockColor.Yellow
        };

        /// <summary>
        /// The next board from <paramref name="rng"/>, each side between 3 and
        /// <paramref name="maxSide"/> cells. Property tests that need an exact
        /// ground truth per board pass a smaller side so it stays cheap to find.
        /// </summary>
        internal static LevelContext Next(Random rng, int maxSide = DefaultMaxSide)
        {
            for (var attempt = 0; attempt < MaxDrawAttempts; attempt++)
            {
                try
                {
                    return Draw(rng, maxSide);
                }
                catch (ArgumentException)
                {
                    // An invalid combination; draw again from the same sequence.
                }
            }

            throw new InvalidOperationException($"No valid board in {MaxDrawAttempts} draws.");
        }

        private static LevelContext Draw(Random rng, int maxSide)
        {
            var width = rng.Next(MinSide, maxSide + 1);
            var height = rng.Next(MinSide, maxSide + 1);
            var colorCount = rng.Next(2, Palette.Length + 1);
            var occupied = new bool[width, height];

            var walls = new List<Coord>();
            if (rng.NextDouble() < WallChance)
            {
                var wall = new Coord(rng.Next(width), rng.Next(height));
                walls.Add(wall);
                occupied[wall.X, wall.Y] = true;
            }

            var placements = new List<(Coord[] cells, Coord origin)>();
            var targetCells = (MinFill + rng.NextDouble() * FillSpread) * width * height;
            var filled = 0;
            for (var tries = 0; filled < targetCells && tries < MaxPlacementTries; tries++)
            {
                var shape = Shapes[rng.Next(Shapes.Length)];
                var origin = new Coord(rng.Next(width), rng.Next(height));
                if (!Fits(shape, origin, occupied, width, height))
                {
                    continue;
                }

                foreach (var cell in shape)
                {
                    occupied[origin.X + cell.X, origin.Y + cell.Y] = true;
                }

                placements.Add((shape, origin));
                filled += shape.Length;
            }

            // A generator's geometry and queue shapes; the queued blocks' other
            // attributes are drawn with every other block's below.
            GeneratorShape generator = null;
            if (rng.NextDouble() < GeneratorChance)
            {
                generator = DrawGeneratorShape(rng, width, height);
            }

            var queuedCount = generator?.QueueCells.Count ?? 0;

            // Locks and keys: choose the owner first — a top-level block or a
            // generator's queued block — then carriers among the top-level rest.
            var lockIdByBlock = new int?[placements.Count + queuedCount];
            var requiredKeysByBlock = new int[placements.Count + queuedCount];
            var keyTargetByBlock = new int?[placements.Count];
            if (placements.Count >= 2 && rng.NextDouble() < LockChance)
            {
                var owner = rng.Next(placements.Count + queuedCount);
                const int LockId = 1;
                lockIdByBlock[owner] = LockId;
                requiredKeysByBlock[owner] = rng.Next(1, MaxRequiredKeys + 1);
                for (var k = 0; k < requiredKeysByBlock[owner]; k++)
                {
                    var carrier = rng.Next(placements.Count);
                    if (carrier != owner && keyTargetByBlock[carrier] == null)
                    {
                        keyTargetByBlock[carrier] = LockId;
                    }
                }
            }

            var blocks = new List<BlockDefinition>();
            for (var b = 0; b < placements.Count; b++)
            {
                var attributes = DrawAttributes(rng, colorCount);
                blocks.Add(Block(
                    b + 1,
                    placements[b].origin,
                    cells: placements[b].cells,
                    colors: attributes.Colors,
                    axis: attributes.Axis,
                    unfreezeAt: attributes.UnfreezeAt,
                    lockId: lockIdByBlock[b],
                    requiredKeys: requiredKeysByBlock[b],
                    keyTarget: keyTargetByBlock[b],
                    keyEffect: attributes.KeyEffect));
            }

            var generators = new List<GeneratorDefinition>();
            if (generator != null)
            {
                var queue = new List<SpawnedBlock>();
                for (var q = 0; q < queuedCount; q++)
                {
                    var attributes = DrawAttributes(rng, colorCount);
                    var slot = placements.Count + q;
                    queue.Add(Spawned(
                        colors: attributes.Colors,
                        cells: generator.QueueCells[q],
                        axis: attributes.Axis,
                        unfreezeAt: attributes.UnfreezeAt,
                        lockId: lockIdByBlock[slot],
                        requiredKeys: requiredKeysByBlock[slot]));
                }

                generators.Add(Spawner(1, generator.Edge, generator.Offset, generator.Width, queue.ToArray()));
            }

            var elevators = new List<ElevatorDefinition>();
            if (rng.NextDouble() < ElevatorChance)
            {
                elevators.Add(DrawElevator(rng, width, height, colorCount));
            }

            var shutters = new List<ShutterDefinition>();
            if (rng.NextDouble() < ShutterChance)
            {
                var min = new Coord(rng.Next(width), rng.Next(height));
                var max = new Coord(
                    Math.Min(width - 1, min.X + rng.Next(MaxShutterSide)),
                    Math.Min(height - 1, min.Y + rng.Next(MaxShutterSide)));
                BlockColor? requiredColor = rng.NextDouble() < ColorBoundShutterChance ? Palette[rng.Next(colorCount)] : (BlockColor?)null;
                shutters.Add(Shutter(1, min, max, threshold: rng.Next(1, MaxThreshold + 1), requiredColor: requiredColor));
            }

            var gates = new List<GateDefinition>();
            for (var c = 0; c < colorCount; c++)
            {
                var count = rng.NextDouble() < SecondGateChance ? 2 : 1;
                for (var k = 0; k < count; k++)
                {
                    var edge = (BoardEdge)rng.Next(EdgeCount);
                    var edgeLength = EdgeLength(edge, width, height);
                    var gateWidth = rng.Next(1, Math.Min(MaxGateWidth, edgeLength) + 1);
                    var offset = rng.Next(0, edgeLength - gateWidth + 1);
                    int? openAt = rng.NextDouble() < CountGatedGateChance ? rng.Next(1, MaxThreshold + 1) : (int?)null;
                    gates.Add(Gate(gates.Count + 1, edge, offset, gateWidth, Palette[c], openAt));
                }
            }

            return Ctx(
                width, height, blocks, gates,
                shutters: shutters, generators: generators, elevators: elevators, staticWalls: walls);
        }

        /// <summary>
        /// Everything about a random generator but its queued blocks' colours
        /// and modifiers: its edge, offset and width, and each queued block's
        /// cells.
        /// </summary>
        private sealed class GeneratorShape
        {
            public GeneratorShape(BoardEdge edge, int offset, int width, List<Coord[]> queueCells)
            {
                Edge = edge;
                Offset = offset;
                Width = width;
                QueueCells = queueCells;
            }

            public BoardEdge Edge { get; }
            public int Offset { get; }
            public int Width { get; }
            public List<Coord[]> QueueCells { get; }
        }

        /// <summary>
        /// A generator whose every queued shape projects no wider than the
        /// generator onto its edge (M6, D34). Every shape is at most two cells
        /// deep and every grid at least three, so each lands inside the grid;
        /// only a wall under the footprint can make <see cref="LevelContext"/>
        /// reject it.
        /// </summary>
        private static GeneratorShape DrawGeneratorShape(Random rng, int width, int height)
        {
            var edge = (BoardEdge)rng.Next(EdgeCount);
            var edgeLength = EdgeLength(edge, width, height);
            var generatorWidth = rng.Next(1, Math.Min(GeneratorDefinition.MaxWidth, edgeLength) + 1);
            var offset = rng.Next(0, edgeLength - generatorWidth + 1);

            var fitting = new List<Coord[]>();
            foreach (var shape in Shapes)
            {
                if (BlockShape.ProjectionOnto(shape, edge) <= generatorWidth)
                {
                    fitting.Add(shape);
                }
            }

            var queueCells = new List<Coord[]>();
            var queueLength = rng.Next(1, MaxQueueLength + 1);
            for (var q = 0; q < queueLength; q++)
            {
                queueCells.Add(fitting[rng.Next(fitting.Count)]);
            }

            return new GeneratorShape(edge, offset, generatorWidth, queueCells);
        }

        /// <summary>
        /// An elevator over a region of at most
        /// <see cref="MaxRegionSide"/> x <see cref="MaxRegionSide"/> cells,
        /// with one or more waves, each tiling the region exactly (M9): a
        /// row-major scan places, at each uncovered cell, a 1x1 or — where it
        /// fits — a horizontal or vertical 1x2, chosen by a draw.
        /// </summary>
        private static ElevatorDefinition DrawElevator(Random rng, int width, int height, int colorCount)
        {
            var min = new Coord(rng.Next(width), rng.Next(height));
            var max = new Coord(
                Math.Min(width - 1, min.X + rng.Next(MaxRegionSide)),
                Math.Min(height - 1, min.Y + rng.Next(MaxRegionSide)));
            var regionWidth = max.X - min.X + 1;
            var regionHeight = max.Y - min.Y + 1;

            var waves = new List<IReadOnlyList<SpawnedBlock>>();
            var waveCount = rng.Next(1, MaxWaves + 1);
            for (var w = 0; w < waveCount; w++)
            {
                var covered = new bool[regionWidth, regionHeight];
                var wave = new List<SpawnedBlock>();
                for (var y = 0; y < regionHeight; y++)
                {
                    for (var x = 0; x < regionWidth; x++)
                    {
                        if (covered[x, y])
                        {
                            continue;
                        }

                        var options = new List<Coord[]> { WaveCell1x1 };
                        if (x + 1 < regionWidth && !covered[x + 1, y])
                        {
                            options.Add(WaveCellHorizontal1x2);
                        }

                        if (y + 1 < regionHeight)
                        {
                            options.Add(WaveCellVertical1x2);
                        }

                        var cells = options[rng.Next(options.Count)];
                        foreach (var cell in cells)
                        {
                            covered[x + cell.X, y + cell.Y] = true;
                        }

                        var attributes = DrawAttributes(rng, colorCount);
                        wave.Add(Spawned(
                            colors: attributes.Colors,
                            cells: cells,
                            axis: attributes.Axis,
                            unfreezeAt: attributes.UnfreezeAt,
                            regionOrigin: new Coord(x, y)));
                    }
                }

                waves.Add(wave);
            }

            return Elevator(1, min, max, waves.ToArray());
        }

        /// <summary>The colours and modifiers every drawn block gets — top-level, queued or wave.</summary>
        private readonly struct BlockAttributes
        {
            public BlockAttributes(List<BlockColor> colors, MovementAxis axis, int? unfreezeAt, KeyEffect keyEffect)
            {
                Colors = colors;
                Axis = axis;
                UnfreezeAt = unfreezeAt;
                KeyEffect = keyEffect;
            }

            public List<BlockColor> Colors { get; }
            public MovementAxis Axis { get; }
            public int? UnfreezeAt { get; }
            public KeyEffect KeyEffect { get; }
        }

        /// <summary>
        /// One block's colours and modifiers, drawn in a fixed order: colour
        /// stack, axis, freeze threshold, key effect. The key effect is drawn
        /// for every block, used or not, so the draw count per block never
        /// depends on whether it carries a key.
        /// </summary>
        private static BlockAttributes DrawAttributes(Random rng, int colorCount)
        {
            var first = Palette[rng.Next(colorCount)];
            var colors = new List<BlockColor> { first };
            if (rng.NextDouble() < LayeredChance)
            {
                colors.Add(OtherColor(rng, first, colorCount));
                if (rng.NextDouble() < ThirdLayerChance)
                {
                    colors.Add(OtherColor(rng, colors[1], colorCount));
                }
            }

            var axis = MovementAxis.Free;
            if (rng.NextDouble() < AxisChance)
            {
                axis = rng.Next(2) == 0 ? MovementAxis.HorizontalOnly : MovementAxis.VerticalOnly;
            }

            int? unfreezeAt = rng.NextDouble() < FrozenChance ? rng.Next(1, MaxThreshold + 1) : (int?)null;
            var effect = rng.NextDouble() < ClearOuterColorKeyChance ? KeyEffect.ClearOuterColor : KeyEffect.UnlockMovement;

            return new BlockAttributes(colors, axis, unfreezeAt, effect);
        }

        private static int EdgeLength(BoardEdge edge, int width, int height) =>
            edge == BoardEdge.Top || edge == BoardEdge.Bottom ? width : height;

        private static bool Fits(Coord[] shape, Coord origin, bool[,] occupied, int width, int height)
        {
            foreach (var cell in shape)
            {
                var x = origin.X + cell.X;
                var y = origin.Y + cell.Y;
                if (x >= width || y >= height || occupied[x, y])
                {
                    return false;
                }
            }

            return true;
        }

        private static BlockColor OtherColor(Random rng, BlockColor except, int colorCount)
        {
            var index = Array.IndexOf(Palette, except);
            return Palette[(index + 1 + rng.Next(colorCount - 1)) % colorCount];
        }
    }
}
