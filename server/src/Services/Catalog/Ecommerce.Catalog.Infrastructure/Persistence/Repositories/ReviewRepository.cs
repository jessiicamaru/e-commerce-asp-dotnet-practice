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
            await RecomputeAsync(productId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }

    public Task<int> TryAddFirstAsync(Review review, Func<CancellationToken, Task> stage, CancellationToken cancellationToken = default) =>
        GuardedAsync(review.ProductId, stage, ct => _context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO product_reviews ("Id", "ProductId", "CustomerId", "AuthorName", "Rating", "Body", "CreatedAt", "UpdatedAt")
            VALUES ({review.Id}, {review.ProductId}, {review.CustomerId}, {review.AuthorName}, {review.Rating}, {review.Body}, {review.CreatedAt}, {review.UpdatedAt})
            ON CONFLICT ("ProductId", "CustomerId") DO NOTHING
            """, ct), cancellationToken);

    public Task<int> TryHideAsync(Guid reviewId, Guid productId, string reason, Guid hiddenBy, DateTime now,
        Func<CancellationToken, Task> stage, CancellationToken cancellationToken = default) =>
        GuardedAsync(productId, stage, ct => _context.Reviews
            .Where(r => r.Id == reviewId && r.HiddenAt == null)
            .ExecuteUpdateAsync(set => set
                .SetProperty(r => r.HiddenAt, now)
                .SetProperty(r => r.HiddenReason, reason)
                .SetProperty(r => r.HiddenBy, hiddenBy), ct), cancellationToken);

    public Task<int> TryRestoreAsync(Guid reviewId, Guid productId, Func<CancellationToken, Task> stage,
        CancellationToken cancellationToken = default) =>
        GuardedAsync(productId, stage, ct => _context.Reviews
            .Where(r => r.Id == reviewId && r.HiddenAt != null)
            .ExecuteUpdateAsync(set => set
                .SetProperty(r => r.HiddenAt, (DateTime?)null)
                .SetProperty(r => r.HiddenReason, (string?)null)
                .SetProperty(r => r.HiddenBy, (Guid?)null), ct), cancellationToken);

    /// <summary>
    /// The one statement decides; only if it changed a row are the audit entry staged and saved and the rating
    /// recomputed - all in its transaction, so a decision and its record commit together or not at all.
    /// </summary>
    private Task<int> GuardedAsync(Guid productId, Func<CancellationToken, Task> stage,
        Func<CancellationToken, Task<int>> statement, CancellationToken cancellationToken)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        return strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            var rows = await statement(cancellationToken);
            if (rows == 1)
            {
                await stage(cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                await RecomputeAsync(productId, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return rows;
        });
    }

    /// <summary>From the rows, never incrementally: an increment drifts the first time two reviews land at once.</summary>
    private Task RecomputeAsync(Guid productId, CancellationToken cancellationToken) =>
        _context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE products SET
                "RatingCount" = (SELECT count(*) FROM product_reviews WHERE "ProductId" = {productId} AND "HiddenAt" IS NULL),
                "RatingAverage" = (SELECT round(avg("Rating")::numeric, 2) FROM product_reviews WHERE "ProductId" = {productId} AND "HiddenAt" IS NULL)
            WHERE "Id" = {productId}
            """, cancellationToken);
}
