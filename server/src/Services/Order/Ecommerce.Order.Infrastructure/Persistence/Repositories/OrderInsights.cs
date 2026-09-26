using Ecommerce.Order.Application.Insights;
using Ecommerce.Order.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Order.Infrastructure.Persistence.Repositories;

/// <summary>
/// The grouping behind the admin's insights (specs/047), done by PostgreSQL. What counts as a sale is one
/// list, here: an order the saga paid for - including one being prepared or shipped - and never one that
/// failed, was cancelled, or is still settling.
/// </summary>
public class OrderInsights(OrderDbContext context) : IOrderInsights
{
    private static readonly OrderStatus[] Sold =
        [OrderStatus.Paid, OrderStatus.Completed, OrderStatus.Preparing, OrderStatus.Shipped];

    private readonly OrderDbContext _context = context;

    /// <summary>
    /// Sold orders in the period, dated by when they were PAID (specs/072, #116) - or, for an order from before
    /// that was recorded, when it was placed. ⚠️ The same <c>PaidAt ?? CreatedAt</c> is written in the grouping
    /// below and in <see cref="SellerLinesIn"/>: the period and the day an order is counted on must agree (#125),
    /// and EF cannot share one expression into an anonymous GroupBy key, so InsightsTests holds them together.
    /// </summary>
    private IQueryable<Domain.Entities.Order> SoldIn(DateTime from, DateTime to) =>
        _context.Orders.AsNoTracking().Where(o => Sold.Contains(o.Status) && (o.PaidAt ?? o.CreatedAt) >= from && (o.PaidAt ?? o.CreatedAt) < to);

    public async Task<List<RevenueRow>> RevenueByDayAsync(DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        var rows = await SoldIn(from, to)
            .GroupBy(o => new { (o.PaidAt ?? o.CreatedAt).Date, o.Currency })
            .Select(g => new { g.Key.Date, g.Key.Currency, Revenue = g.Sum(o => o.TotalAmount), Orders = g.Count() })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new RevenueRow(r.Date, r.Currency, r.Revenue, r.Orders)).ToList();
    }

    public async Task<List<ProductSalesRow>> ProductSalesAsync(DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        var rows = await SoldIn(from, to)
            .SelectMany(o => o.Items, (o, i) => new { i.ProductId, o.Currency, o.CreatedAt, i.ProductName, i.Quantity, i.UnitPrice })
            .GroupBy(x => new { x.ProductId, x.Currency })
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.Currency,
                Name = g.OrderByDescending(x => x.CreatedAt).Select(x => x.ProductName).First(),
                Units = g.Sum(x => x.Quantity),
                Revenue = g.Sum(x => x.Quantity * x.UnitPrice),
            })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new ProductSalesRow(r.ProductId, r.Name, r.Currency, r.Units, r.Revenue)).ToList();
    }

    /// <summary>
    /// One seller's lines on sold orders in the period, less any part that came back and was refunded
    /// (specs/068 research D1) - the part being the seller's parcel of that order, and "came back" its return
    /// having reached Received. A return still open is revenue: it may yet be refused.
    /// </summary>
    private IQueryable<SellerLine> SellerLinesIn(Guid sellerId, DateTime from, DateTime to) =>
        SoldIn(from, to)
            .SelectMany(o => o.Items, (o, i) => new { Order = o, Item = i })
            .Where(x => x.Item.SellerId == sellerId)
            .Where(x => !_context.OrderShipments.Any(s =>
                s.OrderId == x.Order.Id && s.SellerId == sellerId && s.Return != null && s.Return.Status == ReturnStatus.Received))
            .Select(x => new SellerLine
            {
                OrderId = x.Order.Id,
                Day = (x.Order.PaidAt ?? x.Order.CreatedAt).Date,
                CreatedAt = x.Order.CreatedAt,
                Currency = x.Order.Currency,
                ProductId = x.Item.ProductId,
                ProductName = x.Item.ProductName,
                Quantity = x.Item.Quantity,
                // Less their own voucher (specs/069): what the seller gave away is not revenue. A platform voucher
                // is the shop's cost, and the seller is paid as if it were not there.
                Revenue = x.Item.Quantity * x.Item.UnitPrice - x.Item.ShopDiscount,
            });

    public async Task<List<RevenueRow>> SellerRevenueByDayAsync(Guid sellerId, DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        var rows = await SellerLinesIn(sellerId, from, to)
            .GroupBy(l => new { l.Day, l.Currency })
            // Several of her lines on one order are one order.
            .Select(g => new { g.Key.Day, g.Key.Currency, Revenue = g.Sum(l => l.Revenue), Orders = g.Select(l => l.OrderId).Distinct().Count() })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new RevenueRow(r.Day, r.Currency, r.Revenue, r.Orders)).ToList();
    }

    public async Task<List<ProductSalesRow>> SellerProductSalesAsync(Guid sellerId, DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        var rows = await SellerLinesIn(sellerId, from, to)
            .GroupBy(l => new { l.ProductId, l.Currency })
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.Currency,
                Name = g.OrderByDescending(l => l.CreatedAt).Select(l => l.ProductName).First(),
                Units = g.Sum(l => l.Quantity),
                Revenue = g.Sum(l => l.Revenue),
            })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new ProductSalesRow(r.ProductId, r.Name, r.Currency, r.Units, r.Revenue)).ToList();
    }

    /// <summary>
    /// A class with an object initializer, not a positional record: EF cannot group over a projection made with
    /// a record's constructor (found in specs/066).
    /// </summary>
    private sealed class SellerLine
    {
        public Guid OrderId { get; init; }
        public DateTime Day { get; init; }
        public DateTime CreatedAt { get; init; }
        public string? Currency { get; init; }
        public Guid ProductId { get; init; }
        public string ProductName { get; init; } = string.Empty;
        public int Quantity { get; init; }
        public decimal Revenue { get; init; }
    }

    public async Task<List<BuyerRow>> BuyersAsync(DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        var rows = await SoldIn(from, to)
            .GroupBy(o => new { o.UserId, o.Currency })
            .Select(g => new { g.Key.UserId, g.Key.Currency, Orders = g.Count(), Spent = g.Sum(o => o.TotalAmount) })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new BuyerRow(r.UserId, r.Currency, r.Orders, r.Spent)).ToList();
    }
}
