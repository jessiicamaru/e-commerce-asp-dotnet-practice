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

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
