using System;
using System.Collections.Generic;
using System.Linq;
using GateRush.Core;
using GateRush.Solver;

namespace GateRush.Editor
{
    /// <summary>The kind of a <see cref="DraftWarning"/>.</summary>
    public enum DraftWarningCategory
    {
        /// <summary>The draft cannot be turned into a <see cref="LevelContext"/> at all.</summary>
        DraftDoesNotFormValidLevel,

        /// <summary>No block can move at level start (checked against the exhaustive move set).</summary>
        NoLegalOpeningMove,

        /// <summary>No block starts flush against a matching open gate — D16's ready opening move.</summary>
        NoReadyOpeningMove,

        /// <summary>A colour in some block's stack has no gate of that colour anywhere (D26).</summary>
        ColorHasNoGate,

        /// <summary>A gate of the right colour exists but every one is too narrow for the block's projection.</summary>
        GateTooNarrowForBlock,

        /// <summary>An axis-restricted block has no gate of a needed colour on an edge it can reach (M7).</summary>
        AxisRestrictedBlockHasNoGate,

        /// <summary>A gate or shutter threshold is higher than the total clears the level can produce.</summary>
        ThresholdExceedsAvailableClears,

        /// <summary>A lock has fewer keys targeting it than it requires (M8).</summary>
        LockHasTooFewKeys,

        /// <summary>Every cell a gate opens onto is a static wall, so the gate can never be used.</summary>
        GateOpensOntoWall,

        /// <summary>An elevator wave does not tile its region exactly (M9).</summary>
        ElevatorWaveNotExactTiling,

        /// <summary>
        /// A value in the loaded file could not be read — an unrecognised enum or
        /// colour name — and a default was substituted. Reported so the
        /// substitution is not silent (see <see cref="DraftLoadIssue"/>).
        /// </summary>
        UnreadableValueDefaultedOnLoad,
    }

    /// <summary>One thing wrong with a draft, cheap to compute and shown live.</summary>
    public sealed class DraftWarning
    {
        public DraftWarningCategory Category { get; }
        public string Message { get; }

        public DraftWarning(DraftWarningCategory category, string message)
        {
            Category = category;
            Message = message;
        }

        public override string ToString() => Message;
    }

    /// <summary>
    /// The live warnings the editor shows while a level is built. Everything here
    /// is a scan over the draft — no search — so it recomputes on every edit and
    /// is always current (Module 09). The solver runs separately and on demand;
    /// a level can be unsolvable while warnings explain why, and can have
    /// warnings while still solvable.
    /// </summary>
    /// <remarks>
    /// Two checks — <see cref="DraftWarningCategory.NoLegalOpeningMove"/> and
    /// <see cref="DraftWarningCategory.NoReadyOpeningMove"/> — need a valid
    /// <see cref="LevelContext"/>. When the draft does not form one, a single
    /// <see cref="DraftWarningCategory.DraftDoesNotFormValidLevel"/> carries
    /// <c>Core</c>'s message and those two are skipped; the rest still run off
    /// the draft.
    /// </remarks>
    public sealed class DraftValidator
    {
        public IReadOnlyList<DraftWarning> Validate(LevelDraft draft)
        {
            if (draft == null)
            {
                throw new ArgumentNullException(nameof(draft));
            }

            var warnings = new List<DraftWarning>();
            var blockLikes = EnumerateBlockLikes(draft).ToList();

            foreach (var issue in draft.LoadIssues)
            {
                warnings.Add(new DraftWarning(DraftWarningCategory.UnreadableValueDefaultedOnLoad, issue.Message));
            }

            AddColorAndGateWarnings(draft, blockLikes, warnings);
            AddAxisRestrictionWarnings(draft, blockLikes, warnings);
            AddThresholdWarnings(draft, blockLikes, warnings);
            AddLockKeyWarnings(blockLikes, warnings);
            AddGateOntoWallWarnings(draft, warnings);
            AddElevatorTilingWarnings(draft, warnings);

            LevelContext ctx = null;
            try
            {
                ctx = draft.ToContext();
            }
            catch (Exception e)
            {
                warnings.Add(new DraftWarning(
                    DraftWarningCategory.DraftDoesNotFormValidLevel,
                    $"The draft does not form a valid level: {e.Message}"));
            }

            if (ctx != null)
            {
                AddOpeningMoveWarnings(ctx, warnings);
            }

            return warnings;
        }

        // -- Colour / gate compatibility ------------------------------

