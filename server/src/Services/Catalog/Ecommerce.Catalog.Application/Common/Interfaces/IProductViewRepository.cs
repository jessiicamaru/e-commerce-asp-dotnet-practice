namespace Ecommerce.Catalog.Application.Common.Interfaces;

public record ViewedProduct(Guid ProductId, string Name, int Views);

/// <summary>One of a seller's products: its views in the period and its rating over its visible reviews.</summary>
public record SellerProductInsight(Guid ProductId, string Name, int Views, decimal? RatingAverage, int RatingCount);

/// <summary>
/// A seller's products at a glance (specs/068): views of all of them in the period, the rating over every visible
/// review of any of them - weighted by each product's count, null when there are none - and the most viewed.
/// </summary>
public record SellerProductInsights(int Views, decimal? RatingAverage, int RatingCount, List<SellerProductInsight> Products);

public interface IProductViewRepository
{
    /// <summary>
    /// One more view of this product today - one statement, safe under any number at once. With a
    /// <paramref name="viewer"/> (a hash, specs/086) only that viewer's first view of the day counts, and the
    /// product's viewers from earlier days are dropped in the same statement.
    /// </summary>
    Task RecordAsync(Guid productId, DateOnly day, string? viewer, CancellationToken cancellationToken = default);

    /// <summary>The most viewed products over [from, to], most first.</summary>
    Task<List<ViewedProduct>> TopAsync(DateOnly from, DateOnly to, int limit, CancellationToken cancellationToken = default);

    /// <summary>One seller's products over [from, to]: the totals over all of them, and the <paramref name="limit"/> most viewed.</summary>
    Task<SellerProductInsights> SellerAsync(Guid sellerId, DateOnly from, DateOnly to, int limit, CancellationToken cancellationToken = default);
}
