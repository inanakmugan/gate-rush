using System;
using GateRush.Core;
using NUnit.Framework;
using static GateRush.Tests.Fixture;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="ProgressVector"/>: its lexicographic order is a linear
    /// extension of the componentwise order, equal vectors hash equally, and the
    /// vector never decreases across a <see cref="MoveResolver"/> action (the
    /// monotonicity the search's stratum retirement depends on — D6, D32).
    /// </summary>
    /// <remarks>
    /// The spawn-index components of the vector are exercised directly here
    /// through the internal <see cref="BoardState"/> constructor; generators and
    /// elevators cannot appear in a real level until phase 1.13, so the
    /// resolver-driven monotonicity test only moves <c>TotalClearCount</c>.
    /// </remarks>
    public class ProgressVectorTests
    {
        // ----- Direct construction via the internal BoardState ctor -----------

        /// <summary>
        /// Builds a bare state whose only meaningful fields for
        /// <see cref="ProgressVector"/> are the three it reads. Everything else is
        /// empty; this is not a state any resolver would produce.
        /// </summary>
        private static BoardState StateWith(int totalClearCount, int[] generatorIndex, int[] elevatorWaveIndex)
        {
            var generators = generatorIndex ?? Array.Empty<int>();
            var elevators = elevatorWaveIndex ?? Array.Empty<int>();
            var elevatorActive = new bool[elevators.Length];

            return new BoardState(
                origins: Array.Empty<Coord>(),
                clearedColors: Array.Empty<byte>(),
                alive: Array.Empty<bool>(),
                unfrozen: Array.Empty<bool>(),
                unlocked: Array.Empty<bool>(),
                gateOpen: Array.Empty<bool>(),
                shutterOpen: Array.Empty<bool>(),
                generatorIndex: generators,
                elevatorWaveIndex: elevators,
                elevatorWaveActive: elevatorActive,
                totalClearCount: totalClearCount,
                clearCountByColor: Array.Empty<int>(),
                keyConsumed: Array.Empty<bool>());
        }

        [Test]
        public void CompareTo_OrdersByTotalClearCountFirst()
        {
            var low = ProgressVector.Of(StateWith(2, new[] { 9 }, new[] { 9 }));
            var high = ProgressVector.Of(StateWith(3, new[] { 0 }, new[] { 0 }));

            Assert.Less(low.CompareTo(high), 0);
            Assert.Greater(high.CompareTo(low), 0);
        }

        [Test]
        public void CompareTo_BreaksTiesLexicographicallyBySpawnIndex()
        {
            var a = ProgressVector.Of(StateWith(5, new[] { 1, 0 }, new[] { 4 }));
            var b = ProgressVector.Of(StateWith(5, new[] { 1, 1 }, new[] { 0 }));

            // Equal clear count; generator[1] is 0 vs 1, so a precedes b even
            // though its elevator index is larger.
            Assert.Less(a.CompareTo(b), 0);
        }

        [Test]
        public void CompareTo_IsALinearExtensionOfTheComponentwiseOrder()
        {
            var lower = ProgressVector.Of(StateWith(4, new[] { 1, 2 }, new[] { 0 }));
            var higher = ProgressVector.Of(StateWith(4, new[] { 1, 3 }, new[] { 0 }));

            // lower <= higher componentwise and not equal, so it must compare less.
            Assert.Less(lower.CompareTo(higher), 0);
        }

        [Test]
        public void Equals_And_GetHashCode_AgreeForEqualVectors()
        {
            var one = ProgressVector.Of(StateWith(7, new[] { 2, 1 }, new[] { 3 }));
            var same = ProgressVector.Of(StateWith(7, new[] { 2, 1 }, new[] { 3 }));

            Assert.IsTrue(one.Equals(same));
            Assert.AreEqual(one.GetHashCode(), same.GetHashCode());
            Assert.AreEqual(0, one.CompareTo(same));
        }

        [Test]
        public void Of_NoGeneratorsOrElevators_ComparesByClearCountAlone()
        {
            var two = ProgressVector.Of(StateWith(2, null, null));
            var three = ProgressVector.Of(StateWith(3, null, null));

            Assert.Less(two.CompareTo(three), 0);
            Assert.IsTrue(two != three);
        }

        [Test]
        public void Of_NullState_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => ProgressVector.Of(null));
        }

        // ----- Monotonicity across a real resolver action --------------------

        [Test]
        public void ProgressVector_DoesNotDecreaseAcrossANonClearingMove()
        {
            var ctx = Ctx(4, 1, new[] { Block(1, new Coord(0, 0)) }, Array.Empty<GateDefinition>());
            var before = BoardState.CreateInitial(ctx);

            new MoveResolver().TryApplyMove(ctx, before, new Move(0, new Coord(2, 0)), out var after);

            Assert.GreaterOrEqual(after.ProgressVector.CompareTo(before.ProgressVector), 0);
            Assert.AreEqual(0, after.ProgressVector.CompareTo(before.ProgressVector),
                "a plain reposition must not advance the progress vector");
        }

        [Test]
        public void ProgressVector_IncreasesAcrossAClearingMove()
        {
            var ctx = Ctx(
                5, 5,
                new[] { Block(1, new Coord(2, 0)) },
                new[] { Gate(1, BoardEdge.Bottom, 2, 1, BlockColor.Red) });
            var before = BoardState.CreateInitial(ctx);

            new MoveResolver().TryApplyMove(ctx, before, new Move(0, new Coord(2, 0)), out var after);

            Assert.Greater(after.ProgressVector.CompareTo(before.ProgressVector), 0);
        }
    }
}