        private static void AddColorAndGateWarnings(
            LevelDraft draft, IReadOnlyList<BlockLike> blockLikes, List<DraftWarning> warnings)
        {
            foreach (var block in blockLikes)
            {
                foreach (var color in block.ColorStack.Distinct())
                {
                    var matching = draft.Gates.Where(g => g.Color == color).ToList();

                    if (matching.Count == 0)
                    {
                        warnings.Add(new DraftWarning(
                            DraftWarningCategory.ColorHasNoGate,
                            $"{block.Label} has a {color} layer but no {color} gate exists anywhere in the level."));
                        continue;
                    }

                    var bestDeficit = matching.Min(g => Projection(block.Cells, g.Edge) - g.Width);
                    if (bestDeficit > 0)
                    {
                        var widest = matching.Max(g => g.Width);
                        warnings.Add(new DraftWarning(
                            DraftWarningCategory.GateTooNarrowForBlock,
                            $"{block.Label}'s {color} layer has no wide-enough gate: the widest {color} gate is " +
                            $"{widest}, but the block's projection onto its edge is larger by {bestDeficit}."));
                    }
                }
            }
        }

        private static void AddAxisRestrictionWarnings(
            LevelDraft draft, IReadOnlyList<BlockLike> blockLikes, List<DraftWarning> warnings)
        {
            foreach (var block in blockLikes)
            {
                if (block.Axis == MovementAxis.Free)
                {
                    continue;
                }

                var reachableEdges = block.Axis == MovementAxis.HorizontalOnly
                    ? new[] { BoardEdge.Left, BoardEdge.Right }
                    : new[] { BoardEdge.Top, BoardEdge.Bottom };

                foreach (var color in block.ColorStack.Distinct())
                {
                    var gatesOfColor = draft.Gates.Where(g => g.Color == color).ToList();
                    if (gatesOfColor.Count == 0)
                    {
                        // ColorHasNoGate already covers this; do not pile on.
                        continue;
                    }

                    if (!gatesOfColor.Any(g => reachableEdges.Contains(g.Edge)))
                    {
                        var ends = block.Axis == MovementAxis.HorizontalOnly ? "left or right" : "top or bottom";
                        warnings.Add(new DraftWarning(
                            DraftWarningCategory.AxisRestrictedBlockHasNoGate,
                            $"{block.Label} moves {block.Axis} only, but no {color} gate is on the {ends} edge, " +
                            $"so its {color} layer can never be cleared by movement."));
                    }
                }
            }
        }

        // -- Thresholds vs available clears --------------------------

        private static void AddThresholdWarnings(
            LevelDraft draft, IReadOnlyList<BlockLike> blockLikes, List<DraftWarning> warnings)
        {
            var totalClears = blockLikes.Sum(b => b.ColorStack.Count);

            var clearsByColor = new Dictionary<BlockColor, int>();
            foreach (var block in blockLikes)
            {
                foreach (var color in block.ColorStack)
                {
                    clearsByColor.TryGetValue(color, out var count);
                    clearsByColor[color] = count + 1;
                }
            }

            foreach (var gate in draft.Gates)
            {
                if (gate.OpenAtClearCount.HasValue && gate.OpenAtClearCount.Value > totalClears)
                {
                    warnings.Add(new DraftWarning(
                        DraftWarningCategory.ThresholdExceedsAvailableClears,
                        $"Gate {gate.Id} opens at {gate.OpenAtClearCount.Value} clears, but the level can only " +
                        $"produce {totalClears}."));
                }
            }

            foreach (var shutter in draft.Shutters)
            {
                if (shutter.RequiredColor.HasValue)
                {
                    clearsByColor.TryGetValue(shutter.RequiredColor.Value, out var available);
                    if (shutter.Threshold > available)
                    {
                        warnings.Add(new DraftWarning(
                            DraftWarningCategory.ThresholdExceedsAvailableClears,
                            $"Shutter {shutter.Id} opens at {shutter.Threshold} {shutter.RequiredColor.Value} " +
                            $"clears, but the level can only produce {available}."));
                    }
                }
                else if (shutter.Threshold > totalClears)
                {
                    warnings.Add(new DraftWarning(
                        DraftWarningCategory.ThresholdExceedsAvailableClears,
                        $"Shutter {shutter.Id} opens at {shutter.Threshold} clears, but the level can only " +
                        $"produce {totalClears}."));
                }
            }
        }

        // -- Locks and keys ----------------------------------------

