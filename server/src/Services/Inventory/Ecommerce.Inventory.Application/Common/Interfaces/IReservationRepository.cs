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

    /// <summary>
    /// Ends every <see cref="ReservationStatus.Held"/> reservation of these variants as
    /// <see cref="ReservationStatus.Released"/> with <paramref name="reason"/>, in one guarded statement, and
    /// returns how many (#181, specs/090). Settled reservations are left as they are.
    /// </summary>
    Task<int> ReleaseHeldAsync(IReadOnlyCollection<Guid> productIds, string reason, DateTime now, CancellationToken cancellationToken = default);

    Task<bool> ExistsForOrderAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task AddRangeAsync(IEnumerable<StockReservation> reservations, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
