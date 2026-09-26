using Ecommerce.Inventory.Application.Common.Interfaces;
using Ecommerce.Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Inventory.Infrastructure.Persistence.Repositories;

public class StockRepository(InventoryDbContext context) : IStockRepository
{
    private readonly InventoryDbContext _context = context;

    public async Task<List<StockItem>> GetForUpdateAsync(
        IEnumerable<Guid> productIds,
        CancellationToken cancellationToken = default)
    {
        // Ordered by ProductId so that every caller — reserve, release, confirm, expire, and the
        // admin stock endpoint — takes the same locks in the same sequence. Two orders containing
        // the same two products in opposite order would otherwise deadlock against each other.
        var ids = productIds.Distinct().OrderBy(id => id).ToArray();

        if (ids.Length == 0)
        {
            return [];
        }

        return await _context.StockItems
            .FromSqlRaw(
                """
                SELECT * FROM stock_items
                WHERE "ProductId" = ANY({0})
                ORDER BY "ProductId"
                FOR UPDATE
                """,
                ids)
            .ToListAsync(cancellationToken);
    }

    public async Task<StockItem?> GetByProductIdAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        return await _context.StockItems
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ProductId == productId, cancellationToken);
    }

    public async Task<(List<StockItem> Items, int TotalCount)> GetPaginatedAsync(
        int pageNumber,
        int pageSize,
        string? sku,
        CancellationToken cancellationToken = default)
    {
        var query = _context.StockItems.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(sku))
        {
            var term = sku.Trim().ToLower();
            query = query.Where(x => x.Sku.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(x => x.Sku)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task AddAsync(StockItem stockItem, CancellationToken cancellationToken = default)
    {
        await _context.StockItems.AddAsync(stockItem, cancellationToken);
    }

    public Task<int> ForgetAsync(IEnumerable<Guid> productIds, CancellationToken cancellationToken = default)
    {
        var wanted = productIds.Distinct().ToList();

        // One statement, and no read first: there is nothing to decide. A row that is not there was
        // already forgotten, which is the answer the caller wanted either way. Nothing cascades - there is
        // no foreign key from stock_reservations - so the caller releases the variants' held reservations in
        // the same transaction and leaves the settled ones as the orders' history (#181, specs/090).
        return _context.StockItems
            .Where(s => wanted.Contains(s.ProductId))
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
