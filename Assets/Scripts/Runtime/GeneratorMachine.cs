using System;
using System.Collections.Generic;
using GateRush.Core;
using UnityEngine;

namespace GateRush.Runtime
{
    /// <summary>
    /// Where a generator's machine is drawn (M6, D48): a body outside the frame
    /// on the edge it feeds, centred on its span, a screen on the body showing
    /// the next block, and the badge with the queued count centred on the
    /// body's outer rim. The one placement rule for <see cref="BoardView"/>,
    /// which draws the machine, and for the camera fit, which keeps room for it
    /// through <see cref="Reach"/>.
    /// </summary>
    /// <remarks>
    /// <para>All rectangles are in grid units: cell <c>(x, y)</c> covers
    /// <c>[x, x + 1) × [y, y + 1)</c>, and the frame runs its thickness beyond
    /// the grid on every side. The body sits behind the frame where they
    /// overlap, so the screen starts at the frame's outer boundary.</para>
    /// <para>The badge's extent across the edge is fixed per generator from
    /// its starting queue length — the widest number it will ever show, since
    /// the count only falls — so neither the screen nor the camera moves as
    /// blocks spawn. <see cref="Problems"/> is the single statement of the
    /// valid sizes; the constructor throws on what it reports.</para>
    /// </remarks>
    public sealed class GeneratorMachine
    {
        private const string DepthMessage = "Machine Depth Cells must be positive.";
        private const string OverlapMessage = "Machine Frame Overlap Cells must be at least 0 and below Machine Depth Cells.";
        private const string OverhangMessage = "Machine Side Overhang Cells may not be negative.";
        private const string InsetMessage = "Machine Screen Inset Cells may not be negative.";

        private readonly float frameThicknessCells;
        private readonly float depthCells;
        private readonly float sideOverhangCells;
        private readonly float frameOverlapCells;
        private readonly float screenInsetCells;
        private readonly float badgeHeightCells;
        private readonly float badgeDigitWidthCells;
        private readonly float badgePaddingCells;

        /// <summary>A machine rule with every size in cells.</summary>
        /// <param name="frameThicknessCells">The frame's thickness beyond the grid.</param>
        /// <param name="depthCells">How deep the body is, across the edge.</param>
        /// <param name="sideOverhangCells">How far the body runs past the span at each end.</param>
        /// <param name="frameOverlapCells">How far the body's inner side reaches back under the frame; below the depth.</param>
        /// <param name="screenInsetCells">The margin between the body's rim and the screen.</param>
        /// <param name="badgeHeightCells">The badge's height.</param>
        /// <param name="badgeDigitWidthCells">The width each digit adds to the badge.</param>
        /// <param name="badgePaddingCells">The badge's padding either side of its digits.</param>
        /// <exception cref="ArgumentOutOfRangeException">A machine size is one <see cref="Problems"/> reports.</exception>
        public GeneratorMachine(
            float frameThicknessCells, float depthCells, float sideOverhangCells, float frameOverlapCells,
            float screenInsetCells, float badgeHeightCells, float badgeDigitWidthCells, float badgePaddingCells)
        {
            var problems = Problems(depthCells, sideOverhangCells, frameOverlapCells, screenInsetCells);
            if (problems.Count > 0)
            {
                throw new ArgumentOutOfRangeException(nameof(depthCells), string.Join(" ", problems));
            }

            this.frameThicknessCells = frameThicknessCells;
            this.depthCells = depthCells;
            this.sideOverhangCells = sideOverhangCells;
            this.frameOverlapCells = frameOverlapCells;
            this.screenInsetCells = screenInsetCells;
            this.badgeHeightCells = badgeHeightCells;
            this.badgeDigitWidthCells = badgeDigitWidthCells;
            this.badgePaddingCells = badgePaddingCells;
        }

        /// <summary>
        /// Every reason these machine sizes cannot place a machine, one message
        /// per field, named as the config's inspector shows it; empty when valid.
        /// </summary>
        public static IReadOnlyList<string> Problems(
            float depthCells, float sideOverhangCells, float frameOverlapCells, float screenInsetCells)
        {
            var problems = new List<string>();

            if (!(depthCells > 0f) || float.IsInfinity(depthCells))
            {
                problems.Add(DepthMessage);
            }

            if (!(frameOverlapCells >= 0f && frameOverlapCells < depthCells))
            {
                problems.Add(OverlapMessage);
            }

            if (!(sideOverhangCells >= 0f) || float.IsInfinity(sideOverhangCells))
            {
                problems.Add(OverhangMessage);
            }

            if (!(screenInsetCells >= 0f) || float.IsInfinity(screenInsetCells))
            {
                problems.Add(InsetMessage);
            }

            return problems;
        }

