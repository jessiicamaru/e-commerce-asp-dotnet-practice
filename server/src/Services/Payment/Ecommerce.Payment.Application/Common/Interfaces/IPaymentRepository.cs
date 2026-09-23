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

    /// <summary>The refund recorded for an order, if any (specs/039).</summary>
    Task<Refund?> GetRefundAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>The refunds recorded for these orders, by order id - one query for a page of payments.</summary>
    Task<Dictionary<Guid, Refund>> GetRefundsAsync(IReadOnlyCollection<Guid> orderIds, CancellationToken cancellationToken = default);

    Task AddRefundAsync(Refund refund, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
