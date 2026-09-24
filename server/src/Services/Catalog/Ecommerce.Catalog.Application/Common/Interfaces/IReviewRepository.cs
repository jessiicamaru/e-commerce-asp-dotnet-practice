using Ecommerce.Catalog.Domain.Entities;

namespace Ecommerce.Catalog.Application.Common.Interfaces;

/// <summary>A review with the product it is about, for staff (specs/046).</summary>
public record ReviewRow(Review Review, string ProductName);

public interface IReviewRepository
{
    /// <summary>Records that a customer received these products. A second delivery changes nothing.</summary>
    Task RecordEligibilityAsync(Guid customerId, IEnumerable<Guid> productIds, DateTime deliveredAt, CancellationToken cancellationToken = default);

    Task<bool> IsEligibleAsync(Guid productId, Guid customerId, CancellationToken cancellationToken = default);

    Task<Review?> GetMineAsync(Guid productId, Guid customerId, CancellationToken cancellationToken = default);

    Task<Review?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>A product's visible reviews, newest first.</summary>
    Task<(List<Review> Items, int TotalCount)> GetVisibleAsync(Guid productId, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Every product's reviews, newest first, for moderators - the hidden ones or the visible ones.</summary>
    Task<(List<ReviewRow> Items, int TotalCount)> GetForStaffAsync(bool hidden, int page, int pageSize, CancellationToken cancellationToken = default);

    Task AddAsync(Review review, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves what is staged and recomputes the product's average and count from its VISIBLE reviews, in one
    /// transaction - so the stars on a listing always agree with the reviews under them.
    /// </summary>
    Task SaveAndRecomputeAsync(Guid productId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts a customer's FIRST review of a product unless they already have one (specs/057, #127): one
    /// <c>INSERT ... ON CONFLICT DO NOTHING</c>, and only if it inserted, <paramref name="stage"/> (the audit
    /// entry, the seller's notice) and the rating recompute - one transaction. Returns the rows inserted: 0
    /// means a concurrent write got there first, and the caller edits that review instead.
    /// </summary>
    Task<int> TryAddFirstAsync(Review review, Func<CancellationToken, Task> stage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hides a visible review in ONE guarded statement (specs/057, #127), and in the same transaction runs
    /// <paramref name="stage"/> (the audit entry) and recomputes the rating. Returns the rows changed: 0 means
    /// somebody hid it first.
    /// </summary>
    Task<int> TryHideAsync(Guid reviewId, Guid productId, string reason, Guid hiddenBy, DateTime now,
        Func<CancellationToken, Task> stage, CancellationToken cancellationToken = default);

    /// <summary>Shows a hidden review again, guarded the same way; 0 means somebody restored it first.</summary>
    Task<int> TryRestoreAsync(Guid reviewId, Guid productId, Func<CancellationToken, Task> stage, CancellationToken cancellationToken = default);
}
