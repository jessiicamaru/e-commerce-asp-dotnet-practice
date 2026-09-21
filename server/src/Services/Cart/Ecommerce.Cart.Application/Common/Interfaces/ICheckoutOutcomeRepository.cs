using Ecommerce.Cart.Domain.Entities;

namespace Ecommerce.Cart.Application.Common.Interfaces;

public interface ICheckoutOutcomeRepository
{
    /// <summary>
    /// The record for one order, created if absent, <b>locked</b> for the rest of the transaction.
    /// </summary>
    /// <remarks>
    /// Whichever of the three order events arrives first creates the row; every one of them then takes
    /// the same lock. That is what makes "whichever arrives second applies the removal" safe when two
    /// arrive at once. Must run inside a transaction.
    /// </remarks>
    Task<CheckoutOutcome> GetOrCreateForUpdateAsync(Guid orderId, CancellationToken cancellationToken = default);
}
