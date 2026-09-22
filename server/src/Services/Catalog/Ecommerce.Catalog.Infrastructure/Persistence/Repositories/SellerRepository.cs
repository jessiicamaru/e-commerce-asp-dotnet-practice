using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Catalog.Infrastructure.Persistence.Repositories;

public class SellerRepository(CatalogDbContext context) : ISellerRepository
{
    private readonly CatalogDbContext _context = context;

    public async Task<bool> TryRecordAsync(
        Guid sellerId,
        string shopName,
        DateTime observedAt,
        CancellationToken cancellationToken = default)
    {
        // A guarded single statement, like every other announcement this system records. The
        // comparison is on the TIMESTAMP: a message that arrives late carrying an older name must
        // lose to the newer one already stored, and comparing names instead would let it win.
        var updated = await _context.Sellers
            .Where(s => s.SellerId == sellerId && s.ObservedAt < observedAt)
            .ExecuteUpdateAsync(set => set
                .SetProperty(s => s.ShopName, shopName)
                .SetProperty(s => s.ObservedAt, observedAt), cancellationToken);

        if (updated > 0)
        {
            return true;
        }

        if (await _context.Sellers.AnyAsync(s => s.SellerId == sellerId, cancellationToken))
        {
            // There, and at least as new: a redelivery or an overtaken rename. Nothing to do.
            return false;
        }

        _context.Sellers.Add(new Seller
        {
            SellerId = sellerId,
            ShopName = shopName,
            ObservedAt = observedAt,
        });

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<Dictionary<Guid, string>> GetNamesAsync(
        IEnumerable<Guid> sellerIds,
        CancellationToken cancellationToken = default)
    {
        var wanted = sellerIds.Distinct().ToList();

        if (wanted.Count == 0)
        {
            return [];
        }

        // One query for a whole page. FR-003 exists to stop this becoming one per product.
        return await _context.Sellers
            .AsNoTracking()
            .Where(s => wanted.Contains(s.SellerId))
            .ToDictionaryAsync(s => s.SellerId, s => s.ShopName, cancellationToken);
    }
}
