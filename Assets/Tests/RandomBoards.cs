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
    /// <para>Every mechanic that exists at runtime today can appear: static
    /// walls, multi-cell and L-shaped blocks, layered colour stacks,
    /// axis-restricted and frozen blocks, count-gated gates, locks with
    /// <see cref="KeyEffect.UnlockMovement"/> or
    /// <see cref="KeyEffect.ClearOuterColor"/> keys, and global or colour-bound
    /// shutters. Generators and elevators are absent: they do not act at
    /// runtime yet (phase 1.13).</para>
    /// <para>Boards are not required to be solvable. A draw that
    /// <see cref="LevelContext"/> rejects — two edge features overlapping, a
    /// shutter over a wall — is redrawn from the same generator, which keeps
    /// the sequence deterministic.</para>
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
        private const int MaxThreshold = 2;
        private const int MaxShutterSide = 2;
        private const int MaxRequiredKeys = 2;
        private const int MaxGateWidth = 3;
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

            // Locks and keys: choose owners first, then carriers among the rest.
            var lockIdByBlock = new int?[placements.Count];
            var requiredKeysByBlock = new int[placements.Count];
            var keyTargetByBlock = new int?[placements.Count];
            if (placements.Count >= 2 && rng.NextDouble() < LockChance)
            {
                var owner = rng.Next(placements.Count);
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

                blocks.Add(Block(
                    b + 1,
                    placements[b].origin,
                    cells: placements[b].cells,
                    colors: colors,
                    axis: axis,
                    unfreezeAt: unfreezeAt,
                    lockId: lockIdByBlock[b],
                    requiredKeys: requiredKeysByBlock[b],
                    keyTarget: keyTargetByBlock[b],
                    keyEffect: effect));
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
                    var edgeLength = edge == BoardEdge.Top || edge == BoardEdge.Bottom ? width : height;
                    var gateWidth = rng.Next(1, Math.Min(MaxGateWidth, edgeLength) + 1);
                    var offset = rng.Next(0, edgeLength - gateWidth + 1);
                    int? openAt = rng.NextDouble() < CountGatedGateChance ? rng.Next(1, MaxThreshold + 1) : (int?)null;
                    gates.Add(Gate(gates.Count + 1, edge, offset, gateWidth, Palette[c], openAt));
                }
            }

            return Ctx(width, height, blocks, gates, shutters: shutters, staticWalls: walls);
        }

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
