using Ecommerce.Catalog.Domain.Entities;

namespace Ecommerce.Catalog.Application.Common.Interfaces;

/// <summary>A shopper's saved products (specs/075).</summary>
public interface ISavedProductRepository
{
    /// <summary>One statement, <c>ON CONFLICT DO NOTHING</c>: saving twice, or twice at once, keeps one row.</summary>
    Task SaveAsync(Guid customerId, Guid productId, DateTime at, CancellationToken cancellationToken = default);

    Task UnsaveAsync(Guid customerId, Guid productId, CancellationToken cancellationToken = default);

    /// <summary>The shopper's, newest first, each product with what the listing reads (translations, prices).</summary>
    Task<(List<(Product Product, DateTime SavedAt)> Items, int TotalCount)> GetPageAsync(
        Guid customerId, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<List<Guid>> IdsAsync(Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>Everybody who saved this product - who a back-in-stock notice goes to.</summary>
    Task<List<Guid>> SaverIdsAsync(Guid productId, CancellationToken cancellationToken = default);
}
