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

    public async Task<bool> TryRecordSuspensionAsync(
        Guid sellerId, bool suspended, DateTime changedAt, CancellationToken cancellationToken = default)
    {
        async Task<bool> RunAsync(CancellationToken ct)
        {
            // One guarded upsert: an older decision arriving late loses, a redelivery changes nothing, and a seller
            // not heard of yet gets a row whose empty name and minimal ObservedAt the registration overwrites.
            var noName = DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);
            var written = await _context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO sellers ("SellerId", "ShopName", "ObservedAt", "Suspended", "SuspensionChangedAt")
                VALUES ({sellerId}, '', {noName}, {suspended}, {changedAt})
                ON CONFLICT ("SellerId") DO UPDATE SET "Suspended" = EXCLUDED."Suspended", "SuspensionChangedAt" = EXCLUDED."SuspensionChangedAt"
                 WHERE sellers."SuspensionChangedAt" IS NULL OR sellers."SuspensionChangedAt" < EXCLUDED."SuspensionChangedAt"
                """, ct);

            if (written == 0)
            {
                return false;
            }

            // From the whole row, never from this event's bool: a ban lifted while the seller has paused must not reopen
            // the shop (specs/107 research D2).
            await ApplyShopStateAsync(sellerId, ct);
            return true;
        }

        // Inside a consumer's transaction already: take part in it.
        if (_context.Database.CurrentTransaction is not null)
        {
            return await RunAsync(cancellationToken);
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            var changed = await RunAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return changed;
        });
    }

    public async Task<bool> TryRecordDescriptionAsync(
        Guid sellerId, string? description, DateTime observedAt, CancellationToken cancellationToken = default)
    {
        // The same guarded upsert as the suspension (specs/095): newer wins, a redelivery changes nothing.
        var noName = DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);
        var written = await _context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO sellers ("SellerId", "ShopName", "ObservedAt", "Description", "DescriptionObservedAt")
            VALUES ({sellerId}, '', {noName}, {description}, {observedAt})
            ON CONFLICT ("SellerId") DO UPDATE SET "Description" = EXCLUDED."Description", "DescriptionObservedAt" = EXCLUDED."DescriptionObservedAt"
             WHERE sellers."DescriptionObservedAt" IS NULL OR sellers."DescriptionObservedAt" < EXCLUDED."DescriptionObservedAt"
            """, cancellationToken);
        return written > 0;
    }

    /// <summary>
    /// The one statement that writes <c>products.SellerSuspended</c> (#214, specs/107): from all three reasons on the
    /// <c>sellers</c> row - banned, paused, closed - so no change of one of them can reopen a shop another keeps shut.
    /// </summary>
    private Task<int> ApplyShopStateAsync(Guid sellerId, CancellationToken cancellationToken) =>
        _context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE products SET "SellerSuspended" = COALESCE((
                SELECT s."Suspended" OR s."PausedAt" IS NOT NULL OR s."ClosedAt" IS NOT NULL
                  FROM sellers AS s WHERE s."SellerId" = {sellerId}), false)
             WHERE "SellerId" = {sellerId}
            """, cancellationToken);

    public async Task<bool> TryMoveShopAsync(
        Guid sellerId,
        ShopMove move,
        DateTime at,
        string? reason,
        Guid? by,
        Func<CancellationToken, Task> stage,
        CancellationToken cancellationToken = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _context.ChangeTracker.Clear();
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            // One guarded statement per move (data-model.md): of two callers, one moves and the other gets zero rows.
            // A shop whose name has not arrived from Identity is not a shop yet.
            var shop = _context.Sellers.Where(s => s.SellerId == sellerId && s.ShopName != "");
            var moved = move switch
            {
                ShopMove.Pause => await shop
                    .Where(s => s.PausedAt == null && s.ClosedAt == null)
                    .ExecuteUpdateAsync(set => set.SetProperty(s => s.PausedAt, at), cancellationToken),
                // ⚠️ "ClosedAt IS NULL" is what stops a seller reopening a shop staff closed (research D4).
                ShopMove.Resume => await shop
                    .Where(s => s.PausedAt != null && s.ClosedAt == null)
                    .ExecuteUpdateAsync(set => set.SetProperty(s => s.PausedAt, (DateTime?)null), cancellationToken),
                ShopMove.Close => await shop
                    .Where(s => s.ClosedAt == null)
                    .ExecuteUpdateAsync(set => set
                        .SetProperty(s => s.ClosedAt, at)
                        .SetProperty(s => s.ClosedReason, reason)
                        .SetProperty(s => s.ClosedBy, by), cancellationToken),
                // Staff reopening leaves the seller's own pause alone: it is theirs.
                ShopMove.Reopen => await shop
                    .Where(s => s.ClosedAt != null)
                    .ExecuteUpdateAsync(set => set
                        .SetProperty(s => s.ClosedAt, (DateTime?)null)
                        .SetProperty(s => s.ClosedReason, (string?)null)
                        .SetProperty(s => s.ClosedBy, (Guid?)null), cancellationToken),
                _ => throw new ArgumentOutOfRangeException(nameof(move)),
            };

            if (moved == 0)
            {
                return false;
            }

            await ApplyShopStateAsync(sellerId, cancellationToken);
            await stage(cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        });
    }

    public async Task<(List<Seller> Items, int Total)> GetClosedAsync(
        int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.Sellers.AsNoTracking().Where(s => s.ClosedAt != null);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(s => s.ClosedAt).ThenBy(s => s.SellerId)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return (items, total);
    }

    public Task<Seller?> GetAsync(Guid sellerId, CancellationToken cancellationToken = default) =>
        _context.Sellers.AsNoTracking().FirstOrDefaultAsync(s => s.SellerId == sellerId, cancellationToken);

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
            // An empty name is a suspension that overtook the registration (specs/095): no name yet, not a blank one.
            .Where(s => wanted.Contains(s.SellerId) && s.ShopName != "")
            .ToDictionaryAsync(s => s.SellerId, s => s.ShopName, cancellationToken);
    }
}
