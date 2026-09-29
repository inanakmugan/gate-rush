using System;
using System.Linq;
using GateRush.Core;
using GateRush.Runtime;
using NUnit.Framework;
using UnityEngine;
using static GateRush.Tests.Fixture;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="GeneratorMachine"/> (Module 16, D48): a machine sits
    /// outside the frame on the edge its generator feeds, centred on its span
    /// and reaching back under the frame; its screen stays clear of the frame
    /// and the badge; and the camera's reach counts it only on the edges that
    /// hold a generator.
    /// </summary>
    public class GeneratorMachineTests
    {
        private const int BoardWidth = 4;
        private const int BoardHeight = 3;
        private const float Frame = 0.4f;
        private const float Depth = 1f;
        private const float Overhang = 0.1f;
        private const float Overlap = 0.2f;
        private const float Inset = 0.1f;
        private const float BadgeHeight = 0.3f;
        private const float DigitWidth = 0.15f;
        private const float Padding = 0.05f;
        private const float Tolerance = 1e-4f;

        private static GeneratorMachine Machine() =>
            new GeneratorMachine(Frame, Depth, Overhang, Overlap, Inset, BadgeHeight, DigitWidth, Padding);

        /// <summary>A two-wide generator at offset 1 of <paramref name="edge"/>, with one block queued.</summary>
        private static GeneratorDefinition TwoWide(BoardEdge edge) => Spawner(1, edge, 1, 2, Spawned());

        [TestCase(BoardEdge.Bottom, 0.9f, -1.2f, 3.1f, -0.2f)]
        [TestCase(BoardEdge.Top, 0.9f, 3.2f, 3.1f, 4.2f)]
        [TestCase(BoardEdge.Left, -1.2f, 0.9f, -0.2f, 3.1f)]
        [TestCase(BoardEdge.Right, 4.2f, 0.9f, 5.2f, 3.1f)]
        public void Place_OnEachEdge_PutsTheBodyOutsideTheFrameCentredOnTheSpanAndUnderTheFrameByTheOverlap(
            BoardEdge edge, float xMin, float yMin, float xMax, float yMax)
        {
            var placement = Machine().Place(BoardWidth, BoardHeight, TwoWide(edge));

            AssertRect(Rect.MinMaxRect(xMin, yMin, xMax, yMax), placement.Body);
        }

        [Test]
        public void Place_OnTheBottom_PutsTheBadgeOnTheMiddleOfTheBodysOuterRim()
        {
            var placement = Machine().Place(BoardWidth, BoardHeight, TwoWide(BoardEdge.Bottom));

            Assert.AreEqual(2f, placement.BadgeCenter.x, Tolerance, "the middle of the span");
            Assert.AreEqual(placement.Body.yMin, placement.BadgeCenter.y, Tolerance, "the outer rim");
        }

        [Test]
        public void Place_OnTheBottom_KeepsTheScreenInsideTheBodyAndClearOfTheFrameAndTheBadge()
        {
            var placement = Machine().Place(BoardWidth, BoardHeight, TwoWide(BoardEdge.Bottom));
            var screen = placement.Screen;

            Assert.AreEqual(-Frame - Inset, screen.yMax, Tolerance, "starts an inset beyond the frame");
            Assert.AreEqual(placement.Body.yMin + Inset + BadgeHeight / 2f, screen.yMin, Tolerance, "stops short of the badge");
            Assert.AreEqual(placement.Body.xMin + Inset, screen.xMin, Tolerance);
            Assert.AreEqual(placement.Body.xMax - Inset, screen.xMax, Tolerance);
        }

        [Test]
        public void Reach_GeneratorOnTheLeftOnly_ReachesItsRimAndHalfItsBadgeOnTheLeftAndNothingElsewhere()
        {
            var ctx = Ctx(3, 3, generators: new[] { Spawner(1, BoardEdge.Left, 0, 1, Spawned()) });

            var reach = Machine().Reach(ctx);

            // One digit: a square badge, BadgeHeight wide.
            Assert.AreEqual(Depth - Overlap + BadgeHeight / 2f, reach.Left, Tolerance);
            Assert.AreEqual(0f, reach.Right);
            Assert.AreEqual(0f, reach.Bottom);
            Assert.AreEqual(0f, reach.Top);
        }

        [Test]
        public void Reach_OnASide_CountsTheWidestBadgeTheGeneratorWillShow()
        {
            // Twelve queued: the badge starts two digits wide, the widest it
            // will be, and only narrows as blocks spawn.
            var queue = Enumerable.Range(0, 12).Select(_ => Spawned()).ToArray();
            var ctx = Ctx(3, 3, generators: new[] { Spawner(1, BoardEdge.Right, 0, 1, queue) });

            var reach = Machine().Reach(ctx);

            var twoDigitWidth = 2f * DigitWidth + 2f * Padding;
            Assert.AreEqual(Depth - Overlap + twoDigitWidth / 2f, reach.Right, Tolerance);
        }

        [Test]
        public void Problems_OverlapNotBelowTheDepth_NamesTheFieldAndTheMachineRefusesIt()
        {
            var problems = GeneratorMachine.Problems(Depth, Overhang, Depth, Inset);

            Assert.That(problems, Has.Some.StartsWith("Machine Frame Overlap Cells"), string.Join(" | ", problems));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new GeneratorMachine(Frame, Depth, Overhang, Depth, Inset, BadgeHeight, DigitWidth, Padding));
        }

        [Test]
        public void Problems_DefaultLikeValues_AreEmpty()
        {
            var problems = GeneratorMachine.Problems(Depth, Overhang, Overlap, Inset);

            Assert.IsEmpty(problems);
        }

        private static void AssertRect(Rect expected, Rect actual)
        {
            Assert.AreEqual(expected.xMin, actual.xMin, Tolerance, "xMin");
            Assert.AreEqual(expected.yMin, actual.yMin, Tolerance, "yMin");
            Assert.AreEqual(expected.xMax, actual.xMax, Tolerance, "xMax");
            Assert.AreEqual(expected.yMax, actual.yMax, Tolerance, "yMax");
        }
    }
}
