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

    private IQueryable<Domain.Entities.Order> SoldIn(DateTime from, DateTime to) =>
        _context.Orders.AsNoTracking().Where(o => Sold.Contains(o.Status) && o.CreatedAt >= from && o.CreatedAt < to);

    public async Task<List<RevenueRow>> RevenueByDayAsync(DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        var rows = await SoldIn(from, to)
            .GroupBy(o => new { o.CreatedAt.Date, o.Currency })
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

    public async Task<List<BuyerRow>> BuyersAsync(DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        var rows = await SoldIn(from, to)
            .GroupBy(o => new { o.UserId, o.Currency })
            .Select(g => new { g.Key.UserId, g.Key.Currency, Orders = g.Count(), Spent = g.Sum(o => o.TotalAmount) })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new BuyerRow(r.UserId, r.Currency, r.Orders, r.Spent)).ToList();
    }
}
