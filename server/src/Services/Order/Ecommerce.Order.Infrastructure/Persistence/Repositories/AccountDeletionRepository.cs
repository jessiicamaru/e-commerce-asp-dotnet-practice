using Ecommerce.Order.Application.MyData;
using Ecommerce.Order.Application.Orders.Common;
using Ecommerce.Order.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Order.Infrastructure.Persistence.Repositories;

/// <summary>What keeps an account open, and what Order erases once it is deleted (specs/112).</summary>
public class AccountDeletionRepository(OrderDbContext context) : IAccountStandingReader, IAccountErasure
{
    /// <summary>A return waiting for somebody - the buyer to send it, the seller to decide or receive, staff to rule.</summary>
    private static readonly ReturnStatus[] OpenReturnStates =
        [ReturnStatus.Requested, ReturnStatus.Accepted, ReturnStatus.Escalated, ReturnStatus.SentBack];

    private readonly OrderDbContext _context = context;

    public async Task<IReadOnlyList<string>> GetBlockersAsync(Guid personId, CancellationToken cancellationToken = default)
    {
        var earning = Sales.Earning;
        var blockers = new List<string>();

        // Still settling, or paid with a parcel on its way. A paid order with no parts yet (an older image wrote it,
        // specs/035) is open until it moves; a legacy Completed one without parts predates fulfilment and is not.
        var openOrders = await _context.Orders.AsNoTracking().AnyAsync(o => o.UserId == personId
            && (o.Status == OrderStatus.Submitted
                || (earning.Contains(o.Status) && o.Shipments.Any(s => s.CancelledAt == null && s.DeliveredAt == null))
                || ((o.Status == OrderStatus.Paid || o.Status == OrderStatus.Preparing) && !o.Shipments.Any())),
            cancellationToken);
        if (openOrders) blockers.Add(AccountBlockers.OpenOrders);

        var openReturns = await _context.ParcelReturns.AsNoTracking()
            .AnyAsync(r => (r.CustomerId == personId || r.SellerId == personId) && OpenReturnStates.Contains(r.Status), cancellationToken);
        if (openReturns) blockers.Add(AccountBlockers.OpenReturns);

        var openSales = await _context.OrderShipments.AsNoTracking().AnyAsync(s => s.SellerId == personId
            && s.CancelledAt == null && s.DeliveredAt == null && earning.Contains(s.Order!.Status), cancellationToken);
        if (openSales) blockers.Add(AccountBlockers.OpenSales);

        // The same parts a balance counts (PayoutRepository.Earning): theirs, paid, with terms, not returned, not
        // cancelled - and not yet claimed by a payout.
        var unpaid = await _context.OrderShipments.AsNoTracking().AnyAsync(s => s.SellerId == personId
            && s.GoodsTotal != null
            && earning.Contains(s.Order!.Status)
            && (s.Return == null || s.Return.Status != ReturnStatus.Received)
            && s.CancelledAt == null
            && s.PayoutId == null, cancellationToken);
        if (unpaid) blockers.Add(AccountBlockers.UnpaidEarnings);

        return blockers;
    }

    public async Task EraseAsync(Guid personId, CancellationToken cancellationToken = default)
    {
        // Inside a consumer MassTransit's outbox already holds a transaction on this context: join it, as
        // OrderRepository.TrySettleAsync does. Anywhere else, one transaction of our own under the retry strategy.
        if (_context.Database.CurrentTransaction is not null)
        {
            await EraseInTransactionAsync(personId, cancellationToken);
            return;
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            _context.ChangeTracker.Clear();
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            await EraseInTransactionAsync(personId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }

    private async Task EraseInTransactionAsync(Guid personId, CancellationToken cancellationToken)
    {
        // The delivery copy is an owned type, so it is changed through the tracker. The country stays: the tax charged
        // on the order depends on it.
        var orders = await _context.Orders.Where(o => o.UserId == personId && o.ShipTo != null).ToListAsync(cancellationToken);
        foreach (var order in orders)
        {
            var address = order.ShipTo!;
            address.RecipientName = string.Empty;
            address.Line1 = string.Empty;
            address.Line2 = null;
            address.City = string.Empty;
            address.Region = null;
            address.PostalCode = string.Empty;
            address.Phone = null;
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _context.ParcelReturns.Where(r => r.CustomerId == personId)
            .ExecuteUpdateAsync(x => x.SetProperty(r => r.Reason, string.Empty), cancellationToken);
        await _context.Vouchers.Where(v => v.SellerId == personId)
            .ExecuteUpdateAsync(x => x.SetProperty(v => v.Status, VoucherStatus.Disabled), cancellationToken);
        await _context.Payouts.Where(p => p.SellerId == personId)
            .ExecuteUpdateAsync(x => x.SetProperty(p => p.PaidToHolder, (string?)null), cancellationToken);
    }
}
