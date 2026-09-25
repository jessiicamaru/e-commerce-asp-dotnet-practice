using Ecommerce.Payment.Domain.Entities;

namespace Ecommerce.Payment.Application.Common.Interfaces;

public interface IPaymentRepository
{
    Task<Domain.Entities.Payment?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task<(List<Domain.Entities.Payment> Items, int TotalCount)> GetPaginatedAsync(
        int pageNumber,
        int pageSize,
        Guid? orderId,
        string? status,
        CancellationToken cancellationToken = default);

    Task AddAsync(Domain.Entities.Payment payment, CancellationToken cancellationToken = default);

    /// <summary>The refund of a whole order, if any (specs/039) - not a returned parcel's.</summary>
    Task<Refund?> GetRefundAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>The refunds recorded for these orders, by order id - one query for a page of payments.</summary>
    Task<Dictionary<Guid, Refund>> GetRefundsAsync(IReadOnlyCollection<Guid> orderIds, CancellationToken cancellationToken = default);

    Task AddRefundAsync(Refund refund, CancellationToken cancellationToken = default);

    /// <summary>The refund of one returned parcel, if any (specs/066).</summary>
    Task<Refund?> GetReturnRefundAsync(Guid returnId, CancellationToken cancellationToken = default);

    /// <summary>Everything refunded for an order so far, whole or by parcel - never more than was paid.</summary>
    Task<decimal> GetRefundedTotalAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
