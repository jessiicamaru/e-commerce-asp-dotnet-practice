using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Common;
using Ecommerce.Order.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Order.Infrastructure.Persistence.Repositories;

public class OrderRepository(OrderDbContext context) : IOrderRepository
{
    private readonly OrderDbContext _context = context;

    public async Task<Domain.Entities.Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<List<Domain.Entities.Order>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(x => x.Items)
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Domain.Entities.Order order, CancellationToken cancellationToken = default)
    {
        await _context.Orders.AddAsync(order, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// One statement:
    /// <code>
    /// UPDATE orders
    ///    SET "Status" = @settled, "FailureReason" = @reason, "UpdatedAt" = @at
    ///  WHERE "Id" = @orderId AND "Status" = 'Submitted'
    /// </code>
    /// The guard is in the WHERE clause on purpose. Do not "improve" this by loading the order and
    /// testing its status first — that reintroduces the race the guard exists to close, and the
    /// window is small enough that no test would notice.
    /// </summary>
    public async Task<int> TrySettleAsync(
        Guid orderId,
        OrderStatus settledStatus,
        string? failureReason,
        DateTime settledAt,
        CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Where(x => x.Id == orderId && x.Status == OrderStatus.Submitted)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.Status, settledStatus)
                    .SetProperty(x => x.FailureReason, failureReason)
                    .SetProperty(x => x.UpdatedAt, settledAt),
                cancellationToken);
    }

    public async Task<int> TryAdvanceAsync(
        Guid orderId,
        OrderStatus from,
        OrderStatus to,
        string? trackingReference,
        DateTime at,
        CancellationToken cancellationToken = default)
    {
        // A legacy Completed row is a Paid order by another name (feature 011, research D3).
        var alsoFrom = from == OrderStatus.Paid ? OrderStatus.Completed : from;

        var query = _context.Orders
            .Where(x => x.Id == orderId && (x.Status == from || x.Status == alsoFrom));

        // One statement, the guard in its WHERE clause - the same shape as TrySettleAsync, for the same
        // reason: reading, checking and then writing would let two clicks both succeed.
        return trackingReference is null
            ? await query.ExecuteUpdateAsync(
                s => s.SetProperty(x => x.Status, to).SetProperty(x => x.UpdatedAt, at),
                cancellationToken)
            : await query.ExecuteUpdateAsync(
                s => s.SetProperty(x => x.Status, to)
                      .SetProperty(x => x.TrackingReference, trackingReference)
                      .SetProperty(x => x.UpdatedAt, at),
                cancellationToken);
    }

    public async Task<(List<OrderSummaryResponse> Orders, int TotalCount)> GetPageByStatusAsync(
        OrderStatus status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var alsoStatus = status == OrderStatus.Paid ? OrderStatus.Completed : status;

        var query = _context.Orders
            .AsNoTracking()
            .Where(x => x.Status == status || x.Status == alsoStatus);

        var totalCount = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderBy(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.TotalAmount,
                x.Status,
                x.FailureReason,
                ItemCount = x.Items.Count,
                x.CreatedAt,
                x.UpdatedAt,
                // Frozen at checkout: a list of orders must label each amount with the money it was
                // charged in, not with whatever the reader is browsing in (specs/022).
                x.Currency,
                x.Language
            })
            .ToListAsync(cancellationToken);

        var orders = rows.Select(x => new OrderSummaryResponse(
            x.Id, x.TotalAmount, OrderMapping.Describe(x.Status), x.FailureReason, x.ItemCount, x.CreatedAt,
            x.UpdatedAt, x.Currency ?? string.Empty, x.Language ?? string.Empty))
            .ToList();

        return (orders, totalCount);
    }

    public async Task<bool> ExistsAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        return await _context.Orders.AnyAsync(x => x.Id == orderId, cancellationToken);
    }

    public async Task<(List<OrderSummaryResponse> Orders, int TotalCount)> GetPageByUserAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Orders
            .AsNoTracking()
            .Where(x => x.UserId == userId);

        // Counted before paging, so the caller learns the true total even when they have asked for
        // a page past the end — which is how they find out they overshot.
        var totalCount = await query.CountAsync(cancellationToken);

        // Projected in SQL, not loaded and mapped. The item count becomes a correlated COUNT, so a
        // page of ten orders holding a hundred lines between them still transfers ten rows.
        //
        // Status stays an enum here and is turned into a string below. Projecting
        // `x.Status.ToString()` would read more directly, but whether it translates depends on the
        // provider's handling of a value-converted enum, and a projection that silently falls back
        // to client evaluation — or throws at runtime — is not worth the line it saves.
        var rows = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.TotalAmount,
                x.Status,
                x.FailureReason,
                ItemCount = x.Items.Count,
                x.CreatedAt,
                x.UpdatedAt,
                // Frozen at checkout: a list of orders must label each amount with the money it was
                // charged in, not with whatever the reader is browsing in (specs/022).
                x.Currency,
                x.Language
            })
            .ToListAsync(cancellationToken);

        var orders = rows.Select(x => new OrderSummaryResponse(
            x.Id,
            x.TotalAmount,
            OrderMapping.Describe(x.Status),
            x.FailureReason,
            x.ItemCount,
            x.CreatedAt,
            x.UpdatedAt,
            x.Currency ?? string.Empty,
            x.Language ?? string.Empty)).ToList();

        return (orders, totalCount);
    }

    public async Task<(List<SaleSummaryResponse> Sales, int TotalCount)> GetSalesPageAsync(
        Guid sellerId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var statuses = Sales.Statuses;

        // Both guards in SQL: a sale is a paid order with a line of theirs. Filtering either afterwards
        // would page over rows that are then dropped, and report a total that is not the seller's.
        var query = _context.Orders
            .AsNoTracking()
            .Where(x => statuses.Contains(x.Status) && x.Items.Any(i => i.SellerId == sellerId));

        var totalCount = await query.CountAsync(cancellationToken);

        // Every figure is over THIS seller's lines only (research D4). The order's own TotalAmount is
        // not selected at all, so it cannot leak into a row by a later edit to the mapping below.
        var rows = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.Status,
                x.CreatedAt,
                x.UpdatedAt,
                x.Currency,
                LineCount = x.Items.Count(i => i.SellerId == sellerId),
                Units = x.Items.Where(i => i.SellerId == sellerId).Sum(i => i.Quantity),
                Subtotal = x.Items.Where(i => i.SellerId == sellerId).Sum(i => i.UnitPrice * i.Quantity)
            })
            .ToListAsync(cancellationToken);

        var sales = rows.Select(x => new SaleSummaryResponse(
            x.Id,
            OrderMapping.Describe(x.Status),
            x.CreatedAt,
            x.UpdatedAt,
            x.LineCount,
            x.Units,
            x.Subtotal,
            x.Currency ?? string.Empty)).ToList();

        return (sales, totalCount);
    }

    public async Task<SaleDetailResponse?> GetSaleAsync(
        Guid orderId,
        Guid sellerId,
        CancellationToken cancellationToken = default)
    {
        var statuses = Sales.Statuses;

        // One query, three predicates. Loading the order and then checking would read another seller's
        // order - or a failed one - into memory before deciding to refuse it.
        var row = await _context.Orders
            .AsNoTracking()
            .Where(x => x.Id == orderId
                && statuses.Contains(x.Status)
                && x.Items.Any(i => i.SellerId == sellerId))
            .Select(x => new
            {
                x.Id,
                x.Status,
                x.CreatedAt,
                x.UpdatedAt,
                x.Currency,
                x.Language,
                // Only theirs. The other lines of the order are never selected.
                Items = x.Items
                    .Where(i => i.SellerId == sellerId)
                    .Select(i => new OrderItemDetailResponse(
                        i.ProductId,
                        i.ProductName,
                        i.Quantity,
                        i.UnitPrice,
                        i.UnitPrice * i.Quantity,
                        i.TaxAmount,
                        i.VariantId,
                        i.Sku,
                        i.OptionSummary))
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null
            ? null
            : new SaleDetailResponse(
                row.Id,
                OrderMapping.Describe(row.Status),
                row.CreatedAt,
                row.UpdatedAt,
                row.Items,
                row.Items.Sum(i => i.TotalPrice),
                row.Currency ?? string.Empty,
                row.Language ?? string.Empty);
    }

    public async Task<Domain.Entities.Order?> GetByIdForUserAsync(
        Guid orderId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        // Both predicates, one query. Loading by id and comparing the owner afterwards would read
        // another shopper's order into memory before deciding to refuse it, and would make a "not
        // yours" answer distinguishable from a "no such order" one.
        return await _context.Orders
            .AsNoTracking()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == orderId && x.UserId == userId, cancellationToken);
    }
}