        private static void AddLockKeyWarnings(IReadOnlyList<BlockLike> blockLikes, List<DraftWarning> warnings)
        {
            var required = new Dictionary<int, int>();
            var keyCount = new Dictionary<int, int>();

            foreach (var block in blockLikes)
            {
                if (block.LockId.HasValue)
                {
                    required[block.LockId.Value] = block.RequiredKeyCount;
                }

                if (block.KeyTargetLockId.HasValue)
                {
                    keyCount.TryGetValue(block.KeyTargetLockId.Value, out var count);
                    keyCount[block.KeyTargetLockId.Value] = count + 1;
                }
            }

            foreach (var pair in required)
            {
                keyCount.TryGetValue(pair.Key, out var have);
                if (have < pair.Value)
                {
                    warnings.Add(new DraftWarning(
                        DraftWarningCategory.LockHasTooFewKeys,
                        $"Lock {pair.Key} requires {pair.Value} key(s) but only {have} target it."));
                }
            }
        }

        // -- Gate onto a wall -------------------------------------

        private static void AddGateOntoWallWarnings(LevelDraft draft, List<DraftWarning> warnings)
        {
            var walls = new HashSet<Coord>(draft.StaticWalls);

            foreach (var gate in draft.Gates)
            {
                if (gate.Width < 1)
                {
                    continue;
                }

                var openingCells = GateOpeningCells(gate, draft.Width, draft.Height).ToList();
                if (openingCells.Count == 0)
                {
                    // Every opening cell is off the grid — a placement error Core
                    // reports, not this warning's job.
                    continue;
                }

                if (openingCells.All(walls.Contains))
                {
                    warnings.Add(new DraftWarning(
                        DraftWarningCategory.GateOpensOntoWall,
                        $"Gate {gate.Id} opens entirely onto walled cells, so no block can ever exit through it."));
                }
            }
        }

        private static IEnumerable<Coord> GateOpeningCells(GateDraft gate, int width, int height)
        {
            for (var i = 0; i < gate.Width; i++)
            {
                Coord cell;
                switch (gate.Edge)
                {
                    case BoardEdge.Bottom:
                        cell = new Coord(gate.Offset + i, 0);
                        break;
                    case BoardEdge.Top:
                        cell = new Coord(gate.Offset + i, height - 1);
                        break;
                    case BoardEdge.Left:
                        cell = new Coord(0, gate.Offset + i);
                        break;
                    case BoardEdge.Right:
                        cell = new Coord(width - 1, gate.Offset + i);
                        break;
                    default:
                        continue;
                }

                if (cell.X >= 0 && cell.X < width && cell.Y >= 0 && cell.Y < height)
                {
                    yield return cell;
                }
            }
        }

        // -- Elevator wave tiling --------------------------------

        private static void AddElevatorTilingWarnings(LevelDraft draft, List<DraftWarning> warnings)
        {
            foreach (var elevator in draft.Elevators)
            {
                for (var w = 0; w < elevator.Waves.Count; w++)
                {
                    var wave = elevator.Waves[w];
                    if (wave.Blocks.Count == 0)
                    {
                        continue;
                    }

                    var converted = new List<SpawnedBlock>();
                    var convertible = true;
                    foreach (var block in wave.Blocks)
                    {
                        if (TryConvert(block, out var spawned))
                        {
                            converted.Add(spawned);
                        }
                        else
                        {
                            convertible = false;
                            break;
                        }
                    }

                    if (!convertible)
                    {
                        // A block-shape problem surfaces through ToContext; the
                        // tiling of an unbuildable wave is not meaningful.
                        continue;
                    }

                    var tiling = ElevatorTiling.Check(elevator.Min, elevator.Max, converted);
                    if (tiling.IsExact)
                    {
                        continue;
                    }

                    warnings.Add(new DraftWarning(
                        DraftWarningCategory.ElevatorWaveNotExactTiling,
                        $"Elevator {elevator.Id} wave {w} does not tile its region: {DescribeTiling(tiling)}."));
                }
            }
        }

        private static string DescribeTiling(ElevatorTiling.Result tiling)
        {
            var parts = new List<string>();
            if (tiling.BlocksWithoutRegionOrigin.Count > 0)
            {
                parts.Add($"{tiling.BlocksWithoutRegionOrigin.Count} block(s) unplaced");
            }

            if (tiling.OutsideRegionCells.Count > 0)
            {
                parts.Add($"{tiling.OutsideRegionCells.Count} cell(s) outside the region");
            }

            if (tiling.OverlappingCells.Count > 0)
            {
                parts.Add($"{tiling.OverlappingCells.Count} overlapping cell(s)");
            }

            if (tiling.UncoveredCells.Count > 0)
            {
                parts.Add($"{tiling.UncoveredCells.Count} cell(s) uncovered");
            }

            return string.Join(", ", parts);
        }

