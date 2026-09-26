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

        /// <summary>
        /// A block in a generator's queue projects onto the generator's edge
        /// wider than the generator is — the mirror of
        /// <see cref="GateTooNarrowForBlock"/>, for a gate's inverse (M6, D34).
        /// </summary>
        GeneratorTooNarrowForQueuedBlock,

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
        /// A shutter's region has a cell no block covers (M5). A shutter exists
        /// to hide blocks and later reveal them, so an empty cell underneath
        /// hides nothing.
        /// </summary>
        ShutterRegionNotFullyCovered,

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

            // Built once, up front: the generator entries below read their spawn
            // origin from it, and the opening-move checks at the end need it.
            // Its failure is reported last, where it always was.
            LevelContext ctx = null;
            string contextError = null;
            try
            {
                ctx = draft.ToContext();
            }
            catch (Exception e)
            {
                contextError = e.Message;
            }

            var warnings = new List<DraftWarning>();
            var blockLikes = EnumerateBlockLikes(draft, ctx).ToList();

            foreach (var issue in draft.LoadIssues)
            {
                warnings.Add(new DraftWarning(DraftWarningCategory.UnreadableValueDefaultedOnLoad, issue.Message));
            }

            AddColorAndGateWarnings(draft, blockLikes, warnings);
            AddGeneratorWidthWarnings(draft, warnings);
            AddAxisRestrictionWarnings(draft, blockLikes, warnings);
            AddThresholdWarnings(draft, blockLikes, warnings);
            AddLockKeyWarnings(blockLikes, warnings);
            AddGateOntoWallWarnings(draft, warnings);
            AddElevatorTilingWarnings(draft, warnings);
            AddShutterCoverageWarnings(draft, warnings);

            if (contextError != null)
            {
                warnings.Add(new DraftWarning(
                    DraftWarningCategory.DraftDoesNotFormValidLevel,
                    $"The draft does not form a valid level: {contextError}"));
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

                    var bestDeficit = matching.Min(g => BlockShape.ProjectionOnto(block.Cells, g.Edge) - g.Width);
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

        /// <summary>
        /// Reports a queued block wider than the generator that has to push it
        /// out. A generator is a gate's inverse and measures the same projection
        /// a gate does (M6): the block's extent along the generator's edge may not
        /// exceed the generator's width, while its extent into the board is
        /// unconstrained. A width outside <c>[1, MaxWidth]</c> is <c>Core</c>'s
        /// error to throw, not this warning's to repeat, so an invalid width is
        /// skipped here — <see cref="DraftWarningCategory.DraftDoesNotFormValidLevel"/>
        /// already carries it.
        /// </summary>
        private static void AddGeneratorWidthWarnings(LevelDraft draft, List<DraftWarning> warnings)
        {
            foreach (var generator in draft.Generators)
            {
                if (generator.Width < 1 || generator.Width > GeneratorDefinition.MaxWidth)
                {
                    continue;
                }

                for (var i = 0; i < generator.Queue.Count; i++)
                {
                    var projection = BlockShape.ProjectionOnto(generator.Queue[i].Cells, generator.Edge);
                    if (projection <= generator.Width)
                    {
                        continue;
                    }

                    warnings.Add(new DraftWarning(
                        DraftWarningCategory.GeneratorTooNarrowForQueuedBlock,
                        $"Generator {generator.Id} queue entry {i} projects {projection} cells onto the " +
                        $"{generator.Edge} edge, but the generator is only {generator.Width} wide, so that block " +
                        "can never be pushed out."));
                }
            }
        }

        /// <summary>
        /// Reports an axis-restricted block with no gate of some layer's colour
        /// that it can arrive at (M7). Two kinds of edge count: the two ends of
        /// its axis, which it can slide or push into; and an edge across its axis
        /// that its fixed row or column touches, since a block sliding along that
        /// edge into line with a gate there clears on arrival (D39). The second
        /// kind needs the block's position, so it is counted only for blocks
        /// whose position is known — see <see cref="BlockLike.Origin"/>.
        /// </summary>
        private static void AddAxisRestrictionWarnings(
            LevelDraft draft, IReadOnlyList<BlockLike> blockLikes, List<DraftWarning> warnings)
        {
            foreach (var block in blockLikes)
            {
                if (block.Axis == MovementAxis.Free)
                {
                    continue;
                }

                var reachableEdges = ArrivableEdges(draft, block);

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
                        var line = block.Axis == MovementAxis.HorizontalOnly ? "row" : "column";
                        warnings.Add(new DraftWarning(
                            DraftWarningCategory.AxisRestrictedBlockHasNoGate,
                            $"{block.Label} moves {block.Axis} only, but no {color} gate is one it can arrive at — " +
                            $"none on the {ends} edge, nor on an edge its {line} touches — so its {color} layer " +
                            "can never be cleared by movement."));
                    }
                }
            }
        }

        /// <summary>
        /// The edges an axis-restricted <paramref name="block"/> can arrive flush
        /// against: both ends of its axis, plus each edge across its axis that
        /// its footprint touches at <see cref="BlockLike.Origin"/>, when known.
        /// </summary>
        private static List<BoardEdge> ArrivableEdges(LevelDraft draft, BlockLike block)
        {
            var isHorizontal = block.Axis == MovementAxis.HorizontalOnly;
            var edges = isHorizontal
                ? new List<BoardEdge> { BoardEdge.Left, BoardEdge.Right }
                : new List<BoardEdge> { BoardEdge.Top, BoardEdge.Bottom };

            if (!block.Origin.HasValue || block.Cells.Count == 0)
            {
                return edges;
            }

            var origin = block.Origin.Value;
            var min = isHorizontal ? block.Cells.Min(c => c.Y + origin.Y) : block.Cells.Min(c => c.X + origin.X);
            var max = isHorizontal ? block.Cells.Max(c => c.Y + origin.Y) : block.Cells.Max(c => c.X + origin.X);
            var last = (isHorizontal ? draft.Height : draft.Width) - 1;

            if (min == 0)
            {
                edges.Add(isHorizontal ? BoardEdge.Bottom : BoardEdge.Left);
            }

            if (max == last)
            {
                edges.Add(isHorizontal ? BoardEdge.Top : BoardEdge.Right);
            }

            return edges;
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

            foreach (var block in blockLikes)
            {
                if (block.UnfreezeAtClearCount.HasValue && block.UnfreezeAtClearCount.Value > totalClears)
                {
                    warnings.Add(new DraftWarning(
                        DraftWarningCategory.ThresholdExceedsAvailableClears,
                        $"{block.Label} unfreezes at {block.UnfreezeAtClearCount.Value} clears, but the level can " +
                        $"only produce {totalClears}, so it stays frozen for the whole level (M3)."));
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
                    // An empty wave is not skipped: M9 says waves arrive fully
                    // packed, so a wave with nothing in it is the loudest
                    // possible violation of that, and ElevatorTiling.Check
                    // already reports every region cell as uncovered.
                    var wave = elevator.Waves[w];
                    var tiling = DraftTiling.Check(elevator, wave);
                    if (tiling == null)
                    {
                        // A block-shape problem surfaces through ToContext; the
                        // tiling of an unbuildable wave is not meaningful.
                        continue;
                    }

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

        // -- Shutter region coverage -----------------------------

        /// <summary>
        /// Reports a shutter whose region has a cell no block covers. M5: the
        /// region is authored fully packed with blocks, cell for cell, because a
        /// shutter exists to hide and later reveal blocks and an empty cell
        /// underneath hides nothing. The parallel to
        /// <see cref="AddElevatorTilingWarnings"/> is deliberate but the rule is
        /// weaker: a wave must tile its region <em>exactly</em>, while a shutter
        /// only has to be covered — a block may straddle the region's boundary,
        /// and nothing here objects to that.
        /// </summary>
        /// <remarks>
        /// A static wall counts as covering its cell. A wall can never hold a
        /// block, so demanding one there would be a warning with no correct
        /// resolution short of moving the wall or the shutter; and a wall hides
        /// nothing either way, which is what the rule is actually about.
        /// </remarks>
        private static void AddShutterCoverageWarnings(LevelDraft draft, List<DraftWarning> warnings)
        {
            var covered = new HashSet<Coord>(draft.StaticWalls);
            foreach (var block in draft.Blocks)
            {
                foreach (var relative in block.Cells)
                {
                    covered.Add(block.StartOrigin + relative);
                }
            }

            foreach (var shutter in draft.Shutters)
            {
                var uncovered = 0;
                Coord? first = null;

                for (var y = shutter.Min.Y; y <= shutter.Max.Y; y++)
                {
                    for (var x = shutter.Min.X; x <= shutter.Max.X; x++)
                    {
                        var cell = new Coord(x, y);
                        if (covered.Contains(cell))
                        {
                            continue;
                        }

                        uncovered++;
                        if (!first.HasValue)
                        {
                            first = cell;
                        }
                    }
                }

                if (uncovered == 0)
                {
                    continue;
                }

                warnings.Add(new DraftWarning(
                    DraftWarningCategory.ShutterRegionNotFullyCovered,
                    $"Shutter {shutter.Id}'s region is not fully covered: {uncovered} cell(s) uncovered, " +
                    $"first at {first.Value}. A shutter hides nothing over an empty cell (M5)."));
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
                if (BlockReachability.CanClearInPlace(ctx, initial, i))
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
            public int? UnfreezeAtClearCount { get; }
            public int? LockId { get; }
            public int RequiredKeyCount { get; }
            public int? KeyTargetLockId { get; }

            /// <summary>
            /// The absolute grid origin <see cref="Cells"/> are relative to, when
            /// the draft fixes it: a block's start origin, or a placed elevator
            /// wave block's region origin offset by the region's minimum corner,
            /// or a generator queue entry's spawn origin, read from
            /// <see cref="LevelContext.GeneratorSpawnOrigin"/> — Core's one
            /// placement rule, never derived here (D28, D31). Null for an
            /// unplaced wave block, and for a generator entry when the draft does
            /// not form a level, since there is then no context to read it from.
            /// </summary>
            public Coord? Origin { get; }

            public BlockLike(
                string label,
                IReadOnlyList<Coord> cells,
                IReadOnlyList<BlockColor> colorStack,
                MovementAxis axis,
                int? unfreezeAtClearCount,
                int? lockId,
                int requiredKeyCount,
                int? keyTargetLockId,
                Coord? origin)
            {
                Label = label;
                Cells = cells;
                ColorStack = colorStack;
                Axis = axis;
                UnfreezeAtClearCount = unfreezeAtClearCount;
                LockId = lockId;
                RequiredKeyCount = requiredKeyCount;
                KeyTargetLockId = keyTargetLockId;
                Origin = origin;
            }
        }

        /// <summary>
        /// Every block the draft can put on the board. <paramref name="ctx"/>
        /// is the draft's level, or null when it does not form one; a
        /// <see cref="LevelDraft.ToContext"/> keeps the draft's generator and
        /// queue order, so draft generator <c>g</c> entry <c>i</c> is the
        /// context's too.
        /// </summary>
        private static IEnumerable<BlockLike> EnumerateBlockLikes(LevelDraft draft, LevelContext ctx)
        {
            foreach (var b in draft.Blocks)
            {
                yield return new BlockLike(
                    $"Block {b.Id}", b.Cells, b.ColorStack, b.Axis, b.UnfreezeAtClearCount,
                    b.LockId, b.RequiredKeyCount, b.KeyTargetLockId, b.StartOrigin);
            }

            for (var gi = 0; gi < draft.Generators.Count; gi++)
            {
                var g = draft.Generators[gi];
                for (var i = 0; i < g.Queue.Count; i++)
                {
                    var s = g.Queue[i];
                    yield return new BlockLike(
                        $"Generator {g.Id} queue entry {i}", s.Cells, s.ColorStack, s.Axis,
                        s.UnfreezeAtClearCount, s.LockId, s.RequiredKeyCount, s.KeyTargetLockId,
                        ctx != null ? ctx.GeneratorSpawnOrigin(gi, i) : (Coord?)null);
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
                            s.UnfreezeAtClearCount, s.LockId, s.RequiredKeyCount, s.KeyTargetLockId,
                            s.RegionOrigin.HasValue ? e.Min + s.RegionOrigin.Value : (Coord?)null);
                    }
                }
            }
        }
    }
}
