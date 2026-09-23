using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Catalog.Infrastructure.Persistence.Repositories;

public class ReviewRepository(CatalogDbContext context) : IReviewRepository
{
    private readonly CatalogDbContext _context = context;

    public async Task RecordEligibilityAsync(
        Guid customerId, IEnumerable<Guid> productIds, DateTime deliveredAt, CancellationToken cancellationToken = default)
    {
        // Idempotent by the key: a redelivered event, or the same product in a second parcel, is a no-op.
        foreach (var productId in productIds.Distinct())
        {
            await _context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO review_eligibility ("ProductId", "CustomerId", "FirstDeliveredAt")
                VALUES ({productId}, {customerId}, {deliveredAt})
                ON CONFLICT ("ProductId", "CustomerId") DO NOTHING
                """, cancellationToken);
        }
    }

    public Task<bool> IsEligibleAsync(Guid productId, Guid customerId, CancellationToken cancellationToken = default) =>
        _context.ReviewEligibility.AnyAsync(e => e.ProductId == productId && e.CustomerId == customerId, cancellationToken);

    public Task<Review?> GetMineAsync(Guid productId, Guid customerId, CancellationToken cancellationToken = default) =>
        _context.Reviews.FirstOrDefaultAsync(r => r.ProductId == productId && r.CustomerId == customerId, cancellationToken);

    public Task<Review?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Reviews.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<(List<Review> Items, int TotalCount)> GetVisibleAsync(
        Guid productId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.Reviews.AsNoTracking().Where(r => r.ProductId == productId && r.HiddenAt == null);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(r => r.CreatedAt).ThenBy(r => r.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<(List<ReviewRow> Items, int TotalCount)> GetForStaffAsync(
        bool hidden, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.Reviews.AsNoTracking().Where(r => (r.HiddenAt != null) == hidden);
        var total = await query.CountAsync(cancellationToken);
        var joined = from r in query
                     join p in _context.Products.AsNoTracking() on r.ProductId equals p.Id
                     select new { r, p.Name };
        var items = await joined.OrderByDescending(x => x.r.CreatedAt).ThenBy(x => x.r.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new ReviewRow(x.r, x.Name))
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task AddAsync(Review review, CancellationToken cancellationToken = default) =>
        await _context.Reviews.AddAsync(review, cancellationToken);

    public Task SaveAndRecomputeAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        return strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            // From the rows, never incrementally: an increment drifts the first time two reviews land at once.
            await _context.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE products SET
                    "RatingCount" = (SELECT count(*) FROM product_reviews WHERE "ProductId" = {productId} AND "HiddenAt" IS NULL),
                    "RatingAverage" = (SELECT round(avg("Rating")::numeric, 2) FROM product_reviews WHERE "ProductId" = {productId} AND "HiddenAt" IS NULL)
                WHERE "Id" = {productId}
                """, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        });
    }
}
