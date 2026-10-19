using System;
using System.Collections.Generic;

namespace GateRush.Platform
{
    /// <summary>
    /// Sells products for real money (Module 36, D72). The one seam between
    /// the Store and whatever takes the payment: a simulated sheet that
    /// charges nothing in this build, a platform store in a later one.
    /// Nothing above this interface knows which.
    /// </summary>
    /// <remarks>
    /// The service only reports the outcome; what a product gives is the
    /// rules' question, and the caller saves.
    /// </remarks>
    public interface IPurchaseService
    {
        /// <summary>The products on sale, in the service's order.</summary>
        IReadOnlyList<StoreProduct> Products { get; }

        /// <summary>True while a purchase is waiting for its outcome.</summary>
        bool IsBusy { get; }

        /// <summary>
        /// Buys <paramref name="productId"/> and calls <paramref name="done"/>
        /// exactly once with the outcome, now or later. An id the service does
        /// not sell gives <see cref="PurchaseOutcome.Failed"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="productId"/> or <paramref name="done"/> is null.</exception>
        /// <exception cref="InvalidOperationException"><see cref="IsBusy"/> is true; the caller checks it first.</exception>
        void Purchase(string productId, Action<PurchaseOutcome> done);
    }
}
