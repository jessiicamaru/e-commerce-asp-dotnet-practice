using Ecommerce.Inventory.Domain.Entities;

namespace Ecommerce.Inventory.Application.Common.Interfaces;

public interface IStockRepository
{
    /// <summary>
    /// Loads stock rows under a pessimistic row lock, held for the rest of the transaction.
    /// Implementations MUST lock in ascending ProductId order: two orders containing the same two
    /// products in opposite order would otherwise deadlock.
    /// </summary>
    Task<List<StockItem>> GetForUpdateAsync(IEnumerable<Guid> productIds, CancellationToken cancellationToken = default);

    Task<StockItem?> GetByProductIdAsync(Guid productId, CancellationToken cancellationToken = default);

    Task<(List<StockItem> Items, int TotalCount)> GetPaginatedAsync(
        int pageNumber,
        int pageSize,
        string? sku,
        CancellationToken cancellationToken = default);

    Task AddAsync(StockItem stockItem, CancellationToken cancellationToken = default);

    /// <summary>
    /// Drops the stock rows for sellable units the catalogue has deleted, returning how many went
    /// (specs/024). Zero is a normal answer: a redelivery finds nothing left.
    /// </summary>
    Task<int> ForgetAsync(IEnumerable<Guid> productIds, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
