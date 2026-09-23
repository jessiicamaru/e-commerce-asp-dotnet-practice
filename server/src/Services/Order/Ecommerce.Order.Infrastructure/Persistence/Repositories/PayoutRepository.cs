using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Common;
using Ecommerce.Order.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Order.Infrastructure.Persistence.Repositories;

/// <summary>What the shop owes each seller, and the payouts that settle it (specs/037).</summary>
public class PayoutRepository(OrderDbContext context) : IPayoutRepository
{
    private readonly OrderDbContext _context = context;

    /// <summary>
    /// A seller's parts that are money at all: theirs, on a PAID order, with terms recorded at checkout.
    /// </summary>
    /// <remarks>
    /// ⚠️ The status filter is not decoration. Checkout writes a failed order's parts too, so without it a
    /// declined payment would sit in a seller's balance - and, shipped by mistake, be paid out.
    /// </remarks>
    private IQueryable<Domain.Entities.OrderShipment> Earning(Guid? sellerId)
    {
        var statuses = Sales.Statuses;

        return _context.OrderShipments
            .AsNoTracking()
            .Where(s => s.SellerId != null
                && (sellerId == null || s.SellerId == sellerId)
                && s.GoodsTotal != null
                && statuses.Contains(s.Order!.Status));
    }

    public async Task<List<BalanceResponse>> GetBalanceAsync(Guid sellerId, CancellationToken cancellationToken = default)
    {
        // One GROUP BY with conditional sums: three figures per currency in one round trip, all over the
        // same rows, so they cannot disagree about which parts count.
        var rows = await Earning(sellerId)
            .GroupBy(s => s.Order!.Currency)
            .Select(g => new
            {
                Currency = g.Key,
                OnTheWay = g.Sum(s => s.Status != ShipmentStatus.Shipped
                    ? s.GoodsTotal!.Value - s.Commission!.Value + s.ShippingShare!.Value : 0m),
                Due = g.Sum(s => s.Status == ShipmentStatus.Shipped && s.PayoutId == null
                    ? s.GoodsTotal!.Value - s.Commission!.Value + s.ShippingShare!.Value : 0m),
                PaidOut = g.Sum(s => s.PayoutId != null
                    ? s.GoodsTotal!.Value - s.Commission!.Value + s.ShippingShare!.Value : 0m)
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new BalanceResponse(r.Currency ?? string.Empty, r.OnTheWay, r.Due, r.PaidOut))
            .OrderBy(r => r.Currency)
            .ToList();
    }

    public async Task<(List<PayoutResponse> Payouts, int TotalCount)> GetPayoutsPageAsync(
        Guid sellerId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.Payouts.AsNoTracking().Where(p => p.SellerId == sellerId);
        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PayoutResponse(p.Id, p.SellerId, p.Currency, p.Amount, p.PartCount, p.CreatedAt))
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<List<PayoutDueResponse>> GetDueAsync(CancellationToken cancellationToken = default)
    {
        var due = await Earning(null)
            .Where(s => s.Status == ShipmentStatus.Shipped && s.PayoutId == null)
            .GroupBy(s => new { SellerId = s.SellerId!.Value, s.Order!.Currency })
            .Select(g => new
            {
                g.Key.SellerId,
                g.Key.Currency,
                Due = g.Sum(s => s.GoodsTotal!.Value - s.Commission!.Value + s.ShippingShare!.Value),
                Parts = g.Count()
            })
            .ToListAsync(cancellationToken);

        if (due.Count == 0)
        {
            return [];
        }

        // Who they are, in words: the most recent name frozen on their lines (specs/036). Order holds no
        // other record of a seller's name, and asking Identity would be a synchronous call for a label.
        var sellerIds = due.Select(d => (Guid?)d.SellerId).Distinct().ToList();
        var names = await _context.OrderItems
            .AsNoTracking()
            .Where(i => sellerIds.Contains(i.SellerId) && i.SellerName != null)
            .GroupBy(i => i.SellerId)
            .Select(g => new
            {
                SellerId = g.Key!.Value,
                Name = g.OrderByDescending(i => i.Order!.CreatedAt).Select(i => i.SellerName).First()
            })
            .ToDictionaryAsync(x => x.SellerId, x => x.Name, cancellationToken);

        return due
            .Select(d => new PayoutDueResponse(
                d.SellerId, names.GetValueOrDefault(d.SellerId), d.Currency ?? string.Empty, d.Due, d.Parts))
            .OrderBy(d => d.SellerName ?? d.SellerId.ToString())
            .ThenBy(d => d.Currency)
            .ToList();
    }

    public async Task<PayoutResponse?> TryRecordAsync(
        Guid payoutId,
        Guid sellerId,
        string currency,
        Guid recordedBy,
        DateTime at,
        CancellationToken cancellationToken = default)
    {
        var statuses = Sales.Statuses.Select(s => s.ToString()).ToArray();
        var shipped = ShipmentStatus.Shipped.ToString();

        // ONE statement (research D5). The CTE claims the parts - its WHERE is the guard, re-evaluated by
        // PostgreSQL under each row's lock, so a concurrent payout that got there first leaves this one
        // nothing - and the INSERT records the sum of exactly what was claimed, or nothing at all when
        // that is nothing (HAVING). The foreign key from the claimed parts to the new payout is checked at
        // the end of the statement, by which time the payout row exists.
        var inserted = await _context.Database.ExecuteSqlInterpolatedAsync($"""
            WITH claimed AS (
                UPDATE order_shipments AS s
                   SET "PayoutId" = {payoutId}
                  FROM orders AS o
                 WHERE o."Id" = s."OrderId"
                   AND s."SellerId" = {sellerId}
                   AND s."Status" = {shipped}
                   AND s."PayoutId" IS NULL
                   AND s."GoodsTotal" IS NOT NULL
                   AND o."Currency" = {currency}
                   AND o."Status" = ANY ({statuses})
             RETURNING s."GoodsTotal" - s."Commission" + s."ShippingShare" AS owed
            )
            INSERT INTO payouts ("Id", "SellerId", "Currency", "Amount", "PartCount", "RecordedBy", "CreatedAt")
            SELECT {payoutId}, {sellerId}, {currency}, sum(owed), count(*), {recordedBy}, {at}
              FROM claimed
            HAVING count(*) > 0
            """, cancellationToken);

        if (inserted == 0)
        {
            return null;
        }

        return await _context.Payouts
            .AsNoTracking()
            .Where(p => p.Id == payoutId)
            .Select(p => new PayoutResponse(p.Id, p.SellerId, p.Currency, p.Amount, p.PartCount, p.CreatedAt))
            .SingleAsync(cancellationToken);
    }
}