        private static bool TryConvert(SpawnedBlockDraft draft, out SpawnedBlock spawned)
        {
            try
            {
                spawned = new SpawnedBlock(
                    cells: draft.Cells.ToArray(),
                    colorStack: draft.ColorStack.ToArray(),
                    axis: draft.Axis,
                    unfreezeAtClearCount: draft.UnfreezeAtClearCount,
                    lockId: draft.LockId,
                    requiredKeyCount: draft.RequiredKeyCount,
                    keyTargetLockId: draft.KeyTargetLockId,
                    keyEffect: draft.KeyEffect,
                    timeBonusSeconds: draft.TimeBonusSeconds,
                    regionOrigin: draft.RegionOrigin);
                return true;
            }
            catch (ArgumentException)
            {
                spawned = null;
                return false;
            }
        }

        // -- Opening-move checks (need a valid context) -------------

        private static void AddOpeningMoveWarnings(LevelContext ctx, List<DraftWarning> warnings)
        {
            var initial = BoardState.CreateInitial(ctx);

            var hasAnyMove = new MoveGenerator().Generate(ctx, initial, MoveGenMode.Exhaustive).Any();
            if (!hasAnyMove)
            {
                warnings.Add(new DraftWarning(
                    DraftWarningCategory.NoLegalOpeningMove,
                    "No block can move at level start — the board is deadlocked."));
            }

            var hasReadyClear = false;
            for (var i = 0; i < ctx.TotalBlockCapacity; i++)
            {
                if (initial.Alive[i]
                    && BlockReachability.IsAtCompatibleExitGate(ctx, initial, i, initial.Origins[i]))
                {
                    hasReadyClear = true;
                    break;
                }
            }

            if (!hasReadyClear)
            {
                warnings.Add(new DraftWarning(
                    DraftWarningCategory.NoReadyOpeningMove,
                    "No block starts flush against a matching open gate; a dense level needs one ready clear (D16)."));
            }
        }

        // -- Block-like enumeration and geometry --------------------

        private readonly struct BlockLike
        {
            public string Label { get; }
            public IReadOnlyList<Coord> Cells { get; }
            public IReadOnlyList<BlockColor> ColorStack { get; }
            public MovementAxis Axis { get; }
            public int? LockId { get; }
            public int RequiredKeyCount { get; }
            public int? KeyTargetLockId { get; }

            public BlockLike(
                string label,
                IReadOnlyList<Coord> cells,
                IReadOnlyList<BlockColor> colorStack,
                MovementAxis axis,
                int? lockId,
                int requiredKeyCount,
                int? keyTargetLockId)
            {
                Label = label;
                Cells = cells;
                ColorStack = colorStack;
                Axis = axis;
                LockId = lockId;
                RequiredKeyCount = requiredKeyCount;
                KeyTargetLockId = keyTargetLockId;
            }
        }

        private static IEnumerable<BlockLike> EnumerateBlockLikes(LevelDraft draft)
        {
            foreach (var b in draft.Blocks)
            {
                yield return new BlockLike(
                    $"Block {b.Id}", b.Cells, b.ColorStack, b.Axis, b.LockId, b.RequiredKeyCount, b.KeyTargetLockId);
            }

            foreach (var g in draft.Generators)
            {
                for (var i = 0; i < g.Queue.Count; i++)
                {
                    var s = g.Queue[i];
                    yield return new BlockLike(
                        $"Generator {g.Id} queue entry {i}", s.Cells, s.ColorStack, s.Axis,
                        s.LockId, s.RequiredKeyCount, s.KeyTargetLockId);
                }
            }

            foreach (var e in draft.Elevators)
            {
                for (var w = 0; w < e.Waves.Count; w++)
                {
                    var wave = e.Waves[w];
                    for (var i = 0; i < wave.Blocks.Count; i++)
                    {
                        var s = wave.Blocks[i];
                        yield return new BlockLike(
                            $"Elevator {e.Id} wave {w} block {i}", s.Cells, s.ColorStack, s.Axis,
                            s.LockId, s.RequiredKeyCount, s.KeyTargetLockId);
                    }
                }
            }
        }

        /// <summary>
        /// The block footprint's extent along <paramref name="edge"/> — the span
        /// the gate on that edge must be at least as wide as (M1). The whole
        /// footprint projects, not just the cells touching the wall.
        /// </summary>
        private static int Projection(IReadOnlyList<Coord> cells, BoardEdge edge)
        {
            if (cells.Count == 0)
            {
                return 0;
            }

            var minX = cells.Min(c => c.X);
            var maxX = cells.Max(c => c.X);
            var minY = cells.Min(c => c.Y);
            var maxY = cells.Max(c => c.Y);

            return edge == BoardEdge.Top || edge == BoardEdge.Bottom
                ? maxX - minX + 1
                : maxY - minY + 1;
        }
    }
}
