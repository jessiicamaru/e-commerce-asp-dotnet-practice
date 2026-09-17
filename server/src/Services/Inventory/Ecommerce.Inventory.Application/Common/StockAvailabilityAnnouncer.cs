using Ecommerce.Contracts.Inventory;
using Ecommerce.Inventory.Domain.Entities;
using MassTransit;

namespace Ecommerce.Inventory.Application.Common;

/// <summary>
/// Turns a <see cref="StockItem"/> into the announcement Catalog listens for.
/// </summary>
/// <remarks>
/// <para>
/// One implementation because there are <b>six</b> call sites — reserve, release, confirm, expire,
/// set-on-hand and register. Six places to build the same message is six places for them to drift
/// in what they send.
/// </para>
/// <para>
/// It does <b>not</b> hide the publish. Each caller still stages its change, calls this, then calls
/// <c>SaveChangesAsync</c> exactly once, so the stock change and the announcement about it commit
/// together or not at all (constitution III). A helper that also saved would make that ordering
/// somebody else's problem, which is how this repository shipped the outbox bug twice.
/// </para>
/// <para>
/// <b>If you add a seventh path that moves stock, it must call this too.</b> Nothing enforces that —
/// an EF SaveChanges interceptor would, and was rejected as unverified (research D2). What stands in
/// for enforcement is one test per call site in <c>Ecommerce.Inventory.Tests</c>.
/// </para>
/// </remarks>
public static class StockAvailabilityAnnouncer
{
    /// <summary>
    /// Stages an announcement describing <paramref name="stock"/> as it now stands. Call after
    /// mutating the entity and before the single <c>SaveChangesAsync</c>.
    /// </summary>
    public static Task AnnounceAsync(
        IPublishEndpoint publishEndpoint,
        StockItem stock,
        CancellationToken cancellationToken = default)
    {
        return publishEndpoint.Publish(
            new StockAvailabilityChangedEvent(
                stock.ProductId,
                stock.QuantityAvailable,
                stock.QuantityAvailable > 0,
                DateTime.UtcNow),
            cancellationToken);
    }

    /// <summary>
    /// The same, for a path that touched several products at once — the reserve and release paths
    /// both do.
    /// </summary>
    public static async Task AnnounceAsync(
        IPublishEndpoint publishEndpoint,
        IEnumerable<StockItem> stockItems,
        CancellationToken cancellationToken = default)
    {
        foreach (var stock in stockItems)
        {
            await AnnounceAsync(publishEndpoint, stock, cancellationToken);
        }
    }
}
