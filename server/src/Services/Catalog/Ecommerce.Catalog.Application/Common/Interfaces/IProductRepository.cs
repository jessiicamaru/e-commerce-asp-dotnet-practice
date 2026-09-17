using Ecommerce.Catalog.Domain.Entities;

namespace Ecommerce.Catalog.Application.Common.Interfaces;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Product?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default);
    Task<List<Product>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<(List<Product> Items, int TotalCount)> GetPaginatedAsync(int pageNumber, int pageSize, Guid? categoryId, string? searchTerm, string? sortBy, CancellationToken cancellationToken = default);
    Task AddAsync(Product product, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Records what Inventory last said about a product's availability, and returns the number of
    /// rows that changed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Zero is a normal answer.</b> It means the announcement was a duplicate, or an older
    /// observation that a newer one has already overtaken, or about a product this catalogue does
    /// not hold. None of those is an error.
    /// </para>
    /// <para>
    /// Both conditions live in the statement the database executes. The duplicate case is the easy
    /// half; the half that costs something is an <i>older</i> announcement arriving <i>later</i>,
    /// which without the comparison would overwrite a newer one and leave the listing offering goods
    /// that are gone. The broker preserves order only in the absence of redelivery, and redelivery
    /// is normal operation.
    /// </para>
    /// </remarks>
    Task<int> TryRecordAvailabilityAsync(
        Guid productId,
        bool isAvailable,
        DateTime observedAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether this catalogue holds the product at all. Used only to explain a zero-row result: a
    /// product that exists was already current, one that does not is an announcement for somebody
    /// else's database. The two deserve different log levels.
    /// </summary>
    Task<bool> ExistsAsync(Guid productId, CancellationToken cancellationToken = default);
}
