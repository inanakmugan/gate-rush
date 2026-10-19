using System;
using System.Collections.Generic;
using GateRush.Platform;

namespace GateRush.Runtime
{
    /// <summary>
    /// The purchase service of this build (Module 36, D72): it charges
    /// nothing. Every purchase is put to a confirm delegate — the bootstrap
    /// passes it to the front end's test purchase sheet, which says that the
    /// store is simulated — and the player's answer is the outcome: yes is
    /// <see cref="PurchaseOutcome.Succeeded"/>, no is
    /// <see cref="PurchaseOutcome.Cancelled"/>.
    /// </summary>
    /// <remarks>
    /// <para>Plain C#, so the bootstrap hands it a delegate rather than a
    /// screen: <c>Runtime</c> never references <c>GateRush.UI</c>.</para>
    /// <para>The answer given to the delegate takes effect once; a second call
    /// is ignored. <see cref="IsBusy"/> is cleared before <c>done</c> runs, so
    /// <c>done</c> may start another purchase. Everything runs on the caller's
    /// thread (WebGL has no other).</para>
    /// </remarks>
    public sealed class SimulatedPurchaseService : IPurchaseService
    {
        private readonly Action<StoreProduct, Action<bool>> confirm;

        /// <summary>
        /// A service selling <paramref name="products"/>, copied, that asks
        /// <paramref name="confirm"/> to confirm each purchase and answer yes
        /// or no.
        /// </summary>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="products"/> has a problem <see cref="ProblemsOf"/> reports.</exception>
        public SimulatedPurchaseService(IReadOnlyList<StoreProduct> products, Action<StoreProduct, Action<bool>> confirm)
        {
            if (products == null)
            {
                throw new ArgumentNullException(nameof(products));
            }

            this.confirm = confirm ?? throw new ArgumentNullException(nameof(confirm));

            var problems = ProblemsOf(products);
            if (problems.Count > 0)
            {
                throw new ArgumentException($"The simulated products are invalid: {problems[0]}", nameof(products));
            }

            var copy = new StoreProduct[products.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = products[i];
            }

            Products = Array.AsReadOnly(copy);
        }

        /// <inheritdoc />
        public IReadOnlyList<StoreProduct> Products { get; }

        /// <inheritdoc />
        public bool IsBusy { get; private set; }

        /// <summary>
        /// Everything wrong with <paramref name="products"/>, one sentence per
        /// problem, each starting with the element's index: a blank id, an id
        /// already used by an earlier element, or a blank price text. Empty
        /// when the list is valid; an empty list sells nothing. The one check:
        /// the constructor throws on it, and the configuration asset reports
        /// it.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="products"/> is null.</exception>
        public static IReadOnlyList<string> ProblemsOf(IReadOnlyList<StoreProduct> products)
        {
            if (products == null)
            {
                throw new ArgumentNullException(nameof(products));
            }

            var problems = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < products.Count; i++)
            {
                var product = products[i];
                if (string.IsNullOrWhiteSpace(product.Id))
                {
                    problems.Add($"element {i}: Product Id is empty.");
                }
                else if (!seen.Add(product.Id))
                {
                    problems.Add($"element {i}: Product Id '{product.Id}' is used by an earlier element.");
                }

                if (string.IsNullOrWhiteSpace(product.PriceText))
                {
                    problems.Add($"element {i}: Price Text is empty.");
                }
            }

            return problems;
        }

        /// <inheritdoc />
        public void Purchase(string productId, Action<PurchaseOutcome> done)
        {
            if (productId == null)
            {
                throw new ArgumentNullException(nameof(productId));
            }

            if (done == null)
            {
                throw new ArgumentNullException(nameof(done));
            }

            if (IsBusy)
            {
                throw new InvalidOperationException(
                    $"A purchase was started for '{productId}' while another waits for its outcome; check IsBusy first.");
            }

            if (!TryFind(productId, out var product))
            {
                done(PurchaseOutcome.Failed);
                return;
            }

            IsBusy = true;
            var isAnswered = false;
            void Answer(bool isConfirmed)
            {
                if (isAnswered)
                {
                    return;
                }

                isAnswered = true;
                IsBusy = false;
                done(isConfirmed ? PurchaseOutcome.Succeeded : PurchaseOutcome.Cancelled);
            }

            try
            {
                confirm(product, Answer);
            }
            catch
            {
                // A delegate that throws before answering would leave the
                // service busy for good.
                if (!isAnswered)
                {
                    isAnswered = true;
                    IsBusy = false;
                }

                throw;
            }
        }

        private bool TryFind(string productId, out StoreProduct product)
        {
            for (var i = 0; i < Products.Count; i++)
            {
                if (string.Equals(Products[i].Id, productId, StringComparison.Ordinal))
                {
                    product = Products[i];
                    return true;
                }
            }

            product = default;
            return false;
        }
    }
}
