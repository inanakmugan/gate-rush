using System;
using System.Collections.Generic;
using GateRush.Core;

namespace GateRush.Editor
{
    /// <summary>
    /// The span one edge feature — a gate or a generator — occupies on its
    /// board edge: the half-open run of cells <c>[Offset, Offset + Width)</c>,
    /// carrying the draft object it came from so a caller can select or move it.
    /// </summary>
    /// <remarks>
    /// Deliberately mirrors <c>LevelContext</c>'s own private <c>EdgeFeature</c>,
    /// because M6 states the rule once for all three pairings: "two gates, two
    /// generators, or a gate and a generator on the same edge may sit side by
    /// side, but their spans are disjoint." The editor-side copy exists because
    /// <c>Core</c>'s is reached only by constructing a <see cref="LevelContext"/>,
    /// which a draft under edit usually cannot do.
    /// </remarks>
    public readonly struct EdgeFeatureSpan
    {
        /// <summary>The <see cref="GateDraft"/> or <see cref="GeneratorDraft"/> this span describes.</summary>
        public object Owner { get; }

        public BoardEdge Edge { get; }
        public int Offset { get; }
        public int Width { get; }

        public EdgeFeatureSpan(object owner, BoardEdge edge, int offset, int width)
        {
            Owner = owner;
            Edge = edge;
            Offset = offset;
            Width = width;
        }

        /// <summary>One past the last cell of the edge this feature occupies.</summary>
        public int End => Offset + Width;

        /// <summary>
        /// Whether two features contend for the same edge cells. Features on
        /// different edges never overlap however their offsets compare, so the
        /// edge is part of the test rather than a caller's precondition.
        /// </summary>
        public bool OverlapsOnSameEdge(EdgeFeatureSpan other) =>
            Edge == other.Edge && Offset < other.End && other.Offset < End;
    }

    /// <summary>
    /// Everything the editor needs to know about where gates and generators sit
    /// on their edges: whether a span is free, how long an edge is, and — for
    /// drawing — which lane a marker belongs in when spans do overlap. A pure
    /// function over a draft, no <c>UnityEditor</c> in any signature, so
    /// placement (a click that must not author an overlap), dragging (a move
    /// that must not land on one) and marker drawing all ask the same code
    /// rather than three copies of the same interval arithmetic.
    /// </summary>
    public static class EdgeFeatures
    {
        /// <summary>
        /// The span of a gate or generator draft, or <c>null</c> for anything
        /// else. Lets a caller holding the window's untyped <c>selection</c> ask
        /// "is this an edge feature, and where is it" in one step.
        /// </summary>
        public static EdgeFeatureSpan? Of(object feature)
        {
            switch (feature)
            {
                case GateDraft gate:
                    return new EdgeFeatureSpan(gate, gate.Edge, gate.Offset, gate.Width);
                case GeneratorDraft generator:
                    return new EdgeFeatureSpan(generator, generator.Edge, generator.Offset, generator.Width);
                default:
                    return null;
            }
        }

        /// <summary>
        /// Every edge feature in the draft — gates first, then generators, each
        /// in list order. The order is fixed because it decides lane assignment
        /// in <see cref="Lanes"/>, and a marker that changed lanes between two
        /// repaints of an unchanged draft would flicker.
        /// </summary>
        public static IReadOnlyList<EdgeFeatureSpan> Enumerate(LevelDraft draft)
        {
            var spans = new List<EdgeFeatureSpan>(draft.Gates.Count + draft.Generators.Count);

            foreach (var gate in draft.Gates)
            {
                spans.Add(new EdgeFeatureSpan(gate, gate.Edge, gate.Offset, gate.Width));
            }

            foreach (var generator in draft.Generators)
            {
                spans.Add(new EdgeFeatureSpan(generator, generator.Edge, generator.Offset, generator.Width));
            }

            return spans;
        }

        /// <summary>
        /// How many cells long <paramref name="edge"/> is: the grid's width for
        /// the top and bottom edges, its height for the left and right.
        /// </summary>
        public static int EdgeLength(LevelDraft draft, BoardEdge edge) =>
            edge == BoardEdge.Top || edge == BoardEdge.Bottom ? draft.Width : draft.Height;

