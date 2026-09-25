using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Common;
using Ecommerce.Order.Application.Returns;
using Ecommerce.Order.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ecommerce.Order.Infrastructure.Persistence.Repositories;

/// <summary>What the shop owes each seller, and the payouts that settle it (specs/037).</summary>
public class PayoutRepository(OrderDbContext context, IOptions<ReturnOptions> returns) : IPayoutRepository
{
    private readonly OrderDbContext _context = context;
    private readonly TimeSpan _returnWindow = returns.Value.Window;

    /// <summary>
    /// Each earning part with what it is owed and whether that is DUE now (specs/066) - the one place the
    /// LINQ side says what "due" means; the payout claim says it in SQL, and a test holds the two together.
    /// </summary>
    /// <remarks>
    /// ⚠️ Due means delivered MORE THAN THE RETURN WINDOW AGO, with no return of it open. A return can start
    /// only inside the window, so money is never paid for a parcel that can still come back - a hold, never
    /// a debt. Open: requested, escalated or on its way back; or accepted or refused and still inside the
    /// window the buyer has to act on it.
    /// </remarks>
    private IQueryable<PartMoney> Money(Guid? sellerId)
    {
        var cutoff = DateTime.UtcNow - _returnWindow;

        // An object initializer, not a constructor: EF can group over the first and not the second.
        return Earning(sellerId).Select(s => new PartMoney
        {
            SellerId = s.SellerId!.Value,
            Currency = s.Order!.Currency,
            Owed = s.GoodsTotal!.Value - s.Commission!.Value + s.ShippingShare!.Value,
            PaidOut = s.PayoutId != null,
            Due = s.PayoutId == null
                && s.Status == ShipmentStatus.Shipped
                && s.DeliveredAt != null && s.DeliveredAt <= cutoff
                && (s.Return == null
                    || !(s.Return.Status == ReturnStatus.Requested
                        || s.Return.Status == ReturnStatus.Escalated
                        || s.Return.Status == ReturnStatus.SentBack
                        || ((s.Return.Status == ReturnStatus.Accepted || s.Return.Status == ReturnStatus.Refused)
                            && s.Return.DecidedAt > cutoff))),
        });
    }

    private sealed class PartMoney
    {
        public Guid SellerId { get; init; }

        public string? Currency { get; init; }

        public decimal Owed { get; init; }

        public bool PaidOut { get; init; }

        public bool Due { get; init; }
    }

    /// <summary>
    /// A seller's parts that are money at all: theirs, on a PAID order, with terms recorded at checkout.
    /// </summary>
    /// <remarks>
    /// ⚠️ The status filter is not decoration. Checkout writes a failed order's parts too, so without it a
    /// declined payment would sit in a seller's balance - and, shipped by mistake, be paid out.
    /// </remarks>
    private IQueryable<Domain.Entities.OrderShipment> Earning(Guid? sellerId)
    {
        var statuses = Sales.Earning;

        return _context.OrderShipments
            .AsNoTracking()
            .Where(s => s.SellerId != null
                && (sellerId == null || s.SellerId == sellerId)
                && s.GoodsTotal != null
                && statuses.Contains(s.Order!.Status)
                // A returned parcel is no money at all (specs/066): not on the way, not due, never paid.
                && (s.Return == null || s.Return.Status != ReturnStatus.Received));
    }

    public async Task<List<BalanceResponse>> GetBalanceAsync(Guid sellerId, CancellationToken cancellationToken = default)
    {
        // One GROUP BY with conditional sums: three figures per currency in one round trip, all over the
        // same rows, so they cannot disagree about which parts count.
        var rows = await Money(sellerId)
            .GroupBy(m => m.Currency)
            .Select(g => new
            {
                Currency = g.Key,
                // Not yet due - on its way, delivered but still returnable (specs/066), or held by a return.
                OnTheWay = g.Sum(m => !m.PaidOut && !m.Due ? m.Owed : 0m),
                Due = g.Sum(m => m.Due ? m.Owed : 0m),
                PaidOut = g.Sum(m => m.PaidOut ? m.Owed : 0m)
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
        var due = await Money(null)
            .Where(m => m.Due)
            .GroupBy(m => new { m.SellerId, m.Currency })
            .Select(g => new
            {
                g.Key.SellerId,
                g.Key.Currency,
                Due = g.Sum(m => m.Owed),
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
        CancellationToken cancellationToken = default,
        Func<PayoutResponse, CancellationToken, Task>? stage = null)
    {
        // The claim is one statement; its audit entry (specs/041) is saved in the same transaction with it.
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            _context.ChangeTracker.Clear();
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            var payout = await ClaimAsync(payoutId, sellerId, currency, recordedBy, at, cancellationToken);

            if (payout is not null && stage is not null)
            {
                await stage(payout, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return payout;
        });
    }

    private async Task<PayoutResponse?> ClaimAsync(
        Guid payoutId, Guid sellerId, string currency, Guid recordedBy, DateTime at, CancellationToken cancellationToken)
    {
        var statuses = Sales.Earning.Select(s => s.ToString()).ToArray();
        var shipped = ShipmentStatus.Shipped.ToString();
        // The same "due" as Money() (specs/066), in SQL: delivered more than the return window ago, and no
        // return of the part open - or received, which makes it no money at all.
        var cutoff = at - _returnWindow;
        var holding = new[] { nameof(ReturnStatus.Requested), nameof(ReturnStatus.Escalated), nameof(ReturnStatus.SentBack), nameof(ReturnStatus.Received) };
        var deciding = new[] { nameof(ReturnStatus.Accepted), nameof(ReturnStatus.Refused) };

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
                   AND s."DeliveredAt" IS NOT NULL
                   AND s."DeliveredAt" <= {cutoff}
                   AND NOT EXISTS (
                       SELECT 1 FROM parcel_returns AS r
                        WHERE r."ShipmentId" = s."Id"
                          AND (r."Status" = ANY ({holding})
                               OR (r."Status" = ANY ({deciding}) AND r."DecidedAt" > {cutoff})))
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
