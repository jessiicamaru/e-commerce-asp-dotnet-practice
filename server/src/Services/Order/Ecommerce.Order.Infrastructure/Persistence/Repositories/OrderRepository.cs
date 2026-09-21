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
                x.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        var orders = rows.Select(x => new OrderSummaryResponse(
            x.Id, x.TotalAmount, OrderMapping.Describe(x.Status), x.FailureReason, x.ItemCount, x.CreatedAt, x.UpdatedAt))
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
                x.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        var orders = rows.Select(x => new OrderSummaryResponse(
            x.Id,
            x.TotalAmount,
            OrderMapping.Describe(x.Status),
            x.FailureReason,
            x.ItemCount,
            x.CreatedAt,
            x.UpdatedAt)).ToList();

        return (orders, totalCount);
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
