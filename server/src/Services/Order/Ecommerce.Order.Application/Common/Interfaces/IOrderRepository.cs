using Ecommerce.Order.Domain.Entities;

namespace Ecommerce.Order.Application.Common.Interfaces;

public interface IOrderRepository
{
    Task<Domain.Entities.Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Domain.Entities.Order>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(Domain.Entities.Order order, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
