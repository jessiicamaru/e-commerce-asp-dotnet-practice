using Ecommerce.Catalog.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Catalog.Infrastructure.Persistence.Repositories;

public class ProductViewRepository(CatalogDbContext context) : IProductViewRepository
{
    private readonly CatalogDbContext _context = context;

    public Task RecordAsync(Guid productId, DateOnly day, CancellationToken cancellationToken = default) =>
        // An upsert that increments in the database: read-then-write would lose views that arrive together.
        _context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO product_views ("ProductId", "Day", "Views") VALUES ({productId}, {day}, 1)
            ON CONFLICT ("ProductId", "Day") DO UPDATE SET "Views" = product_views."Views" + 1
            """, cancellationToken);

    public async Task<List<ViewedProduct>> TopAsync(DateOnly from, DateOnly to, int limit, CancellationToken cancellationToken = default)
    {
        var rows = await _context.ProductViews.AsNoTracking()
            .Where(v => v.Day >= from && v.Day <= to)
            .GroupBy(v => v.ProductId)
            .Select(g => new { ProductId = g.Key, Views = g.Sum(v => v.Views) })
            .OrderByDescending(x => x.Views)
            .Take(limit)
            .Join(_context.Products.AsNoTracking(), x => x.ProductId, p => p.Id, (x, p) => new { x.ProductId, p.Name, x.Views })
            .ToListAsync(cancellationToken);

        return rows.OrderByDescending(r => r.Views).Select(r => new ViewedProduct(r.ProductId, r.Name, r.Views)).ToList();
    }
}
