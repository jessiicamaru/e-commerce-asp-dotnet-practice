namespace Ecommerce.Catalog.Application.Common.Interfaces;

public record ViewedProduct(Guid ProductId, string Name, int Views);

public interface IProductViewRepository
{
    /// <summary>One more view of this product today - one statement, safe under any number at once.</summary>
    Task RecordAsync(Guid productId, DateOnly day, CancellationToken cancellationToken = default);

    /// <summary>The most viewed products over [from, to], most first.</summary>
    Task<List<ViewedProduct>> TopAsync(DateOnly from, DateOnly to, int limit, CancellationToken cancellationToken = default);
}
