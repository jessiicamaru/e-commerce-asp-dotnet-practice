namespace Ecommerce.Inventory.Application.Common.Interfaces;

/// <summary>Returned parcels already put back on the shelf (specs/066).</summary>
public interface IReturnedParcels
{
    /// <summary>
    /// Claims the return in ONE statement - <c>INSERT ... ON CONFLICT DO NOTHING</c>. True for the first claim;
    /// a redelivery, or a second delivery at once, gets false and restocks nothing. Call inside the transaction.
    /// </summary>
    Task<bool> TryClaimAsync(Guid returnId, Guid orderId, DateTime at, CancellationToken cancellationToken = default);
}
