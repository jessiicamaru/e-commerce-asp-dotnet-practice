using Ecommerce.Domain.Entities;

namespace Ecommerce.Application.Common.Interfaces;

/// <summary>
/// The address book. Every method takes the owner's id, and none can reach another owner's rows.
/// </summary>
public interface IAddressRepository
{
    /// <summary>
    /// <c>SELECT ... FROM users WHERE "Id" = @userId FOR UPDATE</c>. Serialises one customer's address
    /// changes, so "demote the old default, promote the new one" and the 20-address limit cannot race.
    /// Must run inside <see cref="IUnitOfWork.ExecuteInTransactionAsync"/>.
    /// </summary>
    Task LockOwnerAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<List<DeliveryAddress>> ListAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<DeliveryAddress?> GetAsync(Guid userId, Guid addressId, CancellationToken cancellationToken = default);

    Task<DeliveryAddress?> GetDefaultAsync(Guid userId, CancellationToken cancellationToken = default);

    void Add(DeliveryAddress address);

    void Remove(DeliveryAddress address);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
