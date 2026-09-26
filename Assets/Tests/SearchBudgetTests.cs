using System.Threading;
using GateRush.Solver;
using NUnit.Framework;

namespace GateRush.Tests
{
    /// <summary>
    /// Covers <see cref="SearchBudget"/>'s cancellation: a budget never cancels
    /// unless given a token, and <see cref="SearchBudget.WithCancellation"/>
    /// attaches one without touching any limit.
    /// </summary>
    public class SearchBudgetTests
    {
        [Test]
        public void Cancellation_NotGiven_IsNone()
        {
            var budget = new SearchBudget(10, 100, 1_000, MoveGenMode.Exhaustive);

            Assert.AreEqual(CancellationToken.None, budget.Cancellation);
        }

        [Test]
        public void WithCancellation_KeepsEveryLimitAndCarriesTheToken()
        {
            var budget = new SearchBudget(10, 100, 1_000, MoveGenMode.Canonical);
            using (var source = new CancellationTokenSource())
            {
                var cancellable = budget.WithCancellation(source.Token);

                Assert.AreEqual(10, cancellable.MaxDepth);
                Assert.AreEqual(100, cancellable.MaxExploredStates);
                Assert.AreEqual(1_000, cancellable.MaxWallClockMs);
                Assert.AreEqual(MoveGenMode.Canonical, cancellable.Mode);
                Assert.AreEqual(source.Token, cancellable.Cancellation);
            }
        }
    }
}