        /// <summary>
        /// How far along <paramref name="edge"/> a cell sits: its X for the top
        /// and bottom edges, its Y for the left and right. This is the
        /// coordinate an <c>Offset</c> is measured in, so a click or a drag
        /// converts to one the same way wherever it happens.
        /// </summary>
        public static int AlongEdge(BoardEdge edge, Coord cell) =>
            edge == BoardEdge.Top || edge == BoardEdge.Bottom ? cell.X : cell.Y;

        /// <summary>
        /// The board edge <paramref name="cell"/> is closest to. This is where a
        /// newly placed gate or generator lands, and also what a drag re-asks on
        /// every pointer move, so the two agree by construction and the
        /// corner-proximity tie-break is written once.
        /// </summary>
        /// <remarks>
        /// Ties resolve in a fixed order — bottom, then top, then left,
        /// otherwise right — so a press exactly between two edges always picks
        /// the same one. A cell outside the grid yields a negative distance to
        /// the edge it has passed, and the most negative wins, so dragging past
        /// an edge selects that edge rather than an arbitrary one. That matters
        /// because an edge marker hangs outside the grid, which is where a drag
        /// of one spends most of its time.
        /// </remarks>
        public static BoardEdge NearestEdge(LevelDraft draft, Coord cell)
        {
            var toLeft = cell.X;
            var toRight = draft.Width - 1 - cell.X;
            var toBottom = cell.Y;
            var toTop = draft.Height - 1 - cell.Y;
            var min = Math.Min(Math.Min(toLeft, toRight), Math.Min(toBottom, toTop));

            if (min == toBottom)
            {
                return BoardEdge.Bottom;
            }

            if (min == toTop)
            {
                return BoardEdge.Top;
            }

            return min == toLeft ? BoardEdge.Left : BoardEdge.Right;
        }

        /// <summary>Whether a span of <paramref name="width"/> cells at <paramref name="offset"/> lies wholly within <paramref name="edge"/>.</summary>
        public static bool FitsOnEdge(LevelDraft draft, BoardEdge edge, int offset, int width) =>
            offset >= 0 && offset + width <= EdgeLength(draft, edge);

        /// <summary>
        /// The first existing gate or generator whose span would collide with
        /// <paramref name="width"/> cells at <paramref name="offset"/> on
        /// <paramref name="edge"/>, or <c>null</c> when the span is free.
        /// <paramref name="ignore"/> excludes the feature being moved, without
        /// which a drag would always collide with the span it already occupies.
        /// </summary>
        public static object Blocking(
            LevelDraft draft, BoardEdge edge, int offset, int width, object ignore = null)
        {
            var candidate = new EdgeFeatureSpan(null, edge, offset, width);

            foreach (var existing in Enumerate(draft))
            {
                if (ReferenceEquals(existing.Owner, ignore))
                {
                    continue;
                }

                if (candidate.OverlapsOnSameEdge(existing))
                {
                    return existing.Owner;
                }
            }

            return null;
        }

        /// <summary>
        /// A lane index per feature, parallel to <paramref name="features"/>:
        /// zero for a feature contending with nothing before it, and otherwise
        /// the lowest lane no overlapping earlier feature already holds. Drawing
        /// a marker one lane further out than the grid makes an overlap read as
        /// two markers rather than one covering the other — which matters
        /// because a draft is allowed to hold an overlap (typing a wider
        /// <c>Width</c> into the properties panel authors one) and the designer
        /// has to be able to see what they did.
        /// </summary>
        /// <remarks>
        /// The pairwise scan is quadratic and irrelevant at this size: it is
        /// bounded by how many edge features an author placed, the same argument
        /// <c>LevelContext.ValidateEdgeFeatures</c> makes for its own.
        /// </remarks>
        public static IReadOnlyList<int> Lanes(IReadOnlyList<EdgeFeatureSpan> features)
        {
            var lanes = new int[features.Count];

            for (var i = 0; i < features.Count; i++)
            {
                var taken = new HashSet<int>();
                for (var j = 0; j < i; j++)
                {
                    if (features[i].OverlapsOnSameEdge(features[j]))
                    {
                        taken.Add(lanes[j]);
                    }
                }

                var lane = 0;
                while (taken.Contains(lane))
                {
                    lane++;
                }

                lanes[i] = lane;
            }

            return lanes;
        }
    }
}