        /// <summary>
        /// How far every machine on <paramref name="ctx"/>'s board reaches beyond
        /// the frame on each side: its outer rim plus its badge's half-extent
        /// across the edge, on every edge that holds a generator; nothing on the
        /// others. Fixed for the level.
        /// </summary>
        public EdgeReach Reach(LevelContext ctx)
        {
            var left = 0f;
            var right = 0f;
            var bottom = 0f;
            var top = 0f;
            for (var g = 0; g < ctx.Generators.Count; g++)
            {
                var generator = ctx.Generators[g];
                var reach = depthCells - frameOverlapCells + BadgeHalfAcross(generator);
                switch (generator.Edge)
                {
                    case BoardEdge.Left:
                        left = Math.Max(left, reach);
                        break;
                    case BoardEdge.Right:
                        right = Math.Max(right, reach);
                        break;
                    case BoardEdge.Bottom:
                        bottom = Math.Max(bottom, reach);
                        break;
                    default:
                        top = Math.Max(top, reach);
                        break;
                }
            }

            return new EdgeReach(left, right, bottom, top);
        }

        /// <summary>
        /// Where generator <paramref name="generator"/>'s machine goes on a
        /// <paramref name="width"/> x <paramref name="height"/> board.
        /// </summary>
        public MachinePlacement Place(int width, int height, GeneratorDefinition generator)
        {
            // Work in edge coordinates — "along" the edge and "out" from the
            // board — then map them to the grid once.
            var alongMin = generator.Offset - sideOverhangCells;
            var alongMax = generator.Offset + generator.Width + sideOverhangCells;
            var outMin = -frameOverlapCells;
            var outMax = depthCells - frameOverlapCells;
            var badgeHalf = BadgeHalfAcross(generator);

            var screenAlongMin = alongMin + screenInsetCells;
            var screenAlongMax = Math.Max(screenAlongMin, alongMax - screenInsetCells);
            var screenOutMin = screenInsetCells;
            var screenOutMax = Math.Max(screenOutMin, outMax - screenInsetCells - badgeHalf);

            var body = ToGrid(width, height, generator.Edge, alongMin, alongMax, outMin, outMax);
            var screen = ToGrid(width, height, generator.Edge, screenAlongMin, screenAlongMax, screenOutMin, screenOutMax);
            var rim = ToGrid(width, height, generator.Edge, alongMin, alongMax, outMax, outMax);
            return new MachinePlacement(body, screen, rim.center);
        }

        /// <summary>Half the badge's extent across the edge: half its height on the top or bottom, half its widest width on a side.</summary>
        private float BadgeHalfAcross(GeneratorDefinition generator)
        {
            if (generator.Edge == BoardEdge.Top || generator.Edge == BoardEdge.Bottom)
            {
                return badgeHeightCells * 0.5f;
            }

            return MarkLayout.BadgeWidth(generator.Queue.Count, badgeHeightCells, badgeDigitWidthCells, badgePaddingCells) * 0.5f;
        }

        /// <summary>
        /// A rectangle given along the edge and outward from the frame's outer
        /// boundary, in grid units.
        /// </summary>
        private Rect ToGrid(int width, int height, BoardEdge edge, float alongMin, float alongMax, float outMin, float outMax)
        {
            switch (edge)
            {
                case BoardEdge.Bottom:
                    return Rect.MinMaxRect(alongMin, -frameThicknessCells - outMax, alongMax, -frameThicknessCells - outMin);
                case BoardEdge.Top:
                    return Rect.MinMaxRect(alongMin, height + frameThicknessCells + outMin, alongMax, height + frameThicknessCells + outMax);
                case BoardEdge.Left:
                    return Rect.MinMaxRect(-frameThicknessCells - outMax, alongMin, -frameThicknessCells - outMin, alongMax);
                default:
                    return Rect.MinMaxRect(width + frameThicknessCells + outMin, alongMin, width + frameThicknessCells + outMax, alongMax);
            }
        }
    }

    /// <summary>One generator machine's parts, in grid units.</summary>
    public readonly struct MachinePlacement
    {
        /// <summary>A placement.</summary>
        public MachinePlacement(Rect body, Rect screen, Vector2 badgeCenter)
        {
            Body = body;
            Screen = screen;
            BadgeCenter = badgeCenter;
        }

        /// <summary>The machine's body, outside the frame and reaching back under it.</summary>
        public Rect Body { get; }

        /// <summary>The screen the next block is shown on, inside the body.</summary>
        public Rect Screen { get; }

        /// <summary>The centre of the queued-count badge: the middle of the body's outer rim.</summary>
        public Vector2 BadgeCenter { get; }
    }
}
