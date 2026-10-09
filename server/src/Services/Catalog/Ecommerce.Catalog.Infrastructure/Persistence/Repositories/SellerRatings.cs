using Ecommerce.Catalog.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Catalog.Infrastructure.Persistence.Repositories;

/// <summary>
/// A shop's rating, in one place for the shop page (specs/165) and the seller's insights (specs/068): they must never
/// disagree about one shop.
/// </summary>
internal static class SellerRatings
{
    public static async Task<ShopRating> OfAsync(CatalogDbContext context, Guid sellerId, CancellationToken cancellationToken)
    {
        // Each product's stored average is already right - recomputed from its visible reviews on every write, hide and
        // restore (specs/046) - so the shop's is those averages weighted by their counts: one review at 4 and three at 2
        // is 2.5, not 3. Every product of the seller counts, on the shelf or not: withdrawing a badly reviewed product
        // must not lift the shop's rating (research D2).
        var rated = await context.Products.AsNoTracking()
            .Where(p => p.SellerId == sellerId && p.RatingCount > 0 && p.RatingAverage != null)
            .GroupBy(_ => 1)
            .Select(g => new { Weighted = g.Sum(p => p.RatingAverage!.Value * p.RatingCount), Count = g.Sum(p => p.RatingCount) })
            .SingleOrDefaultAsync(cancellationToken);

        return rated is null
            ? new ShopRating(null, 0)
            : new ShopRating(Math.Round(rated.Weighted / rated.Count, 2, MidpointRounding.AwayFromZero), rated.Count);
    }
}
