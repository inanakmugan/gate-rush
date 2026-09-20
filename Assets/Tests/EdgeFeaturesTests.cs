using System.Linq;
using GateRush.Core;
using GateRush.Editor;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="EdgeFeatures"/> (docs/Modules/09a): the span arithmetic
    /// shared by placing a gate or generator, dragging one, and drawing both.
    /// M6 governs gates and generators identically — "two gates, two generators,
    /// or a gate and a generator on the same edge may sit side by side, but their
    /// spans are disjoint" — so the tests below deliberately mix the two kinds
    /// rather than covering each separately.
    /// </summary>
    public class EdgeFeaturesTests
    {
        private static LevelDraft Draft()
        {
            var draft = LevelDraft.NewEmpty(6, 4);
            draft.Gates.Add(new GateDraft
            {
                Id = 1, Edge = BoardEdge.Bottom, Offset = 0, Width = 2, Color = BlockColor.Red,
            });
            draft.Generators.Add(new GeneratorDraft { Id = 1, Edge = BoardEdge.Bottom, Offset = 4, Width = 1 });
            draft.Generators.Add(new GeneratorDraft { Id = 2, Edge = BoardEdge.Left, Offset = 0, Width = 2 });
            return draft;
        }

        private static EdgeFeatureSpan Span(BoardEdge edge, int offset, int width) =>
            new EdgeFeatureSpan(null, edge, offset, width);

        // -- Of --------------------------------------------------------

        [Test]
        public void Of_AGateAndAGenerator_ReadTheSameThreeFields()
        {
            var draft = Draft();

            var gate = EdgeFeatures.Of(draft.Gates[0]).Value;
            var generator = EdgeFeatures.Of(draft.Generators[0]).Value;

            Assert.AreEqual(BoardEdge.Bottom, gate.Edge);
            Assert.AreEqual(0, gate.Offset);
            Assert.AreEqual(2, gate.Width);
            Assert.AreEqual(4, generator.Offset);
            Assert.AreEqual(1, generator.Width);
        }

        [Test]
        public void Of_SomethingThatIsNotAnEdgeFeature_IsNull()
        {
            Assert.IsNull(EdgeFeatures.Of(new ShutterDraft { Id = 1, Threshold = 1 }));
            Assert.IsNull(EdgeFeatures.Of(null));
        }

        // -- Enumerate -------------------------------------------------

        [Test]
        public void Enumerate_GatesComeFirstThenGeneratorsInListOrder()
        {
            // The order decides lane assignment, and a marker that changed lanes
            // between two repaints of an unchanged draft would flicker.
            var spans = EdgeFeatures.Enumerate(Draft());

            CollectionAssert.AreEqual(
                new[] { "Gate", "Generator", "Generator" },
                spans.Select(s => s.Owner is GateDraft ? "Gate" : "Generator").ToList());
            CollectionAssert.AreEqual(new[] { 0, 4, 0 }, spans.Select(s => s.Offset).ToList());
        }

        // -- geometry --------------------------------------------------

        [Test]
        public void EdgeLength_IsTheGridWidthAcrossAndTheHeightUpTheSides()
        {
            var draft = Draft();

            Assert.AreEqual(6, EdgeFeatures.EdgeLength(draft, BoardEdge.Top));
            Assert.AreEqual(6, EdgeFeatures.EdgeLength(draft, BoardEdge.Bottom));
            Assert.AreEqual(4, EdgeFeatures.EdgeLength(draft, BoardEdge.Left));
            Assert.AreEqual(4, EdgeFeatures.EdgeLength(draft, BoardEdge.Right));
        }

        [Test]
        public void AlongEdge_IsXAcrossAndYUpTheSides()
        {
            Assert.AreEqual(3, EdgeFeatures.AlongEdge(BoardEdge.Bottom, new Coord(3, 1)));
            Assert.AreEqual(3, EdgeFeatures.AlongEdge(BoardEdge.Top, new Coord(3, 1)));
            Assert.AreEqual(1, EdgeFeatures.AlongEdge(BoardEdge.Left, new Coord(3, 1)));
            Assert.AreEqual(1, EdgeFeatures.AlongEdge(BoardEdge.Right, new Coord(3, 1)));
        }

        [Test]
        public void FitsOnEdge_SpanRunningPastEitherEndDoesNot()
        {
            var draft = Draft();

            Assert.IsTrue(EdgeFeatures.FitsOnEdge(draft, BoardEdge.Bottom, 4, 2));
            Assert.IsFalse(EdgeFeatures.FitsOnEdge(draft, BoardEdge.Bottom, 5, 2));
            Assert.IsFalse(EdgeFeatures.FitsOnEdge(draft, BoardEdge.Bottom, -1, 2));
            Assert.IsFalse(EdgeFeatures.FitsOnEdge(draft, BoardEdge.Left, 3, 2));
        }

        // -- OverlapsOnSameEdge ---------------------------------------

        [Test]
        public void OverlapsOnSameEdge_AdjacentSpansSitSideBySideWithoutOverlapping()
        {
            Assert.IsFalse(Span(BoardEdge.Bottom, 0, 2).OverlapsOnSameEdge(Span(BoardEdge.Bottom, 2, 2)));
        }

        [Test]
        public void OverlapsOnSameEdge_SpansSharingACell_Overlap()
        {
            Assert.IsTrue(Span(BoardEdge.Bottom, 0, 3).OverlapsOnSameEdge(Span(BoardEdge.Bottom, 2, 2)));
        }

        [Test]
        public void OverlapsOnSameEdge_IdenticalOffsetsOnDifferentEdges_DoNotOverlap()
        {
            Assert.IsFalse(Span(BoardEdge.Bottom, 0, 2).OverlapsOnSameEdge(Span(BoardEdge.Left, 0, 2)));
        }

        // -- Blocking --------------------------------------------------

        [Test]
        public void Blocking_FreeSpan_IsNull()
        {
            var draft = Draft();

            Assert.IsNull(EdgeFeatures.Blocking(draft, BoardEdge.Bottom, 2, 2));
        }

        [Test]
        public void Blocking_SpanOverlappingAGate_ReturnsThatGate()
        {
            var draft = Draft();

            Assert.AreSame(draft.Gates[0], EdgeFeatures.Blocking(draft, BoardEdge.Bottom, 1, 1));
        }

        [Test]
        public void Blocking_SpanOverlappingAGenerator_ReturnsThatGenerator()
        {
            var draft = Draft();

            Assert.AreSame(draft.Generators[0], EdgeFeatures.Blocking(draft, BoardEdge.Bottom, 3, 2));
        }

        [Test]
        public void Blocking_TheFeatureBeingMovedIsIgnored_SoItDoesNotBlockItself()
        {
            var draft = Draft();

            Assert.AreSame(draft.Gates[0], EdgeFeatures.Blocking(draft, BoardEdge.Bottom, 0, 2));
            Assert.IsNull(EdgeFeatures.Blocking(draft, BoardEdge.Bottom, 0, 2, draft.Gates[0]));
        }

        // -- NearestEdge -----------------------------------------------

        [Test]
        public void NearestEdge_CellNearestEachEdge_PicksThatEdge()
        {
            var draft = Draft(); // 6 wide, 4 tall

            Assert.AreEqual(BoardEdge.Left, EdgeFeatures.NearestEdge(draft, new Coord(0, 2)));
            Assert.AreEqual(BoardEdge.Right, EdgeFeatures.NearestEdge(draft, new Coord(5, 2)));
            Assert.AreEqual(BoardEdge.Bottom, EdgeFeatures.NearestEdge(draft, new Coord(3, 0)));
            Assert.AreEqual(BoardEdge.Top, EdgeFeatures.NearestEdge(draft, new Coord(3, 3)));
        }

        [Test]
        public void NearestEdge_CornerCells_ResolveInTheFixedOrder()
        {
            // Bottom first, then top, then left, otherwise right — so a press
            // equidistant from two edges always picks the same one, and placing
            // a feature and dragging one there agree.
            var draft = Draft();

            Assert.AreEqual(BoardEdge.Bottom, EdgeFeatures.NearestEdge(draft, new Coord(0, 0)));
            Assert.AreEqual(BoardEdge.Bottom, EdgeFeatures.NearestEdge(draft, new Coord(5, 0)));
            Assert.AreEqual(BoardEdge.Top, EdgeFeatures.NearestEdge(draft, new Coord(0, 3)));
            Assert.AreEqual(BoardEdge.Top, EdgeFeatures.NearestEdge(draft, new Coord(5, 3)));
        }

        [Test]
        public void NearestEdge_PointerOutsideTheGrid_PicksTheEdgeItHasPassed()
        {
            // A drag of an edge feature spends most of its time out here, since
            // the marker hangs outside the grid. The distance to a passed edge
            // goes negative and the most negative wins, so the answer is the
            // edge the pointer actually left by.
            var draft = Draft();

            Assert.AreEqual(BoardEdge.Left, EdgeFeatures.NearestEdge(draft, new Coord(-2, 2)));
            Assert.AreEqual(BoardEdge.Right, EdgeFeatures.NearestEdge(draft, new Coord(9, 2)));
            Assert.AreEqual(BoardEdge.Bottom, EdgeFeatures.NearestEdge(draft, new Coord(3, -3)));
            Assert.AreEqual(BoardEdge.Top, EdgeFeatures.NearestEdge(draft, new Coord(3, 8)));
        }

        // -- Lanes -----------------------------------------------------

        [Test]
        public void Lanes_DisjointSpans_AllSitInLaneZero()
        {
            var lanes = EdgeFeatures.Lanes(EdgeFeatures.Enumerate(Draft()));

            CollectionAssert.AreEqual(new[] { 0, 0, 0 }, lanes.ToList());
        }

        [Test]
        public void Lanes_AWidenedFeatureCoveringItsNeighbour_MovesToTheNextLane()
        {
            // A draft is allowed to hold this: typing Width 6 into the properties
            // panel authors it, and Core rejects it only once ToContext runs.
            var draft = Draft();
            draft.Gates[0].Width = 6;

            var lanes = EdgeFeatures.Lanes(EdgeFeatures.Enumerate(draft));

            CollectionAssert.AreEqual(new[] { 0, 1, 0 }, lanes.ToList());
        }

        [Test]
        public void Lanes_ThreeMutuallyOverlappingSpans_TakeThreeDistinctLanes()
        {
            var spans = new[]
            {
                Span(BoardEdge.Top, 0, 3),
                Span(BoardEdge.Top, 1, 3),
                Span(BoardEdge.Top, 2, 3),
            };

            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, EdgeFeatures.Lanes(spans).ToList());
        }

        [Test]
        public void Lanes_ASpanOverlappingOnlyTheFirstOfTwo_ReusesTheFreedLane()
        {
            // Lane 0 is taken by the first span, lane 1 by the second. The third
            // clears the first entirely, so lane 0 is free for it again — a lane
            // is a drawing slot, not a running count of features.
            var spans = new[]
            {
                Span(BoardEdge.Top, 0, 2),
                Span(BoardEdge.Top, 1, 2),
                Span(BoardEdge.Top, 2, 2),
            };

            CollectionAssert.AreEqual(new[] { 0, 1, 0 }, EdgeFeatures.Lanes(spans).ToList());
        }

        [Test]
        public void Lanes_OverlappingSpansOnDifferentEdges_BothStayInLaneZero()
        {
            var spans = new[]
            {
                Span(BoardEdge.Top, 0, 2),
                Span(BoardEdge.Bottom, 0, 2),
            };

            CollectionAssert.AreEqual(new[] { 0, 0 }, EdgeFeatures.Lanes(spans).ToList());
        }
    }
}
