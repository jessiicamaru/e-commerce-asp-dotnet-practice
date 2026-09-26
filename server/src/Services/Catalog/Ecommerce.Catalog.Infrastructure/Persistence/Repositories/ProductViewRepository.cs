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

    public async Task<SellerProductInsights> SellerAsync(Guid sellerId, DateOnly from, DateOnly to, int limit, CancellationToken cancellationToken = default)
    {
        var products = _context.Products.AsNoTracking().Where(p => p.SellerId == sellerId);
        var viewed = products.Select(p => new
        {
            p.Id,
            p.Name,
            p.RatingAverage,
            p.RatingCount,
            Views = _context.ProductViews.Where(v => v.ProductId == p.Id && v.Day >= from && v.Day <= to).Sum(v => (int?)v.Views) ?? 0,
        });

        var views = await viewed.SumAsync(p => p.Views, cancellationToken);
        // Research D3: each product's stored average is already right (specs/046), so the shop's is those
        // averages weighted by their counts - one review at 4 and three at 2 is 2.5, not 3.
        var rated = await products.Where(p => p.RatingCount > 0 && p.RatingAverage != null)
            .GroupBy(_ => 1)
            .Select(g => new { Weighted = g.Sum(p => p.RatingAverage!.Value * p.RatingCount), Count = g.Sum(p => p.RatingCount) })
            .SingleOrDefaultAsync(cancellationToken);
        var top = await viewed
            .OrderByDescending(p => p.Views).ThenByDescending(p => p.RatingCount).ThenBy(p => p.Name)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return new SellerProductInsights(
            views,
            rated is null ? null : Math.Round(rated.Weighted / rated.Count, 2, MidpointRounding.AwayFromZero),
            rated?.Count ?? 0,
            top.Select(p => new SellerProductInsight(p.Id, p.Name, p.Views, p.RatingAverage, p.RatingCount)).ToList());
    }
}
