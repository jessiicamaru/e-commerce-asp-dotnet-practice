using Ecommerce.Inventory.Domain.Entities;
using Ecommerce.Inventory.Domain.Enums;

namespace Ecommerce.Inventory.Application.Common.Interfaces;

public interface IReservationRepository
{
    Task<List<StockReservation>> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reservations for an order in the given state. Used by the settlement paths, which must act
    /// only on rows still Held so that a repeat moves no stock.
    /// </summary>
    Task<List<StockReservation>> GetByOrderIdAndStatusAsync(Guid orderId, ReservationStatus status, CancellationToken cancellationToken = default);

    Task<List<StockReservation>> GetExpiredAsync(DateTime asOfUtc, int batchSize, CancellationToken cancellationToken = default);

    Task<bool> ExistsForOrderAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task AddRangeAsync(IEnumerable<StockReservation> reservations, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
