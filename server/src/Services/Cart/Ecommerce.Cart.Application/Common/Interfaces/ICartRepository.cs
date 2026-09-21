using Ecommerce.Cart.Domain.Entities;

namespace Ecommerce.Cart.Application.Common.Interfaces;

public interface ICartRepository
{
    /// <summary>The customer's cart with its lines, for reading. Null when they have never added anything.</summary>
    Task<Domain.Entities.Cart?> GetAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The customer's cart, created if absent, <b>locked</b> for the rest of the transaction.
    /// </summary>
    /// <remarks>
    /// Every write to a cart takes this lock first, so two devices changing one cart at once are
    /// serialised rather than one silently undoing the other. Creation is an insert that does nothing
    /// on conflict, so two first-ever adds racing each other still produce exactly one cart - the
    /// unique index on UserId is what guarantees it, not this code. Must run inside a transaction.
    /// </remarks>
    Task<Domain.Entities.Cart> GetOrCreateForUpdateAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>The customer's cart, locked, or null if they have none. Must run inside a transaction.</summary>
    Task<Domain.Entities.Cart?> GetForUpdateAsync(Guid userId, CancellationToken cancellationToken = default);

    void RemoveLine(CartLine line);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
