using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Catalog.Infrastructure.Persistence.Repositories;

public class SavedProductRepository(CatalogDbContext context) : ISavedProductRepository
{
    private readonly CatalogDbContext _context = context;

    public Task SaveAsync(Guid customerId, Guid productId, DateTime at, CancellationToken cancellationToken = default) =>
        _context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO saved_products ("CustomerId", "ProductId", "SavedAt") VALUES ({customerId}, {productId}, {at})
            ON CONFLICT ("CustomerId", "ProductId") DO NOTHING
            """, cancellationToken);

    public Task UnsaveAsync(Guid customerId, Guid productId, CancellationToken cancellationToken = default) =>
        _context.SavedProducts.Where(s => s.CustomerId == customerId && s.ProductId == productId).ExecuteDeleteAsync(cancellationToken);

    public async Task<(List<(Product Product, DateTime SavedAt)> Items, int TotalCount)> GetPageAsync(
        Guid customerId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var mine = _context.SavedProducts.AsNoTracking().Where(s => s.CustomerId == customerId);
        var total = await mine.CountAsync(cancellationToken);
        var rows = await mine
            .OrderByDescending(s => s.SavedAt).ThenBy(s => s.ProductId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Include(s => s.Product!).ThenInclude(p => p.Category)
            .Include(s => s.Product!).ThenInclude(p => p.Translations)
            .Include(s => s.Product!).ThenInclude(p => p.Variants).ThenInclude(v => v.Prices)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return (rows.Select(s => (s.Product!, s.SavedAt)).ToList(), total);
    }

    public Task<List<Guid>> IdsAsync(Guid customerId, CancellationToken cancellationToken = default) =>
        _context.SavedProducts.AsNoTracking().Where(s => s.CustomerId == customerId)
            .OrderByDescending(s => s.SavedAt).Select(s => s.ProductId).ToListAsync(cancellationToken);

    public Task<List<Guid>> SaverIdsAsync(Guid productId, CancellationToken cancellationToken = default) =>
        _context.SavedProducts.AsNoTracking().Where(s => s.ProductId == productId).Select(s => s.CustomerId).ToListAsync(cancellationToken);
}
